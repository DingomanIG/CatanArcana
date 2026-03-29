using System;
using System.Collections.Generic;

/// <summary>
/// 게임 매니저 인터페이스 - 로컬/네트워크 모드 공통
/// </summary>
public interface IGameManager
{
    // 상태
    int TurnNumber { get; }
    int CurrentPlayerIndex { get; }
    int LocalPlayerIndex { get; }
    int PlayerCount { get; }
    GamePhase CurrentPhase { get; }
    bool IsHost { get; }
    BuildMode CurrentBuildMode { get; }
    DevCardUseState DevCardState { get; }

    // 기존 이벤트
    event Action<int> OnTurnChanged;
    event Action<GamePhase> OnPhaseChanged;
    event Action<int, int, int> OnDiceRolled;
    event Action OnPlayerListChanged;

    // 건설/자원 이벤트
    event Action<int, ResourceType, int> OnResourceChanged;
    event Action<int, int, BuildingType> OnBuildingPlaced;
    event Action<int, int> OnRoadPlaced;
    event Action<int, int> OnVPChanged;
    event Action<HexCoord> OnRobberMoved;
    event Action<BuildMode> OnBuildModeChanged;

    // 발전카드 이벤트
    event Action<int, DevCardType> OnDevCardPurchased;
    event Action<int, DevCardType> OnDevCardUsed;
    event Action<int, bool> OnLongestRoadChanged;  // (playerIndex, gained)
    event Action<int, bool> OnLargestArmyChanged;  // (playerIndex, gained)
    event Action<int, int, ResourceType> OnRobberSteal; // (thief, victim, resource)

    // 기본 액션
    void StartGame();
    void RollDice();
    void EndTurn();
    bool IsMyTurn();
    string GetPlayerName(int playerIndex);

    // 건설 액션
    bool TryBuildSettlement(int vertexId);
    bool TryBuildCity(int vertexId);
    bool TryBuildRoad(int edgeId);
    void EnterBuildMode(BuildMode mode);
    void CancelBuildMode();

    // 도적
    bool TryMoveRobber(HexCoord newTile);
    bool TryStealFromPlayer(int victimIndex);
    List<int> GetRobberStealCandidates();

    // 발전카드 액션
    bool TryBuyDevCard();
    bool TryUseKnight(HexCoord robberTarget);
    bool TryUseRoadBuilding();
    bool TryUseYearOfPlenty(ResourceType res1, ResourceType res2);
    bool TryUseMonopoly(ResourceType targetResource);

    // 조회
    PlayerState GetPlayerState(int playerIndex);
    HexGrid GetGrid();
    int GetLongestRoadLength(int playerIndex);
    int GetLongestRoadHolder();
    int GetLargestArmyHolder();
}

/// <summary>글로벌 게임 매니저 참조</summary>
public static class GameServices
{
    public static IGameManager GameManager { get; set; }
}

public enum GamePhase
{
    WaitingForPlayers,
    InitialPlacement,
    RollDice,
    Action,
    MoveRobber,
    StealResource,
    GameOver
}
