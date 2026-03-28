using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 건물 비주얼 관리 - 건물 생성 + 하이라이트
/// </summary>
public class BuildingVisuals : MonoBehaviour
{
    static readonly Color[] PLAYER_COLORS =
    {
        new Color(0.9f, 0.2f, 0.2f),   // Red
        new Color(0.2f, 0.5f, 0.9f),   // Blue
        new Color(0.9f, 0.6f, 0.1f),   // Orange
        new Color(0.9f, 0.9f, 0.9f),   // White
    };

    static readonly Color HIGHLIGHT_VALID = new Color(0.3f, 1f, 0.3f, 0.5f);
    static readonly Color HIGHLIGHT_HOVER = new Color(1f, 1f, 0.3f, 0.8f);

    Material defaultMaterial;
    Material highlightMaterial;

    Transform buildingsParent;
    Transform highlightsParent;

    readonly Dictionary<int, GameObject> settlementObjects = new();
    readonly Dictionary<int, GameObject> roadObjects = new();
    readonly List<GameObject> highlightObjects = new();

    // 하이라이트 → 데이터 매핑
    readonly Dictionary<GameObject, int> highlightToVertexId = new();
    readonly Dictionary<GameObject, int> highlightToEdgeId = new();

    GameObject currentHover;

    void Awake()
    {
        defaultMaterial = CreateMaterial();
        highlightMaterial = CreateMaterial();
        highlightMaterial.SetFloat("_Surface", 1); // Transparent
        highlightMaterial.SetFloat("_Blend", 0);
        highlightMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        highlightMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        highlightMaterial.SetInt("_ZWrite", 0);
        highlightMaterial.renderQueue = 3000;
        highlightMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        buildingsParent = new GameObject("Buildings").transform;
        buildingsParent.SetParent(transform);

        highlightsParent = new GameObject("Highlights").transform;
        highlightsParent.SetParent(transform);
    }

    public static Color GetPlayerColor(int playerIndex)
    {
        return playerIndex >= 0 && playerIndex < PLAYER_COLORS.Length
            ? PLAYER_COLORS[playerIndex]
            : Color.gray;
    }

    // ========================
    // 건물 생성
    // ========================

    /// <summary>마을 생성</summary>
    public void CreateSettlement(HexVertex vertex, int playerIndex)
    {
        if (settlementObjects.ContainsKey(vertex.Id))
            return;

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"Settlement_P{playerIndex}_V{vertex.Id}";
        go.transform.SetParent(buildingsParent);
        go.transform.position = vertex.Position + Vector3.up * 0.15f;
        go.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);

        var mr = go.GetComponent<MeshRenderer>();
        mr.material = new Material(defaultMaterial);
        mr.material.color = GetPlayerColor(playerIndex);

        settlementObjects[vertex.Id] = go;
    }

    /// <summary>도시 생성 (마을 → 도시 교체)</summary>
    public void CreateCity(HexVertex vertex, int playerIndex)
    {
        // 기존 마을 제거
        if (settlementObjects.TryGetValue(vertex.Id, out var old))
        {
            Destroy(old);
            settlementObjects.Remove(vertex.Id);
        }

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"City_P{playerIndex}_V{vertex.Id}";
        go.transform.SetParent(buildingsParent);
        go.transform.position = vertex.Position + Vector3.up * 0.2f;
        go.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);

        var mr = go.GetComponent<MeshRenderer>();
        mr.material = new Material(defaultMaterial);
        mr.material.color = GetPlayerColor(playerIndex);

        settlementObjects[vertex.Id] = go;
    }

    /// <summary>도로 생성</summary>
    public void CreateRoad(HexEdge edge, int playerIndex)
    {
        if (roadObjects.ContainsKey(edge.Id))
            return;

        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = $"Road_P{playerIndex}_E{edge.Id}";
        go.transform.SetParent(buildingsParent);

        var dir = edge.VertexB.Position - edge.VertexA.Position;
        go.transform.position = edge.MidPoint + Vector3.up * 0.05f;
        go.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(0.1f, dir.magnitude / 2f, 0.1f);

        var mr = go.GetComponent<MeshRenderer>();
        mr.material = new Material(defaultMaterial);
        mr.material.color = GetPlayerColor(playerIndex);

        // 도로 콜라이더 제거 (건물과 겹치지 않게)
        Destroy(go.GetComponent<Collider>());

        roadObjects[edge.Id] = go;
    }

    // ========================
    // 하이라이트
    // ========================

    /// <summary>유효한 교차점 하이라이트 표시</summary>
    public void ShowValidVertices(List<HexVertex> vertices)
    {
        foreach (var vertex in vertices)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Highlight_V{vertex.Id}";
            go.transform.SetParent(highlightsParent);
            go.transform.position = vertex.Position + Vector3.up * 0.1f;
            go.transform.localScale = Vector3.one * 0.3f;
            go.layer = 0; // Default layer for raycasting

            var mr = go.GetComponent<MeshRenderer>();
            mr.material = new Material(highlightMaterial);
            mr.material.color = HIGHLIGHT_VALID;

            highlightObjects.Add(go);
            highlightToVertexId[go] = vertex.Id;
        }
    }

    /// <summary>유효한 변 하이라이트 표시</summary>
    public void ShowValidEdges(List<HexEdge> edges)
    {
        foreach (var edge in edges)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = $"Highlight_E{edge.Id}";
            go.transform.SetParent(highlightsParent);

            var dir = edge.VertexB.Position - edge.VertexA.Position;
            go.transform.position = edge.MidPoint + Vector3.up * 0.05f;
            go.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(0.15f, dir.magnitude / 2f, 0.15f);
            go.layer = 0;

            var mr = go.GetComponent<MeshRenderer>();
            mr.material = new Material(highlightMaterial);
            mr.material.color = HIGHLIGHT_VALID;

            highlightObjects.Add(go);
            highlightToEdgeId[go] = edge.Id;
        }
    }

    /// <summary>모든 하이라이트 제거</summary>
    public void ClearHighlights()
    {
        foreach (var go in highlightObjects)
        {
            if (go != null) Destroy(go);
        }
        highlightObjects.Clear();
        highlightToVertexId.Clear();
        highlightToEdgeId.Clear();
        currentHover = null;
    }

    /// <summary>호버 하이라이트 설정</summary>
    public void SetHover(GameObject target)
    {
        // 이전 호버 복원
        if (currentHover != null && currentHover != target)
        {
            var prevMr = currentHover.GetComponent<MeshRenderer>();
            if (prevMr != null) prevMr.material.color = HIGHLIGHT_VALID;
        }

        currentHover = target;

        if (target != null)
        {
            var mr = target.GetComponent<MeshRenderer>();
            if (mr != null) mr.material.color = HIGHLIGHT_HOVER;
        }
    }

    /// <summary>하이라이트 오브젝트에서 Vertex ID 조회</summary>
    public int GetVertexIdFromHighlight(GameObject go)
    {
        return highlightToVertexId.TryGetValue(go, out var id) ? id : -1;
    }

    /// <summary>하이라이트 오브젝트에서 Edge ID 조회</summary>
    public int GetEdgeIdFromHighlight(GameObject go)
    {
        return highlightToEdgeId.TryGetValue(go, out var id) ? id : -1;
    }

    // ========================
    // Helpers
    // ========================

    Material CreateMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        return new Material(shader);
    }
}
