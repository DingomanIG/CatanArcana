/// <summary>건물 타입</summary>
public enum BuildingType
{
    None,
    Settlement,  // 마을
    City         // 도시
}

/// <summary>건설 모드</summary>
public enum BuildMode
{
    None,
    PlacingRoad,
    PlacingSettlement,
    PlacingCity
}

/// <summary>항구 타입</summary>
public enum PortType
{
    None,       // 항구 없음
    Generic,    // 3:1 일반 항구
    Wood,       // 2:1 목재
    Brick,      // 2:1 벽돌
    Wool,       // 2:1 양모
    Wheat,      // 2:1 밀
    Ore         // 2:1 광석
}
