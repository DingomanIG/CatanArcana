using System;
using System.Collections.Generic;

/// <summary>
/// 게임 매니저 인터페이스 - 로컬/네트워크 모드 공통
/// 로컬: LocalGameManager 구현
/// 네트워크: TurnManager가 구현 (향후)
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

    // 조회
    PlayerState GetPlayerState(int playerIndex);
    HexGrid GetGrid();
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
    GameOver
}
