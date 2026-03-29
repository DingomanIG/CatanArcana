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
        var label = new GameObject("NumberToken");
        label.transform.SetParent(parent.transform);
        label.transform.localPosition = new Vector3(0f, 0.01f, 0f);
        label.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        var tm = label.AddComponent<TextMesh>();
        tm.text = tile.NumberToken.ToString();
        tm.characterSize = hexSize * 0.15f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 48;

        // 6, 8은 빨간색 (높은 확률)
        tm.color = (tile.NumberToken == 6 || tile.NumberToken == 8)
            ? Color.red
            : Color.black;
    }

    void CreateRobberMarker(GameObject parent)
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "Robber";
        marker.transform.SetParent(parent.transform);
        marker.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        marker.transform.localScale = new Vector3(hexSize * 0.2f, 0.15f, hexSize * 0.2f);

        var mr = marker.GetComponent<MeshRenderer>();
        mr.material = new Material(defaultMaterial);
        mr.material.color = Color.black;
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

            // 인접 바다 타일 중심에 배치
            Vector3 pos = edge.MidPoint;
            foreach (var tile in edge.AdjacentTiles)
            {
                if (tile.Resource == ResourceType.Sea)
                {
                    pos = tile.Coord.ToWorldPosition(hexSize);
                    break;
                }
            }

            // 항구 마커 (작은 큐브)
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = $"Port_{portType}_{edge.Id}";
            marker.transform.SetParent(portParent.transform);
            marker.transform.position = pos + Vector3.up * 0.05f;
            marker.transform.localScale = new Vector3(hexSize * 0.25f, 0.08f, hexSize * 0.25f);

            var mr = marker.GetComponent<MeshRenderer>();
            mr.material = new Material(defaultMaterial);
            mr.material.color = PORT_COLORS.GetValueOrDefault(portType, Color.white);

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
        }
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
