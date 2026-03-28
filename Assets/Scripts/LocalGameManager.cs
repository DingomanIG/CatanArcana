using System;
using UnityEngine;

/// <summary>
/// 로컬 전용 게임 매니저 - 네트워크 없이 단독 플레이
/// 나중에 TurnManager(NetworkBehaviour)가 IGameManager를 구현하면 네트워크 모드로 전환
/// </summary>
public class LocalGameManager : MonoBehaviour, IGameManager
{
    [Header("게임 설정")]
    [SerializeField, Range(1, 4)] int playerCount = 2;

    int turnNumber;
    int currentPlayerIndex;
    GamePhase currentPhase = GamePhase.WaitingForPlayers;

    static readonly string[] DefaultPlayerNames = { "플레이어 1", "플레이어 2", "플레이어 3", "플레이어 4" };

    public int TurnNumber => turnNumber;
    public int CurrentPlayerIndex => currentPlayerIndex;
    public int LocalPlayerIndex => 0;
    public int PlayerCount => playerCount;
    public GamePhase CurrentPhase => currentPhase;
    public bool IsHost => true;

    public event Action<int> OnTurnChanged;
    public event Action<GamePhase> OnPhaseChanged;
    public event Action<int, int, int> OnDiceRolled;
    public event Action OnPlayerListChanged;

    void Awake()
    {
        GameServices.GameManager = this;
    }

    void Start()
    {
        OnPlayerListChanged?.Invoke();
    }

    public void StartGame()
    {
        currentPlayerIndex = 0;
        turnNumber = 1;
        SetPhase(GamePhase.RollDice);
        OnTurnChanged?.Invoke(currentPlayerIndex);
        Debug.Log($"[Local] 게임 시작! 턴 1 - {GetPlayerName(0)}");
    }

    public void RollDice()
    {
        if (currentPhase != GamePhase.RollDice) return;
        if (!IsMyTurn()) return;

        int die1 = UnityEngine.Random.Range(1, 7);
        int die2 = UnityEngine.Random.Range(1, 7);
        int total = die1 + die2;

        OnDiceRolled?.Invoke(die1, die2, total);
        SetPhase(GamePhase.Action);
        Debug.Log($"[Local] 주사위: {die1} + {die2} = {total}");
    }

    public void EndTurn()
    {
        if (currentPhase != GamePhase.Action) return;
        if (!IsMyTurn()) return;

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

    void SetPhase(GamePhase phase)
    {
        currentPhase = phase;
        OnPhaseChanged?.Invoke(phase);
    }
}
