using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>AI 난이도 (0=인간, 1~9=AI 레벨)</summary>
public enum AIDifficulty
{
    None = 0,  // 인간 플레이어
    Lv1 = 1,   // 완전 랜덤
    Lv2 = 2,   // 약간의 판단
    Lv3 = 3,   // 기초 전략 (3개 중 택1)
    Lv4 = 4,   // 기초 전략 + 거래
    Lv5 = 5,   // 고급 전략 (6개 전체 + VP 추적)
    Lv6 = 6,   // 고급 전략 + 정밀
    Lv7 = 7,   // 최상위 전략
    Lv8 = 8,   // 준마스터
    Lv9 = 9    // 마스터 (클로디)
}

/// <summary>
/// 레벨별 AI 파라미터 헬퍼
/// Lv1~2: 초급, Lv3~4: 중급, Lv5~8: 고급, Lv9: 마스터
/// </summary>
public static class AIDifficultySettings
{
    /// <summary>전략을 사용하는 레벨인가? (Lv3+)</summary>
    public static bool UsesStrategy(AIDifficulty d) => (int)d >= 3;

    /// <summary>거래를 사용하는 레벨인가? (Lv4+)</summary>
    public static bool UsesTrade(AIDifficulty d) => (int)d >= 4;

    /// <summary>전체 6개 전략을 평가하는 레벨인가? (Lv5+)</summary>
    public static bool UsesFullStrategy(AIDifficulty d) => (int)d >= 5;

    /// <summary>완전 랜덤 레벨인가? (Lv1~2)</summary>
    public static bool IsRandom(AIDifficulty d) => (int)d >= 1 && (int)d <= 2;

    /// <summary>교차점 평가 시 노이즈 (높을수록 랜덤) - Lv5+ 한 단계 상향</summary>
    public static float EvalNoise(AIDifficulty d) => (int)d switch
    {
        1 => 3.0f,
        2 => 2.5f,
        3 => 1.5f,
        4 => 1.2f,
        5 => 0.5f,   // 구 Lv6 수준
        6 => 0.35f,   // 구 Lv7 수준
        7 => 0.2f,    // 구 Lv8 수준
        8 => 0.1f,    // 구 Lv9급
        9 => 0.03f,   // 클로디: 거의 완벽한 판단
        _ => 3.0f
    };

    /// <summary>전략 선택 시 노이즈 범위 - Lv5+ 한 단계 상향</summary>
    public static float StrategyNoise(AIDifficulty d) => (int)d switch
    {
        3 => 0.3f,
        4 => 0.25f,
        5 => 0.08f,   // 구 Lv6 수준
        6 => 0.05f,   // 구 Lv7 수준
        7 => 0.04f,   // 구 Lv8 수준
        8 => 0.02f,   // 구 Lv9급
        9 => 0.005f,  // 클로디: 최적 전략 확정
        _ => 0.3f
    };

    /// <summary>발전카드 사용 최소 레벨 (Lv3+)</summary>
    public static bool UsesDevCards(AIDifficulty d) => (int)d >= 3;

    /// <summary>독점 카드 사용 최소 레벨 (Lv4+)</summary>
    public static bool UsesMonopoly(AIDifficulty d) => (int)d >= 4;

    /// <summary>상대 VP 추적 (선두 타겟팅) 레벨 (Lv5+)</summary>
    public static bool TracksOpponentVP(AIDifficulty d) => (int)d >= 5;

    /// <summary>기사 카드 적극 사용 (방어 외 공격) 레벨 (Lv5+)</summary>
    public static bool UsesKnightOffensively(AIDifficulty d) => (int)d >= 5;

    /// <summary>자원 다양성 보너스 사용 레벨 (Lv3+)</summary>
    public static bool UsesDiversityBonus(AIDifficulty d) => (int)d >= 3;

    /// <summary>항구 보너스 사용 레벨 (Lv5+)</summary>
    public static bool UsesPortBonus(AIDifficulty d) => (int)d >= 5;

    /// <summary>도로/발전카드 구매 시 랜덤 확률 (Lv1~2용)</summary>
    public static float RandomActionChance(AIDifficulty d) => (int)d switch
    {
        1 => 0.3f,  // 30% 확률로 행동
        2 => 0.4f,
        _ => 1.0f   // Lv3+ 는 전략 기반이라 별도 처리
    };

    /// <summary>거래 수락/제안 판단 임계값 - Lv5+ 한 단계 상향</summary>
    public static float TradeThreshold(AIDifficulty d) => (int)d switch
    {
        4 => 1.5f,
        5 => 2.5f,   // 구 Lv6 수준
        6 => 3.0f,   // 구 Lv7 수준
        7 => 3.5f,   // 구 Lv8급
        8 => 3.5f,   // 구 Lv9급
        9 => 4.0f,   // 클로디: 거래 매우 까다롭게
        _ => 2.0f
    };

    /// <summary>Lv9 전용: 선두 플레이어에게 도적을 집중하는가</summary>
    public static bool FocusesLeader(AIDifficulty d) => (int)d >= 9;

    /// <summary>Lv9 전용: 승리 직전(8VP+) 가속 모드 - 건물/카드 올인</summary>
    public static bool UsesEndgameAccel(AIDifficulty d) => (int)d >= 9;

    /// <summary>Lv9 전용: 선두에게 거래 완전 거부</summary>
    public static bool RefusesLeaderTrade(AIDifficulty d) => (int)d >= 9;

    /// <summary>AI 레벨별 캐릭터 이름</summary>
    public static string GetAIName(AIDifficulty d) => (int)d switch
    {
        1 => "덕",         // Duck
        2 => "잼미니",     // Jemini
        3 => "또리",       // Ddori - 기초 전략
        4 => "지피",       // Gipy
        5 => "수리",       // Suri - 중급
        6 => "그룩",       // Grook
        7 => "아르카",     // Arka
        8 => "페이큰",     // Faken
        9 => "클로디",     // Cloudi - 마스터
        _ => "AI"
    };

    /// <summary>AI 레벨별 영문 이름</summary>
    public static string GetAINameEN(AIDifficulty d) => (int)d switch
    {
        1 => "Duck",
        2 => "Jemini",
        3 => "Ddori",
        4 => "Gipy",
        5 => "Suri",
        6 => "Grook",
        7 => "Arka",
        8 => "Faken",
        9 => "Cloudi",
        _ => "AI"
    };
}

/// <summary>
/// AI 컨트롤러 - 비인간 플레이어의 턴 자동 수행
/// LocalGameManager와 같은 GameObject에 부착
/// </summary>
public class AIController : MonoBehaviour
{
    [Header("AI 설정 (0=인간, 1~9=AI 레벨)")]
    [SerializeField] AIDifficulty player0AI = AIDifficulty.None;
    [SerializeField] AIDifficulty player1AI = AIDifficulty.Lv5;
    [SerializeField] AIDifficulty player2AI = AIDifficulty.Lv5;
    [SerializeField] AIDifficulty player3AI = AIDifficulty.Lv5;

    [Header("타이밍 (초)")]
    [SerializeField, Range(0.2f, 3f)] float thinkDelay = 0.8f;
    [SerializeField, Range(0.1f, 1f)] float actionDelay = 0.3f;

    AIDifficulty[] difficulties;
    AIStrategyType[] playerStrategies; // 플레이어별 선택된 전략
    IGameManager gm;
    Coroutine currentCoroutine;
    bool devCardUsedThisAction; // 코루틴 결과 전달용

    // ========================
    // LIFECYCLE
    // ========================

    void Awake()
    {
        // SetDifficulties()가 먼저 호출됐으면 덮어쓰지 않음
        // (LocalGameManager.Awake [-100] → SetDifficulties → AIController.Awake [0])
        difficulties ??= new[] { player0AI, player1AI, player2AI, player3AI };
        playerStrategies ??= new AIStrategyType[4];
    }

    /// <summary>외부에서 난이도 설정 (메인 메뉴 → SceneFlowManager 경유)</summary>
    public void SetDifficulties(AIDifficulty[] diffs)
    {
        // Awake보다 먼저 호출될 수 있음 (LocalGameManager DefaultExecutionOrder -100)
        difficulties ??= new[] { player0AI, player1AI, player2AI, player3AI };
        playerStrategies ??= new AIStrategyType[4];
        if (diffs == null) return;
        for (int i = 0; i < Mathf.Min(diffs.Length, 4); i++)
            difficulties[i] = diffs[i];
    }

    void Start()
    {
        gm = GameServices.GameManager;
        if (gm == null)
        {
            Debug.LogError("[AI] GameManager가 없습니다!");
            enabled = false;
            return;
        }

        // 인간 플레이어 인덱스 설정
        int humanIndex = -1;
        for (int i = 0; i < difficulties.Length; i++)
        {
            if (difficulties[i] == AIDifficulty.None)
            {
                humanIndex = i;
                break;
            }
        }

        var localGM = GetComponent<LocalGameManager>();
        if (localGM != null && humanIndex >= 0)
            localGM.SetHumanPlayerIndex(humanIndex);

        gm.OnTurnChanged += OnTurnChanged;
        gm.OnPhaseChanged += OnPhaseChanged;
    }

    void OnDestroy()
    {
        if (gm != null)
        {
            gm.OnTurnChanged -= OnTurnChanged;
            gm.OnPhaseChanged -= OnPhaseChanged;
        }
    }

    // ========================
    // PUBLIC
    // ========================

    public bool IsAI(int playerIndex) =>
        playerIndex >= 0 && playerIndex < difficulties.Length &&
        difficulties[playerIndex] != AIDifficulty.None;

    public AIDifficulty GetDifficulty(int playerIndex) =>
        playerIndex >= 0 && playerIndex < difficulties.Length
            ? difficulties[playerIndex]
            : AIDifficulty.None;

    /// <summary>플레이어의 선택된 전략 프로필 (None이면 null)</summary>
    AIStrategyProfile GetStrategy(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= playerStrategies.Length) return null;
        return AIStrategySelector.GetProfile(playerStrategies[playerIndex]);
    }

    // ========================
    // EVENT HANDLERS
    // ========================

    void OnTurnChanged(int playerIndex)
    {
        if (!IsAI(playerIndex)) return;

        StopAI();
        switch (gm.CurrentPhase)
        {
            case GamePhase.InitialPlacement:
                currentCoroutine = StartCoroutine(DoInitialPlacement(playerIndex));
                break;
            case GamePhase.RollDice:
                currentCoroutine = StartCoroutine(DoFullTurn(playerIndex));
                break;
        }
    }

    void OnPhaseChanged(GamePhase phase)
    {
        int pi = gm.CurrentPlayerIndex;
        if (!IsAI(pi)) return;

        // 기사카드 사용 후 약탈 선택 등, 코루틴 밖에서 발생하는 페이즈
        if (phase == GamePhase.StealResource && currentCoroutine == null)
            currentCoroutine = StartCoroutine(DoStealResource(pi));
    }

    void StopAI()
    {
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
            currentCoroutine = null;
        }
    }

    // ========================
    // 초기 배치
    // ========================

    IEnumerator DoInitialPlacement(int playerIndex)
    {
        var diff = GetDifficulty(playerIndex);
        yield return new WaitForSeconds(thinkDelay);

        // 초기 배치는 전략 없이 순수 핍 기반 평가
        // (전략은 초기 배치 완료 후 확보한 자원 기반으로 결정)

        // 1. 마을 배치
        var validVertices = gm.GetValidSettlementVertices(playerIndex, true);
        if (validVertices.Count == 0)
        {
            Debug.LogWarning($"[AI] P{playerIndex}: 배치 가능한 교차점 없음!");
            yield break;
        }

        int vertexId = PickBestVertex(validVertices, playerIndex, diff);
        Debug.Log($"[AI] P{playerIndex} ({diff}): 마을 배치 → Vertex {vertexId}");
        gm.TryBuildSettlement(vertexId);

        yield return new WaitForSeconds(actionDelay);

        // 2. 도로 배치
        var validEdges = gm.GetValidRoadEdges(playerIndex, true);
        if (validEdges.Count == 0)
        {
            Debug.LogWarning($"[AI] P{playerIndex}: 배치 가능한 도로 없음!");
            yield break;
        }

        int edgeId = PickBestEdge(validEdges, playerIndex, diff);
        Debug.Log($"[AI] P{playerIndex} ({diff}): 도로 배치 → Edge {edgeId}");
        gm.TryBuildRoad(edgeId);

        currentCoroutine = null;
    }

    // ========================
    // 일반 턴 (주사위 → 액션 → 종료)
    // ========================

    IEnumerator DoFullTurn(int playerIndex)
    {
        var diff = GetDifficulty(playerIndex);

        // 전략 결정 (초기 배치 완료 후 첫 일반 턴에서)
        // 실제 확보한 자원 타일을 보고 전략 선택
        if (playerStrategies[playerIndex] == AIStrategyType.None && AIDifficultySettings.UsesStrategy(diff))
        {
            var allPlayers = GetAllPlayerStates();
            playerStrategies[playerIndex] = AIStrategySelector.SelectStrategyFromOwnedTiles(
                gm.GetGrid(), playerIndex, diff, allPlayers, playerStrategies);
            Debug.Log($"[AI] P{playerIndex} ({diff}): 확보 자원 기반 전략 → {playerStrategies[playerIndex]}");
        }

        // 1. 주사위
        yield return new WaitForSeconds(thinkDelay);
        gm.RollDice();
        yield return new WaitForSeconds(actionDelay);

        // 2. 도적 이동 (7이 나왔을 때)
        if (gm.CurrentPhase == GamePhase.MoveRobber)
        {
            yield return DoMoveRobber(playerIndex, diff);
            yield return new WaitForSeconds(actionDelay);
        }

        // 3. 약탈 선택
        if (gm.CurrentPhase == GamePhase.StealResource)
        {
            yield return DoStealResource(playerIndex);
            yield return new WaitForSeconds(actionDelay);
        }

        // 4. 액션 페이즈
        if (gm.CurrentPhase == GamePhase.Action)
        {
            yield return DoActionPhase(playerIndex, diff);
        }

        // 5. 턴 종료
        if (gm.CurrentPhase == GamePhase.Action)
        {
            yield return new WaitForSeconds(actionDelay);
            gm.EndTurn();
        }

        currentCoroutine = null;
    }

    // ========================
    // 도적 이동
    // ========================

    IEnumerator DoMoveRobber(int playerIndex, AIDifficulty diff)
    {
        yield return new WaitForSeconds(thinkDelay * 0.5f);

        var grid = gm.GetGrid();
        HexCoord bestTile = default;
        float bestScore = -999f;

        // 현재 도적 위치
        HexCoord currentRobber = default;
        foreach (var tile in grid.Tiles.Values)
        {
            if (tile.HasRobber) { currentRobber = tile.Coord; break; }
        }

        // 모든 플레이어 상태
        var allPlayers = GetAllPlayerStates();

        // 도적 노이즈는 평가 노이즈의 30% (도적은 정밀하게 배치해야 함)
        float robberNoise = AIDifficultySettings.EvalNoise(diff) * 0.3f;

        foreach (var tile in grid.Tiles.Values)
        {
            if (tile.Resource == ResourceType.Sea) continue;
            if (tile.Coord.Equals(currentRobber)) continue;

            float score;
            if (AIDifficultySettings.IsRandom(diff))
            {
                score = Random.value;
            }
            else
            {
                float baseScore = AIBoardEvaluator.EvaluateRobberTarget(tile, playerIndex, allPlayers, diff);
                // 적 건물이 없는 타일(score=0)은 건너뜀
                if (baseScore <= 0f) continue;
                score = baseScore + Random.Range(0f, robberNoise);
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestTile = tile.Coord;
            }
        }

        Debug.Log($"[AI] P{playerIndex}: 도적 이동 → {bestTile} (점수: {bestScore:F1})");
        gm.TryMoveRobber(bestTile);
    }

    // ========================
    // 약탈
    // ========================

    IEnumerator DoStealResource(int playerIndex)
    {
        yield return new WaitForSeconds(actionDelay);

        var candidates = gm.GetRobberStealCandidates();
        if (candidates.Count == 0) yield break;

        var diff = GetDifficulty(playerIndex);
        int victim;

        if (AIDifficultySettings.IsRandom(diff))
        {
            victim = candidates[Random.Range(0, candidates.Count)];
        }
        else
        {
            // VP 높은 + 자원 많은 상대 우선
            victim = candidates[0];
            int bestScore = 0;
            foreach (int c in candidates)
            {
                var state = gm.GetPlayerState(c);
                // Lv6+ 는 VP 가중치 높게
                int vpWeight = AIDifficultySettings.TracksOpponentVP(diff) ? 10 : 3;
                int score = state.VictoryPoints * vpWeight + state.TotalResourceCount;
                if (score > bestScore)
                {
                    bestScore = score;
                    victim = c;
                }
            }
        }

        Debug.Log($"[AI] P{playerIndex}: 약탈 대상 → P{victim}");
        gm.TryStealFromPlayer(victim);

        currentCoroutine = null;
    }

    // ========================
    // 액션 페이즈 (전략 기반 순서)
    // ========================

    IEnumerator DoActionPhase(int playerIndex, AIDifficulty diff)
    {
        yield return new WaitForSeconds(thinkDelay * 0.5f);

        var player = gm.GetPlayerState(playerIndex);
        var strategy = GetStrategy(playerIndex);
        var actionOrder = AIStrategySelector.GetActionOrder(strategy);
        int actions = 0;
        // Lv9 엔드게임 가속: 승리 직전(8VP+)이면 더 많은 액션 시도
        bool endgameAccel = AIDifficultySettings.UsesEndgameAccel(diff) && player.VictoryPoints >= 8;
        int maxActions = endgameAccel ? 12 : 8;
        if (endgameAccel) Debug.Log($"[AI] P{playerIndex} 클로디: 🔥 엔드게임 가속 모드! (VP:{player.VictoryPoints})");

        while (actions < maxActions && gm.CurrentPhase == GamePhase.Action)
        {
            bool acted = false;

            foreach (var actionType in actionOrder)
            {
                switch (actionType)
                {
                    case AIActionType.UseDevCard:
                        if (!player.HasUsedDevCardThisTurn && player.HasUsableDevCard(gm.TurnNumber))
                        {
                            devCardUsedThisAction = false;
                            yield return TryUseDevCards(playerIndex, diff);
                            if (devCardUsedThisAction) acted = true;
                        }
                        break;

                    case AIActionType.BuildCity:
                        if (player.CanAfford(BuildingCosts.City))
                        {
                            var cityVertices = gm.GetValidCityVertices(playerIndex);
                            if (cityVertices.Count > 0)
                            {
                                int best = PickBestCityUpgrade(cityVertices, playerIndex, diff);
                                if (gm.TryBuildCity(best))
                                {
                                    Debug.Log($"[AI] P{playerIndex}: 도시 업그레이드 → V{best}");
                                    acted = true;
                                }
                            }
                        }
                        break;

                    case AIActionType.BuildSettlement:
                        if (player.CanAfford(BuildingCosts.Settlement))
                        {
                            var vertices = gm.GetValidSettlementVertices(playerIndex, false);
                            if (vertices.Count > 0)
                            {
                                int best = PickBestVertex(vertices, playerIndex, diff);
                                if (gm.TryBuildSettlement(best))
                                {
                                    Debug.Log($"[AI] P{playerIndex}: 마을 건설 → V{best}");
                                    acted = true;
                                }
                            }
                        }
                        break;

                    case AIActionType.PlayerTrade:
                        if (AIDifficultySettings.UsesTrade(diff) && TryPlayerTrade(playerIndex, diff))
                            acted = true;
                        break;

                    case AIActionType.BankTrade:
                        if (AIDifficultySettings.UsesTrade(diff) && TryBankTrade(playerIndex, diff))
                            acted = true;
                        break;

                    case AIActionType.BuyDevCard:
                        if (player.CanAfford(BuildingCosts.DevelopmentCard))
                        {
                            if (ShouldBuyDevCard(playerIndex, diff) && gm.TryBuyDevCard())
                            {
                                Debug.Log($"[AI] P{playerIndex}: 발전카드 구매");
                                acted = true;
                            }
                        }
                        break;

                    case AIActionType.BuildRoad:
                        if (player.CanAfford(BuildingCosts.Road) && ShouldBuildRoad(playerIndex, diff))
                        {
                            var roadEdges = gm.GetValidRoadEdges(playerIndex, false);
                            if (roadEdges.Count > 0)
                            {
                                int best = PickBestEdge(roadEdges, playerIndex, diff);
                                if (gm.TryBuildRoad(best))
                                {
                                    Debug.Log($"[AI] P{playerIndex}: 도로 건설 → E{best}");
                                    acted = true;
                                }
                            }
                        }
                        break;
                }

                if (acted)
                {
                    yield return new WaitForSeconds(actionDelay);
                    actions++;
                    break; // foreach 탈출 → while 루프 다시
                }
            }

            if (!acted) break; // 모든 액션 시도 실패 → 턴 종료
        }
    }

    // ========================
    // 발전카드 사용
    // ========================

    IEnumerator TryUseDevCards(int playerIndex, AIDifficulty diff)
    {
        devCardUsedThisAction = false;
        var player = gm.GetPlayerState(playerIndex);
        var strategy = GetStrategy(playerIndex);
        int turn = gm.TurnNumber;

        // 기사 카드 (Lv3+ / 전략이 기사단 추구하면 더 적극적)
        var knight = player.FindUsableCard(DevCardType.Knight, turn);
        if (knight != null && AIDifficultySettings.UsesDevCards(diff))
        {
            bool shouldUse = true;
            // 공격적 사용 레벨 미달이고, 전략이 기사단 비추구면 → 방어적으로만 사용
            if (!AIDifficultySettings.UsesKnightOffensively(diff) &&
                (strategy == null || !strategy.PursuesLargestArmy))
            {
                shouldUse = IsRobberOnMyTile(playerIndex);
            }

            if (shouldUse && gm.TryUseKnight(default))
            {
                Debug.Log($"[AI] P{playerIndex}: 기사 카드 사용");
                yield return new WaitForSeconds(actionDelay);

                // 도적 이동 타겟 선정
                var grid = gm.GetGrid();
                HexCoord target = PickRobberTarget(playerIndex, diff, grid);
                gm.TryUseKnight(target);
                yield return new WaitForSeconds(actionDelay);

                // 약탈 처리
                if (gm.CurrentPhase == GamePhase.StealResource)
                {
                    yield return DoStealResource(playerIndex);
                    yield return new WaitForSeconds(actionDelay);
                }

                devCardUsedThisAction = true;
                yield break;
            }
        }

        // 도로건설 카드
        var roadCard = player.FindUsableCard(DevCardType.RoadBuilding, turn);
        if (roadCard != null && player.RoadsRemaining >= 1)
        {
            if (gm.TryUseRoadBuilding())
            {
                Debug.Log($"[AI] P{playerIndex}: 도로건설 카드 사용");
                yield return new WaitForSeconds(actionDelay);

                // 무료 도로 1
                var roads1 = gm.GetValidRoadEdges(playerIndex, false);
                if (roads1.Count > 0)
                {
                    int edge1 = PickBestEdge(roads1, playerIndex, diff);
                    gm.TryBuildRoad(edge1);
                    yield return new WaitForSeconds(actionDelay);
                }

                // 무료 도로 2
                if (gm.DevCardState == DevCardUseState.PlacingFreeRoad2)
                {
                    var roads2 = gm.GetValidRoadEdges(playerIndex, false);
                    if (roads2.Count > 0)
                    {
                        int edge2 = PickBestEdge(roads2, playerIndex, diff);
                        gm.TryBuildRoad(edge2);
                    }
                }

                devCardUsedThisAction = true;
                yield break;
            }
        }

        // 풍년 카드 (전략 기반 자원 선택)
        var yop = player.FindUsableCard(DevCardType.YearOfPlenty, turn);
        if (yop != null)
        {
            AIBoardEvaluator.PickNeededResources(player, strategy, out var res1, out var res2);
            if (gm.TryUseYearOfPlenty(res1, res2))
            {
                Debug.Log($"[AI] P{playerIndex}: 풍년 카드 → {res1} + {res2}");
                devCardUsedThisAction = true;
                yield break;
            }
        }

        // 독점 카드 (Lv4+)
        var monopoly = player.FindUsableCard(DevCardType.Monopoly, turn);
        if (monopoly != null && AIDifficultySettings.UsesMonopoly(diff))
        {
            var allPlayers = GetAllPlayerStates();
            var monopolyTarget = AIBoardEvaluator.PickBestMonopolyTarget(
                playerIndex, allPlayers, gm.PlayerCount);
            if (gm.TryUseMonopoly(monopolyTarget))
            {
                Debug.Log($"[AI] P{playerIndex}: 독점 카드 → {monopolyTarget}");
                devCardUsedThisAction = true;
                yield break;
            }
        }
    }

    // ========================
    // 거래 로직
    // ========================

    bool TryPlayerTrade(int playerIndex, AIDifficulty diff)
    {
        if (AIBoardEvaluator.FindBestPlayerTrade(playerIndex, gm, diff,
            out int target, out var offer, out var request))
        {
            if (gm.TryPlayerTrade(target, offer, request))
            {
                // 로그용 문자열
                string offerStr = "", reqStr = "";
                foreach (var kv in offer) offerStr += $"{kv.Key}×{kv.Value} ";
                foreach (var kv in request) reqStr += $"{kv.Key}×{kv.Value} ";
                Debug.Log($"[AI] P{playerIndex}: P{target}과 거래 - 제공:{offerStr.TrimEnd()} 요청:{reqStr.TrimEnd()}");
                return true;
            }
        }
        return false;
    }

    bool TryBankTrade(int playerIndex, AIDifficulty diff)
    {
        var player = gm.GetPlayerState(playerIndex);
        var strategy = GetStrategy(playerIndex);
        float aggression = strategy?.TradeAggression ?? 1f;

        // 전략 빌드 목표 기반 부족 자원 확인
        var buildGoal = AIStrategySelector.GetPrimaryBuildGoal(strategy);
        int missingCount = 0;
        foreach (var kv in buildGoal)
        {
            if (player.Resources.ContainsKey(kv.Key) && player.Resources[kv.Key] < kv.Value)
                missingCount++;
        }

        // Lv9 엔드게임 가속: 승리 직전이면 거래 조건 완화
        bool endgameAccel = AIDifficultySettings.UsesEndgameAccel(diff) &&
            gm.GetPlayerState(playerIndex).VictoryPoints >= 8;

        // 기본: 1종류 부족하면 거래, 적극적 전략(Port 등): 2종류도 OK
        // 엔드게임 가속: 3종류까지도 거래 시도
        int maxMissing = endgameAccel ? 3 : (aggression >= 1.3f ? 2 : 1);
        if (missingCount == 0 || missingCount > maxMissing) return false;

        if (AIBoardEvaluator.FindBestBankTrade(player, gm, strategy, out var give, out var receive))
        {
            if (gm.TryBankTrade(give, receive))
            {
                Debug.Log($"[AI] P{playerIndex}: 은행 거래 {give} → {receive}");
                return true;
            }
        }

        return false;
    }

    // ========================
    // 의사결정 헬퍼
    // ========================

    int PickBestVertex(List<int> validIds, int playerIndex, AIDifficulty diff)
    {
        if (AIDifficultySettings.IsRandom(diff))
            return validIds[Random.Range(0, validIds.Count)];

        var grid = gm.GetGrid();
        var strategy = GetStrategy(playerIndex);
        float noise = AIDifficultySettings.EvalNoise(diff);
        int best = validIds[0];
        float bestScore = -1f;

        foreach (int id in validIds)
        {
            float score = AIBoardEvaluator.EvaluateVertex(grid.Vertices[id], playerIndex, diff, strategy);
            score += Random.Range(0f, noise);
            if (score > bestScore)
            {
                bestScore = score;
                best = id;
            }
        }

        return best;
    }

    int PickBestEdge(List<int> validIds, int playerIndex, AIDifficulty diff)
    {
        if (AIDifficultySettings.IsRandom(diff))
            return validIds[Random.Range(0, validIds.Count)];

        var grid = gm.GetGrid();
        var strategy = GetStrategy(playerIndex);
        float noise = AIDifficultySettings.EvalNoise(diff);
        int best = validIds[0];
        float bestScore = -1f;

        foreach (int id in validIds)
        {
            float score = AIBoardEvaluator.EvaluateRoadEdge(grid.Edges[id], playerIndex, diff, strategy);
            score += Random.Range(0f, noise);
            if (score > bestScore)
            {
                bestScore = score;
                best = id;
            }
        }

        return best;
    }

    int PickBestCityUpgrade(List<int> validIds, int playerIndex, AIDifficulty diff)
    {
        if (AIDifficultySettings.IsRandom(diff) || validIds.Count == 1)
            return validIds[Random.Range(0, validIds.Count)];

        var grid = gm.GetGrid();
        var strategy = GetStrategy(playerIndex);
        int best = validIds[0];
        float bestScore = -1f;

        foreach (int id in validIds)
        {
            float score = 0f;
            foreach (var tile in grid.Vertices[id].AdjacentTiles)
            {
                if (!tile.ProducesResource) continue;
                int pips = AIBoardEvaluator.GetPips(tile.NumberToken);
                // 전략 가중치 적용
                if (strategy != null && strategy.ResourceWeights.TryGetValue(tile.Resource, out float w))
                    score += pips * w;
                else
                    score += pips;
            }
            score += Random.Range(0f, 0.5f);
            if (score > bestScore)
            {
                bestScore = score;
                best = id;
            }
        }

        return best;
    }

    HexCoord PickRobberTarget(int playerIndex, AIDifficulty diff, HexGrid grid)
    {
        HexCoord best = default;
        float bestScore = -999f;
        var allPlayers = GetAllPlayerStates();

        HexCoord currentRobber = default;
        foreach (var tile in grid.Tiles.Values)
        {
            if (tile.HasRobber) { currentRobber = tile.Coord; break; }
        }

        float robberNoise = AIDifficultySettings.EvalNoise(diff) * 0.3f;

        foreach (var tile in grid.Tiles.Values)
        {
            if (tile.Resource == ResourceType.Sea) continue;
            if (tile.Coord.Equals(currentRobber)) continue;

            float score;
            if (AIDifficultySettings.IsRandom(diff))
            {
                score = Random.value;
            }
            else
            {
                float baseScore = AIBoardEvaluator.EvaluateRobberTarget(tile, playerIndex, allPlayers, diff);
                if (baseScore <= 0f) continue;
                score = baseScore + Random.Range(0f, robberNoise);
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = tile.Coord;
            }
        }

        return best;
    }

    bool ShouldBuildRoad(int playerIndex, AIDifficulty diff)
    {
        if (AIDifficultySettings.IsRandom(diff))
            return Random.value < AIDifficultySettings.RandomActionChance(diff);

        var player = gm.GetPlayerState(playerIndex);
        var strategy = GetStrategy(playerIndex);

        // Lv9 엔드게임 가속: 최장도로로 승리 가능하면 무조건 건설
        if (AIDifficultySettings.UsesEndgameAccel(diff) && player.VictoryPoints >= 8)
        {
            int myRoad = gm.GetLongestRoadLength(playerIndex);
            int holder = gm.GetLongestRoadHolder();
            // 최장도로 탈환/획득으로 +2VP → 승리 가능
            if (holder != playerIndex && myRoad >= 4)
                return true;
            // 이미 최장도로 보유 중이면 방어 불필요
        }

        // 마을 배치 가능한 곳이 없으면 도로로 확장
        var settlements = gm.GetValidSettlementVertices(playerIndex, false);
        if (settlements.Count == 0) return true;

        // 전략이 도로 우선이면 적극 건설
        if (strategy != null && strategy.RoadPriority >= 1.5f)
            return true;

        // 최장도로 추구 전략 또는 Lv6+ 전략 없는 경우
        bool pursuesRoad = strategy != null && strategy.PursuesLongestRoad;
        bool highLevelNoStrategy = strategy == null && AIDifficultySettings.UsesFullStrategy(diff);

        if (pursuesRoad || highLevelNoStrategy)
        {
            int myRoad = gm.GetLongestRoadLength(playerIndex);
            int holder = gm.GetLongestRoadHolder();
            if (holder < 0 && myRoad >= 3) return true;
            if (holder != playerIndex && myRoad >= 4) return true;
        }

        return false;
    }

    bool ShouldBuyDevCard(int playerIndex, AIDifficulty diff)
    {
        if (AIDifficultySettings.IsRandom(diff))
            return Random.value < AIDifficultySettings.RandomActionChance(diff);

        var player = gm.GetPlayerState(playerIndex);
        var strategy = GetStrategy(playerIndex);

        // 승리 직전이면 건물 우선 (BUT Lv9 엔드게임 가속: VP카드로 마무리 가능)
        if (player.VictoryPoints >= 8)
        {
            if (AIDifficultySettings.UsesEndgameAccel(diff))
            {
                // VP카드가 승리를 줄 수 있음 → 적극 구매
                // 최대기사단 미보유 + 기사 2장 이상 → 기사단 노림
                int armyHolder = gm.GetLargestArmyHolder();
                if (armyHolder != playerIndex && player.KnightsPlayed >= 2)
                    return true;
                // 그 외에도 50% 확률로 VP카드 도전
                return Random.value < 0.5f;
            }
            return false;
        }

        // 전략이 발전카드 비추구이면 확률 대폭 감소
        if (strategy != null && strategy.DevCardPriority < 0.5f)
            return Random.value > 0.85f;

        // 전략이 기사단 추구이면 적극 구매
        bool pursuesArmy = strategy != null && strategy.PursuesLargestArmy;
        bool highLevelNoStrategy = strategy == null && AIDifficultySettings.UsesFullStrategy(diff);

        if (pursuesArmy || highLevelNoStrategy)
        {
            int armyHolder = gm.GetLargestArmyHolder();
            if (armyHolder < 0 && player.KnightsPlayed >= 1) return true;
            if (armyHolder != playerIndex && player.KnightsPlayed >= 2) return true;
        }

        // 전략의 발전카드 우선도에 따른 확률
        float buyChance = strategy != null ? 0.3f + strategy.DevCardPriority * 0.15f : 0.5f;
        return Random.value > (1f - buyChance);
    }

    // ========================
    // 유틸리티
    // ========================

    /// <summary>도적이 자기 타일(건물 인접) 위에 있는지</summary>
    bool IsRobberOnMyTile(int playerIndex)
    {
        var player = gm.GetPlayerState(playerIndex);
        foreach (var vertex in player.OwnedVertices)
        {
            foreach (var tile in vertex.AdjacentTiles)
            {
                if (tile.HasRobber) return true;
            }
        }
        return false;
    }

    PlayerState[] GetAllPlayerStates()
    {
        var states = new PlayerState[gm.PlayerCount];
        for (int i = 0; i < gm.PlayerCount; i++)
            states[i] = gm.GetPlayerState(i);
        return states;
    }

}
