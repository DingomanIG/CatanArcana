using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 네트워크 멀티플레이 게임 매니저
/// - IGameManager 구현 (UI 코드 무변경)
/// - 호스트: 내부 LocalGameManager로 게임 로직 실행, 결과를 네트워크 동기화
/// - 클라이언트: ServerRpc로 액션 요청, ClientRpc/NetworkVariable로 결과 수신
/// </summary>
public class NetworkGameManager : NetworkBehaviour, IGameManager
{
    // ================================================================
    // 호스트 전용
    // ================================================================

    LocalGameManager hostLGM;
    HexGridView hexGridView;

    // ================================================================
    // 클라이언트 공통
    // ================================================================

    int localPlayerIndex = -1;

    // clientId → playerIndex 매핑 (호스트가 관리)
    NetworkList<ulong> playerClientIds;

    // ================================================================
    // NetworkVariable — 게임 핵심 상태
    // ================================================================

    NetworkVariable<int> netTurnNumber = new(0);
    NetworkVariable<int> netCurrentPlayerIndex = new(0);
    NetworkVariable<int> netFirstPlayerIndex = new(0);
    NetworkVariable<int> netCurrentPhase = new((int)GamePhase.WaitingForPlayers);
    NetworkVariable<int> netCurrentBuildMode = new((int)BuildMode.None);
    NetworkVariable<int> netDevCardState = new((int)DevCardUseState.None);
    NetworkVariable<int> netDevCardDeckRemaining = new(0);
    NetworkVariable<bool> netIsWaitingForDiscard = new(false);
    NetworkVariable<int> netLongestRoadHolder = new(-1);
    NetworkVariable<int> netLargestArmyHolder = new(-1);
    NetworkVariable<int> netPlayerCount = new(0);
    NetworkVariable<HexCoordNet> netRobberPosition = new();

    // 플레이어별 공개 정보 (VP, 총 자원 수, 기사 수)
    NetworkList<int> netPlayerVP;
    NetworkList<int> netPlayerTotalResCount;
    NetworkList<int> netPlayerKnightsPlayed;

    // 플레이어 이름 (FixedString64Bytes — NetworkList 지원)
    NetworkList<FixedString64Bytes> netPlayerNames;

    // ================================================================
    // 클라이언트 측 미러 데이터
    // ================================================================

    HexGrid clientGrid;
    PlayerState[] clientPlayers;
    ResArray localResources; // 내 자원 (TargetedClientRpc로 수신)

    // ================================================================
    // IGameManager 프로퍼티
    // ================================================================

    public int TurnNumber => netTurnNumber.Value;
    public int CurrentPlayerIndex => netCurrentPlayerIndex.Value;
    public int LocalPlayerIndex => localPlayerIndex;
    public int FirstPlayerIndex => netFirstPlayerIndex.Value;
    public int PlayerCount => netPlayerCount.Value;
    public GamePhase CurrentPhase => (GamePhase)netCurrentPhase.Value;
    public bool IsHost => IsServer;
    public BuildMode CurrentBuildMode => (BuildMode)netCurrentBuildMode.Value;
    public DevCardUseState DevCardState => (DevCardUseState)netDevCardState.Value;
    public int DevCardDeckRemaining => netDevCardDeckRemaining.Value;
    public bool IsWaitingForDiscard => netIsWaitingForDiscard.Value;
    public bool HasPendingIncomingTrade => false; // 네트워크에서는 P2P 거래 RPC로 처리

    // ================================================================
    // 이벤트
    // ================================================================

    public event Action<int> OnTurnChanged;
    public event Action<GamePhase> OnPhaseChanged;
    public event Action<int, int, int> OnDiceRolled;
    public event Action OnPlayerListChanged;
    public event Action<int, ResourceType, int> OnResourceChanged;
    public event Action<int, int, BuildingType> OnBuildingPlaced;
    public event Action<int, int> OnRoadPlaced;
    public event Action<int, int> OnVPChanged;
    public event Action<HexCoord> OnRobberMoved;
    public event Action<BuildMode> OnBuildModeChanged;
    public event Action<int, DevCardType> OnDevCardPurchased;
    public event Action<int, DevCardType> OnDevCardUsed;
    public event Action<int, bool> OnLongestRoadChanged;
    public event Action<int, bool> OnLargestArmyChanged;
    public event Action<int, int, ResourceType> OnRobberSteal;
    public event Action<int, ResourceType, ResourceType, int> OnBankTrade;
    public event Action<int, int> OnPlayerTrade;
    public event Action<int, Dictionary<ResourceType, int>, Dictionary<ResourceType, int>> OnIncomingTradeProposal;
    public event Action OnIncomingTradeCancelled;
    public event Action<int, int> OnDiscardRequired;

    // ================================================================
    // LIFECYCLE
    // ================================================================

    void Awake()
    {
        playerClientIds = new NetworkList<ulong>();
        netPlayerVP = new NetworkList<int>();
        netPlayerTotalResCount = new NetworkList<int>();
        netPlayerKnightsPlayed = new NetworkList<int>();
        netPlayerNames = new NetworkList<FixedString64Bytes>();
    }

    public override void OnNetworkSpawn()
    {
        GameServices.GameManager = this;

        // NetworkVariable 변경 콜백 등록
        netCurrentPhase.OnValueChanged += (_, newVal) => OnPhaseChanged?.Invoke((GamePhase)newVal);
        netCurrentPlayerIndex.OnValueChanged += (_, newVal) => OnTurnChanged?.Invoke(newVal);
        netCurrentBuildMode.OnValueChanged += (_, newVal) => OnBuildModeChanged?.Invoke((BuildMode)newVal);
        netPlayerCount.OnValueChanged += (_, __) => OnPlayerListChanged?.Invoke();

        // NetworkList 변경 시 클라이언트 미러 데이터 갱신
        netPlayerVP.OnListChanged += _ => SyncClientPlayerMirror();
        netPlayerTotalResCount.OnListChanged += _ => SyncClientPlayerMirror();
        netPlayerKnightsPlayed.OnListChanged += _ => SyncClientPlayerMirror();

        if (IsServer)
        {
            SetupHost();
        }

        // 클라이언트(+호스트): 자기 이름을 서버에 등록
        string myName = SceneFlowManager.Instance?.PlayerName ?? "Player";
        RegisterPlayerNameServerRpc(myName);

        Debug.Log($"[NGM] OnNetworkSpawn — IsServer={IsServer}, IsClient={IsClient}");
    }

    public override void OnNetworkDespawn()
    {
        if (GameServices.GameManager == (IGameManager)this)
            GameServices.GameManager = null;

        // 호스트: 콜백 해제
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    /// <summary>클라이언트 퇴장 시 처리 (호스트에서 실행)</summary>
    void OnClientDisconnected(ulong clientId)
    {
        int pi = GetPlayerIndexFromClientId(clientId);
        if (pi >= 0)
        {
            Debug.Log($"[NGM] 플레이어 퇴장: [{pi}] {GetPlayerName(pi)} (clientId={clientId})");
            // TODO: 게임 중 퇴장 시 AI 대체 or 일시 중단 (Phase 7에서 구현)
        }
    }

    // ================================================================
    // 호스트 셋업
    // ================================================================

    void SetupHost()
    {
        hexGridView = FindFirstObjectByType<HexGridView>();
        if (hexGridView == null)
        {
            Debug.LogError("[NGM] HexGridView를 찾을 수 없습니다!");
            return;
        }

        // 호스트 전용 LGM 생성 (씬에 배치하지 않고 내부 인스턴스)
        var lgmGO = new GameObject("HostLGM_Internal");
        hostLGM = lgmGO.AddComponent<LocalGameManager>();
        hostLGM.SetHexGridView(hexGridView);
        hostLGM.SuppressUICommands = true; // BuildModeController 직접 호출 억제 → NGM이 ClientRpc로 처리

        // LGM이 GameServices에 등록하지 않도록 (NGM이 이미 등록됨)
        // InitializePlayers는 Start에서 호출될 것

        // 플레이어 수 설정
        int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;
        if (playerCount < 2) playerCount = 2; // 최소 2인
        hostLGM.InitializePlayers(playerCount);
        netPlayerCount.Value = playerCount;

        // 플레이어 인덱스 매핑
        SetupPlayerMapping();

        // LGM 이벤트 구독 → 네트워크 브로드캐스트
        SubscribeHostEvents();

        // 클라이언트 퇴장 콜백
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        Debug.Log($"[NGM] 호스트 셋업 완료 — {playerCount}명");
    }

    void SetupPlayerMapping()
    {
        var clients = NetworkManager.Singleton.ConnectedClientsList;

        // 셔플
        var shuffled = new List<ulong>();
        foreach (var c in clients) shuffled.Add(c.ClientId);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        playerClientIds.Clear();
        foreach (var id in shuffled)
            playerClientIds.Add(id);

        // NetworkList 초기화
        for (int i = 0; i < shuffled.Count; i++)
        {
            netPlayerVP.Add(0);
            netPlayerTotalResCount.Add(0);
            netPlayerKnightsPlayed.Add(0);
            netPlayerNames.Add(new FixedString64Bytes($"플레이어 {i + 1}")); // 기본값, ServerRpc로 덮어씀
        }

        // 각 클라이언트에게 본인 인덱스 알림
        for (int i = 0; i < shuffled.Count; i++)
        {
            var targetParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { shuffled[i] }
                }
            };
            AssignPlayerIndexClientRpc(i, targetParams);
        }

        // 호스트 자신의 인덱스 직접 설정
        localPlayerIndex = GetPlayerIndexFromClientId(NetworkManager.Singleton.LocalClientId);
    }

    int GetPlayerIndexFromClientId(ulong clientId)
    {
        for (int i = 0; i < playerClientIds.Count; i++)
        {
            if (playerClientIds[i] == clientId) return i;
        }
        return -1;
    }

    ulong GetClientIdFromPlayerIndex(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < playerClientIds.Count)
            return playerClientIds[playerIndex];
        return ulong.MaxValue;
    }

    // ================================================================
    // 호스트 이벤트 구독 (LGM → NetworkVariable/ClientRpc)
    // ================================================================

    void SubscribeHostEvents()
    {
        hostLGM.OnTurnChanged += pi =>
        {
            netCurrentPlayerIndex.Value = pi;
            netTurnNumber.Value = hostLGM.TurnNumber;
        };

        hostLGM.OnPhaseChanged += phase =>
        {
            var prevPhase = (GamePhase)netCurrentPhase.Value;
            netCurrentPhase.Value = (int)phase;

            // 초기 배치 → 본 게임 전환 시 알림
            if (prevPhase == GamePhase.InitialPlacement && phase == GamePhase.RollDice)
            {
                NotifyInitialPlacementFinishedClientRpc();
            }
        };

        hostLGM.OnDiceRolled += (d1, d2, total) =>
        {
            NotifyDiceRolledClientRpc(d1, d2, total);
        };

        hostLGM.OnBuildingPlaced += (pi, vid, bt) =>
        {
            NotifyBuildingPlacedClientRpc(pi, vid, (int)bt);
            SyncPlayerPublicInfo(pi);
        };

        hostLGM.OnRoadPlaced += (pi, eid) =>
        {
            NotifyRoadPlacedClientRpc(pi, eid);
        };

        hostLGM.OnResourceChanged += (pi, rt, count) =>
        {
            // 해당 플레이어에게만 상세 자원 전송
            SendResourceToOwner(pi);
            // 전체에 총 자원 수 공개
            var ps = hostLGM.GetPlayerState(pi);
            if (ps != null && pi < netPlayerTotalResCount.Count)
                netPlayerTotalResCount[pi] = ps.TotalResourceCount;
        };

        hostLGM.OnVPChanged += (pi, vp) =>
        {
            if (pi < netPlayerVP.Count)
                netPlayerVP[pi] = vp;
            NotifyVPChangedClientRpc(pi, vp);
        };

        hostLGM.OnRobberMoved += coord =>
        {
            netRobberPosition.Value = new HexCoordNet(coord);
            NotifyRobberMovedClientRpc(new HexCoordNet(coord));
        };

        hostLGM.OnBuildModeChanged += mode =>
        {
            netCurrentBuildMode.Value = (int)mode;
        };

        hostLGM.OnDevCardPurchased += (pi, ct) =>
        {
            NotifyDevCardPurchasedClientRpc(pi, (int)ct);
            netDevCardDeckRemaining.Value = hostLGM.DevCardDeckRemaining;
        };

        hostLGM.OnDevCardUsed += (pi, ct) =>
        {
            NotifyDevCardUsedClientRpc(pi, (int)ct);
            netDevCardState.Value = (int)hostLGM.DevCardState;
        };

        hostLGM.OnLongestRoadChanged += (pi, gained) =>
        {
            netLongestRoadHolder.Value = hostLGM.GetLongestRoadHolder();
            NotifyLongestRoadChangedClientRpc(pi, gained);
        };

        hostLGM.OnLargestArmyChanged += (pi, gained) =>
        {
            netLargestArmyHolder.Value = hostLGM.GetLargestArmyHolder();
            NotifyLargestArmyChangedClientRpc(pi, gained);
        };

        hostLGM.OnRobberSteal += (thief, victim, res) =>
        {
            NotifyRobberStealClientRpc(thief, victim, (int)res);
            SendResourceToOwner(thief);
            SendResourceToOwner(victim);
        };

        hostLGM.OnBankTrade += (pi, gave, recv, rate) =>
        {
            NotifyBankTradeClientRpc(pi, (int)gave, (int)recv, rate);
        };

        hostLGM.OnPlayerTrade += (p1, p2) =>
        {
            NotifyPlayerTradeClientRpc(p1, p2);
            SendResourceToOwner(p1);
            SendResourceToOwner(p2);
        };

        hostLGM.OnDiscardRequired += (pi, count) =>
        {
            netIsWaitingForDiscard.Value = true;
            var targetParams = GetTargetedParams(pi);
            NotifyDiscardRequiredClientRpc(pi, count, targetParams);
        };

        hostLGM.OnPlayerListChanged += () =>
        {
            OnPlayerListChanged?.Invoke();
        };

        // 초기 배치 흐름: 해당 플레이어에게 BuildMode 진입 알림
        hostLGM.OnInitialPlacementTurn += (pi, isRoad) =>
        {
            int mode = isRoad ? (int)BuildMode.PlacingRoad : (int)BuildMode.PlacingSettlement;
            var targetParams = GetTargetedParams(pi);
            NotifyInitialPlacementBuildModeClientRpc(pi, mode, isRoad, targetParams);
        };
    }

    void SendResourceToOwner(int playerIndex)
    {
        var ps = hostLGM.GetPlayerState(playerIndex);
        if (ps == null) return;

        var res = ResArray.FromDict(ps.Resources);
        var targetParams = GetTargetedParams(playerIndex);
        NotifyResourceUpdateClientRpc(playerIndex, res, targetParams);

        // 공개 총 자원 수
        if (playerIndex < netPlayerTotalResCount.Count)
            netPlayerTotalResCount[playerIndex] = ps.TotalResourceCount;
    }

    void SyncPlayerPublicInfo(int playerIndex)
    {
        var ps = hostLGM.GetPlayerState(playerIndex);
        if (ps == null) return;

        if (playerIndex < netPlayerVP.Count)
            netPlayerVP[playerIndex] = ps.VictoryPoints;
        if (playerIndex < netPlayerTotalResCount.Count)
            netPlayerTotalResCount[playerIndex] = ps.TotalResourceCount;
        if (playerIndex < netPlayerKnightsPlayed.Count)
            netPlayerKnightsPlayed[playerIndex] = ps.KnightsPlayed;
    }

    ClientRpcParams GetTargetedParams(int playerIndex)
    {
        ulong clientId = GetClientIdFromPlayerIndex(playerIndex);
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            }
        };
    }

    // ================================================================
    // ClientRpc — 이벤트 브로드캐스트
    // ================================================================

    [ClientRpc]
    void AssignPlayerIndexClientRpc(int playerIndex, ClientRpcParams clientRpcParams = default)
    {
        localPlayerIndex = playerIndex;
        Debug.Log($"[NGM] 내 플레이어 인덱스: {playerIndex}");
    }

    [ClientRpc]
    void NotifyDiceRolledClientRpc(int d1, int d2, int total)
    {
        OnDiceRolled?.Invoke(d1, d2, total);
    }

    [ClientRpc]
    void NotifyBuildingPlacedClientRpc(int playerIndex, int vertexId, int buildingType)
    {
        // 클라이언트 보드 미러 갱신
        if (!IsServer && clientGrid != null)
        {
            var vertex = clientGrid.Vertices[vertexId];
            vertex.OwnerPlayerIndex = playerIndex;
            vertex.Building = (BuildingType)buildingType;
        }
        OnBuildingPlaced?.Invoke(playerIndex, vertexId, (BuildingType)buildingType);
    }

    [ClientRpc]
    void NotifyRoadPlacedClientRpc(int playerIndex, int edgeId)
    {
        if (!IsServer && clientGrid != null)
        {
            var edge = clientGrid.Edges[edgeId];
            edge.OwnerPlayerIndex = playerIndex;
            edge.HasRoad = true;
        }
        OnRoadPlaced?.Invoke(playerIndex, edgeId);
    }

    [ClientRpc]
    void NotifyRobberMovedClientRpc(HexCoordNet coord)
    {
        if (!IsServer && clientGrid != null)
        {
            foreach (var t in clientGrid.Tiles.Values) t.HasRobber = false;
            var tile = clientGrid.GetTile(coord.ToHexCoord());
            if (tile != null) tile.HasRobber = true;
        }
        OnRobberMoved?.Invoke(coord.ToHexCoord());
    }

    [ClientRpc]
    void NotifyRobberStealClientRpc(int thief, int victim, int resourceType)
    {
        OnRobberSteal?.Invoke(thief, victim, (ResourceType)resourceType);
    }

    [ClientRpc]
    void NotifyVPChangedClientRpc(int playerIndex, int vp)
    {
        OnVPChanged?.Invoke(playerIndex, vp);
    }

    [ClientRpc]
    void NotifyDevCardPurchasedClientRpc(int playerIndex, int cardType)
    {
        OnDevCardPurchased?.Invoke(playerIndex, (DevCardType)cardType);
    }

    [ClientRpc]
    void NotifyDevCardUsedClientRpc(int playerIndex, int cardType)
    {
        OnDevCardUsed?.Invoke(playerIndex, (DevCardType)cardType);
    }

    [ClientRpc]
    void NotifyLongestRoadChangedClientRpc(int playerIndex, bool gained)
    {
        OnLongestRoadChanged?.Invoke(playerIndex, gained);
    }

    [ClientRpc]
    void NotifyLargestArmyChangedClientRpc(int playerIndex, bool gained)
    {
        OnLargestArmyChanged?.Invoke(playerIndex, gained);
    }

    [ClientRpc]
    void NotifyBankTradeClientRpc(int playerIndex, int gave, int received, int rate)
    {
        OnBankTrade?.Invoke(playerIndex, (ResourceType)gave, (ResourceType)received, rate);
    }

    [ClientRpc]
    void NotifyPlayerTradeClientRpc(int p1, int p2)
    {
        OnPlayerTrade?.Invoke(p1, p2);
    }

    [ClientRpc]
    void NotifyResourceUpdateClientRpc(int playerIndex, ResArray resources, ClientRpcParams clientRpcParams = default)
    {
        if (playerIndex == localPlayerIndex)
        {
            localResources = resources;
            // 상세 자원 이벤트 발행
            foreach (ResourceType rt in Enum.GetValues(typeof(ResourceType)))
            {
                if (rt == ResourceType.None || rt == ResourceType.Sea) continue;
                OnResourceChanged?.Invoke(playerIndex, rt, resources[rt]);
            }
        }
    }

    [ClientRpc]
    void NotifyDiscardRequiredClientRpc(int playerIndex, int count, ClientRpcParams clientRpcParams = default)
    {
        OnDiscardRequired?.Invoke(playerIndex, count);
    }

    [ClientRpc]
    void NotifyStealCandidatesClientRpc(int[] candidates, ClientRpcParams clientRpcParams = default)
    {
        // 약탈 후보 목록을 클라이언트에게 전달 (UI에서 선택)
        // 클라이언트 측 캐시
        cachedStealCandidates = new List<int>(candidates);
    }
    List<int> cachedStealCandidates = new();

    [ClientRpc]
    void NotifyIncomingTradeProposalClientRpc(int proposer, ResArray offer, ResArray request,
        ClientRpcParams clientRpcParams = default)
    {
        OnIncomingTradeProposal?.Invoke(proposer, offer.ToDict(), request.ToDict());
    }

    [ClientRpc]
    void SyncFullBoardStateClientRpc(BoardSnapshot snapshot)
    {
        if (IsServer) return; // 호스트는 이미 LGM에 보드 있음

        ApplyBoardSnapshot(snapshot);
        OnPlayerListChanged?.Invoke();
        Debug.Log("[NGM] 보드 전체 동기화 수신 완료");
    }

    /// <summary>
    /// 초기 배치 중 해당 플레이어에게 BuildMode 진입 알림
    /// - 호스트에서도 수신하여 호스트 플레이어의 UI를 제어
    /// </summary>
    [ClientRpc]
    void NotifyInitialPlacementBuildModeClientRpc(int playerIndex, int buildMode, bool isRoad,
        ClientRpcParams clientRpcParams = default)
    {
        if (playerIndex != localPlayerIndex) return;

        var mode = (BuildMode)buildMode;
        Debug.Log($"[NGM] 초기 배치 BuildMode 수신: {mode} (isRoad={isRoad})");

        // 이전 모드 정리 후 새 모드 진입 (라운드 전환 시 연속 배치 대응)
        BuildModeController.Instance?.CancelBuildMode();
        BuildModeController.Instance?.SetInitialPlacement(true);
        BuildModeController.Instance?.EnterBuildMode(mode);
    }

    /// <summary>초기 배치 완료 → BuildModeController 정리</summary>
    [ClientRpc]
    void NotifyInitialPlacementFinishedClientRpc()
    {
        BuildModeController.Instance?.SetInitialPlacement(false);
        Debug.Log("[NGM] 초기 배치 완료 알림 수신");
    }

    // ================================================================
    // ServerRpc — 클라이언트 → 호스트
    // ================================================================

    int ValidateSender(ServerRpcParams rpcParams)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        int pi = GetPlayerIndexFromClientId(senderId);
        if (pi < 0)
        {
            Debug.LogWarning($"[NGM] 알 수 없는 클라이언트: {senderId}");
        }
        return pi;
    }

    [ServerRpc(RequireOwnership = false)]
    void RegisterPlayerNameServerRpc(string playerName, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        int pi = GetPlayerIndexFromClientId(senderId);
        if (pi >= 0 && pi < netPlayerNames.Count)
        {
            netPlayerNames[pi] = new FixedString64Bytes(playerName);
            Debug.Log($"[NGM] 플레이어 이름 등록: [{pi}] {playerName} (clientId={senderId})");
        }
    }

    bool ValidateTurn(int senderPlayerIndex)
    {
        if (senderPlayerIndex != hostLGM.CurrentPlayerIndex)
        {
            Debug.LogWarning($"[NGM] 턴 검증 실패: sender={senderPlayerIndex}, current={hostLGM.CurrentPlayerIndex}");
            return false;
        }
        return true;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestRollDiceServerRpc(ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        hostLGM.RollDice();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestEndTurnServerRpc(ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        hostLGM.EndTurn();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestBuildSettlementServerRpc(int vertexId, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        hostLGM.TryBuildSettlement(vertexId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestBuildCityServerRpc(int vertexId, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        hostLGM.TryBuildCity(vertexId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestBuildRoadServerRpc(int edgeId, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        hostLGM.TryBuildRoad(edgeId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestEnterBuildModeServerRpc(int mode, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        hostLGM.EnterBuildMode((BuildMode)mode);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestCancelBuildModeServerRpc(ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        hostLGM.CancelBuildMode();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestBuyDevCardServerRpc(ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        hostLGM.TryBuyDevCard();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestUseKnightServerRpc(HexCoordNet target, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        hostLGM.TryUseKnight(target.ToHexCoord());
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestUseRoadBuildingServerRpc(ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        hostLGM.TryUseRoadBuilding();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestUseYearOfPlentyServerRpc(int res1, int res2, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        hostLGM.TryUseYearOfPlenty((ResourceType)res1, (ResourceType)res2);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestUseMonopolyServerRpc(int targetResource, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        hostLGM.TryUseMonopoly((ResourceType)targetResource);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestBankTradeServerRpc(int give, int receive, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        hostLGM.TryBankTrade((ResourceType)give, (ResourceType)receive);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestPlayerTradeServerRpc(int otherPlayer, ResArray offer, ResArray request,
        ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        hostLGM.TryPlayerTrade(otherPlayer, offer.ToDict(), request.ToDict());
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestRespondTradeServerRpc(bool accept, ServerRpcParams rpcParams = default)
    {
        // 거래 응답은 턴 플레이어가 아닌 제안 대상이 보냄
        hostLGM.RespondToIncomingTrade(accept);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestMoveRobberServerRpc(HexCoordNet target, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;

        bool success = hostLGM.TryMoveRobber(target.ToHexCoord());
        if (success)
        {
            // 약탈 후보 전달
            var candidates = hostLGM.GetRobberStealCandidates();
            if (candidates.Count > 1)
            {
                var targetParams = GetTargetedParams(pi);
                NotifyStealCandidatesClientRpc(candidates.ToArray(), targetParams);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestStealFromPlayerServerRpc(int victimIndex, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        hostLGM.TryStealFromPlayer(victimIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestConfirmDiscardServerRpc(ResArray toDiscard, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        hostLGM.ConfirmDiscard(toDiscard.ToDict());
        netIsWaitingForDiscard.Value = hostLGM.IsWaitingForDiscard;
    }

    // ================================================================
    // IGameManager 메서드 — 클라이언트에서 호출 시 ServerRpc 전송
    // ================================================================

    public void PrepareGame()
    {
        if (IsServer) hostLGM.PrepareGame();
    }

    public void StartGame()
    {
        if (IsServer)
        {
            // 1. 보드 준비 + 전체 동기화 먼저
            hostLGM.PrepareGame();
            var snapshot = CreateBoardSnapshot();
            SyncFullBoardStateClientRpc(snapshot);

            // 2. 게임 시작 (InitialPlacement 진입 → OnInitialPlacementTurn 이벤트 발행)
            //    → SubscribeHostEvents에서 자동으로 ClientRpc 전송
            hostLGM.StartGame();
        }
    }

    public void RollDice()
    {
        if (IsServer)
            hostLGM.RollDice();
        else
            RequestRollDiceServerRpc();
    }

    public void EndTurn()
    {
        if (IsServer)
            hostLGM.EndTurn();
        else
            RequestEndTurnServerRpc();
    }

    public bool IsMyTurn() => localPlayerIndex == CurrentPlayerIndex;
    public bool IsPlayerAI(int playerIndex) => false; // 네트워크에서는 AI 없음

    public string GetPlayerName(int index)
    {
        if (index >= 0 && index < netPlayerNames.Count)
        {
            string name = netPlayerNames[index].ToString();
            if (!string.IsNullOrEmpty(name)) return name;
        }
        return $"플레이어 {index + 1}";
    }

    public bool TryBuildSettlement(int vertexId)
    {
        if (IsServer)
            return hostLGM.TryBuildSettlement(vertexId);
        RequestBuildSettlementServerRpc(vertexId);
        return false; // 클라이언트는 결과를 ClientRpc로 수신
    }

    public bool TryBuildCity(int vertexId)
    {
        if (IsServer)
            return hostLGM.TryBuildCity(vertexId);
        RequestBuildCityServerRpc(vertexId);
        return false;
    }

    public bool TryBuildRoad(int edgeId)
    {
        if (IsServer)
            return hostLGM.TryBuildRoad(edgeId);
        RequestBuildRoadServerRpc(edgeId);
        return false;
    }

    public void EnterBuildMode(BuildMode mode)
    {
        if (IsServer)
        {
            hostLGM.EnterBuildMode(mode);
        }
        else
        {
            // 클라이언트: 로컬에서 즉시 빌드모드 진입 (하이라이트 표시) + 서버 확인 요청
            BuildModeController.Instance?.EnterBuildMode(mode);
            RequestEnterBuildModeServerRpc((int)mode);
        }
    }

    public void CancelBuildMode()
    {
        if (IsServer)
        {
            hostLGM.CancelBuildMode();
        }
        else
        {
            BuildModeController.Instance?.CancelBuildMode();
            RequestCancelBuildModeServerRpc();
        }
    }

    public void ConfirmDiscard(Dictionary<ResourceType, int> toDiscard)
    {
        if (IsServer)
            hostLGM.ConfirmDiscard(toDiscard);
        else
            RequestConfirmDiscardServerRpc(ResArray.FromDict(toDiscard));
    }

    public bool TryMoveRobber(HexCoord newTile)
    {
        if (IsServer)
            return hostLGM.TryMoveRobber(newTile);
        RequestMoveRobberServerRpc(new HexCoordNet(newTile));
        return false;
    }

    public bool TryStealFromPlayer(int victimIndex)
    {
        if (IsServer)
            return hostLGM.TryStealFromPlayer(victimIndex);
        RequestStealFromPlayerServerRpc(victimIndex);
        return false;
    }

    public List<int> GetRobberStealCandidates()
    {
        if (IsServer)
            return hostLGM.GetRobberStealCandidates();
        return cachedStealCandidates;
    }

    public bool TryBuyDevCard()
    {
        if (IsServer)
            return hostLGM.TryBuyDevCard();
        RequestBuyDevCardServerRpc();
        return false;
    }

    public bool TryUseKnight(HexCoord robberTarget)
    {
        if (IsServer)
            return hostLGM.TryUseKnight(robberTarget);
        RequestUseKnightServerRpc(new HexCoordNet(robberTarget));
        return false;
    }

    public bool TryUseRoadBuilding()
    {
        if (IsServer)
            return hostLGM.TryUseRoadBuilding();
        RequestUseRoadBuildingServerRpc();
        return false;
    }

    public bool TryUseYearOfPlenty(ResourceType res1, ResourceType res2)
    {
        if (IsServer)
            return hostLGM.TryUseYearOfPlenty(res1, res2);
        RequestUseYearOfPlentyServerRpc((int)res1, (int)res2);
        return false;
    }

    public bool TryUseMonopoly(ResourceType targetResource)
    {
        if (IsServer)
            return hostLGM.TryUseMonopoly(targetResource);
        RequestUseMonopolyServerRpc((int)targetResource);
        return false;
    }

    public bool TryBankTrade(ResourceType give, ResourceType receive)
    {
        if (IsServer)
            return hostLGM.TryBankTrade(give, receive);
        RequestBankTradeServerRpc((int)give, (int)receive);
        return false;
    }

    public bool TryPlayerTrade(int otherPlayer, Dictionary<ResourceType, int> offer, Dictionary<ResourceType, int> request)
    {
        if (IsServer)
            return hostLGM.TryPlayerTrade(otherPlayer, offer, request);
        RequestPlayerTradeServerRpc(otherPlayer, ResArray.FromDict(offer), ResArray.FromDict(request));
        return false;
    }

    public int GetTradeRate(ResourceType resource)
    {
        if (IsServer) return hostLGM.GetTradeRate(resource);

        // 클라이언트: 로컬 보드 데이터로 계산
        if (clientGrid == null || clientPlayers == null) return 4;
        // 간소화: 클라이언트도 자기 건물의 항구를 알 수 있음
        bool has2to1 = false, has3to1 = false;
        foreach (var v in clientGrid.Vertices)
        {
            if (v.OwnerPlayerIndex != localPlayerIndex) continue;
            if (v.Port == PortType.Generic) has3to1 = true;
            else if (v.Port != PortType.None && PortMatchesResource(v.Port, resource)) has2to1 = true;
        }
        if (has2to1) return 2;
        if (has3to1) return 3;
        return 4;
    }

    public void RespondToIncomingTrade(bool accept)
    {
        if (IsServer)
            hostLGM.RespondToIncomingTrade(accept);
        else
            RequestRespondTradeServerRpc(accept);
    }

    // ================================================================
    // 조회 메서드
    // ================================================================

    public PlayerState GetPlayerState(int playerIndex)
    {
        if (IsServer)
            return hostLGM.GetPlayerState(playerIndex);

        // 클라이언트: 미러 PlayerState + NetworkList 공개 정보 보강
        if (clientPlayers == null || playerIndex < 0 || playerIndex >= clientPlayers.Length)
            return null;

        var ps = clientPlayers[playerIndex];

        if (playerIndex == localPlayerIndex)
        {
            // 내 자원: TargetedClientRpc로 수신한 localResources 사용
            foreach (ResourceType rt in Enum.GetValues(typeof(ResourceType)))
            {
                if (rt == ResourceType.None || rt == ResourceType.Sea) continue;
                ps.Resources[rt] = localResources[rt];
            }
        }
        else
        {
            // 상대 자원: 총합만 공개 — Wood에 총합을 넣어 TotalResourceCount가 맞게 함
            int totalRes = (playerIndex < netPlayerTotalResCount.Count) ? netPlayerTotalResCount[playerIndex] : 0;
            ps.Resources[ResourceType.Wood] = totalRes;
            ps.Resources[ResourceType.Brick] = 0;
            ps.Resources[ResourceType.Wool] = 0;
            ps.Resources[ResourceType.Wheat] = 0;
            ps.Resources[ResourceType.Ore] = 0;
        }

        return ps;
    }

    public HexGrid GetGrid()
    {
        if (IsServer)
            return hostLGM.GetGrid();
        return clientGrid;
    }

    public int GetBankResourceCount(ResourceType type)
    {
        if (IsServer) return hostLGM.GetBankResourceCount(type);
        return 19; // 클라이언트는 정확한 은행 잔고 모름
    }

    public int GetLongestRoadLength(int playerIndex)
    {
        if (IsServer) return hostLGM.GetLongestRoadLength(playerIndex);
        // 클라이언트: 로컬 계산 가능 (보드 미러에서)
        if (clientGrid != null) return LongestRoadCalculator.Calculate(playerIndex, clientGrid);
        return 0;
    }

    public int GetLongestRoadHolder() => netLongestRoadHolder.Value;
    public int GetLargestArmyHolder() => netLargestArmyHolder.Value;

    public List<int> GetValidSettlementVertices(int playerIndex, bool isInitial)
    {
        if (IsServer) return hostLGM.GetValidSettlementVertices(playerIndex, isInitial);
        // 클라이언트: 로컬 BuildingSystem으로 계산
        if (clientGrid != null)
        {
            var bs = new BuildingSystem(clientGrid);
            var verts = bs.GetValidSettlementPositions(playerIndex, isInitial);
            var ids = new List<int>(verts.Count);
            foreach (var v in verts) ids.Add(v.Id);
            return ids;
        }
        return new List<int>();
    }

    public List<int> GetValidRoadEdges(int playerIndex, bool isInitial)
    {
        if (IsServer) return hostLGM.GetValidRoadEdges(playerIndex, isInitial);
        if (clientGrid != null)
        {
            var bs = new BuildingSystem(clientGrid);
            var edges = bs.GetValidRoadPositions(playerIndex, isInitial);
            var ids = new List<int>(edges.Count);
            foreach (var e in edges) ids.Add(e.Id);
            return ids;
        }
        return new List<int>();
    }

    public List<int> GetValidCityVertices(int playerIndex)
    {
        if (IsServer) return hostLGM.GetValidCityVertices(playerIndex);
        if (clientGrid != null)
        {
            var bs = new BuildingSystem(clientGrid);
            var verts = bs.GetValidCityUpgrades(playerIndex);
            var ids = new List<int>(verts.Count);
            foreach (var v in verts) ids.Add(v.Id);
            return ids;
        }
        return new List<int>();
    }

    // ================================================================
    // 보드 스냅샷
    // ================================================================

    BoardSnapshot CreateBoardSnapshot()
    {
        var grid = hostLGM.GetGrid();
        var snapshot = new BoardSnapshot();

        // 게임 상태
        snapshot.TurnNumber = hostLGM.TurnNumber;
        snapshot.CurrentPlayerIndex = hostLGM.CurrentPlayerIndex;
        snapshot.FirstPlayerIndex = hostLGM.FirstPlayerIndex;
        snapshot.CurrentPhase = (int)hostLGM.CurrentPhase;
        snapshot.CurrentBuildMode = (int)hostLGM.CurrentBuildMode;
        snapshot.DevCardDeckRemaining = hostLGM.DevCardDeckRemaining;
        snapshot.LongestRoadHolder = hostLGM.GetLongestRoadHolder();
        snapshot.LargestArmyHolder = hostLGM.GetLargestArmyHolder();

        // 도적 위치
        foreach (var t in grid.Tiles.Values)
        {
            if (t.HasRobber)
            {
                snapshot.RobberPosition = new HexCoordNet(t.Coord);
                break;
            }
        }

        // 타일
        var tiles = new List<TileSnapshot>();
        foreach (var t in grid.Tiles.Values)
        {
            if (t.Resource != ResourceType.Sea) // 바다 타일은 클라이언트가 로컬 생성
                tiles.Add(new TileSnapshot(t));
        }
        snapshot.Tiles = tiles.ToArray();

        // 꼭짓점 (건물/항구가 있는 것만)
        var verts = new List<VertexSnapshot>();
        foreach (var v in grid.Vertices)
        {
            if (v.Building != BuildingType.None || v.Port != PortType.None)
                verts.Add(new VertexSnapshot(v));
        }
        snapshot.Vertices = verts.ToArray();

        // 변 (도로가 있는 것만)
        var edges = new List<EdgeSnapshot>();
        foreach (var e in grid.Edges)
        {
            if (e.HasRoad)
                edges.Add(new EdgeSnapshot(e));
        }
        snapshot.Edges = edges.ToArray();

        // 플레이어 공개 정보
        int pc = hostLGM.PlayerCount;
        snapshot.Players = new PlayerPublicSnapshot[pc];
        for (int i = 0; i < pc; i++)
            snapshot.Players[i] = new PlayerPublicSnapshot(hostLGM.GetPlayerState(i));

        return snapshot;
    }

    void ApplyBoardSnapshot(BoardSnapshot snapshot)
    {
        // 클라이언트 보드는 HexGridView의 Grid를 미러로 사용
        hexGridView = FindFirstObjectByType<HexGridView>();
        if (hexGridView != null)
            clientGrid = hexGridView.Grid;

        if (clientGrid == null)
        {
            Debug.LogError("[NGM] 클라이언트 Grid 없음!");
            return;
        }

        // 타일 상태 적용 (호스트의 보드 데이터로 덮어쓰기)
        foreach (var ts in snapshot.Tiles)
        {
            var tile = clientGrid.GetTile(ts.Coord.ToHexCoord());
            if (tile != null)
            {
                tile.Resource = ts.Resource;
                tile.NumberToken = ts.NumberToken;
                tile.HasRobber = ts.HasRobber;
            }
        }

        // 타일 비주얼 재생성 (호스트 보드 데이터 반영)
        hexGridView.RebuildVisuals();

        // 건물 적용
        // 먼저 모든 vertex의 Port 초기화 (호스트 데이터로 덮어쓰기 위해)
        foreach (var v in clientGrid.Vertices)
            v.Port = PortType.None;

        foreach (var vs in snapshot.Vertices)
        {
            if (vs.Id >= 0 && vs.Id < clientGrid.Vertices.Count)
            {
                var v = clientGrid.Vertices[vs.Id];
                v.OwnerPlayerIndex = vs.OwnerPlayerIndex;
                v.Building = vs.Building;
                v.Port = vs.Port; // 호스트의 항구 데이터 적용
            }
        }

        // 도로 적용
        foreach (var es in snapshot.Edges)
        {
            if (es.Id >= 0 && es.Id < clientGrid.Edges.Count)
            {
                var e = clientGrid.Edges[es.Id];
                e.OwnerPlayerIndex = es.OwnerPlayerIndex;
                e.HasRoad = es.HasRoad;
            }
        }

        // 플레이어 미러 생성
        clientPlayers = new PlayerState[snapshot.Players.Length];
        for (int i = 0; i < snapshot.Players.Length; i++)
        {
            var ps = new PlayerState(snapshot.Players[i].PlayerIndex);
            // 공개 정보만 설정 (자원은 TargetedClientRpc로 별도 수신)
            clientPlayers[i] = ps;
        }

        Debug.Log($"[NGM] 보드 스냅샷 적용: 타일 {snapshot.Tiles.Length}, " +
                  $"건물 {snapshot.Vertices.Length}, 도로 {snapshot.Edges.Length}");
    }

    // ================================================================
    // 유틸리티
    // ================================================================

    /// <summary>NetworkList → clientPlayers 미러 동기화 (클라이언트 전용)</summary>
    void SyncClientPlayerMirror()
    {
        if (IsServer) return;
        if (clientPlayers == null)
        {
            // clientPlayers가 아직 없으면 생성
            int pc = netPlayerCount.Value;
            if (pc <= 0) return;
            clientPlayers = new PlayerState[pc];
            for (int i = 0; i < pc; i++)
                clientPlayers[i] = new PlayerState(i);
        }

        for (int i = 0; i < clientPlayers.Length; i++)
        {
            // 건물 정보를 보드 미러에서 동기화
            if (clientGrid != null)
            {
                clientPlayers[i].OwnedVertices.Clear();
                clientPlayers[i].OwnedEdges.Clear();
                foreach (var v in clientGrid.Vertices)
                {
                    if (v.OwnerPlayerIndex == i)
                        clientPlayers[i].OwnedVertices.Add(v);
                }
                foreach (var e in clientGrid.Edges)
                {
                    if (e.OwnerPlayerIndex == i)
                        clientPlayers[i].OwnedEdges.Add(e);
                }
            }

            // NetworkList 공개 정보
            if (i < netPlayerKnightsPlayed.Count)
                clientPlayers[i].KnightsPlayed = netPlayerKnightsPlayed[i];
            if (i < netPlayerVP.Count)
            {
                clientPlayers[i].HasLongestRoad = (netLongestRoadHolder.Value == i);
                clientPlayers[i].HasLargestArmy = (netLargestArmyHolder.Value == i);
            }
        }
    }

    static bool PortMatchesResource(PortType port, ResourceType resource) => port switch
    {
        PortType.Wood => resource == ResourceType.Wood,
        PortType.Brick => resource == ResourceType.Brick,
        PortType.Wool => resource == ResourceType.Wool,
        PortType.Wheat => resource == ResourceType.Wheat,
        PortType.Ore => resource == ResourceType.Ore,
        _ => false
    };
}
