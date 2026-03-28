using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 헥스 변 (도로 배치 위치)
/// 최대 2개 타일이 공유, 2개 교차점을 연결
/// </summary>
public class HexEdge
{
    public int Id { get; }
    public Vector3 MidPoint { get; }
    public HexVertex VertexA { get; }
    public HexVertex VertexB { get; }

    public List<HexTile> AdjacentTiles { get; } = new(2);

    /// <summary>소유 플레이어 (-1 = 비어있음)</summary>
    public int OwnerPlayerIndex { get; set; } = -1;

    /// <summary>도로 건설 여부</summary>
    public bool HasRoad { get; set; }

    public HexEdge(int id, Vector3 midPoint, HexVertex vertexA, HexVertex vertexB)
    {
        Id = id;
        MidPoint = midPoint;
        VertexA = vertexA;
        VertexB = vertexB;
    }

    /// <summary>변의 길이</summary>
    public float Length => Vector3.Distance(VertexA.Position, VertexB.Position);

    public override string ToString() => $"Edge({Id}: V{VertexA.Id}-V{VertexB.Id})";
}
