using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 카탄 턴 기반 동기화 매니저
/// 호스트가 턴 순서를 관리하고 모든 클라이언트에 동기화
/// </summary>
public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance { get; private set; }

    /// <summary>현재 턴 플레이어의 clientId</summary>
    public NetworkVariable<ulong> CurrentTurnPlayerId = new(0, NetworkVariableReadPermission.Everyone);

    /// <summary>현재 턴 번호</summary>
    public NetworkVariable<int> TurnNumber = new(0, NetworkVariableReadPermission.Everyone);

    /// <summary>현재 게임 페이즈</summary>
    public NetworkVariable<GamePhase> CurrentPhase = new(GamePhase.WaitingForPlayers, NetworkVariableReadPermission.Everyone);

    /// <summary>주사위 결과 (2d6)</summary>
    public NetworkVariable<int> DiceResult = new(0, NetworkVariableReadPermission.Everyone);

    /// <summary>주사위 1 개별 결과</summary>
    public NetworkVariable<int> Die1Result = new(0, NetworkVariableReadPermission.Everyone);

    /// <summary>주사위 2 개별 결과</summary>
    public NetworkVariable<int> Die2Result = new(0, NetworkVariableReadPermission.Everyone);

    public event Action<ulong> OnTurnChanged;
    public event Action<GamePhase> OnPhaseChanged;
    public event Action<int, int, int> OnDiceRolled;

    ulong[] playerOrder;
    int currentPlayerIndex;

    void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        CurrentTurnPlayerId.OnValueChanged += (_, newVal) => OnTurnChanged?.Invoke(newVal);
        CurrentPhase.OnValueChanged += (_, newVal) => OnPhaseChanged?.Invoke(newVal);
        DiceResult.OnValueChanged += (_, newVal) => OnDiceRolled?.Invoke(Die1Result.Value, Die2Result.Value, newVal);
    }

    /// <summary>
    /// 게임 시작 (호스트만 호출)
    /// </summary>
    public void StartGame()
    {
        if (!IsServer) return;

        var connectedClients = NetworkManager.Singleton.ConnectedClientsIds;
        playerOrder = new ulong[connectedClients.Count];
        for (int i = 0; i < connectedClients.Count; i++)
            playerOrder[i] = connectedClients[i];

        // 순서 섞기
        for (int i = playerOrder.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (playerOrder[i], playerOrder[j]) = (playerOrder[j], playerOrder[i]);
        }

        currentPlayerIndex = 0;
        TurnNumber.Value = 1;
        CurrentTurnPlayerId.Value = playerOrder[0];
        CurrentPhase.Value = GamePhase.RollDice;

        Debug.Log($"[Turn] 게임 시작. 첫 턴: Player {playerOrder[0]}");
    }

    /// <summary>
    /// 주사위 굴리기 요청 (클라이언트 → 서버)
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RollDiceServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != CurrentTurnPlayerId.Value)
        {
            Debug.LogWarning($"[Turn] Player {senderId}는 현재 턴이 아닙니다");
            return;
        }

        if (CurrentPhase.Value != GamePhase.RollDice) return;

        int die1 = UnityEngine.Random.Range(1, 7);
        int die2 = UnityEngine.Random.Range(1, 7);
        Die1Result.Value = die1;
        Die2Result.Value = die2;
        DiceResult.Value = die1 + die2;
        CurrentPhase.Value = GamePhase.Action;

        Debug.Log($"[Turn] 주사위 결과: {die1} + {die2} = {DiceResult.Value}");
    }

    /// <summary>
    /// 턴 종료 요청 (클라이언트 → 서버)
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void EndTurnServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != CurrentTurnPlayerId.Value) return;
        if (CurrentPhase.Value != GamePhase.Action) return;

        NextTurn();
    }

    void NextTurn()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % playerOrder.Length;
        if (currentPlayerIndex == 0) TurnNumber.Value++;

        CurrentTurnPlayerId.Value = playerOrder[currentPlayerIndex];
        CurrentPhase.Value = GamePhase.RollDice;
        DiceResult.Value = 0;

        Debug.Log($"[Turn] 턴 {TurnNumber.Value} - Player {CurrentTurnPlayerId.Value}");
    }

    /// <summary>
    /// 자기 턴인지 확인
    /// </summary>
    public bool IsMyTurn()
    {
        return CurrentTurnPlayerId.Value == NetworkManager.Singleton.LocalClientId;
    }

    public int GetPlayerCount()
    {
        return playerOrder?.Length ?? 0;
    }
}
