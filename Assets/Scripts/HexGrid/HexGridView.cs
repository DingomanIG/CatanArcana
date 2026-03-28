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
    [SerializeField] bool showVertices = true;
    [SerializeField] bool showEdges = true;

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
                var tile = grid.AddTile(coord);
                tile.Resource = ResourceType.Sea;
            }
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
        BuildVisuals();
    }
}
