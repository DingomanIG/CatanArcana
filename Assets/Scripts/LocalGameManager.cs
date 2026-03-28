using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로컬 전용 게임 매니저 - 네트워크 없이 단독 플레이
/// 자원 분배, 건설, 도적, 승리 판정까지 처리
/// </summary>
[DefaultExecutionOrder(-100)]
public class LocalGameManager : MonoBehaviour, IGameManager
{
    [Header("게임 설정")]
    [SerializeField, Range(2, 4)] int playerCount = 2;

    [Header("참조")]
    [SerializeField] HexGridView hexGridView;

    // 상태
    int turnNumber;
    int currentPlayerIndex;
    GamePhase currentPhase = GamePhase.WaitingForPlayers;
    BuildMode currentBuildMode = BuildMode.None;

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
    public int LocalPlayerIndex => 0;
    public int PlayerCount => playerCount;
    public GamePhase CurrentPhase => currentPhase;
    public bool IsHost => true;
    public BuildMode CurrentBuildMode => currentBuildMode;

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

    // ========================
    // LIFECYCLE
    // ========================

    void Awake()
    {
        GameServices.GameManager = this;
    }

    void Start()
    {
        grid = hexGridView.Grid;
        buildingSystem = new BuildingSystem(grid);
        players = new PlayerState[playerCount];
        for (int i = 0; i < playerCount; i++)
            players[i] = new PlayerState(i);

        OnPlayerListChanged?.Invoke();
    }

    // ========================
    // 기본 액션
    // ========================

    public void StartGame()
    {
        currentPlayerIndex = 0;
        turnNumber = 1;

        // 초기 테스트용: 각 플레이어에게 시작 자원 지급
        for (int i = 0; i < playerCount; i++)
        {
            players[i].AddResource(ResourceType.Wood, 4);
            players[i].AddResource(ResourceType.Brick, 4);
            players[i].AddResource(ResourceType.Wool, 2);
            players[i].AddResource(ResourceType.Wheat, 2);
            players[i].AddResource(ResourceType.Ore, 1);
            NotifyAllResources(i);
        }

        SetPhase(GamePhase.RollDice);
        OnTurnChanged?.Invoke(currentPlayerIndex);
        Debug.Log($"[Local] 게임 시작! 턴 1 - {GetPlayerName(0)} (시작 자원 지급됨)");
    }

    public void RollDice()
    {
        if (currentPhase != GamePhase.RollDice) return;
        if (!IsMyTurn()) return;

        int die1 = UnityEngine.Random.Range(1, 7);
        int die2 = UnityEngine.Random.Range(1, 7);
        int total = die1 + die2;

        OnDiceRolled?.Invoke(die1, die2, total);
        Debug.Log($"[Local] 주사위: {die1} + {die2} = {total}");

        if (total == 7)
        {
            HandleSeven();
        }
        else
        {
            DistributeResources(total);
            SetPhase(GamePhase.Action);
        }
    }

    public void EndTurn()
    {
        if (currentPhase != GamePhase.Action) return;
        if (!IsMyTurn()) return;

        CancelBuildMode();

        currentPlayerIndex = (currentPlayerIndex + 1) % playerCount;
        if (currentPlayerIndex == 0) turnNumber++;

        SetPhase(GamePhase.RollDice);
        OnTurnChanged?.Invoke(currentPlayerIndex);
        Debug.Log($"[Local] 턴 {turnNumber} - {GetPlayerName(currentPlayerIndex)}");
    }

    public bool IsMyTurn() => currentPlayerIndex == LocalPlayerIndex;

    public string GetPlayerName(int index)
    {
        string name = index < DefaultPlayerNames.Length ? DefaultPlayerNames[index] : $"Player {index + 1}";
        return index == LocalPlayerIndex ? $"{name} (나)" : name;
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
        Debug.Log($"[Local] {GetPlayerName(playerIndex)}: 자원 {toDiscard}장 버림 (남은: {player.TotalResourceCount})");
    }

    public bool TryMoveRobber(HexCoord newTile)
    {
        if (currentPhase != GamePhase.MoveRobber) return false;

        var tile = grid.GetTile(newTile);
        if (tile == null || tile.Resource == ResourceType.Sea) return false;

        foreach (var t in grid.Tiles.Values)
        {
            if (t.HasRobber) { t.HasRobber = false; break; }
        }

        tile.HasRobber = true;
        OnRobberMoved?.Invoke(newTile);
        Debug.Log($"[Local] 도적 이동: {newTile}");

        SetPhase(GamePhase.Action);
        return true;
    }

    // ========================
    // 건설 (BuildModeController가 호출)
    // ========================

    public void EnterBuildMode(BuildMode mode)
    {
        if (currentPhase != GamePhase.Action || !IsMyTurn()) return;

        var player = players[currentPlayerIndex];
        var cost = GetBuildCost(mode);
        if (cost != null && !player.CanAfford(cost))
        {
            Debug.Log($"[Local] 건설 불가: 자원 부족 ({mode})");
            return;
        }

        currentBuildMode = mode;
        OnBuildModeChanged?.Invoke(mode);

        // BuildModeController가 하이라이트 + 입력 처리
        BuildModeController.Instance?.EnterBuildMode(mode);
    }

    public void CancelBuildMode()
    {
        if (currentBuildMode == BuildMode.None) return;
        currentBuildMode = BuildMode.None;
        OnBuildModeChanged?.Invoke(BuildMode.None);
    }

    public bool TryBuildSettlement(int vertexId)
    {
        if (currentPhase != GamePhase.Action) return false;

        var player = players[currentPlayerIndex];
        if (!player.CanAfford(BuildingCosts.Settlement)) return false;
        if (player.SettlementsRemaining <= 0) return false;
        if (!buildingSystem.CanPlaceSettlement(vertexId, currentPlayerIndex)) return false;

        buildingSystem.PlaceSettlement(vertexId, currentPlayerIndex);
        player.DeductCost(BuildingCosts.Settlement);
        player.SettlementsRemaining--;
        player.OwnedVertices.Add(grid.Vertices[vertexId]);

        NotifyAllResources(currentPlayerIndex);
        OnBuildingPlaced?.Invoke(currentPlayerIndex, vertexId, BuildingType.Settlement);
        CheckVictory(currentPlayerIndex);
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
        CheckVictory(currentPlayerIndex);
        return true;
    }

    public bool TryBuildRoad(int edgeId)
    {
        if (currentPhase != GamePhase.Action) return false;

        var player = players[currentPlayerIndex];
        if (!player.CanAfford(BuildingCosts.Road)) return false;
        if (player.RoadsRemaining <= 0) return false;
        if (!buildingSystem.CanPlaceRoad(edgeId, currentPlayerIndex)) return false;

        buildingSystem.PlaceRoad(edgeId, currentPlayerIndex);
        player.DeductCost(BuildingCosts.Road);
        player.RoadsRemaining--;
        player.OwnedEdges.Add(grid.Edges[edgeId]);

        NotifyAllResources(currentPlayerIndex);
        OnRoadPlaced?.Invoke(currentPlayerIndex, edgeId);
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
    // 승리 판정
    // ========================

    void CheckVictory(int playerIndex)
    {
        int vp = players[playerIndex].VictoryPoints;
        OnVPChanged?.Invoke(playerIndex, vp);

        if (vp >= 10)
        {
            SetPhase(GamePhase.GameOver);
            Debug.Log($"[Local] {GetPlayerName(playerIndex)} 승리! ({vp}점)");
        }
    }

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
}
