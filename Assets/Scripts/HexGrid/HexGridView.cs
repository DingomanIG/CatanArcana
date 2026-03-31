using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 헥스 그리드 비주얼 컨트롤러
/// HexGrid 데이터를 씬에 렌더링
/// </summary>
public class HexGridView : MonoBehaviour
{
    [Header("그리드 설정")]
    [SerializeField] float hexSize = 1f;
    [SerializeField] int boardRadius = 2;
    [SerializeField] int seaRings = 3;

    [Header("비주얼 설정")]
    [SerializeField] float tileGap = 0.05f;
    [SerializeField] bool showVertices = false;
    [SerializeField] bool showEdges = false;

    HexGrid grid;
    Dictionary<HexCoord, GameObject> tileViews = new();
    Mesh hexMesh;
    Material defaultMaterial;

    static readonly Dictionary<ResourceType, Color> RESOURCE_COLORS = new()
    {
        { ResourceType.None,  new Color(0.85f, 0.78f, 0.55f) },   // 사막 - 모래색
        { ResourceType.Wood,  new Color(0.18f, 0.55f, 0.18f) },   // 숲 - 초록
        { ResourceType.Brick, new Color(0.78f, 0.38f, 0.18f) },   // 언덕 - 주황갈색
        { ResourceType.Wool,  new Color(0.60f, 0.85f, 0.40f) },   // 초원 - 연두
        { ResourceType.Wheat, new Color(0.95f, 0.85f, 0.20f) },   // 밭 - 노랑
        { ResourceType.Ore,   new Color(0.55f, 0.55f, 0.60f) },   // 산 - 회색
        { ResourceType.Sea,   new Color(0.15f, 0.45f, 0.75f) },   // 바다 - 파랑
    };

    public HexGrid Grid => grid;

    void Awake()
    {
        hexMesh = HexMeshGenerator.CreateFlatHexMesh(hexSize - tileGap);
        defaultMaterial = CreateDefaultMaterial();

        grid = new HexGrid(hexSize);
        grid.GenerateHexagonal(boardRadius);
        HexBoardSetup.SetupStandardBoard(grid);
        AddSeaRings();
        HexBoardSetup.SetupPorts(grid, boardRadius);

        BuildVisuals();
    }

    void BuildVisuals()
    {
        ClearVisuals();

        foreach (var tile in grid.Tiles.Values)
        {
            CreateTileView(tile);
        }

        if (showVertices) CreateVertexVisuals();
        if (showEdges) CreateEdgeVisuals();
        CreatePortVisuals();

        Debug.Log($"[HexGridView] 보드 생성 완료: 타일 {grid.Tiles.Count}, 교차점 {grid.Vertices.Count}, 변 {grid.Edges.Count}");
    }

    void CreateTileView(HexTile tile)
    {
        var go = new GameObject($"Tile_{tile.Coord}");
        go.transform.SetParent(transform);
        go.transform.position = tile.Coord.ToWorldPosition(hexSize);

        var mf = go.AddComponent<MeshFilter>();
        mf.mesh = hexMesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.material = new Material(defaultMaterial);
        mr.material.color = RESOURCE_COLORS[tile.Resource];

        // 도적 이동용 콜라이더
        go.AddComponent<MeshCollider>();

        // 숫자 토큰 라벨
        if (tile.NumberToken > 0)
        {
            CreateNumberLabel(go, tile);
        }

        // 도적 표시
        if (tile.HasRobber)
        {
            CreateRobberMarker(go);
        }

        tileViews[tile.Coord] = go;
    }

    void CreateNumberLabel(GameObject parent, HexTile tile)
    {
        bool isHot = tile.NumberToken == 6 || tile.NumberToken == 8;
        float tokenRadius = hexSize * 0.45f;

        // 테두리 링 (약간 더 큰 납작 실린더, 먼저 깔기)
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "TokenRing";
        ring.transform.SetParent(parent.transform);
        ring.transform.localPosition = new Vector3(0f, 0.015f, 0f);
        ring.transform.localScale = new Vector3(tokenRadius * 1.12f, 0.008f, tokenRadius * 1.12f);

        var ringCol = ring.GetComponent<Collider>();
        if (ringCol != null) Destroy(ringCol);

        var ringMr = ring.GetComponent<MeshRenderer>();
        ringMr.material = new Material(defaultMaterial);
        ringMr.material.color = new Color(0.35f, 0.25f, 0.15f); // 다크 브라운

        // 토큰 배경 (납작한 실린더)
        var token = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        token.name = "NumberToken";
        token.transform.SetParent(parent.transform);
        token.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        token.transform.localScale = new Vector3(tokenRadius, 0.01f, tokenRadius);

        var col = token.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var tokenMr = token.GetComponent<MeshRenderer>();
        tokenMr.material = new Material(defaultMaterial);
        tokenMr.material.color = new Color(0.95f, 0.92f, 0.85f); // 크림색

        // 숫자 텍스트 (타일 직접 자식 - 비균일 스케일 회피)
        var label = new GameObject("NumberText");
        label.transform.SetParent(parent.transform);
        label.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        label.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        var tm = label.AddComponent<TextMesh>();
        tm.text = tile.NumberToken.ToString();
        tm.characterSize = hexSize * 0.12f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 64;
        tm.fontStyle = FontStyle.Bold;
        tm.color = isHot ? new Color(0.85f, 0.1f, 0.1f) : new Color(0.15f, 0.15f, 0.15f);

        // 확률 도트 (타일 직접 자식)
        int dotCount = GetProbabilityDots(tile.NumberToken);
        if (dotCount > 0)
        {
            var dots = new GameObject("Dots");
            dots.transform.SetParent(parent.transform);
            dots.transform.localPosition = new Vector3(0f, 0.03f, -hexSize * 0.08f);
            dots.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var dotTm = dots.AddComponent<TextMesh>();
            dotTm.text = new string('\u2022', dotCount);
            dotTm.characterSize = hexSize * 0.05f;
            dotTm.anchor = TextAnchor.MiddleCenter;
            dotTm.alignment = TextAlignment.Center;
            dotTm.fontSize = 48;
            dotTm.color = isHot ? new Color(0.85f, 0.1f, 0.1f) : new Color(0.4f, 0.4f, 0.4f);
        }
    }

    static int GetProbabilityDots(int number) => number switch
    {
        2 or 12 => 1,
        3 or 11 => 2,
        4 or 10 => 3,
        5 or 9 => 4,
        6 or 8 => 5,
        _ => 0
    };

    void CreateRobberMarker(GameObject parent)
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "Robber";
        marker.transform.SetParent(parent.transform);
        marker.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        marker.transform.localScale = new Vector3(hexSize * 0.25f, 0.3f, hexSize * 0.25f);

        var mr = marker.GetComponent<MeshRenderer>();
        mr.material = new Material(defaultMaterial);
        mr.material.color = new Color(0.15f, 0.15f, 0.15f);
    }

    void CreateVertexVisuals()
    {
        var vertexParent = new GameObject("Vertices");
        vertexParent.transform.SetParent(transform);

        foreach (var vertex in grid.Vertices)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Vertex_{vertex.Id}";
            go.transform.SetParent(vertexParent.transform);
            go.transform.position = vertex.Position;
            go.transform.localScale = Vector3.one * hexSize * 0.12f;

            var mr = go.GetComponent<MeshRenderer>();
            mr.material = new Material(defaultMaterial);
            mr.material.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        }
    }

    void CreateEdgeVisuals()
    {
        var edgeParent = new GameObject("Edges");
        edgeParent.transform.SetParent(transform);

        foreach (var edge in grid.Edges)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = $"Edge_{edge.Id}";
            go.transform.SetParent(edgeParent.transform);

            var dir = edge.VertexB.Position - edge.VertexA.Position;
            go.transform.position = edge.MidPoint;
            go.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(
                hexSize * 0.04f,
                dir.magnitude / 2f,
                hexSize * 0.04f);

            var mr = go.GetComponent<MeshRenderer>();
            mr.material = new Material(defaultMaterial);
            mr.material.color = new Color(0.4f, 0.35f, 0.25f, 0.5f);
        }
    }

    static readonly Dictionary<PortType, Color> PORT_COLORS = new()
    {
        { PortType.Generic, new Color(1f, 1f, 1f, 0.9f) },    // 3:1 - 흰색
        { PortType.Wood,    new Color(0.18f, 0.55f, 0.18f) },  // 목재 - 초록
        { PortType.Brick,   new Color(0.78f, 0.38f, 0.18f) },  // 벽돌 - 주황
        { PortType.Wool,    new Color(0.60f, 0.85f, 0.40f) },  // 양모 - 연두
        { PortType.Wheat,   new Color(0.95f, 0.85f, 0.20f) },  // 밀 - 노랑
        { PortType.Ore,     new Color(0.55f, 0.55f, 0.60f) },  // 광석 - 회색
    };

    static readonly Dictionary<PortType, string> PORT_LABELS = new()
    {
        { PortType.Generic, "3:1" },
        { PortType.Wood,    "2:1" },
        { PortType.Brick,   "2:1" },
        { PortType.Wool,    "2:1" },
        { PortType.Wheat,   "2:1" },
        { PortType.Ore,     "2:1" },
    };

    void CreatePortVisuals()
    {
        var portParent = new GameObject("Ports");
        portParent.transform.SetParent(transform);

        // 항구 변 찾기 (양 끝 vertex가 같은 항구)
        var processedEdges = new HashSet<int>();
        foreach (var edge in grid.Edges)
        {
            if (processedEdges.Contains(edge.Id)) continue;
            if (edge.VertexA.Port == PortType.None) continue;
            if (edge.VertexA.Port != edge.VertexB.Port) continue;

            processedEdges.Add(edge.Id);
            var portType = edge.VertexA.Port;
            var portColor = PORT_COLORS.GetValueOrDefault(portType, Color.white);

            // 인접 바다 타일 중심에 배치
            Vector3 seaCenter = edge.MidPoint;
            foreach (var tile in edge.AdjacentTiles)
            {
                if (tile.Resource == ResourceType.Sea)
                {
                    seaCenter = tile.Coord.ToWorldPosition(hexSize);
                    break;
                }
            }

            // 항구 마커 (바다 타일 중심 - 자원 큐브)
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = $"Port_{portType}_{edge.Id}";
            marker.transform.SetParent(portParent.transform);
            marker.transform.position = seaCenter + Vector3.up * 0.05f;
            marker.transform.localScale = new Vector3(hexSize * 0.25f, 0.08f, hexSize * 0.25f);

            var mr = marker.GetComponent<MeshRenderer>();
            mr.material = new Material(defaultMaterial);
            mr.material.color = portColor;

            // 항구 라벨
            var label = new GameObject("PortLabel");
            label.transform.SetParent(marker.transform);
            label.transform.localPosition = new Vector3(0f, 0.6f, -1.8f);
            label.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var tm = label.AddComponent<TextMesh>();
            tm.text = PORT_LABELS.GetValueOrDefault(portType, "?");
            tm.characterSize = hexSize * 0.06f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 36;
            tm.color = portType == PortType.Generic ? Color.black : Color.white;

            // 부두 마커 (항구 꼭짓점 2개)
            CreateDockMarker(portParent.transform, edge.VertexA.Position, seaCenter, portColor);
            CreateDockMarker(portParent.transform, edge.VertexB.Position, seaCenter, portColor);
        }
    }

    void CreateDockMarker(Transform parent, Vector3 vertexPos, Vector3 seaCenter, Color color)
    {
        // 부두 기둥 (꼭짓점 위치)
        var dock = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        dock.name = "Dock";
        dock.transform.SetParent(parent);
        dock.transform.position = vertexPos + Vector3.up * 0.1f;
        dock.transform.localScale = new Vector3(hexSize * 0.12f, 0.1f, hexSize * 0.12f);

        var dockMr = dock.GetComponent<MeshRenderer>();
        dockMr.material = new Material(defaultMaterial);
        dockMr.material.color = new Color(0.45f, 0.30f, 0.15f); // 나무색

        // 다리 (바다 중심 → 꼭짓점 연결)
        var bridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bridge.name = "Bridge";
        bridge.transform.SetParent(parent);

        var midPoint = (seaCenter + vertexPos) / 2f + Vector3.up * 0.05f;
        var dir = vertexPos - seaCenter;
        float length = dir.magnitude;

        bridge.transform.position = midPoint;
        bridge.transform.rotation = Quaternion.LookRotation(dir);
        bridge.transform.localScale = new Vector3(hexSize * 0.06f, 0.04f, length);

        var bridgeMr = bridge.GetComponent<MeshRenderer>();
        bridgeMr.material = new Material(defaultMaterial);
        bridgeMr.material.color = new Color(0.55f, 0.38f, 0.20f); // 밝은 나무색
    }

    void ClearVisuals()
    {
        foreach (var go in tileViews.Values)
        {
            if (go != null) Destroy(go);
        }
        tileViews.Clear();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    Material CreateDefaultMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        return new Material(shader);
    }

    /// <summary>바다 타일 링 추가 (육지 바깥)</summary>
    void AddSeaRings()
    {
        for (int ring = boardRadius + 1; ring <= boardRadius + seaRings; ring++)
        {
            var coords = HexCoord.Ring(HexCoord.Zero, ring);
            foreach (var coord in coords)
            {
                if (!grid.Tiles.ContainsKey(coord))
                {
                    var tile = new HexTile(coord) { Resource = ResourceType.Sea };
                    grid.Tiles[coord] = tile;
                }
            }
        }
        grid.RebuildTopology();
    }

    /// <summary>좌표로 타일 GameObject 조회</summary>
    public GameObject GetTileGameObject(HexCoord coord)
    {
        tileViews.TryGetValue(coord, out var go);
        return go;
    }

    /// <summary>히트된 게임 오브젝트에서 타일 좌표 조회</summary>
    public bool TryGetTileCoord(GameObject go, out HexCoord coord)
    {
        foreach (var kv in tileViews)
        {
            if (kv.Value == go)
            {
                coord = kv.Key;
                return true;
            }
        }
        coord = default;
        return false;
    }

    /// <summary>도적 마커를 새 위치로 이동</summary>
    public void MoveRobberVisual(HexCoord newCoord)
    {
        // 기존 도적 마커 제거
        foreach (var kv in tileViews)
        {
            var robber = kv.Value.transform.Find("Robber");
            if (robber != null)
            {
                Destroy(robber.gameObject);
                break;
            }
        }

        // 새 위치에 도적 마커 생성
        if (tileViews.TryGetValue(newCoord, out var tileGo))
        {
            CreateRobberMarker(tileGo);
        }
    }

    /// <summary>보드 크기 변경 후 재생성</summary>
    public void RegenerateBoard(int newRadius)
    {
        boardRadius = newRadius;
        grid.GenerateHexagonal(boardRadius);
        if (grid.Tiles.Count == 19)
        {
            HexBoardSetup.SetupStandardBoard(grid);
        }
        AddSeaRings();
        HexBoardSetup.SetupPorts(grid, boardRadius);
        BuildVisuals();
    }
}
