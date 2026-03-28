using System;

/// <summary>
/// 게임 매니저 인터페이스 - 로컬/네트워크 모드 공통
/// 로컬: LocalGameManager 구현
/// 네트워크: TurnManager가 구현 (향후)
/// </summary>
public interface IGameManager
{
    int TurnNumber { get; }
    int CurrentPlayerIndex { get; }
    int LocalPlayerIndex { get; }
    int PlayerCount { get; }
    GamePhase CurrentPhase { get; }
    bool IsHost { get; }

    event Action<int> OnTurnChanged;
    event Action<GamePhase> OnPhaseChanged;
    event Action<int, int, int> OnDiceRolled;
    event Action OnPlayerListChanged;

    void StartGame();
    void RollDice();
    void EndTurn();
    bool IsMyTurn();
    string GetPlayerName(int playerIndex);
}

/// <summary>글로벌 게임 매니저 참조</summary>
public static class GameServices
{
    public static IGameManager GameManager { get; set; }
}

public enum GamePhase
{
    WaitingForPlayers,
    RollDice,
    Action,
    GameOver
}
