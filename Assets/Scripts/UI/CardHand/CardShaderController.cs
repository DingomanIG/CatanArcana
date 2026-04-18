using UnityEngine;
using UnityEngine.UI;

namespace ArcanaCatan.UI.CardHand
{
    /// <summary>
    /// 카드 Image에 BalatroCard 쉐이더 머티리얼을 적용하고 런타임 제어.
    /// Inspector에서 마스크 텍스처와 파라미터를 세팅하면 자동 적용.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class CardShaderController : MonoBehaviour
    {
        [Header("Shader Material")]
        [Tooltip("UI/BalatroCard 쉐이더를 사용하는 머티리얼 (공유용)")]
        [SerializeField] private Material balatroMaterial;

        [Header("Per-Card Override")]
        [Tooltip("카드별 마스크 텍스처 (R=홀로, G=글로우). null이면 머티리얼 기본값 사용")]
        [SerializeField] private Texture2D maskOverride;

        [Header("Tilt Source")]
        [Tooltip("기울기를 읽어올 RectTransform (보통 visualContainer). null이면 자기 자신")]
        [SerializeField] private RectTransform tiltSource;

        private Image image;
        private Material instanceMaterial;

        // Shader property IDs (cached)
        private static readonly int PropMaskTex = Shader.PropertyToID("_MaskTex");
        private static readonly int PropHoloIntensity = Shader.PropertyToID("_HoloIntensity");
        private static readonly int PropGlowIntensity = Shader.PropertyToID("_GlowIntensity");
        private static readonly int PropGlowColor = Shader.PropertyToID("_GlowColor");
        private static readonly int PropTiltX = Shader.PropertyToID("_TiltX");
        private static readonly int PropTiltY = Shader.PropertyToID("_TiltY");

        private void Awake()
        {
            image = GetComponent<Image>();
            if (tiltSource == null)
                tiltSource = GetComponent<RectTransform>();
            ApplyMaterial();
        }

        private void ApplyMaterial()
        {
            if (balatroMaterial == null) return;

            // 인스턴스 머티리얼 생성 (카드별 독립 제어)
            instanceMaterial = new Material(balatroMaterial);
            image.material = instanceMaterial;

            if (maskOverride != null)
                instanceMaterial.SetTexture(PropMaskTex, maskOverride);
        }

        private void Update()
        {
            if (instanceMaterial == null || tiltSource == null) return;

            // localRotation에서 기울기 각도 추출
            Vector3 euler = tiltSource.localRotation.eulerAngles;
            // 0~360을 -180~180으로 변환
            float tiltX = euler.x > 180f ? euler.x - 360f : euler.x;
            float tiltY = euler.y > 180f ? euler.y - 360f : euler.y;

            instanceMaterial.SetFloat(PropTiltX, tiltX);
            instanceMaterial.SetFloat(PropTiltY, tiltY);
        }

        /// <summary>런타임 마스크 변경</summary>
        public void SetMask(Texture2D mask)
        {
            if (instanceMaterial == null) return;
            instanceMaterial.SetTexture(PropMaskTex, mask);
        }

        /// <summary>홀로그래픽 강도 조절 (0~2)</summary>
        public void SetHoloIntensity(float intensity)
        {
            if (instanceMaterial == null) return;
            instanceMaterial.SetFloat(PropHoloIntensity, intensity);
        }

        /// <summary>글로우 강도 조절 (0~3)</summary>
        public void SetGlowIntensity(float intensity)
        {
            if (instanceMaterial == null) return;
            instanceMaterial.SetFloat(PropGlowIntensity, intensity);
        }

        /// <summary>글로우 색상 변경</summary>
        public void SetGlowColor(Color color)
        {
            if (instanceMaterial == null) return;
            instanceMaterial.SetColor(PropGlowColor, color);
        }

        private void OnDestroy()
        {
            if (instanceMaterial != null)
                Destroy(instanceMaterial);
        }
    }
}
