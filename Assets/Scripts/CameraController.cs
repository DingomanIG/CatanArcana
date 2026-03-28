using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Follow Target")]
    [Tooltip("카메라가 따라갈 대상 (플레이어)")]
    public Transform target;

    [Header("Camera Settings")]
    [Tooltip("카메라 오프셋 (타겟 기준 상대 위치)")]
    public Vector3 offset = new Vector3(0f, 12f, -8f);

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
    public float zoomSpeed = 2f;

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = zoomLevel;
    }

    void LateUpdate()
    {
        HandleZoom();
        FollowTarget();
    }

    void FollowTarget()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            zoomLevel -= scroll * zoomSpeed;
            zoomLevel = Mathf.Clamp(zoomLevel, minZoom, maxZoom);
            cam.orthographicSize = zoomLevel;
        }
    }
}
