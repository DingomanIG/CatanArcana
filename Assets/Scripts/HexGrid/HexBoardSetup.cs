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

    // 표준 항구 배분 (9개: 3:1 × 4, 2:1 × 5)
    static readonly PortType[] STANDARD_PORTS = new[]
    {
        PortType.Generic, PortType.Generic, PortType.Generic, PortType.Generic,
        PortType.Wood, PortType.Brick, PortType.Wool, PortType.Wheat, PortType.Ore
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

        // 항구 배치
        SetupPorts(grid, random);
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

    /// <summary>해안 변에 항구 배치 (표준 9개)</summary>
    static void SetupPorts(HexGrid grid, System.Random random)
    {
        // 해안 변 찾기: 육지 타일 1개만 인접한 변
        var coastalEdges = new List<HexEdge>();
        foreach (var edge in grid.Edges)
        {
            int landCount = 0;
            foreach (var tile in edge.AdjacentTiles)
            {
                if (tile.Resource != ResourceType.Sea)
                    landCount++;
            }
            if (landCount == 1)
                coastalEdges.Add(edge);
        }

        if (coastalEdges.Count < 9)
        {
            UnityEngine.Debug.LogWarning($"[HexBoardSetup] 해안 변 부족: {coastalEdges.Count}개 (9개 필요)");
            return;
        }

        // 해안 변을 균일하게 분산 배치 (매 3~4번째)
        int spacing = coastalEdges.Count / 9;
        var portEdges = new List<HexEdge>();
        int startOffset = random.Next(spacing);
        for (int i = 0; i < 9; i++)
        {
            int idx = (startOffset + i * spacing) % coastalEdges.Count;
            portEdges.Add(coastalEdges[idx]);
        }

        // 항구 타입 셔플 및 할당
        var ports = STANDARD_PORTS.ToArray();
        Shuffle(ports, random);

        for (int i = 0; i < portEdges.Count; i++)
        {
            var edge = portEdges[i];
            edge.VertexA.Port = ports[i];
            edge.VertexB.Port = ports[i];
        }

        UnityEngine.Debug.Log($"[HexBoardSetup] 항구 {portEdges.Count}개 배치 완료 (해안 변: {coastalEdges.Count})");
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
