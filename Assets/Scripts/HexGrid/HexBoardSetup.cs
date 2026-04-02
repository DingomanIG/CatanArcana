using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 카탄 보드 초기 설정 (자원 배치, 숫자 토큰 할당)
/// </summary>
public static class HexBoardSetup
{
    // 표준 카탄 자원 배분 (19타일)
    static readonly ResourceType[] STANDARD_RESOURCES = new[]
    {
        ResourceType.None, // 사막 1개
        ResourceType.Wood, ResourceType.Wood, ResourceType.Wood, ResourceType.Wood,       // 숲 4개
        ResourceType.Brick, ResourceType.Brick, ResourceType.Brick,                       // 언덕 3개
        ResourceType.Wool, ResourceType.Wool, ResourceType.Wool, ResourceType.Wool,       // 초원 4개
        ResourceType.Wheat, ResourceType.Wheat, ResourceType.Wheat, ResourceType.Wheat,   // 밭 4개
        ResourceType.Ore, ResourceType.Ore, ResourceType.Ore,                             // 산 3개
    };

    // 표준 숫자 토큰 (사막 제외 18개, 나선형 배치)
    static readonly int[] STANDARD_NUMBERS = new[]
    {
        5, 2, 6, 3, 8, 10, 9, 12, 11, 4, 8, 10, 9, 4, 5, 6, 3, 11
    };

    /// <summary>표준 카탄 보드 설정 (19타일)</summary>
    public static void SetupStandardBoard(HexGrid grid, System.Random random = null)
    {
        random ??= new System.Random();

        var tiles = grid.Tiles.Values.ToList();
        if (tiles.Count != 19)
        {
            UnityEngine.Debug.LogWarning($"[HexBoardSetup] 표준 보드는 19타일 필요 (현재: {tiles.Count})");
            return;
        }

        // 자원 셔플
        var resources = STANDARD_RESOURCES.ToArray();
        Shuffle(resources, random);

        // 자원 할당
        for (int i = 0; i < tiles.Count; i++)
        {
            tiles[i].Resource = resources[i];
        }

        // 숫자 토큰 할당 (사막 건너뛰기)
        int numberIndex = 0;
        foreach (var tile in tiles)
        {
            if (tile.Resource == ResourceType.None)
            {
                tile.HasRobber = true;
                tile.NumberToken = 0;
            }
            else if (numberIndex < STANDARD_NUMBERS.Length)
            {
                tile.NumberToken = STANDARD_NUMBERS[numberIndex++];
            }
        }

        // 항구 배치는 바다 타일 추가 후 별도 호출 (SetupPorts)
        // RebuildTopology가 vertex를 새로 생성하므로 포트 할당이 소멸됨
    }

    /// <summary>커스텀 자원/숫자 배분으로 보드 설정</summary>
    public static void SetupCustomBoard(
        HexGrid grid,
        ResourceType[] resources,
        int[] numbers,
        System.Random random = null)
    {
        random ??= new System.Random();
        var tiles = grid.Tiles.Values.ToList();

        if (resources.Length != tiles.Count)
        {
            UnityEngine.Debug.LogWarning($"[HexBoardSetup] 자원 수({resources.Length})와 타일 수({tiles.Count}) 불일치");
            return;
        }

        var shuffled = resources.ToArray();
        Shuffle(shuffled, random);

        for (int i = 0; i < tiles.Count; i++)
        {
            tiles[i].Resource = shuffled[i];
        }

        int numberIndex = 0;
        foreach (var tile in tiles)
        {
            if (tile.Resource == ResourceType.None)
            {
                tile.HasRobber = true;
                tile.NumberToken = 0;
            }
            else if (numberIndex < numbers.Length)
            {
                tile.NumberToken = numbers[numberIndex++];
            }
        }
    }

    // 표준 비율: 30 해안엣지 / 9 항구 ≈ 3.33
    const float EDGES_PER_PORT = 3.33f;

    /// <summary>
    /// 해안 변에 항구 배치 (토폴로지 기반, 보드 크기 자동 적응)
    /// 1) 해안 엣지 = 정확히 1개 육지 타일에 인접한 엣지
    /// 2) 그래프 워킹으로 링 순회 (각도 수학 없음)
    /// 3) 해안 엣지 수에 비례하여 항구 개수/타입 자동 결정
    /// 4) 균등 간격 배치 + 타입 셔플
    /// </summary>
    public static void SetupPorts(HexGrid grid, int boardRadius = 2, System.Random random = null)
    {
        random ??= new System.Random();

        // 1. 해안 엣지 수집 (정확히 1개 육지 타일에 인접)
        var coastalEdges = new List<HexEdge>();
        foreach (var edge in grid.Edges)
        {
            int landCount = 0;
            foreach (var tile in edge.AdjacentTiles)
            {
                if (tile.Resource != ResourceType.Sea) landCount++;
            }
            if (landCount == 1) coastalEdges.Add(edge);
        }

        if (coastalEdges.Count < 3)
        {
            UnityEngine.Debug.LogWarning($"[HexBoardSetup] 해안 엣지 부족 ({coastalEdges.Count}개)");
            return;
        }

        // 2. 그래프 워킹으로 해안 링 순회
        var orderedEdges = WalkBoundaryRing(coastalEdges);
        if (orderedEdges.Count == 0)
        {
            UnityEngine.Debug.LogWarning("[HexBoardSetup] 해안 링 구성 실패");
            return;
        }

        // 3. 보드 크기에 맞는 항구 타입 배열 생성
        var portTypes = BuildPortTypes(orderedEdges.Count);

        // 4. 균등 간격으로 항구 엣지 선택
        float spacing = (float)orderedEdges.Count / portTypes.Length;
        var portEdges = new List<HexEdge>(portTypes.Length);
        for (int i = 0; i < portTypes.Length; i++)
        {
            int index = UnityEngine.Mathf.FloorToInt(i * spacing);
            portEdges.Add(orderedEdges[index % orderedEdges.Count]);
        }

        // 5. 항구 타입 셔플 후 버텍스에 할당
        Shuffle(portTypes, random);
        for (int i = 0; i < portEdges.Count; i++)
        {
            portEdges[i].VertexA.Port = portTypes[i];
            portEdges[i].VertexB.Port = portTypes[i];
        }

        UnityEngine.Debug.Log($"[HexBoardSetup] 항구 {portEdges.Count}개 배치 " +
            $"(해안 엣지 {orderedEdges.Count}개, 간격 {spacing:F1})");
    }

    /// <summary>
    /// 해안 엣지를 그래프 연결 순서대로 정렬
    /// 각 해안 버텍스는 정확히 2개 해안 엣지에 연결 → 단순 링 워킹
    /// </summary>
    static List<HexEdge> WalkBoundaryRing(List<HexEdge> coastalEdges)
    {
        // 버텍스ID → 연결된 해안 엣지 룩업
        var vertexToEdges = new Dictionary<int, List<HexEdge>>();
        foreach (var edge in coastalEdges)
        {
            AddToVertexMap(vertexToEdges, edge.VertexA.Id, edge);
            AddToVertexMap(vertexToEdges, edge.VertexB.Id, edge);
        }

        // 결정론적 시작점: Z 최대(북쪽) → X 최대(동쪽)
        var start = coastalEdges[0];
        foreach (var edge in coastalEdges)
        {
            if (edge.MidPoint.z > start.MidPoint.z + 0.001f ||
                (UnityEngine.Mathf.Abs(edge.MidPoint.z - start.MidPoint.z) < 0.001f
                 && edge.MidPoint.x > start.MidPoint.x))
            {
                start = edge;
            }
        }

        var ordered = new List<HexEdge>(coastalEdges.Count) { start };
        var visited = new HashSet<int> { start.Id };

        // 시작 방향: X가 큰 버텍스 쪽으로 진행
        var nextVertex = start.VertexA.Position.x >= start.VertexB.Position.x
            ? start.VertexA : start.VertexB;

        for (int i = 0; i < coastalEdges.Count; i++)
        {
            HexEdge next = null;
            if (vertexToEdges.TryGetValue(nextVertex.Id, out var candidates))
            {
                foreach (var c in candidates)
                {
                    if (!visited.Contains(c.Id))
                    {
                        next = c;
                        break;
                    }
                }
            }

            if (next == null) break;

            ordered.Add(next);
            visited.Add(next.Id);
            nextVertex = (next.VertexA.Id == nextVertex.Id) ? next.VertexB : next.VertexA;
        }

        return ordered;
    }

    static void AddToVertexMap(Dictionary<int, List<HexEdge>> map, int vertexId, HexEdge edge)
    {
        if (!map.TryGetValue(vertexId, out var list))
        {
            list = new List<HexEdge>(2);
            map[vertexId] = list;
        }
        list.Add(edge);
    }

    /// <summary>해안 엣지 수에 비례하여 항구 타입 배열 생성</summary>
    static PortType[] BuildPortTypes(int coastalEdgeCount)
    {
        const int resourcePorts = 5;
        int totalPorts = UnityEngine.Mathf.Max(resourcePorts,
            UnityEngine.Mathf.RoundToInt(coastalEdgeCount / EDGES_PER_PORT));

        var ports = new PortType[totalPorts];
        ports[0] = PortType.Wood;
        ports[1] = PortType.Brick;
        ports[2] = PortType.Wool;
        ports[3] = PortType.Wheat;
        ports[4] = PortType.Ore;
        for (int i = resourcePorts; i < totalPorts; i++)
            ports[i] = PortType.Generic;

        return ports;
    }

    static void Shuffle<T>(T[] array, System.Random random)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }
}
