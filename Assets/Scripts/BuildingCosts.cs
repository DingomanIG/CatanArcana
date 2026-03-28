using System.Collections.Generic;

/// <summary>
/// 건설 비용 정적 데이터 (카탄 기본)
/// </summary>
public static class BuildingCosts
{
    public static readonly Dictionary<ResourceType, int> Road = new()
    {
        { ResourceType.Wood, 1 },
        { ResourceType.Brick, 1 },
    };

    public static readonly Dictionary<ResourceType, int> Settlement = new()
    {
        { ResourceType.Wood, 1 },
        { ResourceType.Brick, 1 },
        { ResourceType.Wool, 1 },
        { ResourceType.Wheat, 1 },
    };

    public static readonly Dictionary<ResourceType, int> City = new()
    {
        { ResourceType.Wheat, 2 },
        { ResourceType.Ore, 3 },
    };

    public static readonly Dictionary<ResourceType, int> DevelopmentCard = new()
    {
        { ResourceType.Wool, 1 },
        { ResourceType.Wheat, 1 },
        { ResourceType.Ore, 1 },
    };
}
