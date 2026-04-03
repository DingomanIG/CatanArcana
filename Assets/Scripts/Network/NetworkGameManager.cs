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
    AIDifficulty[] hostAIDifficulties; // AI 슬롯 난이도 (호스트 전용)
    int expectedHumanCount; // 로비에서 받은 예상 인간 플레이어 수

    public const ulong AI_CLIENT_BASE = ulong.MaxValue - 100;

    // ================================================================
    // 클라이언트 공통
    // ================================================================

    int localPlayerIndex = -1;

    // clientId → playerIndex 매핑 (호스트가 관리)
    NetworkList<ulong> playerClientIds;

    // K1/K2: 동시 건설 방지 + 더블클릭 쿨다운
    bool isProcessingAction;
    readonly Dictionary<ulong, float> lastActionTime = new();

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

    // 플레이어별 공개 정보 (VP, 총 자원 수, 기사 수, 개발카드 수)
    NetworkList<int> netPlayerVP;
    NetworkList<int> netPlayerTotalResCount;
    NetworkList<int> netPlayerKnightsPlayed;
    NetworkList<int> netPlayerDevCardCounts;

    // 플레이어 이름 (FixedString64Bytes — NetworkList 지원)
    NetworkList<FixedString64Bytes> netPlayerNames;

    // 은행 자원 잔고 (인덱스 0~4 = Wood, Brick, Wool, Wheat, Ore)
    NetworkList<int> netBankResources;

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
    public new bool IsHost => IsServer;
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
    /// <summary>카드 목록 변경 시 UI 갱신 전용 (이벤트 로그 없음)</summary>
    public event Action<int> OnDevCardCountChanged;
    /// <summary>은행 자원 잔고 변경 시 UI 갱신용</summary>
    public event Action OnBankResourcesChanged;
    public event Action<int, bool> OnLongestRoadChanged;
    public event Action<int, bool> OnLargestArmyChanged;
    public event Action<int, int, ResourceType> OnRobberSteal;
    public event Action<int, ResourceType, ResourceType, int> OnBankTrade;
    public event Action<int, int> OnPlayerTrade;
    public event Action<int, Dictionary<ResourceType, int>, Dictionary<ResourceType, int>> OnIncomingTradeProposal;
#pragma warning disable CS0067 // 인터페이스 구현용 — 향후 네트워크 로직에서 사용 예정
    public event Action OnIncomingTradeCancelled;
#pragma warning restore CS0067
    public event Action<int> OnTradeDeclined; // H3/H4: 거래 거절 알림 (declinerPlayerIndex)
    public event Action<string> OnTradeRequestFailed; // 거래 요청 서버 검증 실패 알림
    public event Action<int, int> OnDiscardRequired;
    public event Action<int, string> OnPlayerDisconnected;
    public event Action OnHostDisconnected;

    // ================================================================
    // LIFECYCLE
    // ================================================================

    void Awake()
    {
        playerClientIds = new NetworkList<ulong>();
        netPlayerVP = new NetworkList<int>();
        netPlayerTotalResCount = new NetworkList<int>();
        netPlayerKnightsPlayed = new NetworkList<int>();
        netPlayerDevCardCounts = new NetworkList<int>();
        netPlayerNames = new NetworkList<FixedString64Bytes>();
        netBankResources = new NetworkList<int>();
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
        netPlayerDevCardCounts.OnListChanged += _ => SyncClientPlayerMirror();
        netPlayerNames.OnListChanged += _ => OnPlayerListChanged?.Invoke();
        netBankResources.OnListChanged += _ => OnBankResourcesChanged?.Invoke();

        // G1/G2: 최장도로/최대기사단 보유자 변경 시 미러 갱신
        netLongestRoadHolder.OnValueChanged += (_, __) => SyncClientPlayerMirror();
        netLargestArmyHolder.OnValueChanged += (_, __) => SyncClientPlayerMirror();

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
        // 클라이언트: 호스트가 나갔을 때 알림
        if (!IsServer && GameServices.GameManager == (IGameManager)this)
        {
            Debug.Log("[NGM] 호스트 연결 끊김 — 게임 종료");
            OnHostDisconnected?.Invoke();
        }

        if (GameServices.GameManager == (IGameManager)this)
            GameServices.GameManager = null;

        // 호스트: 콜백 해제
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnWaitForClients;
        }
    }

    /// <summary>클라이언트 퇴장 시 처리 (호스트에서 실행)</summary>
    void OnClientDisconnected(ulong clientId)
    {
        int pi = GetPlayerIndexFromClientId(clientId);
        if (pi < 0 || IsAIPlayer(pi)) return; // AI는 퇴장 처리 불필요

        string playerName = GetPlayerName(pi);
        Debug.Log($"[NGM] 플레이어 퇴장: [{pi}] {playerName} (clientId={clientId})");

        // 모든 클라이언트에 알림
        NotifyPlayerDisconnectedClientRpc(pi, playerName);

        // 퇴장한 플레이어 턴이면 자동으로 턴 넘기기
        if (hostLGM.CurrentPlayerIndex == pi && hostLGM.CurrentPhase != GamePhase.GameOver)
        {
            hostLGM.EndTurn();
        }
    }

    [ClientRpc]
    void NotifyPlayerDisconnectedClientRpc(int playerIndex, string playerName)
    {
        Debug.Log($"[NGM] 플레이어 퇴장 알림: [{playerIndex}] {playerName}");
        OnPlayerDisconnected?.Invoke(playerIndex, playerName);
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

        // AI 슬롯 정보 읽기
        var flow = SceneFlowManager.Instance;
        hostAIDifficulties = flow?.AIDifficulties ?? new AIDifficulty[4];

        // 로비 데이터에서 정확한 인원수 계산 (ConnectedClientsList는 씬 로딩 중 불완전)
        int aiCount = 0;
        for (int i = 0; i < hostAIDifficulties.Length; i++)
            if (hostAIDifficulties[i] != AIDifficulty.None) aiCount++;

        int totalFromLobby = flow?.LocalPlayerCount ?? 2;
        expectedHumanCount = Mathf.Max(1, totalFromLobby - aiCount);

        int playerCount = expectedHumanCount + aiCount;
        if (playerCount < 2) playerCount = 2; // 최소 2인
        hostLGM.InitializePlayers(playerCount);
        netPlayerCount.Value = playerCount;

        // LGM 이벤트 구독 → 네트워크 브로드캐스트
        SubscribeHostEvents();

        // 클라이언트 퇴장 콜백
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        // 플레이어 매핑: 모든 인간 클라이언트 접속 대기
        TryFinalizePlayerMapping();

        Debug.Log($"[NGM] 호스트 셋업 — {playerCount}명 (인간 {expectedHumanCount}, AI {aiCount}), 접속 대기 중...");
    }

    /// <summary>모든 예상 인간 클라이언트가 접속했으면 매핑 실행</summary>
    void TryFinalizePlayerMapping()
    {
        int currentHumans = NetworkManager.Singleton.ConnectedClientsList.Count;
        if (currentHumans >= expectedHumanCount)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnWaitForClients;
            SetupPlayerMapping();
            Debug.Log($"[NGM] 플레이어 매핑 완료 — 전원 접속 ({currentHumans}명)");
        }
        else
        {
            Debug.Log($"[NGM] 접속 대기: {currentHumans}/{expectedHumanCount}명");
            NetworkManager.Singleton.OnClientConnectedCallback += OnWaitForClients;
        }
    }

    void OnWaitForClients(ulong clientId)
    {
        Debug.Log($"[NGM] 클라이언트 접속 감지: {clientId} ({NetworkManager.Singleton.ConnectedClientsList.Count}/{expectedHumanCount}명)");
        TryFinalizePlayerMapping();
    }

    void SetupPlayerMapping()
    {
        var clients = NetworkManager.Singleton.ConnectedClientsList;

        // 인간 클라이언트 + AI sentinel ID 합치기
        var allIds = new List<ulong>();
        foreach (var c in clients) allIds.Add(c.ClientId);

        // AI 슬롯 추가 (hostAIDifficulties에서 None이 아닌 슬롯)
        int aiIdx = 0;
        if (hostAIDifficulties != null)
        {
            for (int i = 0; i < hostAIDifficulties.Length; i++)
            {
                if (hostAIDifficulties[i] != AIDifficulty.None)
                    allIds.Add(AI_CLIENT_BASE + (ulong)aiIdx++);
            }
        }

        // 셔플
        for (int i = allIds.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (allIds[i], allIds[j]) = (allIds[j], allIds[i]);
        }

        playerClientIds.Clear();
        foreach (var id in allIds)
            playerClientIds.Add(id);

        // NetworkList 초기화
        for (int i = 0; i < allIds.Count; i++)
        {
            netPlayerVP.Add(0);
            netPlayerTotalResCount.Add(0);
            netPlayerKnightsPlayed.Add(0);
            netPlayerDevCardCounts.Add(0);

            // AI 플레이어는 이름을 바로 설정
            if (IsAIPlayer(i))
            {
                var diff = GetAIDifficultyForPlayer(i);
                netPlayerNames.Add(new FixedString64Bytes(AIDifficultySettings.GetAIName(diff)));
            }
            else
            {
                netPlayerNames.Add(new FixedString64Bytes($"플레이어 {i + 1}")); // 기본값, ServerRpc로 덮어씀
            }
        }

        // 각 인간 클라이언트에게 본인 인덱스 알림
        for (int i = 0; i < allIds.Count; i++)
        {
            if (IsAIPlayer(i)) continue; // AI에게는 RPC 불필요

            var targetParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { allIds[i] }
                }
            };
            AssignPlayerIndexClientRpc(i, targetParams);
        }

        // 호스트 자신의 인덱스 직접 설정
        localPlayerIndex = GetPlayerIndexFromClientId(NetworkManager.Singleton.LocalClientId);
    }

    /// <summary>해당 playerIndex가 AI인지 확인</summary>
    public bool IsAIPlayer(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= playerClientIds.Count) return false;
        return playerClientIds[playerIndex] >= AI_CLIENT_BASE;
    }

    /// <summary>네트워크용 AI 난이도 배열 반환 (playerIndex 기준, 인간=None)</summary>
    public AIDifficulty[] GetNetworkAIDifficulties()
    {
        var result = new AIDifficulty[playerClientIds.Count];
        for (int i = 0; i < playerClientIds.Count; i++)
            result[i] = IsAIPlayer(i) ? GetAIDifficultyForPlayer(i) : AIDifficulty.None;
        return result;
    }

    /// <summary>AI playerIndex의 난이도 반환</summary>
    AIDifficulty GetAIDifficultyForPlayer(int playerIndex)
    {
        if (!IsAIPlayer(playerIndex) || hostAIDifficulties == null) return AIDifficulty.None;

        // AI sentinel ID에서 aiIdx 추출 → hostAIDifficulties에서 해당 난이도 찾기
        ulong clientId = playerClientIds[playerIndex];
        int aiIdx = (int)(clientId - AI_CLIENT_BASE);

        // hostAIDifficulties에서 aiIdx번째 AI 찾기
        int count = 0;
        for (int i = 0; i < hostAIDifficulties.Length; i++)
        {
            if (hostAIDifficulties[i] != AIDifficulty.None)
            {
                if (count == aiIdx) return hostAIDifficulties[i];
                count++;
            }
        }
        return AIDifficulty.Lv5; // 폴백
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
            bool sameValue = netCurrentPlayerIndex.Value == pi;
            netCurrentPlayerIndex.Value = pi;
            netTurnNumber.Value = hostLGM.TurnNumber;
            NetLog.Phase($"턴 전환 → P{pi} (턴#{hostLGM.TurnNumber})");

            // 같은 값이면 OnValueChanged가 안 터지므로 직접 이벤트 발생
            // (예: 초기 배치 마지막 플레이어 == firstPlayerIndex일 때)
            if (sameValue)
                OnTurnChanged?.Invoke(pi);
        };

        hostLGM.OnPhaseChanged += phase =>
        {
            var prevPhase = (GamePhase)netCurrentPhase.Value;
            netCurrentPhase.Value = (int)phase;
            NetLog.Phase($"페이즈 전환: {prevPhase} → {phase}");

            // 초기 배치 → 본 게임 전환 시 알림
            if (prevPhase == GamePhase.InitialPlacement && phase == GamePhase.RollDice)
            {
                NotifyInitialPlacementFinishedClientRpc();
            }
        };

        hostLGM.OnDiceRolled += (d1, d2, total) =>
        {
            NetLog.ClientRpc("DiceRolled", $"{d1}+{d2}={total}");
            NotifyDiceRolledClientRpc(d1, d2, total);
        };

        hostLGM.OnBuildingPlaced += (pi, vid, bt) =>
        {
            NetLog.ClientRpc("BuildingPlaced", $"P{pi} {bt} @v{vid}");
            NotifyBuildingPlacedClientRpc(pi, vid, (int)bt);
            SyncPlayerPublicInfo(pi);
        };

        hostLGM.OnRoadPlaced += (pi, eid) =>
        {
            NetLog.ClientRpc("RoadPlaced", $"P{pi} @e{eid}");
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
            // 은행 자원 동기화
            SyncBankResources();
            // 호스트 UI: 상대 자원 변동 시 이벤트 전파 (상대 카드 갱신용)
            // 호스트 본인은 NotifyResourceUpdateClientRpc에서 처리되므로 제외
            if (pi != localPlayerIndex)
                OnResourceChanged?.Invoke(pi, rt, count);
        };

        hostLGM.OnVPChanged += (pi, vp) =>
        {
            if (pi < netPlayerVP.Count)
                netPlayerVP[pi] = vp;
            NotifyVPChangedClientRpc(pi, vp);
        };

        hostLGM.OnRobberMoved += coord =>
        {
            NetLog.ClientRpc("RobberMoved", $"({coord.Q},{coord.R})");
            netRobberPosition.Value = new HexCoordNet(coord);
            NotifyRobberMovedClientRpc(new HexCoordNet(coord));
        };

        hostLGM.OnBuildModeChanged += mode =>
        {
            netCurrentBuildMode.Value = (int)mode;
        };

        hostLGM.OnDevCardPurchased += (pi, ct) =>
        {
            NetLog.ClientRpc("DevCardPurchased", $"P{pi} {ct}");
            // 구매자에게: 카드 추가 + 구매 알림 (카드 타입 포함)
            var buyerParams = GetTargetedParams(pi);
            NotifyDevCardAddedClientRpc(pi, (int)ct, buyerParams);
            // 전체에: 구매 사실만 알림 (카드 타입 비공개)
            NotifyDevCardPurchasedClientRpc(pi, (int)DevCardType.Hidden);
            netDevCardDeckRemaining.Value = hostLGM.DevCardDeckRemaining;
            // 개발카드 수 동기화
            if (pi < netPlayerDevCardCounts.Count)
                netPlayerDevCardCounts[pi] = hostLGM.GetPlayerState(pi).DevCards.Count;
        };

        hostLGM.OnDevCardUsed += (pi, ct) =>
        {
            NetLog.ClientRpc("DevCardUsed", $"P{pi} {ct}");
            NotifyDevCardUsedClientRpc(pi, (int)ct);
            netDevCardState.Value = (int)hostLGM.DevCardState;
            // 개발카드 수 동기화
            if (pi < netPlayerDevCardCounts.Count)
                netPlayerDevCardCounts[pi] = hostLGM.GetPlayerState(pi).DevCards.Count;
            // 기사 카운트 등 공개 정보 동기화
            SyncPlayerPublicInfo(pi);
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
            NetLog.ClientRpc("RobberSteal", $"P{thief}→P{victim} {res}");
            NotifyRobberStealClientRpc(thief, victim, (int)res);
            SendResourceToOwner(thief);
            SendResourceToOwner(victim);
        };

        hostLGM.OnBankTrade += (pi, gave, recv, rate) =>
        {
            NetLog.ClientRpc("BankTrade", $"P{pi} {gave}→{recv} ({rate}:1)");
            NotifyBankTradeClientRpc(pi, (int)gave, (int)recv, rate);
        };

        hostLGM.OnPlayerTrade += (p1, p2) =>
        {
            NetLog.ClientRpc("PlayerTrade", $"P{p1}↔P{p2}");
            NotifyPlayerTradeClientRpc(p1, p2);
            SendResourceToOwner(p1);
            SendResourceToOwner(p2);
        };

        hostLGM.OnIncomingTradeProposal += (proposer, offer, request) =>
        {
            int target = hostLGM.PendingTradeTarget;
            if (target < 0) return;
            var targetParams = GetTargetedParams(target);
            NotifyIncomingTradeProposalClientRpc(proposer, ResArray.FromDict(offer), ResArray.FromDict(request), targetParams);
        };

        hostLGM.OnTradeDeclined += (decliner) =>
        {
            // 제안자(현재 턴 플레이어)에게 거절 알림
            int proposer = hostLGM.CurrentPlayerIndex;
            var proposerParams = GetTargetedParams(proposer);
            NotifyTradeDeclinedClientRpc(decliner, proposerParams);
        };

        hostLGM.OnDiscardRequired += (pi, count) =>
        {
            NetLog.ClientRpc("DiscardRequired", $"P{pi} {count}장");
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

    void SyncBankResources()
    {
        if (!IsServer || hostLGM == null) return;

        // 초기화: 아직 리스트가 비어있으면 5개 항목 추가
        while (netBankResources.Count < 5)
            netBankResources.Add(19);

        var types = new[] { ResourceType.Wood, ResourceType.Brick, ResourceType.Wool, ResourceType.Wheat, ResourceType.Ore };
        for (int i = 0; i < types.Length; i++)
            netBankResources[i] = hostLGM.GetBankResourceCount(types[i]);
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
        // AI 플레이어에게는 호스트 자신에게 보냄 (AI 턴은 호스트에서 처리)
        if (clientId >= AI_CLIENT_BASE)
            clientId = NetworkManager.Singleton.LocalClientId;
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

    /// <summary>F1: 구매자에게만 카드 타입 전송 → 클라이언트 DevCards에 추가</summary>
    [ClientRpc]
    void NotifyDevCardAddedClientRpc(int playerIndex, int cardType, ClientRpcParams clientRpcParams = default)
    {
        if (IsServer) return; // 호스트는 hostLGM이 이미 처리
        if (clientPlayers == null || playerIndex < 0 || playerIndex >= clientPlayers.Length) return;

        var card = new DevelopmentCard((DevCardType)cardType, TurnNumber);
        clientPlayers[playerIndex].DevCards.Add(card);
        Debug.Log($"[NGM] 발전카드 추가: {(DevCardType)cardType} (DevCards 수: {clientPlayers[playerIndex].DevCards.Count})");

        // 카드 추가 후 HUD 갱신만 트리거 (이벤트 로그는 NotifyDevCardPurchasedClientRpc에서 처리)
        if (playerIndex == localPlayerIndex)
            OnDevCardCountChanged?.Invoke(playerIndex);
    }

    [ClientRpc]
    void NotifyDevCardUsedClientRpc(int playerIndex, int cardType)
    {
        // 클라이언트: 로컬 DevCards에서 사용된 카드 제거
        if (!IsServer && clientPlayers != null && playerIndex == localPlayerIndex
            && playerIndex >= 0 && playerIndex < clientPlayers.Length)
        {
            var devCards = clientPlayers[playerIndex].DevCards;
            for (int i = 0; i < devCards.Count; i++)
            {
                if (devCards[i].Type == (DevCardType)cardType)
                {
                    devCards.RemoveAt(i);
                    break;
                }
            }
        }
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

    /// <summary>H3/H4: 거래 거절 알림 (제안자에게만 전송)</summary>
    [ClientRpc]
    void NotifyTradeDeclinedClientRpc(int declinerPlayerIndex, ClientRpcParams clientRpcParams = default)
    {
        OnTradeDeclined?.Invoke(declinerPlayerIndex);
    }

    /// <summary>거래 요청 서버 검증 실패 시 제안자에게 알림</summary>
    [ClientRpc]
    void NotifyTradeRequestFailedClientRpc(FixedString128Bytes reason, ClientRpcParams clientRpcParams = default)
    {
        OnTradeRequestFailed?.Invoke(reason.ToString());
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
    // 게임 시작 준비 (턴 순서 확인 후 전원 Ready)
    // ================================================================

    readonly HashSet<int> readyPlayers = new();

    [ServerRpc(RequireOwnership = false)]
    public void RequestPlayerReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        int pi = GetPlayerIndexFromClientId(rpcParams.Receive.SenderClientId);
        if (pi < 0) return;

        readyPlayers.Add(pi);

        // AI 플레이어는 자동 준비 처리
        for (int i = 0; i < playerClientIds.Count; i++)
        {
            if (IsAIPlayer(i)) readyPlayers.Add(i);
        }

        Debug.Log($"[NGM] 플레이어 준비 완료: [{pi}] ({readyPlayers.Count}/{netPlayerCount.Value})");

        if (readyPlayers.Count >= netPlayerCount.Value)
        {
            Debug.Log("[NGM] 전원 준비 완료 → 게임 시작!");
            readyPlayers.Clear();
            StartGame();
        }
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

    /// <summary>K1/K2: 액션 처리 중 락 + 더블클릭 쿨다운 (0.3초)</summary>
    bool TryAcquireActionLock(ServerRpcParams rpcParams)
    {
        if (isProcessingAction)
        {
            NetLog.Warn("LOCK", "액션 락 충돌 — 이미 처리 중");
            return false;
        }

        // 초기 배치 중에는 마을→도로 연속 배치이므로 쿨다운 스킵
        bool isInitialPlacement = hostLGM != null && hostLGM.CurrentPhase == GamePhase.InitialPlacement;

        ulong clientId = rpcParams.Receive.SenderClientId;
        float now = Time.time;
        if (!isInitialPlacement && lastActionTime.TryGetValue(clientId, out float last) && now - last < 0.3f)
        {
            NetLog.Warn("LOCK", $"더블클릭 쿨다운 (client={clientId})");
            return false;
        }

        isProcessingAction = true;
        lastActionTime[clientId] = now;
        return true;
    }

    void ReleaseActionLock() => isProcessingAction = false;

    [ServerRpc(RequireOwnership = false)]
    public void RequestRollDiceServerRpc(ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        NetLog.ServerRpc("RollDice", pi);
        hostLGM.RollDice();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestEndTurnServerRpc(ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        NetLog.ServerRpc("EndTurn", pi);
        hostLGM.EndTurn();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestBuildSettlementServerRpc(int vertexId, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        if (!TryAcquireActionLock(rpcParams)) return;
        NetLog.ServerRpc("BuildSettlement", pi, $"v{vertexId}");
        try { hostLGM.TryBuildSettlement(vertexId); }
        finally { ReleaseActionLock(); }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestBuildCityServerRpc(int vertexId, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        if (!TryAcquireActionLock(rpcParams)) return;
        NetLog.ServerRpc("BuildCity", pi, $"v{vertexId}");
        try { hostLGM.TryBuildCity(vertexId); }
        finally { ReleaseActionLock(); }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestBuildRoadServerRpc(int edgeId, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        if (!TryAcquireActionLock(rpcParams)) return;
        NetLog.ServerRpc("BuildRoad", pi, $"e{edgeId}");
        try { hostLGM.TryBuildRoad(edgeId); }
        finally { ReleaseActionLock(); }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestEnterBuildModeServerRpc(int mode, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        NetLog.ServerRpc("EnterBuildMode", pi, $"{(BuildMode)mode}");
        hostLGM.EnterBuildMode((BuildMode)mode);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestCancelBuildModeServerRpc(ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        NetLog.ServerRpc("CancelBuildMode", pi);
        hostLGM.CancelBuildMode();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestBuyDevCardServerRpc(ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        if (!TryAcquireActionLock(rpcParams)) return;
        NetLog.ServerRpc("BuyDevCard", pi);
        try { hostLGM.TryBuyDevCard(); }
        finally { ReleaseActionLock(); }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestUseKnightServerRpc(HexCoordNet target, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        NetLog.ServerRpc("UseKnight", pi, $"({target.Q},{target.R})");
        hostLGM.TryUseKnight(target.ToHexCoord());
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestUseRoadBuildingServerRpc(ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        NetLog.ServerRpc("UseRoadBuilding", pi);
        hostLGM.TryUseRoadBuilding();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestUseYearOfPlentyServerRpc(int res1, int res2, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        NetLog.ServerRpc("UseYearOfPlenty", pi, $"{(ResourceType)res1}+{(ResourceType)res2}");
        hostLGM.TryUseYearOfPlenty((ResourceType)res1, (ResourceType)res2);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestUseMonopolyServerRpc(int targetResource, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        NetLog.ServerRpc("UseMonopoly", pi, $"{(ResourceType)targetResource}");
        hostLGM.TryUseMonopoly((ResourceType)targetResource);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestBankTradeServerRpc(int give, int receive, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        if (!TryAcquireActionLock(rpcParams)) return;
        NetLog.ServerRpc("BankTrade", pi, $"{(ResourceType)give}→{(ResourceType)receive}");
        try { hostLGM.TryBankTrade((ResourceType)give, (ResourceType)receive); }
        finally { ReleaseActionLock(); }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestPlayerTradeServerRpc(int otherPlayer, ResArray offer, ResArray request,
        ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (pi < 0)
        {
            Debug.LogWarning($"[NGM] PlayerTrade: ValidateSender 실패 (senderId={rpcParams.Receive.SenderClientId})");
            return;
        }
        if (!ValidateTurn(pi))
        {
            Debug.LogWarning($"[NGM] PlayerTrade: 턴 검증 실패 sender={pi}, current={hostLGM.CurrentPlayerIndex}");
            var failParams = GetTargetedParams(pi);
            NotifyTradeRequestFailedClientRpc("현재 당신의 턴이 아닙니다.", failParams);
            return;
        }
        NetLog.ServerRpc("PlayerTrade", pi, $"→P{otherPlayer}");
        bool success = hostLGM.TryPlayerTrade(otherPlayer, offer.ToDict(), request.ToDict());
        if (!success && hostLGM.PendingTradeTarget < 0)
        {
            // 제안 전환이 아닌 진짜 실패 (자원 부족, 페이즈 불일치 등)
            Debug.LogWarning($"[NGM] PlayerTrade: TryPlayerTrade 실패 (P{pi}→P{otherPlayer}, phase={hostLGM.CurrentPhase})");
            var failParams = GetTargetedParams(pi);
            NotifyTradeRequestFailedClientRpc("거래 요청이 실패했습니다.", failParams);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestRespondTradeServerRpc(bool accept, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        NetLog.ServerRpc("RespondTrade", pi, accept ? "수락" : "거절");
        // 거래 응답은 턴 플레이어가 아닌 제안 대상이 보냄
        hostLGM.RespondToIncomingTrade(accept);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestMoveRobberServerRpc(HexCoordNet target, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        if (!ValidateTurn(pi)) return;
        NetLog.ServerRpc("MoveRobber", pi, $"({target.Q},{target.R})");

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
        NetLog.ServerRpc("StealFromPlayer", pi, $"→P{victimIndex}");
        hostLGM.TryStealFromPlayer(victimIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestConfirmDiscardServerRpc(ResArray toDiscard, ServerRpcParams rpcParams = default)
    {
        int pi = ValidateSender(rpcParams);
        // L2: 디스카드 대상 플레이어가 맞는지 검증
        if (!hostLGM.IsWaitingForDiscard)
        {
            Debug.LogWarning($"[NGM] 디스카드 대기 중이 아님 (sender={pi})");
            return;
        }
        var pending = hostLGM.GetPendingDiscardPlayer();
        if (pending >= 0 && pending != pi)
        {
            Debug.LogWarning($"[NGM] 디스카드 발신자 불일치: expected={pending}, sender={pi}");
            return;
        }
        NetLog.ServerRpc("ConfirmDiscard", pi);
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

    /// <summary>호스트: PrepareGame + 보드 동기화 + 턴 순서 오버레이 알림</summary>
    public void PrepareAndSyncBoard()
    {
        if (!IsServer) return;

        hostLGM.PrepareGame();

        // 보드 스냅샷 전송 (타일+항구 동기화)
        var snapshot = CreateBoardSnapshot();
        SyncFullBoardStateClientRpc(snapshot);

        // 모든 클라이언트에 턴 순서 오버레이 표시 알림
        ShowTurnOrderClientRpc();
    }

    [ClientRpc]
    void ShowTurnOrderClientRpc()
    {
        // GameHUDController의 ShowTurnOrderOverlay 호출
        var hud = FindFirstObjectByType<GameHUDController>();
        hud?.ShowTurnOrderOverlay();
    }

    public void StartGame()
    {
        if (IsServer)
        {
            // 보드 전체 동기화 (PrepareGame에서 재배치된 보드 전송)
            var snapshot = CreateBoardSnapshot();
            SyncFullBoardStateClientRpc(snapshot);

            // 게임 시작 (InitialPlacement 진입)
            hostLGM.StartGame();

            // 은행 자원 초기 동기화
            SyncBankResources();
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
    public bool IsPlayerAI(int playerIndex) => IsAIPlayer(playerIndex);

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
            // 호스트도 로컬 UI에 빌드모드 진입 (SuppressUICommands로 LGM이 직접 호출 못함)
            BuildModeController.Instance?.EnterBuildMode(mode);
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
            BuildModeController.Instance?.CancelBuildMode();
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

        // 클라이언트: NetworkList에서 은행 잔고 조회
        int idx = type switch
        {
            ResourceType.Wood  => 0,
            ResourceType.Brick => 1,
            ResourceType.Wool  => 2,
            ResourceType.Wheat => 3,
            ResourceType.Ore   => 4,
            _ => -1
        };
        if (idx >= 0 && idx < netBankResources.Count)
            return netBankResources[idx];
        return 19;
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

        // 건물/항구 적용 (비주얼 재생성 전에 데이터 먼저 덮어쓰기)
        // 먼저 모든 vertex의 Port 초기화
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

        // 타일 + 항구 데이터 적용 완료 후 비주얼 재생성
        hexGridView.RebuildVisuals();

        // BUG-1: 보드 재구축 후 BuildModeController가 새 그리드 참조하도록 갱신
        BuildModeController.Instance?.RefreshBuildingSystem(clientGrid);

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

    // 상대 자원 총합 캐시 (변동 감지용)
    readonly Dictionary<int, int> prevOpponentResCount = new();

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

            // 개발카드 수 동기화 (상대는 수량만 알 수 있음)
            if (i != localPlayerIndex && i < netPlayerDevCardCounts.Count)
            {
                int targetCount = netPlayerDevCardCounts[i];
                // DevCards 리스트 크기를 맞춤 (타입은 Unknown으로)
                while (clientPlayers[i].DevCards.Count < targetCount)
                    clientPlayers[i].DevCards.Add(new DevelopmentCard(DevCardType.Hidden, 0));
                while (clientPlayers[i].DevCards.Count > targetCount)
                    clientPlayers[i].DevCards.RemoveAt(clientPlayers[i].DevCards.Count - 1);
            }

            // C4: 상대 자원 총합 변동 시 OnResourceChanged 발화 → 상대 카드 UI 갱신
            if (i != localPlayerIndex && i < netPlayerTotalResCount.Count)
            {
                int newTotal = netPlayerTotalResCount[i];
                prevOpponentResCount.TryGetValue(i, out int prevTotal);
                if (newTotal != prevTotal)
                {
                    prevOpponentResCount[i] = newTotal;
                    OnResourceChanged?.Invoke(i, ResourceType.Wood, newTotal);
                }
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
