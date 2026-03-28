using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 건설 시스템 - 배치 규칙 검증 및 상태 변경
/// 순수 C# 클래스 (HexGrid 데이터 기반)
/// </summary>
public class BuildingSystem
{
    readonly HexGrid grid;

    public BuildingSystem(HexGrid grid)
    {
        this.grid = grid;
    }

    // ========================
    // 마을 (Settlement)
    // ========================

    /// <summary>마을 배치 가능 여부</summary>
    public bool CanPlaceSettlement(int vertexId, int playerIndex, bool isInitialPlacement = false)
    {
        var vertex = FindVertex(vertexId);
        if (vertex == null) return false;

        // 이미 점유됨
        if (vertex.OwnerPlayerIndex != -1) return false;

        // 거리 규칙: 인접 교차점에 건물 없어야 함
        foreach (var adj in vertex.AdjacentVertices)
        {
            if (adj.Building != BuildingType.None) return false;
        }

        // 바다 전용 교차점 제외 (인접 타일이 전부 바다)
        if (IsSeaOnlyVertex(vertex)) return false;

        // 초기 배치가 아니면 자기 도로 연결 필요
        if (!isInitialPlacement)
        {
            bool hasConnectedRoad = false;
            foreach (var edge in vertex.AdjacentEdges)
            {
                if (edge.HasRoad && edge.OwnerPlayerIndex == playerIndex)
                {
                    hasConnectedRoad = true;
                    break;
                }
            }
            if (!hasConnectedRoad) return false;
        }

        return true;
    }

    /// <summary>마을 배치</summary>
    public void PlaceSettlement(int vertexId, int playerIndex)
    {
        var vertex = FindVertex(vertexId);
        if (vertex == null) return;

        vertex.OwnerPlayerIndex = playerIndex;
        vertex.Building = BuildingType.Settlement;
        Debug.Log($"[Building] 플레이어 {playerIndex} 마을 배치: Vertex {vertexId}");
    }

    /// <summary>유효한 마을 배치 위치 목록</summary>
    public List<HexVertex> GetValidSettlementPositions(int playerIndex, bool isInitialPlacement = false)
    {
        var valid = new List<HexVertex>();
        foreach (var vertex in grid.Vertices)
        {
            if (CanPlaceSettlement(vertex.Id, playerIndex, isInitialPlacement))
                valid.Add(vertex);
        }
        return valid;
    }

    // ========================
    // 도로 (Road)
    // ========================

    /// <summary>도로 배치 가능 여부</summary>
    public bool CanPlaceRoad(int edgeId, int playerIndex, bool isInitialPlacement = false)
    {
        var edge = FindEdge(edgeId);
        if (edge == null) return false;

        // 이미 도로 있음
        if (edge.HasRoad) return false;

        // 바다 전용 변 제외
        if (IsSeaOnlyEdge(edge)) return false;

        // 연결 규칙: 자기 건물 또는 도로에 연결되어야 함
        bool connected = false;

        // VertexA 또는 VertexB에 자기 건물이 있는지
        if (edge.VertexA.OwnerPlayerIndex == playerIndex || edge.VertexB.OwnerPlayerIndex == playerIndex)
        {
            connected = true;
        }

        // 또는 인접 변에 자기 도로가 있는지
        if (!connected)
        {
            connected = HasAdjacentPlayerRoad(edge.VertexA, edgeId, playerIndex) ||
                        HasAdjacentPlayerRoad(edge.VertexB, edgeId, playerIndex);
        }

        // 초기 배치: 방금 놓은 마을에 연결만 되면 OK (연결 규칙 완화)
        if (isInitialPlacement)
        {
            connected = edge.VertexA.OwnerPlayerIndex == playerIndex ||
                        edge.VertexB.OwnerPlayerIndex == playerIndex;
        }

        return connected;
    }

    /// <summary>도로 배치</summary>
    public void PlaceRoad(int edgeId, int playerIndex)
    {
        var edge = FindEdge(edgeId);
        if (edge == null) return;

        edge.HasRoad = true;
        edge.OwnerPlayerIndex = playerIndex;
        Debug.Log($"[Building] 플레이어 {playerIndex} 도로 배치: Edge {edgeId}");
    }

    /// <summary>유효한 도로 배치 위치 목록</summary>
    public List<HexEdge> GetValidRoadPositions(int playerIndex, bool isInitialPlacement = false)
    {
        var valid = new List<HexEdge>();
        foreach (var edge in grid.Edges)
        {
            if (CanPlaceRoad(edge.Id, playerIndex, isInitialPlacement))
                valid.Add(edge);
        }
        return valid;
    }

    // ========================
    // 도시 (City)
    // ========================

    /// <summary>도시 업그레이드 가능 여부</summary>
    public bool CanUpgradeToCity(int vertexId, int playerIndex)
    {
        var vertex = FindVertex(vertexId);
        if (vertex == null) return false;

        return vertex.OwnerPlayerIndex == playerIndex && vertex.Building == BuildingType.Settlement;
    }

    /// <summary>도시 업그레이드</summary>
    public void UpgradeToCity(int vertexId, int playerIndex)
    {
        var vertex = FindVertex(vertexId);
        if (vertex == null) return;

        vertex.Building = BuildingType.City;
        Debug.Log($"[Building] 플레이어 {playerIndex} 도시 업그레이드: Vertex {vertexId}");
    }

    /// <summary>유효한 도시 업그레이드 위치 목록</summary>
    public List<HexVertex> GetValidCityUpgrades(int playerIndex)
    {
        var valid = new List<HexVertex>();
        foreach (var vertex in grid.Vertices)
        {
            if (CanUpgradeToCity(vertex.Id, playerIndex))
                valid.Add(vertex);
        }
        return valid;
    }

    // ========================
    // Helpers
    // ========================

    HexVertex FindVertex(int id)
    {
        if (id >= 0 && id < grid.Vertices.Count)
            return grid.Vertices[id];
        return null;
    }

    HexEdge FindEdge(int id)
    {
        if (id >= 0 && id < grid.Edges.Count)
            return grid.Edges[id];
        return null;
    }

    /// <summary>교차점이 바다 타일만 인접하는지</summary>
    bool IsSeaOnlyVertex(HexVertex vertex)
    {
        if (vertex.AdjacentTiles.Count == 0) return true;
        foreach (var tile in vertex.AdjacentTiles)
        {
            if (tile.Resource != ResourceType.Sea) return false;
        }
        return true;
    }

    /// <summary>변이 바다 타일만 인접하는지</summary>
    bool IsSeaOnlyEdge(HexEdge edge)
    {
        if (edge.AdjacentTiles.Count == 0) return true;
        foreach (var tile in edge.AdjacentTiles)
        {
            if (tile.Resource != ResourceType.Sea) return false;
        }
        return true;
    }

    /// <summary>특정 교차점에서 특정 변을 제외하고 해당 플레이어의 도로가 연결되어 있는지</summary>
    bool HasAdjacentPlayerRoad(HexVertex vertex, int excludeEdgeId, int playerIndex)
    {
        foreach (var edge in vertex.AdjacentEdges)
        {
            if (edge.Id == excludeEdgeId) continue;
            if (edge.HasRoad && edge.OwnerPlayerIndex == playerIndex)
                return true;
        }
        return false;
    }
}
