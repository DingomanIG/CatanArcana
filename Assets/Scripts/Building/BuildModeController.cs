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
        gridView = FindFirstObjectByType<HexGridView>();
        buildingVisuals = GetComponent<BuildingVisuals>();
        if (buildingVisuals == null)
            buildingVisuals = gameObject.AddComponent<BuildingVisuals>();

        uiDocument = FindFirstObjectByType<UIDocument>();

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

        // 모든 배치(인간+AI)에 비주얼 생성
        SubscribeToGameManager();
    }

    bool subscribedToGM;

    void SubscribeToGameManager()
    {
        if (subscribedToGM) return;
        var gm = GameServices.GameManager;
        if (gm != null)
        {
            gm.OnBuildingPlaced += OnBuildingPlacedEvent;
            gm.OnRoadPlaced += OnRoadPlacedEvent;
            subscribedToGM = true;
        }
    }

    // (Update는 아래 기존 메서드에 통합)

    void OnDestroy()
    {
        var gm = GameServices.GameManager;
        if (gm != null)
        {
            gm.OnBuildingPlaced -= OnBuildingPlacedEvent;
            gm.OnRoadPlaced -= OnRoadPlacedEvent;
        }
    }

    void OnBuildingPlacedEvent(int playerIndex, int vertexId, BuildingType type)
    {
        if (gridView == null || buildingVisuals == null) return;
        var vertex = gridView.Grid.Vertices[vertexId];
        if (type == BuildingType.Settlement)
            buildingVisuals.CreateSettlement(vertex, playerIndex);
        else if (type == BuildingType.City)
            buildingVisuals.CreateCity(vertex, playerIndex);
    }

    void OnRoadPlacedEvent(int playerIndex, int edgeId)
    {
        if (gridView == null || buildingVisuals == null) return;
        var edge = gridView.Grid.Edges[edgeId];
        buildingVisuals.CreateRoad(edge, playerIndex);
    }

    static void SetPickingIgnore(VisualElement root, string name)
    {
        var el = root.Q(name);
        if (el != null) el.pickingMode = PickingMode.Ignore;
    }

    /// <summary>건설 모드 진입</summary>
    public void EnterBuildMode(BuildMode mode)
    {
        // BUG-1: 네트워크 모드에서 보드 스냅샷 도착 전 Start()가 실행되면
        // buildingSystem이 null일 수 있음 → 지연 초기화
        if (buildingSystem == null && gridView != null && gridView.Grid != null)
        {
            buildingSystem = new BuildingSystem(gridView.Grid);
            Debug.Log("[BuildMode] 건설 시스템 지연 초기화");
        }
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

    /// <summary>BUG-1: 보드 재구축 후 buildingSystem 갱신 (네트워크 스냅샷 적용 시)</summary>
    public void RefreshBuildingSystem(HexGrid grid)
    {
        if (grid != null)
        {
            buildingSystem = new BuildingSystem(grid);
            Debug.Log("[BuildMode] 건설 시스템 갱신 (보드 스냅샷 적용)");
        }
    }

    /// <summary>초기 배치 모드 설정</summary>
    public void SetInitialPlacement(bool value)
    {
        isInitialPlacement = value;
    }

    void Update()
    {
        // GameManager가 늦게 등록될 수 있음 (네트워크 모드)
        if (!subscribedToGM) SubscribeToGameManager();

        if (Mouse.current == null) return;

        // 도적 이동 모드 (주사위 7 또는 기사 카드)
        var gm = GameServices.GameManager;
        if (gm != null && (gm.CurrentPhase == GamePhase.MoveRobber
                        || gm.DevCardState == DevCardUseState.SelectingKnightTarget))
        {
            HandleRobberPlacement(gm);
            return;
        }

        if (currentMode == BuildMode.None) return;

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

        // 화면 좌표 거리 기반 하이라이트 감지 (콜라이더/해상도 무관)
        var mousePos = Mouse.current.position.ReadValue();
        var highlightHit = buildingVisuals.FindClosestHighlight(mousePos, Camera.main);

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
                bool result = gm != null && gm.TryBuildSettlement(vertexId);
                if (result)
                {
                    // 로컬: 즉시 성공 → 모드 정리
                    if (currentMode == modeBefore)
                        CancelBuildMode();
                }
                else if (gm != null && !gm.IsHost)
                {
                    // 네트워크 클라이언트: ServerRpc 전송됨, 결과는 ClientRpc로 수신
                    // 클라이언트 예측: 즉시 로컬 비주얼 표시 (서버 응답 시 중복 체크로 스킵됨)
                    PredictBuildingVisual(vertexId, BuildingType.Settlement);
                    CancelBuildMode();
                }
                else
                    SFXManager.Instance?.Play(SFXType.BuildFailed);
                break;
            }

            case BuildMode.PlacingRoad:
            {
                int edgeId = buildingVisuals.GetEdgeIdFromHighlight(hitObject);
                if (edgeId < 0) return;

                var modeBefore = currentMode;
                bool result = gm != null && gm.TryBuildRoad(edgeId);
                if (result)
                {
                    if (currentMode == modeBefore)
                        CancelBuildMode();
                }
                else if (gm != null && !gm.IsHost)
                {
                    PredictRoadVisual(edgeId);
                    CancelBuildMode();
                }
                else
                    SFXManager.Instance?.Play(SFXType.BuildFailed);
                break;
            }

            case BuildMode.PlacingCity:
            {
                int vertexId = buildingVisuals.GetVertexIdFromHighlight(hitObject);
                if (vertexId < 0) return;

                var modeBefore = currentMode;
                bool cityResult = gm != null && gm.TryBuildCity(vertexId);
                if (cityResult)
                {
                    if (currentMode == modeBefore)
                        CancelBuildMode();
                }
                else if (gm != null && !gm.IsHost)
                {
                    PredictBuildingVisual(vertexId, BuildingType.City);
                    CancelBuildMode();
                }
                else
                    SFXManager.Instance?.Play(SFXType.BuildFailed);
                break;
            }
        }
    }

    /// <summary>도적 이동: 타일 클릭으로 도적 배치</summary>
    void HandleRobberPlacement(IGameManager gm)
    {
        if (IsPointerOverUI()) return;

        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        var ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        var hits = Physics.RaycastAll(ray, 100f);

        foreach (var hit in hits)
        {
            var go = hit.collider.gameObject;
            if (gridView != null && gridView.TryGetTileCoord(go, out var coord))
            {
                if (gm.DevCardState == DevCardUseState.SelectingKnightTarget)
                    gm.TryUseKnight(coord);
                else
                    gm.TryMoveRobber(coord);
                return;
            }
        }
    }

    /// <summary>클라이언트 예측: 서버 응답 전 즉시 건물 비주얼 표시</summary>
    void PredictBuildingVisual(int vertexId, BuildingType type)
    {
        if (gridView?.Grid == null || buildingVisuals == null) return;
        var verts = gridView.Grid.Vertices;
        if (vertexId < 0 || vertexId >= verts.Count) return;
        var vertex = verts[vertexId];

        int pi = GetLocalPlayerIndex();
        if (type == BuildingType.Settlement)
            buildingVisuals.CreateSettlement(vertex, pi);
        else if (type == BuildingType.City)
            buildingVisuals.CreateCity(vertex, pi);
    }

    /// <summary>클라이언트 예측: 서버 응답 전 즉시 도로 비주얼 표시</summary>
    void PredictRoadVisual(int edgeId)
    {
        if (gridView?.Grid == null || buildingVisuals == null) return;
        var edges = gridView.Grid.Edges;
        if (edgeId < 0 || edgeId >= edges.Count) return;

        buildingVisuals.CreateRoad(edges[edgeId], GetLocalPlayerIndex());
    }

    int GetLocalPlayerIndex()
    {
        var gm = GameServices.GameManager;
        return gm != null ? gm.LocalPlayerIndex : activePlayerIndex;
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
