using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>AI 난이도</summary>
public enum AIDifficulty
{
    None,   // 인간 플레이어
    Easy,   // 랜덤 행동
    Medium, // 확률 기반
    Hard    // 전략적 판단
}

/// <summary>
/// AI 컨트롤러 - 비인간 플레이어의 턴 자동 수행
/// LocalGameManager와 같은 GameObject에 부착
/// </summary>
public class AIController : MonoBehaviour
{
    [Header("AI 설정")]
    [SerializeField] AIDifficulty player0AI = AIDifficulty.None;
    [SerializeField] AIDifficulty player1AI = AIDifficulty.Medium;
    [SerializeField] AIDifficulty player2AI = AIDifficulty.Medium;
    [SerializeField] AIDifficulty player3AI = AIDifficulty.Medium;

    [Header("타이밍 (초)")]
    [SerializeField, Range(0.2f, 3f)] float thinkDelay = 0.8f;
    [SerializeField, Range(0.1f, 1f)] float actionDelay = 0.3f;

    AIDifficulty[] difficulties;
    IGameManager gm;
    Coroutine currentCoroutine;
    bool devCardUsedThisAction; // 코루틴 결과 전달용

    // ========================
    // LIFECYCLE
    // ========================

    void Awake()
    {
        difficulties = new[] { player0AI, player1AI, player2AI, player3AI };
    }

    /// <summary>외부에서 난이도 설정 (메인 메뉴 → SceneFlowManager 경유)</summary>
    public void SetDifficulties(AIDifficulty[] diffs)
    {
        // Awake보다 먼저 호출될 수 있음 (LocalGameManager DefaultExecutionOrder -100)
        difficulties ??= new[] { player0AI, player1AI, player2AI, player3AI };
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

        foreach (var tile in grid.Tiles.Values)
        {
            if (tile.Resource == ResourceType.Sea) continue;
            if (tile.Coord.Equals(currentRobber)) continue;

            float score;
            if (diff == AIDifficulty.Easy)
            {
                score = Random.value;
            }
            else
            {
                score = AIBoardEvaluator.EvaluateRobberTarget(tile, playerIndex, allPlayers, diff);
                score += Random.Range(0f, 0.5f); // 약간의 랜덤성
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestTile = tile.Coord;
            }
        }

        Debug.Log($"[AI] P{playerIndex}: 도적 이동 → {bestTile}");
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

        if (diff == AIDifficulty.Easy)
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
                int score = state.VictoryPoints * 10 + state.TotalResourceCount;
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
    // 액션 페이즈 (건설/카드/거래)
    // ========================

    IEnumerator DoActionPhase(int playerIndex, AIDifficulty diff)
    {
        yield return new WaitForSeconds(thinkDelay * 0.5f);

        var player = gm.GetPlayerState(playerIndex);
        int actions = 0;
        int maxActions = 8;

        while (actions < maxActions && gm.CurrentPhase == GamePhase.Action)
        {
            // --- 발전카드 사용 ---
            if (!player.HasUsedDevCardThisTurn && player.HasUsableDevCard(gm.TurnNumber))
            {
                devCardUsedThisAction = false;
                yield return TryUseDevCards(playerIndex, diff);
                if (devCardUsedThisAction)
                {
                    yield return new WaitForSeconds(actionDelay);
                    actions++;
                    continue;
                }
            }

            // --- 도시 업그레이드 (VP 효율 최고) ---
            if (player.CanAfford(BuildingCosts.City))
            {
                var cityVertices = gm.GetValidCityVertices(playerIndex);
                if (cityVertices.Count > 0)
                {
                    int best = PickBestCityUpgrade(cityVertices, playerIndex, diff);
                    if (gm.TryBuildCity(best))
                    {
                        Debug.Log($"[AI] P{playerIndex}: 도시 업그레이드 → V{best}");
                        yield return new WaitForSeconds(actionDelay);
                        actions++;
                        continue;
                    }
                }
            }

            // --- 마을 건설 ---
            if (player.CanAfford(BuildingCosts.Settlement))
            {
                var settlementVertices = gm.GetValidSettlementVertices(playerIndex, false);
                if (settlementVertices.Count > 0)
                {
                    int best = PickBestVertex(settlementVertices, playerIndex, diff);
                    if (gm.TryBuildSettlement(best))
                    {
                        Debug.Log($"[AI] P{playerIndex}: 마을 건설 → V{best}");
                        yield return new WaitForSeconds(actionDelay);
                        actions++;
                        continue;
                    }
                }
            }

            // --- 플레이어 간 거래 (Medium+, 1:1 교환) ---
            if (diff >= AIDifficulty.Medium)
            {
                bool playerTraded = TryPlayerTrade(playerIndex, diff);
                if (playerTraded)
                {
                    yield return new WaitForSeconds(actionDelay);
                    actions++;
                    continue;
                }
            }

            // --- 은행 거래 (Medium+, 4:1~2:1) ---
            if (diff >= AIDifficulty.Medium)
            {
                bool traded = TryBankTrade(playerIndex, diff);
                if (traded)
                {
                    yield return new WaitForSeconds(actionDelay);
                    actions++;
                    continue;
                }
            }

            // --- 발전카드 구매 ---
            if (player.CanAfford(BuildingCosts.DevelopmentCard))
            {
                bool shouldBuy = ShouldBuyDevCard(playerIndex, diff);
                if (shouldBuy && gm.TryBuyDevCard())
                {
                    Debug.Log($"[AI] P{playerIndex}: 발전카드 구매");
                    yield return new WaitForSeconds(actionDelay);
                    actions++;
                    continue;
                }
            }

            // --- 도로 건설 ---
            if (player.CanAfford(BuildingCosts.Road))
            {
                bool shouldBuild = ShouldBuildRoad(playerIndex, diff);
                if (shouldBuild)
                {
                    var roadEdges = gm.GetValidRoadEdges(playerIndex, false);
                    if (roadEdges.Count > 0)
                    {
                        int best = PickBestEdge(roadEdges, playerIndex, diff);
                        if (gm.TryBuildRoad(best))
                        {
                            Debug.Log($"[AI] P{playerIndex}: 도로 건설 → E{best}");
                            yield return new WaitForSeconds(actionDelay);
                            actions++;
                            continue;
                        }
                    }
                }
            }

            // 더 이상 할 게 없음
            break;
        }
    }

    // ========================
    // 발전카드 사용
    // ========================

    IEnumerator TryUseDevCards(int playerIndex, AIDifficulty diff)
    {
        devCardUsedThisAction = false;
        var player = gm.GetPlayerState(playerIndex);
        int turn = gm.TurnNumber;

        // 기사 카드 (항상 유용 - 도적 이동 + 기사단 진행)
        var knight = player.FindUsableCard(DevCardType.Knight, turn);
        if (knight != null && diff >= AIDifficulty.Medium)
        {
            // 1단계: 기사 카드 활성화
            if (gm.TryUseKnight(default))
            {
                Debug.Log($"[AI] P{playerIndex}: 기사 카드 사용");
                yield return new WaitForSeconds(actionDelay);

                // 2단계: 도적 이동 타겟 선정 후 실행
                var grid = gm.GetGrid();
                HexCoord target = PickRobberTarget(playerIndex, diff, grid);
                gm.TryUseKnight(target);
                yield return new WaitForSeconds(actionDelay);

                // 3단계: 약탈 처리
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

        // 풍년 카드
        var yop = player.FindUsableCard(DevCardType.YearOfPlenty, turn);
        if (yop != null)
        {
            AIBoardEvaluator.PickNeededResources(player, out var res1, out var res2);
            if (gm.TryUseYearOfPlenty(res1, res2))
            {
                Debug.Log($"[AI] P{playerIndex}: 풍년 카드 → {res1} + {res2}");
                devCardUsedThisAction = true;
                yield break;
            }
        }

        // 독점 카드
        var monopoly = player.FindUsableCard(DevCardType.Monopoly, turn);
        if (monopoly != null && diff >= AIDifficulty.Medium)
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

        // 건설에 필요한 자원이 부족할 때만 거래
        bool needsTrade = false;

        // 마을 짓고 싶은데 자원 부족?
        if (player.SettlementsRemaining > 0)
        {
            int missing = 0;
            foreach (var kv in BuildingCosts.Settlement)
            {
                if (player.Resources[kv.Key] < kv.Value) missing++;
            }
            if (missing == 1) needsTrade = true; // 1종류만 부족하면 거래 가치 있음
        }

        // 도시 짓고 싶은데 자원 부족?
        if (!needsTrade && player.CitiesRemaining > 0)
        {
            var cityVertices = gm.GetValidCityVertices(playerIndex);
            if (cityVertices.Count > 0)
            {
                int missing = 0;
                foreach (var kv in BuildingCosts.City)
                {
                    if (player.Resources[kv.Key] < kv.Value) missing++;
                }
                if (missing == 1) needsTrade = true;
            }
        }

        if (!needsTrade) return false;

        if (AIBoardEvaluator.FindBestBankTrade(player, gm, out var give, out var receive))
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
        if (diff == AIDifficulty.Easy)
            return validIds[Random.Range(0, validIds.Count)];

        var grid = gm.GetGrid();
        int best = validIds[0];
        float bestScore = -1f;

        foreach (int id in validIds)
        {
            float score = AIBoardEvaluator.EvaluateVertex(grid.Vertices[id], playerIndex, diff);
            score += Random.Range(0f, 1f); // 자연스러운 변동
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
        if (diff == AIDifficulty.Easy)
            return validIds[Random.Range(0, validIds.Count)];

        var grid = gm.GetGrid();
        int best = validIds[0];
        float bestScore = -1f;

        foreach (int id in validIds)
        {
            float score = AIBoardEvaluator.EvaluateRoadEdge(grid.Edges[id], playerIndex, diff);
            score += Random.Range(0f, 0.5f);
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
        if (diff == AIDifficulty.Easy || validIds.Count == 1)
            return validIds[Random.Range(0, validIds.Count)];

        var grid = gm.GetGrid();
        int best = validIds[0];
        float bestScore = -1f;

        foreach (int id in validIds)
        {
            float score = 0f;
            foreach (var tile in grid.Vertices[id].AdjacentTiles)
            {
                if (tile.ProducesResource)
                    score += AIBoardEvaluator.GetPips(tile.NumberToken);
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

        foreach (var tile in grid.Tiles.Values)
        {
            if (tile.Resource == ResourceType.Sea) continue;
            if (tile.Coord.Equals(currentRobber)) continue;

            float score = diff == AIDifficulty.Easy
                ? Random.value
                : AIBoardEvaluator.EvaluateRobberTarget(tile, playerIndex, allPlayers, diff) + Random.Range(0f, 0.5f);

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
        if (diff == AIDifficulty.Easy)
            return Random.value > 0.6f;

        var player = gm.GetPlayerState(playerIndex);

        // 마을 배치 가능한 곳이 없으면 도로로 확장
        var settlements = gm.GetValidSettlementVertices(playerIndex, false);
        if (settlements.Count == 0) return true;

        // Hard: 최장교역로 추격
        if (diff == AIDifficulty.Hard)
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
        if (diff == AIDifficulty.Easy)
            return Random.value > 0.7f;

        var player = gm.GetPlayerState(playerIndex);

        // 승리 직전이면 건물 우선
        if (player.VictoryPoints >= 8) return false;

        // 기사단 추격
        if (diff == AIDifficulty.Hard)
        {
            int armyHolder = gm.GetLargestArmyHolder();
            if (armyHolder < 0 && player.KnightsPlayed >= 1) return true;
            if (armyHolder != playerIndex && player.KnightsPlayed >= 2) return true;
        }

        return Random.value > 0.5f;
    }

    // ========================
    // 유틸리티
    // ========================

    PlayerState[] GetAllPlayerStates()
    {
        var states = new PlayerState[gm.PlayerCount];
        for (int i = 0; i < gm.PlayerCount; i++)
            states[i] = gm.GetPlayerState(i);
        return states;
    }

}
