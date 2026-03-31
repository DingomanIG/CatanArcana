using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 헥스 그리드 매니저
/// 타일/교차점/변 생성 및 토폴로지 관리
/// </summary>
public class HexGrid
{
    public float HexSize { get; }
    public Dictionary<HexCoord, HexTile> Tiles { get; } = new();
    public List<HexVertex> Vertices { get; } = new();
    public List<HexEdge> Edges { get; } = new();

    Dictionary<long, HexVertex> vertexLookup = new();
    Dictionary<long, HexEdge> edgeLookup = new();

    public HexGrid(float hexSize = 1f)
    {
        HexSize = hexSize;
    }

    /// <summary>정육각형 보드 생성 (radius=2 → 19타일)</summary>
    public void GenerateHexagonal(int radius)
    {
        Clear();
        var coords = HexCoord.Hexagon(HexCoord.Zero, radius);
        foreach (var coord in coords)
        {
            Tiles[coord] = new HexTile(coord);
        }
        RebuildTopology();
    }

    /// <summary>임의 좌표 목록으로 보드 생성</summary>
    public void GenerateFromCoords(IEnumerable<HexCoord> coords)
    {
        Clear();
        foreach (var coord in coords)
        {
            Tiles[coord] = new HexTile(coord);
        }
        RebuildTopology();
    }

    /// <summary>직사각형 보드 생성</summary>
    public void GenerateRectangular(int width, int height)
    {
        Clear();
        for (int r = 0; r < height; r++)
        {
            int offset = r / 2;
            for (int q = -offset; q < width - offset; q++)
            {
                Tiles[new HexCoord(q, r)] = new HexTile(new HexCoord(q, r));
            }
        }
        RebuildTopology();
    }

    /// <summary>개별 타일 추가</summary>
    public HexTile AddTile(HexCoord coord)
    {
        if (Tiles.ContainsKey(coord)) return Tiles[coord];
        var tile = new HexTile(coord);
        Tiles[coord] = tile;
        RebuildTopology();
        return tile;
    }

    /// <summary>개별 타일 제거</summary>
    public bool RemoveTile(HexCoord coord)
    {
        if (!Tiles.Remove(coord)) return false;
        RebuildTopology();
        return true;
    }

    public void Clear()
    {
        Tiles.Clear();
        Vertices.Clear();
        Edges.Clear();
        vertexLookup.Clear();
        edgeLookup.Clear();
    }

    /// <summary>보드 상태만 리셋 (건물/도로/도적 제거, 토폴로지 유지)</summary>
    public void ResetBoardState()
    {
        foreach (var vertex in Vertices)
        {
            vertex.OwnerPlayerIndex = -1;
            vertex.Building = BuildingType.None;
        }
        foreach (var edge in Edges)
        {
            edge.OwnerPlayerIndex = -1;
            edge.HasRoad = false;
        }
        foreach (var tile in Tiles.Values)
        {
            tile.HasRobber = tile.Resource == ResourceType.None && tile.Resource != ResourceType.Sea;
        }
    }

    public HexTile GetTile(HexCoord coord)
    {
        return Tiles.TryGetValue(coord, out var tile) ? tile : null;
    }

    /// <summary>특정 숫자 토큰을 가진 타일 목록</summary>
    public List<HexTile> GetTilesWithNumber(int number)
    {
        var result = new List<HexTile>();
        foreach (var tile in Tiles.Values)
        {
            if (tile.NumberToken == number) result.Add(tile);
        }
        return result;
    }

    /// <summary>특정 좌표의 이웃 타일 목록 (실제 존재하는 것만)</summary>
    public List<HexTile> GetNeighborTiles(HexCoord coord)
    {
        var result = new List<HexTile>();
        foreach (var neighbor in coord.GetNeighbors())
        {
            if (Tiles.TryGetValue(neighbor, out var tile))
                result.Add(tile);
        }
        return result;
    }

    /// <summary>현재 타일 기반으로 교차점/변 토폴로지 재구축</summary>
    public void RebuildTopology()
    {
        Vertices.Clear();
        Edges.Clear();
        vertexLookup.Clear();
        edgeLookup.Clear();

        int vertexId = 0;
        int edgeId = 0;

        foreach (var tile in Tiles.Values)
        {
            tile.Vertices.Clear();
            tile.Edges.Clear();

            var corners = tile.Coord.GetCornerPositions(HexSize);

            // 각 꼭짓점 생성 또는 기존 것 재사용
            var tileVertices = new HexVertex[6];
            for (int i = 0; i < 6; i++)
            {
                var key = PositionKey(corners[i]);
                if (!vertexLookup.TryGetValue(key, out var vertex))
                {
                    vertex = new HexVertex(vertexId++, corners[i]);
                    vertexLookup[key] = vertex;
                    Vertices.Add(vertex);
                }
                tileVertices[i] = vertex;
                tile.Vertices.Add(vertex);
                if (!vertex.AdjacentTiles.Contains(tile))
                    vertex.AdjacentTiles.Add(tile);
            }

            // 각 변 생성 또는 기존 것 재사용
            for (int i = 0; i < 6; i++)
            {
                int j = (i + 1) % 6;
                var midPoint = (corners[i] + corners[j]) / 2f;
                var key = PositionKey(midPoint);

                if (!edgeLookup.TryGetValue(key, out var edge))
                {
                    edge = new HexEdge(edgeId++, midPoint, tileVertices[i], tileVertices[j]);
                    edgeLookup[key] = edge;
                    Edges.Add(edge);

                    // 교차점 ↔ 변 연결
                    tileVertices[i].AdjacentEdges.Add(edge);
                    tileVertices[j].AdjacentEdges.Add(edge);

                    // 교차점 ↔ 교차점 연결
                    if (!tileVertices[i].AdjacentVertices.Contains(tileVertices[j]))
                        tileVertices[i].AdjacentVertices.Add(tileVertices[j]);
                    if (!tileVertices[j].AdjacentVertices.Contains(tileVertices[i]))
                        tileVertices[j].AdjacentVertices.Add(tileVertices[i]);
                }

                tile.Edges.Add(edge);
                if (!edge.AdjacentTiles.Contains(tile))
                    edge.AdjacentTiles.Add(tile);
            }
        }

        Debug.Log($"[HexGrid] 토폴로지 구축 완료: 타일 {Tiles.Count}, 교차점 {Vertices.Count}, 변 {Edges.Count}");
    }

    /// <summary>위치 기반 키 생성 (0.001 정밀도, 중복 제거용)</summary>
    static long PositionKey(Vector3 pos)
    {
        int x = Mathf.RoundToInt(pos.x * 1000f);
        int z = Mathf.RoundToInt(pos.z * 1000f);
        return ((long)x << 32) | (uint)z;
    }
}
