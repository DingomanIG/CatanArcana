using System.Collections.Generic;
using UnityEngine;

/// <summary>AI 전략 유형 (6가지 핵심 전략 + None)</summary>
public enum AIStrategyType
{
    None,           // Easy: 전략 없음 (기존 랜덤)
    FullOWS,        // 광석+밀+양모 → 도시 + 발전카드 + 최대기사단
    RoadBuilder,    // 나무+벽돌 → 빠른 확장 + 최장도로
    FiveResource,   // 5자원 균형 → 독립적 플레이
    CityRoad,       // 광석+밀+나무+벽돌 → 도시 + 최장도로
    Port,           // 대량 생산 + 2:1 항구 활용
    HybridOWS       // OWS 코어 + 소량 나무/벽돌 (최강 전략)
}

/// <summary>AI 액션 타입 (DoActionPhase 순서 결정용)</summary>
public enum AIActionType
{
    UseDevCard,
    BuildCity,
    BuildSettlement,
    PlayerTrade,
    BankTrade,
    BuyDevCard,
    BuildRoad
}

/// <summary>
/// 전략 프로필 - 전략별 가중치와 우선순위 (불변 데이터)
/// </summary>
public class AIStrategyProfile
{
    public AIStrategyType Type { get; }

    /// <summary>자원별 핍 곱셈 계수 (기본 1.0)</summary>
    public Dictionary<ResourceType, float> ResourceWeights { get; }

    /// <summary>항구 타입별 추가 점수</summary>
    public Dictionary<PortType, float> PortWeights { get; }

    /// <summary>자원 다양성 보너스 배율 (기본 1.0 = 기존 1.5점 기준)</summary>
    public float DiversityMultiplier { get; }

    /// <summary>도시 우선도 (0~2)</summary>
    public float CityPriority { get; }
    /// <summary>발전카드 구매 우선도 (0~2)</summary>
    public float DevCardPriority { get; }
    /// <summary>도로 우선도 (0~2)</summary>
    public float RoadPriority { get; }
    /// <summary>마을 우선도 (0~2)</summary>
    public float SettlementPriority { get; }

    /// <summary>최장도로 추구</summary>
    public bool PursuesLongestRoad { get; }
    /// <summary>최대기사단 추구</summary>
    public bool PursuesLargestArmy { get; }

    /// <summary>해안 확장 선호도 (0~1)</summary>
    public float CoastalPreference { get; }
    /// <summary>거래 적극성 (높을수록 적극, 기본 1.0)</summary>
    public float TradeAggression { get; }

    /// <summary>액션 실행 순서</summary>
    public AIActionType[] ActionOrder { get; }

    public AIStrategyProfile(
        AIStrategyType type,
        Dictionary<ResourceType, float> resourceWeights,
        Dictionary<PortType, float> portWeights,
        float diversityMultiplier,
        float cityPriority, float devCardPriority,
        float roadPriority, float settlementPriority,
        bool pursuesLongestRoad, bool pursuesLargestArmy,
        float coastalPreference, float tradeAggression,
        AIActionType[] actionOrder)
    {
        Type = type;
        ResourceWeights = resourceWeights;
        PortWeights = portWeights;
        DiversityMultiplier = diversityMultiplier;
        CityPriority = cityPriority;
        DevCardPriority = devCardPriority;
        RoadPriority = roadPriority;
        SettlementPriority = settlementPriority;
        PursuesLongestRoad = pursuesLongestRoad;
        PursuesLargestArmy = pursuesLargestArmy;
        CoastalPreference = coastalPreference;
        TradeAggression = tradeAggression;
        ActionOrder = actionOrder;
    }
}

/// <summary>
/// AI 전략 선택 - 보드 평가 후 최적 전략 결정
/// </summary>
public static class AIStrategySelector
{
    // ========================
    // 기본 액션 순서 (전략 없을 때)
    // ========================

    static readonly AIActionType[] DefaultActionOrder =
    {
        AIActionType.UseDevCard, AIActionType.BuildCity, AIActionType.BuildSettlement,
        AIActionType.PlayerTrade, AIActionType.BankTrade, AIActionType.BuyDevCard,
        AIActionType.BuildRoad
    };

    // ========================
    // 6개 전략 프로필
    // ========================

    static readonly AIStrategyProfile ProfileFullOWS = new(
        AIStrategyType.FullOWS,
        new Dictionary<ResourceType, float>
            { {ResourceType.Ore,2f}, {ResourceType.Wheat,2f}, {ResourceType.Wool,1.5f}, {ResourceType.Wood,0.3f}, {ResourceType.Brick,0.3f} },
        new Dictionary<PortType, float>
            { {PortType.None,0f}, {PortType.Generic,0.5f}, {PortType.Ore,2.5f}, {PortType.Wheat,2.5f}, {PortType.Wool,2f}, {PortType.Wood,0.5f}, {PortType.Brick,0.5f} },
        diversityMultiplier: 0.5f,
        cityPriority: 2f, devCardPriority: 1.8f,
        roadPriority: 0.3f, settlementPriority: 0.5f,
        pursuesLongestRoad: false, pursuesLargestArmy: true,
        coastalPreference: 0.4f, tradeAggression: 1.2f,
        new[] { AIActionType.UseDevCard, AIActionType.BuildCity, AIActionType.BankTrade,
                AIActionType.PlayerTrade, AIActionType.BuyDevCard, AIActionType.BuildSettlement,
                AIActionType.BuildRoad }
    );

    static readonly AIStrategyProfile ProfileRoadBuilder = new(
        AIStrategyType.RoadBuilder,
        new Dictionary<ResourceType, float>
            { {ResourceType.Ore,0.5f}, {ResourceType.Wheat,0.8f}, {ResourceType.Wool,0.8f}, {ResourceType.Wood,2f}, {ResourceType.Brick,2f} },
        new Dictionary<PortType, float>
            { {PortType.None,0f}, {PortType.Generic,1f}, {PortType.Wood,2.5f}, {PortType.Brick,2.5f}, {PortType.Ore,0.5f}, {PortType.Wheat,0.5f}, {PortType.Wool,0.5f} },
        diversityMultiplier: 0.8f,
        cityPriority: 0.5f, devCardPriority: 0.3f,
        roadPriority: 2f, settlementPriority: 1.8f,
        pursuesLongestRoad: true, pursuesLargestArmy: false,
        coastalPreference: 0.3f, tradeAggression: 1f,
        new[] { AIActionType.UseDevCard, AIActionType.BuildSettlement, AIActionType.BuildRoad,
                AIActionType.PlayerTrade, AIActionType.BankTrade, AIActionType.BuildCity,
                AIActionType.BuyDevCard }
    );

    static readonly AIStrategyProfile ProfileFiveResource = new(
        AIStrategyType.FiveResource,
        new Dictionary<ResourceType, float>
            { {ResourceType.Ore,1f}, {ResourceType.Wheat,1f}, {ResourceType.Wool,1f}, {ResourceType.Wood,1f}, {ResourceType.Brick,1f} },
        new Dictionary<PortType, float>
            { {PortType.None,0f}, {PortType.Generic,1.5f}, {PortType.Wood,1f}, {PortType.Brick,1f}, {PortType.Ore,1f}, {PortType.Wheat,1f}, {PortType.Wool,1f} },
        diversityMultiplier: 2f,
        cityPriority: 1.2f, devCardPriority: 0.8f,
        roadPriority: 1f, settlementPriority: 1.5f,
        pursuesLongestRoad: false, pursuesLargestArmy: false,
        coastalPreference: 0.5f, tradeAggression: 0.8f,
        new[] { AIActionType.UseDevCard, AIActionType.BuildSettlement, AIActionType.BuildCity,
                AIActionType.PlayerTrade, AIActionType.BankTrade, AIActionType.BuildRoad,
                AIActionType.BuyDevCard }
    );

    static readonly AIStrategyProfile ProfileCityRoad = new(
        AIStrategyType.CityRoad,
        new Dictionary<ResourceType, float>
            { {ResourceType.Ore,1.8f}, {ResourceType.Wheat,1.8f}, {ResourceType.Wool,0.3f}, {ResourceType.Wood,1.5f}, {ResourceType.Brick,1.5f} },
        new Dictionary<PortType, float>
            { {PortType.None,0f}, {PortType.Generic,1f}, {PortType.Ore,2f}, {PortType.Wheat,2f}, {PortType.Wood,1.5f}, {PortType.Brick,1.5f}, {PortType.Wool,0.5f} },
        diversityMultiplier: 0.8f,
        cityPriority: 1.8f, devCardPriority: 0.5f,
        roadPriority: 1.5f, settlementPriority: 1f,
        pursuesLongestRoad: true, pursuesLargestArmy: false,
        coastalPreference: 0.4f, tradeAggression: 1f,
        new[] { AIActionType.UseDevCard, AIActionType.BuildCity, AIActionType.BuildRoad,
                AIActionType.PlayerTrade, AIActionType.BankTrade, AIActionType.BuildSettlement,
                AIActionType.BuyDevCard }
    );

    static readonly AIStrategyProfile ProfilePort = new(
        AIStrategyType.Port,
        new Dictionary<ResourceType, float>
            { {ResourceType.Ore,1f}, {ResourceType.Wheat,1f}, {ResourceType.Wool,1f}, {ResourceType.Wood,1f}, {ResourceType.Brick,1f} },
        new Dictionary<PortType, float>
            { {PortType.None,0f}, {PortType.Generic,2f}, {PortType.Wood,3f}, {PortType.Brick,3f}, {PortType.Ore,3f}, {PortType.Wheat,3f}, {PortType.Wool,3f} },
        diversityMultiplier: 0.5f,
        cityPriority: 1.2f, devCardPriority: 0.8f,
        roadPriority: 1.2f, settlementPriority: 1.2f,
        pursuesLongestRoad: false, pursuesLargestArmy: false,
        coastalPreference: 0.7f, tradeAggression: 1.5f,
        new[] { AIActionType.UseDevCard, AIActionType.BankTrade, AIActionType.BuildSettlement,
                AIActionType.BuildCity, AIActionType.PlayerTrade, AIActionType.BuildRoad,
                AIActionType.BuyDevCard }
    );

    static readonly AIStrategyProfile ProfileHybridOWS = new(
        AIStrategyType.HybridOWS,
        new Dictionary<ResourceType, float>
            { {ResourceType.Ore,1.8f}, {ResourceType.Wheat,1.8f}, {ResourceType.Wool,1.3f}, {ResourceType.Wood,0.7f}, {ResourceType.Brick,0.7f} },
        new Dictionary<PortType, float>
            { {PortType.None,0f}, {PortType.Generic,1f}, {PortType.Ore,2f}, {PortType.Wheat,2f}, {PortType.Wool,1.5f}, {PortType.Wood,0.8f}, {PortType.Brick,0.8f} },
        diversityMultiplier: 0.8f,
        cityPriority: 1.8f, devCardPriority: 1.5f,
        roadPriority: 0.8f, settlementPriority: 1f,
        pursuesLongestRoad: false, pursuesLargestArmy: true,
        coastalPreference: 0.5f, tradeAggression: 1.1f,
        new[] { AIActionType.UseDevCard, AIActionType.BuildCity, AIActionType.BankTrade,
                AIActionType.PlayerTrade, AIActionType.BuildSettlement, AIActionType.BuyDevCard,
                AIActionType.BuildRoad }
    );

    static readonly Dictionary<AIStrategyType, AIStrategyProfile> Profiles = new()
    {
        { AIStrategyType.FullOWS,      ProfileFullOWS },
        { AIStrategyType.RoadBuilder,  ProfileRoadBuilder },
        { AIStrategyType.FiveResource, ProfileFiveResource },
        { AIStrategyType.CityRoad,     ProfileCityRoad },
        { AIStrategyType.Port,         ProfilePort },
        { AIStrategyType.HybridOWS,    ProfileHybridOWS }
    };

    // Lv3~5 중급용 전략 후보 (간단한 3개)
    static readonly AIStrategyType[] BasicCandidates =
        { AIStrategyType.FiveResource, AIStrategyType.HybridOWS, AIStrategyType.RoadBuilder };

    // Lv6+ 고급용 전략 후보 (전체 6개)
    static readonly AIStrategyType[] FullCandidates =
        { AIStrategyType.FullOWS, AIStrategyType.RoadBuilder, AIStrategyType.FiveResource,
          AIStrategyType.CityRoad, AIStrategyType.Port, AIStrategyType.HybridOWS };

    // ========================
    // PUBLIC API
    // ========================

    /// <summary>전략 프로필 조회 (None이면 null)</summary>
    public static AIStrategyProfile GetProfile(AIStrategyType type)
    {
        return Profiles.TryGetValue(type, out var p) ? p : null;
    }

    /// <summary>액션 순서 조회</summary>
    public static AIActionType[] GetActionOrder(AIStrategyProfile strategy)
    {
        return strategy?.ActionOrder ?? DefaultActionOrder;
    }

    /// <summary>
    /// 보드 평가 후 최적 전략 선택
    /// 초기 배치 전 호출. alreadyChosen = 다른 AI가 이미 고른 전략 (중복 방지)
    /// </summary>
    public static AIStrategyType SelectStrategy(HexGrid grid, int playerIndex,
        AIDifficulty difficulty, PlayerState[] allPlayers,
        AIStrategyType[] alreadyChosen = null)
    {
        if (!AIDifficultySettings.UsesStrategy(difficulty)) return AIStrategyType.None;

        var candidates = AIDifficultySettings.UsesFullStrategy(difficulty) ? FullCandidates : BasicCandidates;
        float noiseRange = AIDifficultySettings.StrategyNoise(difficulty);

        // 이미 선택된 전략 카운트 (중복 페널티용)
        var chosenCount = new Dictionary<AIStrategyType, int>();
        if (alreadyChosen != null)
        {
            foreach (var s in alreadyChosen)
            {
                if (s != AIStrategyType.None)
                {
                    chosenCount.TryGetValue(s, out int c);
                    chosenCount[s] = c + 1;
                }
            }
        }

        // 보드 자원별 가용 핍 계산
        var availablePips = CalculateAvailablePips(grid);
        bool hasPort = HasAccessiblePorts(grid);

        AIStrategyType best = candidates[0];
        float bestScore = -999f;

        foreach (var type in candidates)
        {
            float score = ScoreStrategyFit(type, availablePips, grid, hasPort);
            score *= 1f + Random.Range(-noiseRange, noiseRange);

            // 중복 전략 페널티: 이미 고른 AI 수만큼 50% 감점
            if (chosenCount.TryGetValue(type, out int dupCount))
                score *= 1f / (1f + dupCount * 0.5f);

            if (score > bestScore)
            {
                bestScore = score;
                best = type;
            }
        }

        return best;
    }

    /// <summary>
    /// 초기 배치 완료 후 확보한 자원 기반으로 전략 선택
    /// 실제 보유 타일의 핍을 분석하여 최적 전략 결정
    /// </summary>
    public static AIStrategyType SelectStrategyFromOwnedTiles(HexGrid grid, int playerIndex,
        AIDifficulty difficulty, PlayerState[] allPlayers,
        AIStrategyType[] alreadyChosen = null)
    {
        if (!AIDifficultySettings.UsesStrategy(difficulty)) return AIStrategyType.None;

        var candidates = AIDifficultySettings.UsesFullStrategy(difficulty) ? FullCandidates : BasicCandidates;
        float noiseRange = AIDifficultySettings.StrategyNoise(difficulty);

        // 이미 선택된 전략 카운트 (중복 페널티용)
        var chosenCount = new Dictionary<AIStrategyType, int>();
        if (alreadyChosen != null)
        {
            foreach (var s in alreadyChosen)
            {
                if (s != AIStrategyType.None)
                {
                    chosenCount.TryGetValue(s, out int c);
                    chosenCount[s] = c + 1;
                }
            }
        }

        // 실제 확보한 타일의 핍 계산 (보드 전체가 아닌 내 건물 인접 타일)
        var ownedPips = CalculateOwnedPips(allPlayers[playerIndex]);
        bool ownsPort = PlayerOwnsPort(allPlayers[playerIndex]);

        AIStrategyType best = candidates[0];
        float bestScore = -999f;

        foreach (var type in candidates)
        {
            float score = ScoreStrategyFit(type, ownedPips, grid, ownsPort);
            score *= 1f + Random.Range(-noiseRange, noiseRange);

            // 중복 전략 페널티
            if (chosenCount.TryGetValue(type, out int dupCount))
                score *= 1f / (1f + dupCount * 0.5f);

            if (score > bestScore)
            {
                bestScore = score;
                best = type;
            }
        }

        return best;
    }

    /// <summary>플레이어가 확보한 건물 인접 타일의 핍 합산</summary>
    static Dictionary<ResourceType, float> CalculateOwnedPips(PlayerState player)
    {
        var pips = new Dictionary<ResourceType, float>
        {
            { ResourceType.Wood, 0 }, { ResourceType.Brick, 0 },
            { ResourceType.Wool, 0 }, { ResourceType.Wheat, 0 },
            { ResourceType.Ore, 0 }
        };

        var countedTiles = new HashSet<int>(); // 타일 중복 방지

        foreach (var vertex in player.OwnedVertices)
        {
            float multiplier = vertex.Building == BuildingType.City ? 2f : 1f;
            foreach (var tile in vertex.AdjacentTiles)
            {
                if (!tile.ProducesResource) continue;
                int tilePips = AIBoardEvaluator.GetPips(tile.NumberToken);
                if (tilePips <= 0) continue;

                // 타일이 여러 건물에 인접할 수 있으므로, 건물 수 반영
                if (pips.ContainsKey(tile.Resource))
                    pips[tile.Resource] += tilePips * multiplier;
            }
        }

        return pips;
    }

    /// <summary>플레이어가 항구를 보유하고 있는지</summary>
    static bool PlayerOwnsPort(PlayerState player)
    {
        foreach (var vertex in player.OwnedVertices)
        {
            if (vertex.Port != PortType.None) return true;
        }
        return false;
    }

    // ========================
    // 전략 적합도 계산
    // ========================

    /// <summary>전체 보드에서 자원별 가용 핍 합산 (점유되지 않은 교차점 기준)</summary>
    static Dictionary<ResourceType, float> CalculateAvailablePips(HexGrid grid)
    {
        var pips = new Dictionary<ResourceType, float>
        {
            { ResourceType.Wood, 0 }, { ResourceType.Brick, 0 },
            { ResourceType.Wool, 0 }, { ResourceType.Wheat, 0 },
            { ResourceType.Ore, 0 }
        };

        foreach (var tile in grid.Tiles.Values)
        {
            if (!tile.ProducesResource) continue;
            int tilePips = AIBoardEvaluator.GetPips(tile.NumberToken);
            if (tilePips <= 0) continue;

            // 열린 교차점이 있는 타일만 카운트
            bool hasOpenVertex = false;
            foreach (var v in tile.Vertices)
            {
                if (v.Building == BuildingType.None && v.OwnerPlayerIndex < 0)
                {
                    hasOpenVertex = true;
                    break;
                }
            }
            if (hasOpenVertex && pips.ContainsKey(tile.Resource))
                pips[tile.Resource] += tilePips;
        }

        return pips;
    }

    /// <summary>해안 항구에 접근 가능한 빈 교차점이 있는지</summary>
    static bool HasAccessiblePorts(HexGrid grid)
    {
        foreach (var v in grid.Vertices)
        {
            if (v.Port != PortType.None && v.Building == BuildingType.None && v.OwnerPlayerIndex < 0)
                return true;
        }
        return false;
    }

    /// <summary>특정 PortType의 2:1 항구에 빈 교차점이 있는지</summary>
    static bool HasAccessibleSpecificPort(HexGrid grid, PortType portType)
    {
        foreach (var v in grid.Vertices)
        {
            if (v.Port == portType && v.Building == BuildingType.None && v.OwnerPlayerIndex < 0)
                return true;
        }
        return false;
    }

    /// <summary>전략별 적합도 점수 (보드 전체 핍 OR 확보 핍 모두 호환)</summary>
    static float ScoreStrategyFit(AIStrategyType type, Dictionary<ResourceType, float> pips,
        HexGrid grid, bool hasPort)
    {
        var profile = GetProfile(type);
        if (profile == null) return 0f;

        float score = 0f;

        // 총 핍 규모 파악 (보드 전체 vs 확보 타일 판별)
        float totalPips = 0f;
        foreach (var kv in pips) totalPips += kv.Value;
        // owned 핍은 보통 10~25, 보드 전체는 50~80
        bool isOwnedContext = totalPips < 35f;

        // 1. 핵심 자원 가용량 × 전략 가중치
        foreach (var kv in profile.ResourceWeights)
        {
            if (pips.TryGetValue(kv.Key, out float available))
                score += available * kv.Value;
        }

        // 2. 전략별 특수 조건 (owned 핍 기준일 때 임계값 낮춤)
        switch (type)
        {
            case AIStrategyType.HybridOWS:
                float oreGate = isOwnedContext ? 2f : 4f;
                float wheatGate = isOwnedContext ? 2f : 4f;
                float woolGate = isOwnedContext ? 1f : 2f;
                if (pips[ResourceType.Ore] < oreGate || pips[ResourceType.Wheat] < wheatGate ||
                    pips[ResourceType.Wool] < woolGate)
                    return 0f;
                score += 5f;
                break;

            case AIStrategyType.FullOWS:
                float owsThreshold = isOwnedContext ? 4f : 8f;
                if (pips[ResourceType.Ore] + pips[ResourceType.Wheat] < owsThreshold)
                    score *= 0.5f;
                break;

            case AIStrategyType.RoadBuilder:
                float roadThreshold = isOwnedContext ? 4f : 8f;
                if (pips[ResourceType.Wood] + pips[ResourceType.Brick] < roadThreshold)
                    score *= 0.5f;
                break;

            case AIStrategyType.CityRoad:
                float cityRoadMin = isOwnedContext ? 1.5f : 3f;
                if (pips[ResourceType.Ore] < cityRoadMin || pips[ResourceType.Wheat] < cityRoadMin ||
                    pips[ResourceType.Wood] < cityRoadMin || pips[ResourceType.Brick] < cityRoadMin)
                    score *= 0.6f;
                break;

            case AIStrategyType.Port:
                if (!hasPort) return 0f;
                float portResourceThreshold = isOwnedContext ? 4f : 8f;
                foreach (var kv in pips)
                {
                    PortType pt = ResourceToPort(kv.Key);
                    if (kv.Value >= portResourceThreshold && HasAccessibleSpecificPort(grid, pt))
                        score += 10f;
                }
                break;

            case AIStrategyType.FiveResource:
                float minPips = float.MaxValue;
                foreach (var kv in pips)
                {
                    if (kv.Value < minPips) minPips = kv.Value;
                }
                score += minPips * 2f;
                break;
        }

        return score;
    }

    // ========================
    // 유틸리티
    // ========================

    /// <summary>ResourceType → PortType 변환</summary>
    public static PortType ResourceToPort(ResourceType res)
    {
        return res switch
        {
            ResourceType.Wood  => PortType.Wood,
            ResourceType.Brick => PortType.Brick,
            ResourceType.Wool  => PortType.Wool,
            ResourceType.Wheat => PortType.Wheat,
            ResourceType.Ore   => PortType.Ore,
            _ => PortType.None
        };
    }

    /// <summary>PortType → ResourceType 변환</summary>
    public static ResourceType PortToResource(PortType port)
    {
        return port switch
        {
            PortType.Wood  => ResourceType.Wood,
            PortType.Brick => ResourceType.Brick,
            PortType.Wool  => ResourceType.Wool,
            PortType.Wheat => ResourceType.Wheat,
            PortType.Ore   => ResourceType.Ore,
            _ => ResourceType.None
        };
    }

    /// <summary>전략의 우선 빌드 목표 비용</summary>
    public static Dictionary<ResourceType, int> GetPrimaryBuildGoal(AIStrategyProfile strategy)
    {
        if (strategy == null) return BuildingCosts.Settlement;

        // 도시 우선도가 가장 높으면 도시, 도로면 도로, 아니면 마을
        if (strategy.CityPriority >= strategy.SettlementPriority &&
            strategy.CityPriority >= strategy.RoadPriority)
            return BuildingCosts.City;
        if (strategy.RoadPriority >= strategy.SettlementPriority)
            return BuildingCosts.Road;
        return BuildingCosts.Settlement;
    }
}
