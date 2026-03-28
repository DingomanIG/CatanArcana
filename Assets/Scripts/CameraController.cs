using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Follow Target")]
    [Tooltip("카메라가 따라갈 대상 (플레이어)")]
    public Transform target;

    [Header("Camera Settings")]
    [Tooltip("카메라 오프셋 (타겟 기준 상대 위치)")]
    public Vector3 offset = new Vector3(0f, 12f, -12f);

    [Tooltip("카메라 추적 부드러움 (낮을수록 즉각 반응)")]
    [Range(0.01f, 1f)]
    public float smoothSpeed = 0.125f;

    [Header("Zoom")]
    [Tooltip("현재 Orthographic Size")]
    public float zoomLevel = 8f;

    [Tooltip("최소 줌 (가까이)")]
    public float minZoom = 4f;

    [Tooltip("최대 줌 (멀리)")]
    public float maxZoom = 14f;

    [Tooltip("줌 속도")]
    public float zoomSpeed = 30f;

    [Header("Pan (미들 마우스)")]
    [Tooltip("팬 이동 속도")]
    public float panSpeed = 0.5f;

    private Camera cam;
    private bool isPanning;
    private Vector3 lastMouseWorldPos;

    void Start()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = zoomLevel;
        transform.position = offset;
    }

    void LateUpdate()
    {
        HandleZoom();
        HandlePan();
        FollowTarget();
    }

    void FollowTarget()
    {
        if (target == null || isPanning) return;

        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
    }

    void HandleZoom()
    {
        if (Mouse.current == null) return;
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll != 0f)
        {
            zoomLevel -= scroll * zoomSpeed * 0.01f;
            zoomLevel = Mathf.Clamp(zoomLevel, minZoom, maxZoom);
            cam.orthographicSize = zoomLevel;
        }
    }

    void HandlePan()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
            isPanning = true;
            lastMouseWorldPos = GetMouseWorldPosition();
        }

        if (Mouse.current.middleButton.isPressed && isPanning)
        {
            Vector3 currentMouseWorldPos = GetMouseWorldPosition();
            Vector3 delta = lastMouseWorldPos - currentMouseWorldPos;
            transform.position += delta;
            lastMouseWorldPos = GetMouseWorldPosition();
        }

        if (Mouse.current.middleButton.wasReleasedThisFrame)
        {
            isPanning = false;
        }
    }

    Vector3 GetMouseWorldPosition()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, cam.nearClipPlane));
        worldPos.y = transform.position.y;
        return worldPos;
    }
}
