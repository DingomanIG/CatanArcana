/// <summary>카드 대분류</summary>
public enum CardCategory
{
    Resource,       // 자원 카드 (왼쪽 핸드)
    Development,    // 발전 카드 (오른쪽 핸드)
    Bonus           // 보너스 카드 — 최장도로/최강기사 (오른쪽 핸드, 사용 불가)
}

/// <summary>보너스 카드 타입</summary>
public enum BonusCardType
{
    LongestRoad,    // 최장도로
    LargestArmy     // 최강기사
}

/// <summary>
/// 통합 카드 데이터. 카테고리에 따라 세부 타입 하나만 유효.
/// </summary>
public class CardData
{
    public CardCategory Category { get; }

    // Resource
    public ResourceType ResourceType { get; private set; }

    // Development
    public DevCardType DevCardType { get; private set; }

    // Bonus
    public BonusCardType BonusType { get; private set; }

    // 정렬 우선순위 (핸드 내 그룹핑용)
    public int SortOrder { get; }

    private CardData(CardCategory category, int sortOrder)
    {
        Category = category;
        SortOrder = sortOrder;
    }

    /// <summary>자원 카드 생성</summary>
    public static CardData Resource(ResourceType type)
    {
        // 정렬: Wood=0, Brick=1, Wool=2, Wheat=3, Ore=4
        int order = type switch
        {
            ResourceType.Wood  => 0,
            ResourceType.Brick => 1,
            ResourceType.Wool  => 2,
            ResourceType.Wheat => 3,
            ResourceType.Ore   => 4,
            _ => 99
        };
        return new CardData(CardCategory.Resource, order) { ResourceType = type };
    }

    /// <summary>발전 카드 생성</summary>
    public static CardData Development(DevCardType type)
    {
        // 정렬: Knight=0, RoadBuilding=1, YearOfPlenty=2, Monopoly=3, VictoryPoint=4
        int order = type switch
        {
            DevCardType.Knight       => 0,
            DevCardType.RoadBuilding => 1,
            DevCardType.YearOfPlenty => 2,
            DevCardType.Monopoly     => 3,
            DevCardType.VictoryPoint => 4,
            _ => 99
        };
        return new CardData(CardCategory.Development, order) { DevCardType = type };
    }

    /// <summary>보너스 카드 생성</summary>
    public static CardData Bonus(BonusCardType type)
    {
        int order = type == BonusCardType.LongestRoad ? 10 : 11;
        return new CardData(CardCategory.Bonus, order) { BonusType = type };
    }

    /// <summary>카드 표시 이름</summary>
    public string DisplayName => Category switch
    {
        CardCategory.Resource => ResourceType switch
        {
            ResourceType.Wood  => "목재",
            ResourceType.Brick => "벽돌",
            ResourceType.Wool  => "양모",
            ResourceType.Wheat => "밀",
            ResourceType.Ore   => "광석",
            _ => "?"
        },
        CardCategory.Development => DevCardType switch
        {
            DevCardType.Knight       => "기사",
            DevCardType.RoadBuilding => "도로건설",
            DevCardType.YearOfPlenty => "풍년",
            DevCardType.Monopoly     => "독점",
            DevCardType.VictoryPoint => "승리점",
            _ => "?"
        },
        CardCategory.Bonus => BonusType switch
        {
            BonusCardType.LongestRoad => "최장도로",
            BonusCardType.LargestArmy => "최강기사",
            _ => "?"
        },
        _ => "?"
    };

    /// <summary>사용 가능한 카드인지 (보너스는 불가)</summary>
    public bool IsPlayable => Category != CardCategory.Bonus;

    /// <summary>드래그 가능한 카드인지 (보너스는 불가)</summary>
    public bool IsDraggable => Category != CardCategory.Bonus;
}
