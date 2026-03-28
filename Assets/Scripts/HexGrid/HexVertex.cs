using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 헥스 교차점 (마을/도시 배치 위치)
/// 최대 3개 타일이 공유
/// </summary>
public class HexVertex
{
    public int Id { get; }
    public Vector3 Position { get; }

    public List<HexTile> AdjacentTiles { get; } = new(3);
    public List<HexEdge> AdjacentEdges { get; } = new(3);
    public List<HexVertex> AdjacentVertices { get; } = new(3);

    public HexVertex(int id, Vector3 position)
    {
        Id = id;
        Position = position;
    }

    public override string ToString() => $"Vertex({Id})";
}
