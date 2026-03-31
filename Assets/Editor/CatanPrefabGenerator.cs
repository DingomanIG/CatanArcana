using UnityEditor;
using UnityEngine;

/// <summary>
/// 카탄 프리팹 자동 생성기
/// 메뉴: Catan > 프리팹 생성
/// </summary>
public static class CatanPrefabGenerator
{
    const string BOARD = "Assets/Prefabs/Board";
    const string BUILDINGS = "Assets/Prefabs/Buildings";

    [MenuItem("Catan/프리팹 생성")]
    public static void GenerateAll()
    {
        EnsureFolders();
        var baseMat = GetOrCreateBaseMaterial();

        GenerateHexTile(baseMat);
        GenerateNumberToken(baseMat);
        GenerateRobber(baseMat);
        GeneratePortMarker(baseMat);
        GenerateDock(baseMat);
        GenerateBridge(baseMat);
        GenerateVertex(baseMat);
        GenerateEdge(baseMat);
        GenerateRoad(baseMat);
        GenerateSettlement(baseMat);
        GenerateCity(baseMat);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Catan] 프리팹 11개 생성 완료! (Board: HexTile, NumberToken, Robber, PortMarker, Dock, Bridge / Buildings: Vertex, Edge, Road, Settlement, City)");
    }

    [MenuItem("Catan/프리팹 자동 연결")]
    public static void AutoWirePrefabs()
    {
        // HexGridView에 프리팹 연결
        var gridView = Object.FindFirstObjectByType<HexGridView>();
        if (gridView != null)
        {
            var so = new SerializedObject(gridView);
            TryAssign(so, "hexTilePrefab", BOARD + "/HexTile.prefab");
            TryAssign(so, "numberTokenPrefab", BOARD + "/NumberToken.prefab");
            TryAssign(so, "robberPrefab", BOARD + "/Robber.prefab");
            TryAssign(so, "edgePrefab", BUILDINGS + "/Edge.prefab");
            TryAssign(so, "vertexPrefab", BUILDINGS + "/Vertex.prefab");
            TryAssign(so, "portMarkerPrefab", BOARD + "/PortMarker.prefab");
            TryAssign(so, "dockPrefab", BOARD + "/Dock.prefab");
            TryAssign(so, "bridgePrefab", BOARD + "/Bridge.prefab");
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(gridView);
            Debug.Log("[Catan] HexGridView 프리팹 연결 완료");
        }

        // BuildingVisuals에 프리팹 연결
        var buildingVisuals = Object.FindFirstObjectByType<BuildingVisuals>();
        if (buildingVisuals != null)
        {
            var so = new SerializedObject(buildingVisuals);
            TryAssign(so, "settlementPrefab", BUILDINGS + "/Settlement.prefab");
            TryAssign(so, "cityPrefab", BUILDINGS + "/City.prefab");
            TryAssign(so, "roadPrefab", BUILDINGS + "/Road.prefab");
            TryAssign(so, "vertexHighlightPrefab", BUILDINGS + "/Vertex.prefab");
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(buildingVisuals);
            Debug.Log("[Catan] BuildingVisuals 프리팹 연결 완료");
        }
    }

    static void TryAssign(SerializedObject so, string propName, string assetPath)
    {
        var prop = so.FindProperty(propName);
        if (prop == null) return;
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset != null) prop.objectReferenceValue = asset;
    }

    // ============================
    // 유틸리티
    // ============================

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(BOARD))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Board");
        if (!AssetDatabase.IsValidFolder(BUILDINGS))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Buildings");
    }

    static Material GetOrCreateBaseMaterial()
    {
        var path = BOARD + "/BaseMaterial.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Material CreateColorMaterial(Material baseMat, string path, Color color)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.color = color;
            return existing;
        }

        var mat = new Material(baseMat) { color = color };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void SavePrefab(GameObject go, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    static void DestroyCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
    }

    // ============================
    // 보드 프리팹
    // ============================

    static void GenerateHexTile(Material baseMat)
    {
        // 헥스 메시 에셋 저장
        var meshPath = BOARD + "/HexMesh.asset";
        var mesh = HexMeshGenerator.CreateFlatHexMesh(0.95f); // hexSize(1) - tileGap(0.05)

        var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existingMesh != null)
        {
            existingMesh.Clear();
            existingMesh.vertices = mesh.vertices;
            existingMesh.triangles = mesh.triangles;
            existingMesh.uv = mesh.uv;
            existingMesh.RecalculateNormals();
            existingMesh.RecalculateBounds();
            Object.DestroyImmediate(mesh);
            mesh = existingMesh;
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, meshPath);
        }

        var go = new GameObject("HexTile");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = baseMat;
        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;

        SavePrefab(go, BOARD + "/HexTile.prefab");
    }

    static void GenerateNumberToken(Material baseMat)
    {
        var root = new GameObject("NumberToken");

        // 테두리 링
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "TokenRing";
        ring.transform.SetParent(root.transform);
        ring.transform.localPosition = new Vector3(0f, 0.015f, 0f);
        ring.transform.localScale = new Vector3(0.504f, 0.008f, 0.504f);
        DestroyCollider(ring);
        ring.GetComponent<MeshRenderer>().sharedMaterial =
            CreateColorMaterial(baseMat, BOARD + "/TokenRingMat.mat", new Color(0.35f, 0.25f, 0.15f));

        // 토큰 배경
        var bg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bg.name = "TokenBg";
        bg.transform.SetParent(root.transform);
        bg.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        bg.transform.localScale = new Vector3(0.45f, 0.01f, 0.45f);
        DestroyCollider(bg);
        bg.GetComponent<MeshRenderer>().sharedMaterial =
            CreateColorMaterial(baseMat, BOARD + "/TokenBgMat.mat", new Color(0.95f, 0.92f, 0.85f));

        // 숫자 텍스트
        var textGo = new GameObject("NumberText");
        textGo.transform.SetParent(root.transform);
        textGo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        textGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var tm = textGo.AddComponent<TextMesh>();
        tm.text = "0";
        tm.characterSize = 0.06f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 64;
        tm.fontStyle = FontStyle.Bold;
        tm.color = Color.black;

        // 확률 도트
        var dotsGo = new GameObject("Dots");
        dotsGo.transform.SetParent(root.transform);
        dotsGo.transform.localPosition = new Vector3(0f, 0.05f, -0.18f);
        dotsGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var dotsTm = dotsGo.AddComponent<TextMesh>();
        dotsTm.text = "";
        dotsTm.characterSize = 0.025f;
        dotsTm.anchor = TextAnchor.MiddleCenter;
        dotsTm.alignment = TextAlignment.Center;
        dotsTm.fontSize = 48;
        dotsTm.color = Color.black;

        SavePrefab(root, BOARD + "/NumberToken.prefab");
    }

    static void GenerateRobber(Material baseMat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Robber";
        go.transform.localScale = new Vector3(0.25f, 0.3f, 0.25f);
        DestroyCollider(go);
        go.GetComponent<MeshRenderer>().sharedMaterial =
            CreateColorMaterial(baseMat, BOARD + "/RobberMat.mat", new Color(0.15f, 0.15f, 0.15f));

        SavePrefab(go, BOARD + "/Robber.prefab");
    }

    static void GeneratePortMarker(Material baseMat)
    {
        var root = new GameObject("PortMarker");

        // 마커 큐브
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Marker";
        cube.transform.SetParent(root.transform);
        cube.transform.localPosition = Vector3.zero;
        cube.transform.localScale = new Vector3(0.25f, 0.08f, 0.25f);
        DestroyCollider(cube);
        cube.GetComponent<MeshRenderer>().sharedMaterial = baseMat;

        // 라벨 텍스트
        var labelGo = new GameObject("PortLabel");
        labelGo.transform.SetParent(root.transform);
        labelGo.transform.localPosition = new Vector3(0f, 0.6f, -1.8f);
        labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var tm = labelGo.AddComponent<TextMesh>();
        tm.text = "3:1";
        tm.characterSize = 0.06f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 36;
        tm.color = Color.black;

        SavePrefab(root, BOARD + "/PortMarker.prefab");
    }

    static void GenerateDock(Material baseMat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Dock";
        go.transform.localScale = new Vector3(0.12f, 0.1f, 0.12f);
        DestroyCollider(go);
        go.GetComponent<MeshRenderer>().sharedMaterial =
            CreateColorMaterial(baseMat, BOARD + "/DockMat.mat", new Color(0.45f, 0.30f, 0.15f));

        SavePrefab(go, BOARD + "/Dock.prefab");
    }

    static void GenerateBridge(Material baseMat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Bridge";
        go.transform.localScale = new Vector3(0.06f, 0.04f, 1f);
        DestroyCollider(go);
        go.GetComponent<MeshRenderer>().sharedMaterial =
            CreateColorMaterial(baseMat, BOARD + "/BridgeMat.mat", new Color(0.55f, 0.38f, 0.20f));

        SavePrefab(go, BOARD + "/Bridge.prefab");
    }

    // ============================
    // 건물 프리팹
    // ============================

    static void GenerateVertex(Material baseMat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Vertex";
        go.transform.localScale = Vector3.one * 0.3f;
        go.GetComponent<MeshRenderer>().sharedMaterial = baseMat;

        SavePrefab(go, BUILDINGS + "/Vertex.prefab");
    }

    static void GenerateEdge(Material baseMat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Edge";
        go.transform.localScale = new Vector3(0.04f, 0.5f, 0.04f);
        DestroyCollider(go);
        go.GetComponent<MeshRenderer>().sharedMaterial =
            CreateColorMaterial(baseMat, BUILDINGS + "/EdgeMat.mat", new Color(0.4f, 0.35f, 0.25f));

        SavePrefab(go, BUILDINGS + "/Edge.prefab");
    }

    static void GenerateRoad(Material baseMat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Road";
        go.transform.localScale = new Vector3(0.1f, 0.5f, 0.1f);
        DestroyCollider(go);
        go.GetComponent<MeshRenderer>().sharedMaterial = baseMat;

        SavePrefab(go, BUILDINGS + "/Road.prefab");
    }

    static void GenerateSettlement(Material baseMat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Settlement";
        go.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
        go.GetComponent<MeshRenderer>().sharedMaterial = baseMat;

        SavePrefab(go, BUILDINGS + "/Settlement.prefab");
    }

    static void GenerateCity(Material baseMat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "City";
        go.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
        go.GetComponent<MeshRenderer>().sharedMaterial = baseMat;

        SavePrefab(go, BUILDINGS + "/City.prefab");
    }
}
