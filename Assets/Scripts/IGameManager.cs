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
    int FirstPlayerIndex { get; }
    int PlayerCount { get; }
    GamePhase CurrentPhase { get; }
    bool IsHost { get; }
    BuildMode CurrentBuildMode { get; }
    DevCardUseState DevCardState { get; }
    int DevCardDeckRemaining { get; }
    bool IsWaitingForDiscard { get; }
    bool HasPendingIncomingTrade { get; }

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

    // 디스카드 이벤트
    event Action<int, int> OnDiscardRequired; // (playerIndex, discardCount) — 인간 플레이어에게 디스카드 UI 표시

    // 연결 이벤트
    event Action<int, string> OnPlayerDisconnected; // (playerIndex, playerName) — 플레이어 퇴장
    event Action OnHostDisconnected; // 호스트 퇴장 → 게임 종료

    // 기본 액션
    void PrepareGame();
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

    // 디스카드
    void ConfirmDiscard(Dictionary<ResourceType, int> toDiscard);

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

    // 거래
    bool TryBankTrade(ResourceType give, ResourceType receive);
    bool TryPlayerTrade(int otherPlayer, Dictionary<ResourceType, int> offer, Dictionary<ResourceType, int> request);
    int GetTradeRate(ResourceType resource);
    bool IsPlayerAI(int playerIndex);

    // 거래 이벤트
    event Action<int, ResourceType, ResourceType, int> OnBankTrade; // (player, gave, received, rate)
    event Action<int, int> OnPlayerTrade; // (player1, player2)
    // AI→인간 거래 제안 수신 (proposer, offerToHuman, requestFromHuman)
    event Action<int, Dictionary<ResourceType, int>, Dictionary<ResourceType, int>> OnIncomingTradeProposal;
    event Action OnIncomingTradeCancelled; // 제안자가 다른 거래 성사 시 자동 취소
    void RespondToIncomingTrade(bool accept);

    // 조회
    PlayerState GetPlayerState(int playerIndex);
    HexGrid GetGrid();
    int GetBankResourceCount(ResourceType type);
    int GetLongestRoadLength(int playerIndex);
    int GetLongestRoadHolder();
    int GetLargestArmyHolder();

    // AI 쿼리
    List<int> GetValidSettlementVertices(int playerIndex, bool isInitial);
    List<int> GetValidRoadEdges(int playerIndex, bool isInitial);
    List<int> GetValidCityVertices(int playerIndex);
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
