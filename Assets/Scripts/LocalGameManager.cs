using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로컬 전용 게임 매니저 - 네트워크 없이 단독 플레이
/// 자원 분배, 건설, 도적, 발전카드, 최장교역로, 최대기사단 처리
/// </summary>
[DefaultExecutionOrder(-100)]
public class LocalGameManager : MonoBehaviour, IGameManager
{
    [Header("게임 설정")]
    [SerializeField, Range(2, 4)] int playerCount = 4;

    [Header("참조")]
    [SerializeField] HexGridView hexGridView;

    // AI 모드 (-1 = 핫시트/전원 인간)
    int humanPlayerIndex = -1;

    // 상태
    int turnNumber;
    int currentPlayerIndex;
    int firstPlayerIndex;
    GamePhase currentPhase = GamePhase.WaitingForPlayers;
    BuildMode currentBuildMode = BuildMode.None;

    // 초기 배치 상태
    int initialRound;
    int initialStepInRound;
    bool initialWaitingForRoad;
    int lastPlacedVertexId;

    // 발전카드 상태
    DevCardDeck devCardDeck;
    DevCardUseState devCardUseState = DevCardUseState.None;
    int freeRoadsRemaining;

    // 도적 약탈 상태
    List<int> robberStealCandidates = new();
    bool returnToActionAfterSteal; // 기사 카드 사용 후 약탈 시 true

    // 보너스 보유자 (-1 = 없음)
    int longestRoadHolder = -1;
    int largestArmyHolder = -1;

    // 시스템
    HexGrid grid;
    BuildingSystem buildingSystem;
    PlayerState[] players;

    static readonly string[] DefaultPlayerNames = { "플레이어 1", "플레이어 2", "플레이어 3", "플레이어 4" };

    // ========================
    // IGameManager 프로퍼티
    // ========================

    public int TurnNumber => turnNumber;
    public int CurrentPlayerIndex => currentPlayerIndex;
    public int LocalPlayerIndex => humanPlayerIndex >= 0 ? humanPlayerIndex : currentPlayerIndex;
    public int FirstPlayerIndex => firstPlayerIndex;
    public int PlayerCount => playerCount;
    public GamePhase CurrentPhase => currentPhase;
    public bool IsHost => true;
    public BuildMode CurrentBuildMode => currentBuildMode;
    public DevCardUseState DevCardState => devCardUseState;
    public int DevCardDeckRemaining => devCardDeck?.RemainingCount ?? 0;

    // ========================
    // 이벤트
    // ========================

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

    // 수신 대기 중인 거래 제안 (AI→인간)
    class PendingTrade
    {
        public int proposer;
        public int target;
        public Dictionary<ResourceType, int> offer;   // 제안자가 주는 것 (인간이 받는 것)
        public Dictionary<ResourceType, int> request; // 제안자가 원하는 것 (인간이 줘야 하는 것)
    }
    PendingTrade pendingIncomingTrade;

    // ========================
    // LIFECYCLE
    // ========================

    void Awake()
    {
        GameServices.GameManager = this;

        // SceneFlowManager에서 로컬 플레이 설정 적용
        if (SceneFlowManager.Instance != null && SceneFlowManager.Instance.IsLocalPlay)
        {
            playerCount = SceneFlowManager.Instance.LocalPlayerCount;
            humanPlayerIndex = 0;

            // AIController가 없으면 자동 추가
            var ai = GetComponent<AIController>();
            if (ai == null)
                ai = gameObject.AddComponent<AIController>();

            // 메뉴에서 선택한 난이도 적용
            ai.SetDifficulties(SceneFlowManager.Instance.AIDifficulties);
        }

        // players는 Awake에서 초기화 (Start 전에 접근될 수 있음)
        players = new PlayerState[playerCount];
        for (int i = 0; i < playerCount; i++)
            players[i] = new PlayerState(i);
    }

    void Start()
    {
        grid = hexGridView.Grid;
        buildingSystem = new BuildingSystem(grid);
        devCardDeck = new DevCardDeck();

        // 타일 플래시 이펙트 자동 추가
        if (GetComponent<TileFlashEffect>() == null)
            gameObject.AddComponent<TileFlashEffect>();

        OnPlayerListChanged?.Invoke();
    }

    // ========================
    // 기본 액션
    // ========================

    public void PrepareGame()
    {
        // 보드 데이터 리셋 (건물/도로/도적 초기화)
        grid.ResetBoardState();

        // 건물 비주얼 제거
        var buildingVisuals = FindObjectOfType<BuildingVisuals>();
        buildingVisuals?.ClearAllBuildings();

        // 도적 비주얼 리셋
        hexGridView.ResetRobberVisual();

        // 발전카드 덱 재생성
        devCardDeck = new DevCardDeck();

        // 플레이어 상태 재생성
        for (int i = 0; i < playerCount; i++)
            players[i] = new PlayerState(i);

        // 보너스 보유자 리셋
        longestRoadHolder = -1;
        largestArmyHolder = -1;

        // 발전카드/도적 상태 리셋
        devCardUseState = DevCardUseState.None;
        freeRoadsRemaining = 0;
        robberStealCandidates.Clear();
        returnToActionAfterSteal = false;
        pendingIncomingTrade = null;
        currentBuildMode = BuildMode.None;

        // 턴 상태 리셋
        firstPlayerIndex = UnityEngine.Random.Range(0, playerCount);
        currentPlayerIndex = firstPlayerIndex;
        turnNumber = 0;
        initialRound = 0;
        initialStepInRound = 0;
        initialWaitingForRoad = false;

        OnPlayerListChanged?.Invoke();
        Debug.Log($"[Local] 게임 준비 완료! 선플레이어: {GetPlayerName(firstPlayerIndex)}");
    }

    public void StartGame()
    {
        SetPhase(GamePhase.InitialPlacement);
        OnTurnChanged?.Invoke(currentPlayerIndex);
        Debug.Log($"[Local] 초기 배치 시작! 선플레이어: {GetPlayerName(firstPlayerIndex)} - 마을을 배치하세요");

        StartInitialSettlementMode();
    }

    public void RollDice()
    {
        if (currentPhase != GamePhase.RollDice) return;

        int die1 = UnityEngine.Random.Range(1, 7);
        int die2 = UnityEngine.Random.Range(1, 7);
        int total = die1 + die2;

        OnDiceRolled?.Invoke(die1, die2, total);
        Debug.Log($"[Local] 주사위: {die1} + {die2} = {total}");

        if (total == 7)
        {
            SFXManager.Instance?.Play(SFXType.DiceSeven);
            HandleSeven();
        }
        else
        {
            SFXManager.Instance?.Play(SFXType.DiceLand);
            DistributeResources(total);
            SetPhase(GamePhase.Action);
        }
    }

    public void EndTurn()
    {
        if (currentPhase != GamePhase.Action) return;

        CancelBuildMode();
        devCardUseState = DevCardUseState.None;
        freeRoadsRemaining = 0;
        players[currentPlayerIndex].HasUsedDevCardThisTurn = false;

        currentPlayerIndex = (currentPlayerIndex + 1) % playerCount;
        if (currentPlayerIndex == 0) turnNumber++;

        SetPhase(GamePhase.RollDice);
        OnTurnChanged?.Invoke(currentPlayerIndex);
        Debug.Log($"[Local] 턴 {turnNumber} - {GetPlayerName(currentPlayerIndex)}");
    }

    public bool IsMyTurn() => humanPlayerIndex < 0 || currentPlayerIndex == humanPlayerIndex;
    public bool IsPlayerAI(int playerIndex) => humanPlayerIndex >= 0 && playerIndex != humanPlayerIndex;

    public string GetPlayerName(int index)
    {
        // AI 플레이어면 레벨별 캐릭터 이름 사용
        var ai = GetComponent<AIController>();
        if (ai != null && ai.IsAI(index))
        {
            var diff = ai.GetDifficulty(index);
            return $"{AIDifficultySettings.GetAIName(diff)}(AI)";
        }

        string name = index < DefaultPlayerNames.Length ? DefaultPlayerNames[index] : $"Player {index + 1}";
        return name;
    }

    // ========================
    // 초기 배치
    // ========================

    void StartInitialSettlementMode()
    {
        // AI 턴에서는 BuildModeController 생략 (AI가 직접 TryBuildSettlement 호출)
        if (humanPlayerIndex >= 0 && currentPlayerIndex != humanPlayerIndex) return;
        BuildModeController.Instance?.SetInitialPlacement(true);
        BuildModeController.Instance?.EnterBuildMode(BuildMode.PlacingSettlement);
    }

    void StartInitialRoadMode()
    {
        if (humanPlayerIndex >= 0 && currentPlayerIndex != humanPlayerIndex) return;
        BuildModeController.Instance?.SetInitialPlacement(true);
        BuildModeController.Instance?.EnterBuildMode(BuildMode.PlacingRoad);
    }

    void OnInitialSettlementPlaced(int vertexId)
    {
        lastPlacedVertexId = vertexId;
        initialWaitingForRoad = true;
        Debug.Log($"[Local] {GetPlayerName(currentPlayerIndex)} 마을 배치 완료 → 도로를 배치하세요");
        StartInitialRoadMode();
    }

    void OnInitialRoadPlaced()
    {
        initialWaitingForRoad = false;

        if (initialRound == 1)
        {
            GrantInitialResources(lastPlacedVertexId);
        }

        if (!AdvanceInitialPlacement())
        {
            FinishInitialPlacement();
        }
    }

    bool AdvanceInitialPlacement()
    {
        initialStepInRound++;

        if (initialStepInRound >= playerCount)
        {
            if (initialRound == 0)
            {
                // 라운드 1 시작 (역순) - 마지막 플레이어가 연속 배치
                initialRound = 1;
                initialStepInRound = 0;
                // currentPlayerIndex 유지 (스네이크 드래프트)
            }
            else
            {
                return false; // 초기 배치 완료
            }
        }
        else
        {
            if (initialRound == 0)
                currentPlayerIndex = (firstPlayerIndex + initialStepInRound) % playerCount;
            else
                currentPlayerIndex = (firstPlayerIndex + playerCount - 1 - initialStepInRound) % playerCount;
        }

        OnTurnChanged?.Invoke(currentPlayerIndex);
        Debug.Log($"[Local] 초기 배치 - {GetPlayerName(currentPlayerIndex)} 마을을 배치하세요 (라운드 {initialRound + 1})");
        StartInitialSettlementMode();
        return true;
    }

    void GrantInitialResources(int vertexId)
    {
        var vertex = grid.Vertices[vertexId];
        var player = players[currentPlayerIndex];

        foreach (var tile in vertex.AdjacentTiles)
        {
            if (tile.ProducesResource)
            {
                player.AddResource(tile.Resource, 1);
                OnResourceChanged?.Invoke(currentPlayerIndex, tile.Resource, player.Resources[tile.Resource]);
                Debug.Log($"[Local] {GetPlayerName(currentPlayerIndex)}: 초기 자원 +1 {tile.Resource}");
            }
        }
    }

    void FinishInitialPlacement()
    {
        BuildModeController.Instance?.SetInitialPlacement(false);
        currentPlayerIndex = firstPlayerIndex;
        turnNumber = 1;

        SetPhase(GamePhase.RollDice);
        OnTurnChanged?.Invoke(currentPlayerIndex);
        Debug.Log($"[Local] 초기 배치 완료! 본 게임 시작 - 턴 1 {GetPlayerName(firstPlayerIndex)}");
    }

    // ========================
    // 자원 분배
    // ========================

    void DistributeResources(int diceTotal)
    {
        var matchingTiles = grid.GetTilesWithNumber(diceTotal);
        foreach (var tile in matchingTiles)
        {
            if (!tile.ProducesResource) continue;

            foreach (var vertex in tile.Vertices)
            {
                if (vertex.Building == BuildingType.None) continue;

                int owner = vertex.OwnerPlayerIndex;
                if (owner < 0 || owner >= playerCount) continue;

                int amount = vertex.Building == BuildingType.City ? 2 : 1;
                players[owner].AddResource(tile.Resource, amount);
                OnResourceChanged?.Invoke(owner, tile.Resource, players[owner].Resources[tile.Resource]);
                SFXManager.Instance?.Play(SFXType.ResourceGain);

                Debug.Log($"[Local] {GetPlayerName(owner)}: +{amount} {tile.Resource} (타일 {tile.Coord})");
            }
        }
    }

    // ========================
    // 도적 (7)
    // ========================

    void HandleSeven()
    {
        for (int i = 0; i < playerCount; i++)
        {
            if (players[i].TotalResourceCount > 7)
                AutoDiscardHalf(i);
        }

        SetPhase(GamePhase.MoveRobber);
        Debug.Log("[Local] 7 나옴! 도적 이동 필요 (아무 육지 타일 클릭)");
    }

    void AutoDiscardHalf(int playerIndex)
    {
        var player = players[playerIndex];
        int toDiscard = player.TotalResourceCount / 2;

        var pool = new List<ResourceType>();
        foreach (var kv in player.Resources)
        {
            for (int i = 0; i < kv.Value; i++)
                pool.Add(kv.Key);
        }

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        for (int i = 0; i < toDiscard && i < pool.Count; i++)
            player.Resources[pool[i]]--;

        NotifyAllResources(playerIndex);
        SFXManager.Instance?.Play(SFXType.ResourceLost);
        Debug.Log($"[Local] {GetPlayerName(playerIndex)}: 자원 {toDiscard}장 버림 (남은: {player.TotalResourceCount})");
    }

    public bool TryMoveRobber(HexCoord newTile)
    {
        // 기사 카드에서도 도적 이동 허용
        if (currentPhase != GamePhase.MoveRobber && devCardUseState != DevCardUseState.SelectingKnightTarget)
            return false;

        var tile = grid.GetTile(newTile);
        if (tile == null || tile.Resource == ResourceType.Sea) return false;

        foreach (var t in grid.Tiles.Values)
        {
            if (t.HasRobber) { t.HasRobber = false; break; }
        }

        tile.HasRobber = true;
        OnRobberMoved?.Invoke(newTile);
        SFXManager.Instance?.Play(SFXType.RobberMove);
        Debug.Log($"[Local] 도적 이동: {newTile}");

        bool fromKnight = devCardUseState == DevCardUseState.SelectingKnightTarget;
        if (fromKnight)
            devCardUseState = DevCardUseState.None;

        // 약탈 후보 확인
        robberStealCandidates = FindStealCandidates(tile);

        if (robberStealCandidates.Count == 0)
        {
            // 약탈 대상 없음 → 바로 진행
            if (!fromKnight)
                SetPhase(GamePhase.Action);
            Debug.Log("[Local] 도적 약탈 대상 없음");
        }
        else if (robberStealCandidates.Count == 1)
        {
            // 대상 1명 → 자동 약탈
            StealRandomResource(currentPlayerIndex, robberStealCandidates[0]);
            robberStealCandidates.Clear();
            if (!fromKnight)
                SetPhase(GamePhase.Action);
        }
        else
        {
            // 대상 2명 이상 → 선택 UI 표시
            returnToActionAfterSteal = !fromKnight;
            SetPhase(GamePhase.StealResource);
            Debug.Log($"[Local] 도적 약탈 대상 선택 필요: {robberStealCandidates.Count}명");
        }

        return true;
    }

    public bool TryStealFromPlayer(int victimIndex)
    {
        if (currentPhase != GamePhase.StealResource) return false;
        if (!robberStealCandidates.Contains(victimIndex)) return false;

        StealRandomResource(currentPlayerIndex, victimIndex);
        robberStealCandidates.Clear();

        if (returnToActionAfterSteal)
            SetPhase(GamePhase.Action);
        else
            SetPhase(GamePhase.Action);

        return true;
    }

    public List<int> GetRobberStealCandidates() => new(robberStealCandidates);

    List<int> FindStealCandidates(HexTile tile)
    {
        var candidates = new HashSet<int>();
        foreach (var vertex in tile.Vertices)
        {
            if (vertex.Building == BuildingType.None) continue;
            int owner = vertex.OwnerPlayerIndex;
            if (owner < 0 || owner == currentPlayerIndex) continue;
            if (players[owner].TotalResourceCount <= 0) continue;
            candidates.Add(owner);
        }
        return new List<int>(candidates);
    }

    void StealRandomResource(int thiefIndex, int victimIndex)
    {
        var victim = players[victimIndex];
        var thief = players[thiefIndex];

        // 보유 자원을 풀로 만들어 랜덤 선택
        var pool = new List<ResourceType>();
        foreach (var kv in victim.Resources)
        {
            for (int i = 0; i < kv.Value; i++)
                pool.Add(kv.Key);
        }

        if (pool.Count == 0) return;

        var stolen = pool[UnityEngine.Random.Range(0, pool.Count)];
        victim.Resources[stolen]--;
        thief.AddResource(stolen, 1);

        NotifyAllResources(victimIndex);
        NotifyAllResources(thiefIndex);
        OnRobberSteal?.Invoke(thiefIndex, victimIndex, stolen);
        SFXManager.Instance?.Play(SFXType.RobberSteal);

        Debug.Log($"[Local] {GetPlayerName(thiefIndex)}이 {GetPlayerName(victimIndex)}에게서 {stolen} 1장 약탈!");
    }

    // ========================
    // 건설
    // ========================

    public void EnterBuildMode(BuildMode mode)
    {
        if (currentPhase == GamePhase.InitialPlacement) return;
        if (currentPhase != GamePhase.Action) return;

        // 무료 도로 모드에서는 자원 체크 생략
        bool isFreeRoad = (devCardUseState == DevCardUseState.PlacingFreeRoad1 ||
                           devCardUseState == DevCardUseState.PlacingFreeRoad2) &&
                          mode == BuildMode.PlacingRoad;

        if (!isFreeRoad)
        {
            var player = players[currentPlayerIndex];
            var cost = GetBuildCost(mode);
            if (cost != null && !player.CanAfford(cost))
            {
                Debug.Log($"[Local] 건설 불가: 자원 부족 ({mode})");
                return;
            }
        }

        currentBuildMode = mode;
        OnBuildModeChanged?.Invoke(mode);

        BuildModeController.Instance?.EnterBuildMode(mode);
    }

    public void CancelBuildMode()
    {
        if (currentBuildMode == BuildMode.None) return;
        currentBuildMode = BuildMode.None;
        OnBuildModeChanged?.Invoke(BuildMode.None);
        BuildModeController.Instance?.CancelBuildMode();
    }

    public bool TryBuildSettlement(int vertexId)
    {
        bool isInitial = currentPhase == GamePhase.InitialPlacement;

        if (!isInitial && currentPhase != GamePhase.Action) return false;

        var player = players[currentPlayerIndex];

        if (!isInitial)
        {
            if (!player.CanAfford(BuildingCosts.Settlement)) return false;
        }
        if (player.SettlementsRemaining <= 0) return false;
        if (!buildingSystem.CanPlaceSettlement(vertexId, currentPlayerIndex, isInitial)) return false;

        buildingSystem.PlaceSettlement(vertexId, currentPlayerIndex);
        if (!isInitial)
            player.DeductCost(BuildingCosts.Settlement);
        player.SettlementsRemaining--;
        player.OwnedVertices.Add(grid.Vertices[vertexId]);

        if (!isInitial)
            NotifyAllResources(currentPlayerIndex);
        OnBuildingPlaced?.Invoke(currentPlayerIndex, vertexId, BuildingType.Settlement);
        SFXManager.Instance?.Play(SFXType.BuildSettlement);
        CheckVictory(currentPlayerIndex);

        if (isInitial)
            OnInitialSettlementPlaced(vertexId);

        return true;
    }

    public bool TryBuildCity(int vertexId)
    {
        if (currentPhase != GamePhase.Action) return false;

        var player = players[currentPlayerIndex];
        if (!player.CanAfford(BuildingCosts.City)) return false;
        if (player.CitiesRemaining <= 0) return false;
        if (!buildingSystem.CanUpgradeToCity(vertexId, currentPlayerIndex)) return false;

        buildingSystem.UpgradeToCity(vertexId, currentPlayerIndex);
        player.DeductCost(BuildingCosts.City);
        player.CitiesRemaining--;
        player.SettlementsRemaining++;

        NotifyAllResources(currentPlayerIndex);
        OnBuildingPlaced?.Invoke(currentPlayerIndex, vertexId, BuildingType.City);
        SFXManager.Instance?.Play(SFXType.BuildCity);
        CheckVictory(currentPlayerIndex);
        return true;
    }

    public bool TryBuildRoad(int edgeId)
    {
        bool isInitial = currentPhase == GamePhase.InitialPlacement;
        bool isFreeRoad = devCardUseState == DevCardUseState.PlacingFreeRoad1 ||
                          devCardUseState == DevCardUseState.PlacingFreeRoad2;

        if (!isInitial && !isFreeRoad && currentPhase != GamePhase.Action) return false;

        var player = players[currentPlayerIndex];

        // 초기 배치 & 무료 도로: 자원 소모 없음
        if (!isInitial && !isFreeRoad)
        {
            if (!player.CanAfford(BuildingCosts.Road)) return false;
        }
        if (player.RoadsRemaining <= 0) return false;
        if (!buildingSystem.CanPlaceRoad(edgeId, currentPlayerIndex, isInitial)) return false;

        buildingSystem.PlaceRoad(edgeId, currentPlayerIndex);
        if (!isInitial && !isFreeRoad)
            player.DeductCost(BuildingCosts.Road);
        player.RoadsRemaining--;
        player.OwnedEdges.Add(grid.Edges[edgeId]);

        if (!isInitial && !isFreeRoad)
            NotifyAllResources(currentPlayerIndex);
        OnRoadPlaced?.Invoke(currentPlayerIndex, edgeId);
        SFXManager.Instance?.Play(SFXType.BuildRoad);

        // 최장교역로 갱신
        UpdateLongestRoad();

        // 무료 도로 모드 처리
        if (isFreeRoad)
        {
            freeRoadsRemaining--;
            if (freeRoadsRemaining <= 0)
            {
                devCardUseState = DevCardUseState.None;
                Debug.Log("[Local] 도로건설 카드 완료");
            }
            else
            {
                devCardUseState = DevCardUseState.PlacingFreeRoad2;
                BuildModeController.Instance?.EnterBuildMode(BuildMode.PlacingRoad);
                Debug.Log("[Local] 도로건설 카드: 두 번째 도로를 배치하세요");
            }
        }

        // 초기 배치: 도로 후 다음 플레이어로
        if (isInitial)
            OnInitialRoadPlaced();

        return true;
    }

    static Dictionary<ResourceType, int> GetBuildCost(BuildMode mode) => mode switch
    {
        BuildMode.PlacingRoad => BuildingCosts.Road,
        BuildMode.PlacingSettlement => BuildingCosts.Settlement,
        BuildMode.PlacingCity => BuildingCosts.City,
        _ => null
    };

    // ========================
    // 발전카드
    // ========================

    public bool TryBuyDevCard()
    {
        if (currentPhase != GamePhase.Action) return false;

        var player = players[currentPlayerIndex];
        if (!player.CanAfford(BuildingCosts.DevelopmentCard)) return false;
        if (devCardDeck.RemainingCount <= 0)
        {
            Debug.Log("[Local] 발전카드 덱이 비었습니다");
            return false;
        }

        var cardType = devCardDeck.Draw();
        if (cardType == null) return false;

        player.DeductCost(BuildingCosts.DevelopmentCard);
        var card = new DevelopmentCard(cardType.Value, turnNumber);
        player.DevCards.Add(card);

        NotifyAllResources(currentPlayerIndex);
        OnDevCardPurchased?.Invoke(currentPlayerIndex, cardType.Value);
        SFXManager.Instance?.Play(SFXType.DevCardBuy);

        if (cardType.Value == DevCardType.VictoryPoint)
            CheckVictory(currentPlayerIndex);

        Debug.Log($"[Local] {GetPlayerName(currentPlayerIndex)} 발전카드 구매: {cardType.Value}");
        return true;
    }

    public bool TryUseKnight(HexCoord robberTarget)
    {
        if (currentPhase != GamePhase.Action && devCardUseState != DevCardUseState.SelectingKnightTarget)
            return false;

        var player = players[currentPlayerIndex];

        // 이미 기사 카드 사용 시작한 상태면 도적 이동만 처리
        if (devCardUseState == DevCardUseState.SelectingKnightTarget)
        {
            return TryMoveRobber(robberTarget);
        }

        if (player.HasUsedDevCardThisTurn) return false;
        var card = player.FindUsableCard(DevCardType.Knight, turnNumber);
        if (card == null) return false;

        card.IsUsed = true;
        player.HasUsedDevCardThisTurn = true;
        player.KnightsPlayed++;

        OnDevCardUsed?.Invoke(currentPlayerIndex, DevCardType.Knight);
        SFXManager.Instance?.Play(SFXType.KnightUse);
        devCardUseState = DevCardUseState.SelectingKnightTarget;

        Debug.Log($"[Local] {GetPlayerName(currentPlayerIndex)} 기사 카드 사용 → 도적을 이동하세요");

        UpdateLargestArmy();
        CheckVictory(currentPlayerIndex);
        return true;
    }

    public bool TryUseRoadBuilding()
    {
        if (currentPhase != GamePhase.Action) return false;

        var player = players[currentPlayerIndex];
        if (player.HasUsedDevCardThisTurn) return false;
        if (player.RoadsRemaining <= 0) return false;

        var card = player.FindUsableCard(DevCardType.RoadBuilding, turnNumber);
        if (card == null) return false;

        card.IsUsed = true;
        player.HasUsedDevCardThisTurn = true;
        freeRoadsRemaining = Mathf.Min(2, player.RoadsRemaining);
        devCardUseState = DevCardUseState.PlacingFreeRoad1;

        OnDevCardUsed?.Invoke(currentPlayerIndex, DevCardType.RoadBuilding);
        SFXManager.Instance?.Play(SFXType.DevCardUse);

        // 무료 도로 건설 모드 진입
        BuildModeController.Instance?.SetInitialPlacement(false);
        BuildModeController.Instance?.EnterBuildMode(BuildMode.PlacingRoad);

        Debug.Log($"[Local] {GetPlayerName(currentPlayerIndex)} 도로건설 카드 사용 → 무료 도로 {freeRoadsRemaining}개");
        return true;
    }

    public bool TryUseYearOfPlenty(ResourceType res1, ResourceType res2)
    {
        if (currentPhase != GamePhase.Action) return false;

        var player = players[currentPlayerIndex];
        if (player.HasUsedDevCardThisTurn) return false;

        var card = player.FindUsableCard(DevCardType.YearOfPlenty, turnNumber);
        if (card == null) return false;

        card.IsUsed = true;
        player.HasUsedDevCardThisTurn = true;

        player.AddResource(res1, 1);
        player.AddResource(res2, 1);

        NotifyAllResources(currentPlayerIndex);
        OnDevCardUsed?.Invoke(currentPlayerIndex, DevCardType.YearOfPlenty);
        SFXManager.Instance?.Play(SFXType.DevCardUse);

        Debug.Log($"[Local] {GetPlayerName(currentPlayerIndex)} 풍년 카드: {res1} + {res2}");
        return true;
    }

    public bool TryUseMonopoly(ResourceType targetResource)
    {
        if (currentPhase != GamePhase.Action) return false;

        var player = players[currentPlayerIndex];
        if (player.HasUsedDevCardThisTurn) return false;

        var card = player.FindUsableCard(DevCardType.Monopoly, turnNumber);
        if (card == null) return false;

        card.IsUsed = true;
        player.HasUsedDevCardThisTurn = true;

        int totalStolen = 0;
        for (int i = 0; i < playerCount; i++)
        {
            if (i == currentPlayerIndex) continue;
            int amount = players[i].Resources[targetResource];
            if (amount > 0)
            {
                players[i].Resources[targetResource] = 0;
                totalStolen += amount;
                NotifyAllResources(i);
            }
        }

        player.AddResource(targetResource, totalStolen);
        NotifyAllResources(currentPlayerIndex);
        OnDevCardUsed?.Invoke(currentPlayerIndex, DevCardType.Monopoly);
        SFXManager.Instance?.Play(SFXType.DevCardUse);

        Debug.Log($"[Local] {GetPlayerName(currentPlayerIndex)} 독점 카드: {targetResource} x{totalStolen} 획득");
        return true;
    }

    // ========================
    // 최장교역로 / 최대기사단
    // ========================

    void UpdateLongestRoad()
    {
        int maxLength = 0;
        int maxPlayer = -1;

        for (int i = 0; i < playerCount; i++)
        {
            int len = LongestRoadCalculator.Calculate(i, grid);
            if (len >= 5 && len > maxLength)
            {
                maxLength = len;
                maxPlayer = i;
            }
        }

        // 동률이면 기존 보유자 유지 (카탄 규칙)
        if (maxPlayer != -1 && longestRoadHolder >= 0 && maxPlayer != longestRoadHolder)
        {
            int holderLen = LongestRoadCalculator.Calculate(longestRoadHolder, grid);
            if (maxLength <= holderLen)
                maxPlayer = longestRoadHolder;
        }

        if (maxPlayer != longestRoadHolder)
        {
            if (longestRoadHolder >= 0)
            {
                players[longestRoadHolder].HasLongestRoad = false;
                OnLongestRoadChanged?.Invoke(longestRoadHolder, false);
                OnVPChanged?.Invoke(longestRoadHolder, players[longestRoadHolder].VictoryPoints);
            }

            longestRoadHolder = maxPlayer;

            if (longestRoadHolder >= 0)
            {
                players[longestRoadHolder].HasLongestRoad = true;
                SFXManager.Instance?.Play(SFXType.LongestRoad);
                OnLongestRoadChanged?.Invoke(longestRoadHolder, true);
                OnVPChanged?.Invoke(longestRoadHolder, players[longestRoadHolder].VictoryPoints);
                Debug.Log($"[Local] 최장교역로: {GetPlayerName(longestRoadHolder)} ({maxLength}칸)");
                CheckVictory(longestRoadHolder);
            }
        }
    }

    void UpdateLargestArmy()
    {
        int maxKnights = 0;
        int maxPlayer = -1;

        for (int i = 0; i < playerCount; i++)
        {
            if (players[i].KnightsPlayed >= 3 && players[i].KnightsPlayed > maxKnights)
            {
                maxKnights = players[i].KnightsPlayed;
                maxPlayer = i;
            }
        }

        if (maxPlayer != largestArmyHolder)
        {
            if (largestArmyHolder >= 0)
            {
                players[largestArmyHolder].HasLargestArmy = false;
                OnLargestArmyChanged?.Invoke(largestArmyHolder, false);
                OnVPChanged?.Invoke(largestArmyHolder, players[largestArmyHolder].VictoryPoints);
            }

            largestArmyHolder = maxPlayer;

            if (largestArmyHolder >= 0)
            {
                players[largestArmyHolder].HasLargestArmy = true;
                SFXManager.Instance?.Play(SFXType.LargestArmy);
                OnLargestArmyChanged?.Invoke(largestArmyHolder, true);
                OnVPChanged?.Invoke(largestArmyHolder, players[largestArmyHolder].VictoryPoints);
                Debug.Log($"[Local] 최대기사단: {GetPlayerName(largestArmyHolder)} ({maxKnights}명)");
                CheckVictory(largestArmyHolder);
            }
        }
    }

    // ========================
    // 승리 판정
    // ========================

    void CheckVictory(int playerIndex)
    {
        int vp = players[playerIndex].VictoryPoints;
        OnVPChanged?.Invoke(playerIndex, vp);

        if (vp >= 10)
        {
            SFXManager.Instance?.Play(SFXType.Victory);
            SetPhase(GamePhase.GameOver);
            Debug.Log($"[Local] {GetPlayerName(playerIndex)} 승리! ({vp}점)");
        }
    }

    // ========================
    // 거래
    // ========================

    public int GetTradeRate(ResourceType resource)
    {
        var player = players[currentPlayerIndex];
        bool has2to1 = false;
        bool has3to1 = false;

        foreach (var vertex in player.OwnedVertices)
        {
            if (vertex.Port == PortType.None) continue;

            if (vertex.Port == PortType.Generic)
            {
                has3to1 = true;
            }
            else if (PortMatchesResource(vertex.Port, resource))
            {
                has2to1 = true;
            }
        }

        if (has2to1) return 2;
        if (has3to1) return 3;
        return 4;
    }

    public bool TryBankTrade(ResourceType give, ResourceType receive)
    {
        if (currentPhase != GamePhase.Action) return false;
        if (give == receive) return false;

        var player = players[currentPlayerIndex];
        int rate = GetTradeRate(give);

        if (player.Resources[give] < rate)
        {
            Debug.Log($"[Local] 은행 거래 실패: {give} {rate}개 필요 (보유: {player.Resources[give]})");
            return false;
        }

        player.Resources[give] -= rate;
        player.AddResource(receive, 1);

        NotifyAllResources(currentPlayerIndex);
        OnBankTrade?.Invoke(currentPlayerIndex, give, receive, rate);
        SFXManager.Instance?.Play(SFXType.BankTrade);

        Debug.Log($"[Local] {GetPlayerName(currentPlayerIndex)}: 은행 거래 {give}×{rate} → {receive}×1");
        return true;
    }

    public bool TryPlayerTrade(int otherPlayer, Dictionary<ResourceType, int> offer, Dictionary<ResourceType, int> request)
    {
        if (currentPhase != GamePhase.Action) return false;
        if (otherPlayer < 0 || otherPlayer >= playerCount || otherPlayer == currentPlayerIndex) return false;

        var me = players[currentPlayerIndex];
        var them = players[otherPlayer];

        // 양쪽 자원 충분한지 확인
        foreach (var kv in offer)
            if (me.Resources[kv.Key] < kv.Value) return false;
        foreach (var kv in request)
            if (them.Resources[kv.Key] < kv.Value) return false;

        // AI가 인간 플레이어에게 거래 시: 즉시 실행 대신 제안으로 전환
        if (IsPlayerAI(currentPlayerIndex) && !IsPlayerAI(otherPlayer))
        {
            pendingIncomingTrade = new PendingTrade
            {
                proposer = currentPlayerIndex,
                target = otherPlayer,
                offer = new Dictionary<ResourceType, int>(offer),
                request = new Dictionary<ResourceType, int>(request)
            };
            OnIncomingTradeProposal?.Invoke(currentPlayerIndex, offer, request);
            SFXManager.Instance?.Play(SFXType.TradeOffer);
            return false; // 실제 실행은 RespondToIncomingTrade에서
        }

        ExecuteTrade(currentPlayerIndex, otherPlayer, offer, request);
        return true;
    }

    public void RespondToIncomingTrade(bool accept)
    {
        if (pendingIncomingTrade == null) return;
        var trade = pendingIncomingTrade;
        pendingIncomingTrade = null;

        if (!accept) return;

        // 재검증 (자원 상황이 바뀔 수 있음)
        var proposer = players[trade.proposer];
        var target = players[trade.target];
        foreach (var kv in trade.offer)
            if (proposer.Resources[kv.Key] < kv.Value) return;
        foreach (var kv in trade.request)
            if (target.Resources[kv.Key] < kv.Value) return;

        ExecuteTrade(trade.proposer, trade.target, trade.offer, trade.request);
    }

    void ExecuteTrade(int p1, int p2, Dictionary<ResourceType, int> offer, Dictionary<ResourceType, int> request)
    {
        var me = players[p1];
        var them = players[p2];

        foreach (var kv in offer)
        {
            me.Resources[kv.Key] -= kv.Value;
            them.AddResource(kv.Key, kv.Value);
        }
        foreach (var kv in request)
        {
            them.Resources[kv.Key] -= kv.Value;
            me.AddResource(kv.Key, kv.Value);
        }

        NotifyAllResources(p1);
        NotifyAllResources(p2);
        OnPlayerTrade?.Invoke(p1, p2);
        SFXManager.Instance?.Play(SFXType.TradeAccept);
        Debug.Log($"[Local] {GetPlayerName(p1)} ↔ {GetPlayerName(p2)} 거래 성사!");
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

    // ========================
    // 조회
    // ========================

    public PlayerState GetPlayerState(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < players.Length)
            return players[playerIndex];
        return null;
    }

    public HexGrid GetGrid() => grid;

    public int GetLongestRoadLength(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= playerCount) return 0;
        return LongestRoadCalculator.Calculate(playerIndex, grid);
    }

    public int GetLongestRoadHolder() => longestRoadHolder;
    public int GetLargestArmyHolder() => largestArmyHolder;

    const int BANK_RESOURCE_PER_TYPE = 19;
    public int GetBankResourceCount(ResourceType type)
    {
        int held = 0;
        for (int i = 0; i < playerCount; i++)
            held += players[i].Resources.GetValueOrDefault(type, 0);
        return BANK_RESOURCE_PER_TYPE - held;
    }

    // ========================
    // 헬퍼
    // ========================

    void SetPhase(GamePhase phase)
    {
        currentPhase = phase;
        OnPhaseChanged?.Invoke(phase);
    }

    void NotifyAllResources(int playerIndex)
    {
        var player = players[playerIndex];
        foreach (var kv in player.Resources)
            OnResourceChanged?.Invoke(playerIndex, kv.Key, kv.Value);
    }

    // ========================
    // AI 지원
    // ========================

    /// <summary>AI 모드 설정 (humanIndex = 인간 플레이어 인덱스)</summary>
    public void SetHumanPlayerIndex(int humanIndex) => humanPlayerIndex = humanIndex;

    public List<int> GetValidSettlementVertices(int playerIndex, bool isInitial)
    {
        var vertices = buildingSystem.GetValidSettlementPositions(playerIndex, isInitial);
        var ids = new List<int>(vertices.Count);
        foreach (var v in vertices) ids.Add(v.Id);
        return ids;
    }

    public List<int> GetValidRoadEdges(int playerIndex, bool isInitial)
    {
        var edges = buildingSystem.GetValidRoadPositions(playerIndex, isInitial);
        var ids = new List<int>(edges.Count);
        foreach (var e in edges) ids.Add(e.Id);
        return ids;
    }

    public List<int> GetValidCityVertices(int playerIndex)
    {
        var vertices = buildingSystem.GetValidCityUpgrades(playerIndex);
        var ids = new List<int>(vertices.Count);
        foreach (var v in vertices) ids.Add(v.Id);
        return ids;
    }
}
