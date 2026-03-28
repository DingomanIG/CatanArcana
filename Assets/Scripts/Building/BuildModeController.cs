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

        if (gridView != null && gridView.Grid != null)
        {
            buildingSystem = new BuildingSystem(gridView.Grid);
            Debug.Log("[BuildMode] 건설 시스템 초기화 완료");
        }
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

        // 레이캐스트
        var ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            // 호버 하이라이트
            buildingVisuals.SetHover(hit.collider.gameObject);

            // 좌클릭: 배치
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryPlace(hit.collider.gameObject);
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

                if (gm != null && gm.TryBuildSettlement(vertexId))
                {
                    var vertex = gridView.Grid.Vertices[vertexId];
                    buildingVisuals.CreateSettlement(vertex, activePlayerIndex);
                    CancelBuildMode();
                }
                break;
            }

            case BuildMode.PlacingRoad:
            {
                int edgeId = buildingVisuals.GetEdgeIdFromHighlight(hitObject);
                if (edgeId < 0) return;

                if (gm != null && gm.TryBuildRoad(edgeId))
                {
                    var edge = gridView.Grid.Edges[edgeId];
                    buildingVisuals.CreateRoad(edge, activePlayerIndex);
                    CancelBuildMode();
                }
                break;
            }

            case BuildMode.PlacingCity:
            {
                int vertexId = buildingVisuals.GetVertexIdFromHighlight(hitObject);
                if (vertexId < 0) return;

                if (gm != null && gm.TryBuildCity(vertexId))
                {
                    var vertex = gridView.Grid.Vertices[vertexId];
                    buildingVisuals.CreateCity(vertex, activePlayerIndex);
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

        // root 자체가 아닌 실제 UI 요소를 클릭했는지
        return picked != null && picked != root;
    }
}
