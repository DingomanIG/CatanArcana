using System.Collections.Generic;

/// <summary>
/// 최장교역로 계산기 - Edge 기반 DFS
/// </summary>
public static class LongestRoadCalculator
{
    /// <summary>플레이어의 최장 연결 도로 길이 계산</summary>
    public static int Calculate(int playerIndex, HexGrid grid)
    {
        var playerEdges = new HashSet<int>();
        foreach (var edge in grid.Edges)
        {
            if (edge.HasRoad && edge.OwnerPlayerIndex == playerIndex)
                playerEdges.Add(edge.Id);
        }

        if (playerEdges.Count == 0) return 0;

        int maxLength = 0;
        var visited = new HashSet<int>();

        foreach (int edgeId in playerEdges)
        {
            visited.Clear();
            visited.Add(edgeId);
            var edge = grid.Edges[edgeId];

            int extA = Extend(edge.VertexA, edgeId, playerIndex, grid, playerEdges, visited);
            int extB = Extend(edge.VertexB, edgeId, playerIndex, grid, playerEdges, visited);
            int length = 1 + extA + extB;

            if (length > maxLength) maxLength = length;
            visited.Remove(edgeId);
        }

        return maxLength;
    }

    /// <summary>교차점에서 연결 도로로 확장. 적 건물에서 끊김</summary>
    static int Extend(HexVertex vertex, int fromEdgeId, int playerIndex,
                      HexGrid grid, HashSet<int> playerEdges, HashSet<int> visited)
    {
        // 적 건물이 있으면 도로 끊김
        if (vertex.OwnerPlayerIndex != -1 && vertex.OwnerPlayerIndex != playerIndex)
            return 0;

        int maxExt = 0;

        foreach (var adjEdge in vertex.AdjacentEdges)
        {
            if (adjEdge.Id == fromEdgeId) continue;
            if (visited.Contains(adjEdge.Id)) continue;
            if (!playerEdges.Contains(adjEdge.Id)) continue;

            visited.Add(adjEdge.Id);
            var otherVertex = adjEdge.VertexA == vertex ? adjEdge.VertexB : adjEdge.VertexA;
            int ext = 1 + Extend(otherVertex, adjEdge.Id, playerIndex, grid, playerEdges, visited);
            if (ext > maxExt) maxExt = ext;
            visited.Remove(adjEdge.Id);
        }

        return maxExt;
    }
}
