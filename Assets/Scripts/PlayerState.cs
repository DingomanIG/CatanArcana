using System.Collections.Generic;

/// <summary>
/// 플레이어 상태 - 자원/건물/발전카드/승리점 관리
/// 순수 C# 클래스 (MonoBehaviour 아님)
/// </summary>
public class PlayerState
{
    public int PlayerIndex { get; }

    /// <summary>자원 보유량</summary>
    public Dictionary<ResourceType, int> Resources { get; } = new()
    {
        { ResourceType.Wood, 0 },
        { ResourceType.Brick, 0 },
        { ResourceType.Wool, 0 },
        { ResourceType.Wheat, 0 },
        { ResourceType.Ore, 0 },
    };

    /// <summary>남은 건물 수 (카탄 기본)</summary>
    public int RoadsRemaining { get; set; } = 15;
    public int SettlementsRemaining { get; set; } = 5;
    public int CitiesRemaining { get; set; } = 4;

    /// <summary>소유 건물 참조</summary>
    public List<HexVertex> OwnedVertices { get; } = new();
    public List<HexEdge> OwnedEdges { get; } = new();

    /// <summary>발전카드 보유</summary>
    public List<DevelopmentCard> DevCards { get; } = new();

    /// <summary>사용한 기사 카드 수</summary>
    public int KnightsPlayed { get; set; }

    /// <summary>보너스 보유 여부</summary>
    public bool HasLongestRoad { get; set; }
    public bool HasLargestArmy { get; set; }

    /// <summary>이번 턴 발전카드 사용 여부</summary>
    public bool HasUsedDevCardThisTurn { get; set; }

    /// <summary>총 보유 자원 수</summary>
    public int TotalResourceCount
    {
        get
        {
            int sum = 0;
            foreach (var kv in Resources) sum += kv.Value;
            return sum;
        }
    }

    /// <summary>승리점 계산 (건물 + 발전카드 + 보너스)</summary>
    public int VictoryPoints
    {
        get
        {
            int vp = 0;
            foreach (var v in OwnedVertices)
            {
                if (v.Building == BuildingType.Settlement) vp += 1;
                else if (v.Building == BuildingType.City) vp += 2;
            }
            foreach (var card in DevCards)
            {
                if (card.Type == DevCardType.VictoryPoint) vp += 1;
            }
            if (HasLongestRoad) vp += 2;
            if (HasLargestArmy) vp += 2;
            return vp;
        }
    }

    public PlayerState(int playerIndex) => PlayerIndex = playerIndex;

    /// <summary>비용 지불 가능한지</summary>
    public bool CanAfford(Dictionary<ResourceType, int> cost)
    {
        foreach (var kv in cost)
        {
            if (!Resources.ContainsKey(kv.Key) || Resources[kv.Key] < kv.Value)
                return false;
        }
        return true;
    }

    /// <summary>비용 차감</summary>
    public void DeductCost(Dictionary<ResourceType, int> cost)
    {
        foreach (var kv in cost)
            Resources[kv.Key] -= kv.Value;
    }

    /// <summary>자원 추가</summary>
    public void AddResource(ResourceType type, int amount)
    {
        if (type == ResourceType.None || type == ResourceType.Sea) return;
        Resources[type] += amount;
    }

    /// <summary>특정 타입의 사용 가능한 발전카드 찾기</summary>
    public DevelopmentCard FindUsableCard(DevCardType type, int currentTurn)
    {
        foreach (var card in DevCards)
        {
            if (card.Type == type && card.CanUseOnTurn(currentTurn))
                return card;
        }
        return null;
    }

    /// <summary>사용 가능한 발전카드가 있는지</summary>
    public bool HasUsableDevCard(int currentTurn)
    {
        if (HasUsedDevCardThisTurn) return false;
        foreach (var card in DevCards)
        {
            if (card.Type != DevCardType.VictoryPoint && card.CanUseOnTurn(currentTurn))
                return true;
        }
        return false;
    }
}
