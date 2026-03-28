using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// 건설 모드 컨트롤러 - 빌드 모드 상태머신 + 레이캐스트 입력
/// </summary>
public class BuildModeController : MonoBehaviour
{
    public static BuildModeController Instance { get; private set; }

    BuildingSystem buildingSystem;
    BuildingVisuals buildingVisuals;
    HexGridView gridView;

    BuildMode currentMode = BuildMode.None;
    int activePlayerIndex = 0;
    bool isInitialPlacement = false;

    // UI 클릭 관통 방지
    UIDocument uiDocument;

    public BuildMode CurrentMode => currentMode;
    public event Action<BuildMode> OnBuildModeChanged;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        gridView = FindObjectOfType<HexGridView>();
        buildingVisuals = GetComponent<BuildingVisuals>();
        if (buildingVisuals == null)
            buildingVisuals = gameObject.AddComponent<BuildingVisuals>();

        uiDocument = FindObjectOfType<UIDocument>();

        // 투명 레이아웃 컨테이너의 picking-mode를 Ignore로 설정
        // USS만으로 불확실하므로 C#에서도 확실히 설정
        if (uiDocument != null)
        {
            var root = uiDocument.rootVisualElement;
            SetPickingIgnore(root, "hud-root");
            SetPickingIgnore(root, "middle-area");
        }

        if (gridView != null && gridView.Grid != null)
        {
            buildingSystem = new BuildingSystem(gridView.Grid);
            Debug.Log("[BuildMode] 건설 시스템 초기화 완료");
        }
    }

    static void SetPickingIgnore(VisualElement root, string name)
    {
        var el = root.Q(name);
        if (el != null) el.pickingMode = PickingMode.Ignore;
    }

    /// <summary>건설 모드 진입</summary>
    public void EnterBuildMode(BuildMode mode)
    {
        if (buildingSystem == null) return;

        // 현재 플레이어 인덱스
        var gm = GameServices.GameManager;
        if (gm != null)
            activePlayerIndex = gm.CurrentPlayerIndex;

        CancelBuildMode();
        currentMode = mode;

        // 유효 위치 하이라이트
        switch (mode)
        {
            case BuildMode.PlacingSettlement:
                var validVertices = buildingSystem.GetValidSettlementPositions(activePlayerIndex, isInitialPlacement);
                buildingVisuals.ShowValidVertices(validVertices);
                Debug.Log($"[BuildMode] 마을 건설 모드 (유효 위치: {validVertices.Count})");
                break;

            case BuildMode.PlacingRoad:
                var validEdges = buildingSystem.GetValidRoadPositions(activePlayerIndex, isInitialPlacement);
                buildingVisuals.ShowValidEdges(validEdges);
                Debug.Log($"[BuildMode] 도로 건설 모드 (유효 위치: {validEdges.Count})");
                break;

            case BuildMode.PlacingCity:
                var validCities = buildingSystem.GetValidCityUpgrades(activePlayerIndex);
                buildingVisuals.ShowValidVertices(validCities);
                Debug.Log($"[BuildMode] 도시 업그레이드 모드 (유효 위치: {validCities.Count})");
                break;
        }

        OnBuildModeChanged?.Invoke(currentMode);
    }

    /// <summary>건설 모드 취소</summary>
    public void CancelBuildMode()
    {
        if (currentMode == BuildMode.None) return;

        buildingVisuals.ClearHighlights();
        currentMode = BuildMode.None;
        OnBuildModeChanged?.Invoke(currentMode);
        Debug.Log("[BuildMode] 건설 모드 취소");
    }

    /// <summary>초기 배치 모드 설정</summary>
    public void SetInitialPlacement(bool value)
    {
        isInitialPlacement = value;
    }

    void Update()
    {
        if (currentMode == BuildMode.None) return;
        if (Mouse.current == null) return;

        // ESC 또는 우클릭으로 취소
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CancelBuildMode();
            return;
        }
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelBuildMode();
            return;
        }

        // UI 위에 있으면 무시
        if (IsPointerOverUI()) return;

        // RaycastAll로 모든 히트 수집 → 하이라이트 오브젝트 우선
        var ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        var hits = Physics.RaycastAll(ray, 100f);

        GameObject highlightHit = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var go = hit.collider.gameObject;
            bool isHighlight = buildingVisuals.GetVertexIdFromHighlight(go) >= 0
                            || buildingVisuals.GetEdgeIdFromHighlight(go) >= 0;
            if (isHighlight && hit.distance < closestDist)
            {
                highlightHit = go;
                closestDist = hit.distance;
            }
        }

        if (highlightHit != null)
        {
            buildingVisuals.SetHover(highlightHit);

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryPlace(highlightHit);
            }
        }
        else
        {
            buildingVisuals.SetHover(null);
        }
    }

    void TryPlace(GameObject hitObject)
    {
        var gm = GameServices.GameManager;

        switch (currentMode)
        {
            case BuildMode.PlacingSettlement:
            {
                int vertexId = buildingVisuals.GetVertexIdFromHighlight(hitObject);
                if (vertexId < 0) return;

                var modeBefore = currentMode;
                int playerBefore = activePlayerIndex;
                if (gm != null && gm.TryBuildSettlement(vertexId))
                {
                    var vertex = gridView.Grid.Vertices[vertexId];
                    buildingVisuals.CreateSettlement(vertex, playerBefore);
                    if (currentMode == modeBefore)
                        CancelBuildMode();
                }
                break;
            }

            case BuildMode.PlacingRoad:
            {
                int edgeId = buildingVisuals.GetEdgeIdFromHighlight(hitObject);
                if (edgeId < 0) return;

                var modeBefore = currentMode;
                int playerBefore = activePlayerIndex;
                if (gm != null && gm.TryBuildRoad(edgeId))
                {
                    var edge = gridView.Grid.Edges[edgeId];
                    buildingVisuals.CreateRoad(edge, playerBefore);
                    if (currentMode == modeBefore)
                        CancelBuildMode();
                }
                break;
            }

            case BuildMode.PlacingCity:
            {
                int vertexId = buildingVisuals.GetVertexIdFromHighlight(hitObject);
                if (vertexId < 0) return;

                var modeBefore = currentMode;
                int playerBefore = activePlayerIndex;
                if (gm != null && gm.TryBuildCity(vertexId))
                {
                    var vertex = gridView.Grid.Vertices[vertexId];
                    buildingVisuals.CreateCity(vertex, playerBefore);
                    if (currentMode == modeBefore)
                        CancelBuildMode();
                }
                break;
            }
        }
    }

    bool IsPointerOverUI()
    {
        if (uiDocument == null) return false;

        var root = uiDocument.rootVisualElement;
        if (root == null) return false;

        var mousePos = Mouse.current.position.ReadValue();
        // UI Toolkit 좌표는 Y축 반전
        var uiPos = new Vector2(mousePos.x, Screen.height - mousePos.y);
        var picked = root.panel.Pick(uiPos);

        // root 자체이거나 null이면 UI 위가 아님
        if (picked == null || picked == root) return false;

        // picking-mode: ignore 설정이 안 먹힐 경우 대비
        // 실제 배경색이 있는 UI 요소만 차단
        var el = picked;
        while (el != null && el != root)
        {
            if (el.resolvedStyle.backgroundColor.a > 0.01f)
                return true;
            el = el.parent;
        }

        return false;
    }
}
