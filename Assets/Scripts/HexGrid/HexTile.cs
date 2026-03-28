using System.Collections.Generic;

/// <summary>자원 타입</summary>
public enum ResourceType
{
    None,    // 사막
    Wood,    // 숲
    Brick,   // 언덕
    Wool,    // 초원
    Wheat,   // 밭
    Ore,     // 산
    Sea      // 바다
}

/// <summary>헥스 타일 데이터</summary>
public class HexTile
{
    public HexCoord Coord { get; }
    public ResourceType Resource { get; set; }
    public int NumberToken { get; set; }
    public bool HasRobber { get; set; }

    public List<HexVertex> Vertices { get; } = new(6);
    public List<HexEdge> Edges { get; } = new(6);

    public HexTile(HexCoord coord)
    {
        Coord = coord;
        Resource = ResourceType.None;
        NumberToken = 0;
    }

    /// <summary>이 타일이 자원을 생산하는지</summary>
    public bool ProducesResource => Resource != ResourceType.None && Resource != ResourceType.Sea && !HasRobber;

    public override string ToString() => $"Tile({Coord}, {Resource}, #{NumberToken})";
}
