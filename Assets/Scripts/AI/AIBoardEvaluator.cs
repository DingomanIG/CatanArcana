using System.Collections.Generic;

/// <summary>
/// AI 보드 평가 - 타일/교차점/변 가치 계산
/// </summary>
public static class AIBoardEvaluator
{
    // 주사위 확률 핍 (2~12)
    static readonly int[] PipTable = { 0, 0, 1, 2, 3, 4, 5, 0, 5, 4, 3, 2, 1 };

    /// <summary>주사위 숫자의 확률 핍 (6,8 = 5핍, 2,12 = 1핍)</summary>
    public static int GetPips(int number)
    {
        if (number < 0 || number >= PipTable.Length) return 0;
        return PipTable[number];
    }

    /// <summary>교차점 평가 (마을 배치용)</summary>
    public static float EvaluateVertex(HexVertex vertex, int playerIndex, AIDifficulty difficulty)
    {
        if (vertex.Building != BuildingType.None) return -1f;

        float score = 0f;
        var resourceTypes = new HashSet<ResourceType>();

        foreach (var tile in vertex.AdjacentTiles)
        {
            if (!tile.ProducesResource) continue;

            int pips = GetPips(tile.NumberToken);
            score += pips;
            resourceTypes.Add(tile.Resource);

            // Hard: 밀/광석 가중치 (도시 건설에 필수)
            if (difficulty >= AIDifficulty.Hard)
            {
                if (tile.Resource == ResourceType.Ore || tile.Resource == ResourceType.Wheat)
                    score += pips * 0.3f;
            }
        }

        // Medium+: 자원 다양성 보너스
        if (difficulty >= AIDifficulty.Medium)
            score += resourceTypes.Count * 1.5f;

        // Hard: 항구 보너스
        if (difficulty >= AIDifficulty.Hard && vertex.Port != PortType.None)
            score += vertex.Port == PortType.Generic ? 1f : 2f;

        return score;
    }

    /// <summary>도적 타겟 타일 평가 (높을수록 좋은 타겟)</summary>
    public static float EvaluateRobberTarget(HexTile tile, int myIndex, PlayerState[] players, AIDifficulty difficulty)
    {
        if (!tile.ProducesResource) return -1f;

        float score = 0f;
        int tilePips = GetPips(tile.NumberToken);

        foreach (var vertex in tile.Vertices)
        {
            if (vertex.Building == BuildingType.None) continue;
            int owner = vertex.OwnerPlayerIndex;
            if (owner == myIndex || owner < 0) continue;

            float buildingValue = vertex.Building == BuildingType.City ? 2f : 1f;
            score += tilePips * buildingValue;

            // Hard: 선두 플레이어 우선 타겟
            if (difficulty >= AIDifficulty.Hard)
                score += players[owner].VictoryPoints * 0.5f;
        }

        return score;
    }

    /// <summary>도로 변 평가 (확장 가치)</summary>
    public static float EvaluateRoadEdge(HexEdge edge, int playerIndex, AIDifficulty difficulty)
    {
        float score = 0f;

        // 양 끝점의 마을 건설 가능성 평가
        var endpoints = new[] { edge.VertexA, edge.VertexB };
        foreach (var vertex in endpoints)
        {
            if (vertex.Building != BuildingType.None) continue;
            if (vertex.OwnerPlayerIndex >= 0) continue;

            // 거리 규칙: 인접 교차점에 건물 없어야 함
            bool hasAdjacentBuilding = false;
            foreach (var adj in vertex.AdjacentVertices)
            {
                if (adj.Building != BuildingType.None)
                {
                    hasAdjacentBuilding = true;
                    break;
                }
            }

            if (!hasAdjacentBuilding)
                score += EvaluateVertex(vertex, playerIndex, difficulty) * 0.5f;
        }

        return score;
    }

    /// <summary>AI가 가장 필요한 자원 2개 선택 (풍년 카드용)</summary>
    public static void PickNeededResources(PlayerState player, out ResourceType res1, out ResourceType res2)
    {
        // 건설 비용 기반으로 부족한 자원 파악
        var needs = new Dictionary<ResourceType, int>
        {
            { ResourceType.Wood, 0 },
            { ResourceType.Brick, 0 },
            { ResourceType.Wool, 0 },
            { ResourceType.Wheat, 0 },
            { ResourceType.Ore, 0 }
        };

        // 마을 비용 우선
        foreach (var kv in BuildingCosts.Settlement)
            needs[kv.Key] += kv.Value;

        // 보유량 대비 부족분
        ResourceType best1 = ResourceType.Wheat, best2 = ResourceType.Ore;
        int maxNeed1 = -1, maxNeed2 = -1;

        foreach (var kv in needs)
        {
            int deficit = kv.Value - player.Resources[kv.Key];
            if (deficit > maxNeed1)
            {
                maxNeed2 = maxNeed1;
                best2 = best1;
                maxNeed1 = deficit;
                best1 = kv.Key;
            }
            else if (deficit > maxNeed2)
            {
                maxNeed2 = deficit;
                best2 = kv.Key;
            }
        }

        res1 = best1;
        res2 = best2;
    }

    /// <summary>독점 카드용: 상대가 가장 많이 가진 자원 타입</summary>
    public static ResourceType PickBestMonopolyTarget(int myIndex, PlayerState[] allPlayers, int playerCount)
    {
        var totals = new Dictionary<ResourceType, int>
        {
            { ResourceType.Wood, 0 },
            { ResourceType.Brick, 0 },
            { ResourceType.Wool, 0 },
            { ResourceType.Wheat, 0 },
            { ResourceType.Ore, 0 }
        };

        for (int i = 0; i < playerCount; i++)
        {
            if (i == myIndex) continue;
            foreach (var kv in allPlayers[i].Resources)
            {
                if (kv.Key != ResourceType.None && kv.Key != ResourceType.Sea)
                    totals[kv.Key] += kv.Value;
            }
        }

        ResourceType best = ResourceType.Wheat;
        int maxTotal = 0;
        foreach (var kv in totals)
        {
            if (kv.Value > maxTotal)
            {
                maxTotal = kv.Value;
                best = kv.Key;
            }
        }

        return best;
    }

    /// <summary>은행 거래 평가: 남는 자원 → 필요한 자원</summary>
    public static bool FindBestBankTrade(PlayerState player, IGameManager gm,
        out ResourceType give, out ResourceType receive)
    {
        give = ResourceType.None;
        receive = ResourceType.None;

        // 가장 많이 보유한 자원 찾기
        ResourceType surplus = ResourceType.None;
        int maxCount = 0;
        foreach (var kv in player.Resources)
        {
            int rate = gm.GetTradeRate(kv.Key);
            if (kv.Value >= rate && kv.Value > maxCount)
            {
                maxCount = kv.Value;
                surplus = kv.Key;
            }
        }

        if (surplus == ResourceType.None) return false;

        // 가장 부족한 자원 찾기
        ResourceType deficit = ResourceType.None;
        int minCount = int.MaxValue;
        foreach (var kv in player.Resources)
        {
            if (kv.Key == surplus) continue;
            if (kv.Value < minCount)
            {
                minCount = kv.Value;
                deficit = kv.Key;
            }
        }

        if (deficit == ResourceType.None) return false;

        give = surplus;
        receive = deficit;
        return true;
    }
}
