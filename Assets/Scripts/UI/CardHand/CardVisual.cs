using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using MoreMountains.Feedbacks;

namespace ArcanaCatan.UI.CardHand
{
    /// <summary>
    /// 카드 비주얼 담당.
    /// 호버/선택 애니메이션, 카테고리 테두리 색상, 디스카드 빨간 테두리.
    /// </summary>
    public class CardVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BaseCard baseCard;
        [SerializeField] private Image cardImage;
        [SerializeField] private Image shadowImage;
        [SerializeField] private RectTransform visualContainer;


        [Header("Idle Animation")]
        [SerializeField] private float idleRotationAmount = 3f;
        [SerializeField] private float idleSpeed = 1f;

        [Header("Hover Animation")]
        [SerializeField] private float hoverScale = 1.15f;
        [SerializeField] private float hoverDuration = 0.15f;
        [SerializeField] private float hoverRotationAmount = 15f;

        [Header("Select Animation (Feel)")]
        [SerializeField] private MMF_Player selectFeedback;
        [SerializeField] private MMF_Player deselectFeedback;

        [Header("Shadow")]
        [SerializeField] private Vector2 shadowOffset = new Vector2(5f, -10f);
        [SerializeField] private float shadowDragOffset = -30f;


        private RectTransform rectTransform;
        private bool isHovering;
        private bool isDragging;
        private float idleTimer;
        private Tween currentScaleTween;
        private int tweenId;


        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (visualContainer == null)
                visualContainer = rectTransform;
            tweenId = GetInstanceID();
            idleTimer = Random.Range(0f, Mathf.PI * 2f);
        }

        private void OnEnable()
        {
            if (baseCard == null) return;
            SubscribeEvents();
        }

        private void OnDisable()
        {
            if (baseCard == null) return;
            UnsubscribeEvents();
            DOTween.Kill(tweenId);
        }

        public void Initialize(BaseCard card)
        {
            baseCard = card;
            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            baseCard.OnHoverEnter += HandleHoverEnter;
            baseCard.OnHoverExit += HandleHoverExit;
            baseCard.OnSelect += HandleSelect;
            baseCard.OnDeselect += HandleDeselect;
            baseCard.OnDragStart += HandleDragStart;
            baseCard.OnDragEnd += HandleDragEnd;
            baseCard.OnCardUseRejected += HandleCardUseRejected;
        }

        private void UnsubscribeEvents()
        {
            baseCard.OnHoverEnter -= HandleHoverEnter;
            baseCard.OnHoverExit -= HandleHoverExit;
            baseCard.OnSelect -= HandleSelect;
            baseCard.OnDeselect -= HandleDeselect;
            baseCard.OnDragStart -= HandleDragStart;
            baseCard.OnDragEnd -= HandleDragEnd;
            baseCard.OnCardUseRejected -= HandleCardUseRejected;
        }

        private void Update()
        {
            // Idle 회전 (호버/드래그 아닐 때)
            if (!isHovering && !isDragging)
            {
                idleTimer += Time.deltaTime * idleSpeed;
                float rotX = Mathf.Sin(idleTimer) * idleRotationAmount;
                float rotY = Mathf.Cos(idleTimer) * idleRotationAmount;
                visualContainer.localRotation = Quaternion.Slerp(
                    visualContainer.localRotation,
                    Quaternion.Euler(rotX, rotY, 0f),
                    Time.deltaTime * 5f);
            }

            // 호버 시 마우스 방향으로 기울기
            if (isHovering && !isDragging)
            {
                Vector3 mousePos = Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
                Vector3 cardScreenPos = RectTransformUtility.WorldToScreenPoint(null, rectTransform.position);
                Vector2 diff = (Vector2)(mousePos - cardScreenPos);
                float rotY = Mathf.Clamp(diff.x / Screen.width * hoverRotationAmount, -hoverRotationAmount, hoverRotationAmount);
                float rotX = Mathf.Clamp(-diff.y / Screen.height * hoverRotationAmount, -hoverRotationAmount, hoverRotationAmount);
                visualContainer.localRotation = Quaternion.Slerp(
                    visualContainer.localRotation,
                    Quaternion.Euler(rotX, rotY, 0f),
                    Time.deltaTime * 8f);
            }

            // Shadow
            if (shadowImage != null)
            {
                float yOffset = isDragging ? shadowDragOffset : shadowOffset.y;
                shadowImage.rectTransform.localPosition = new Vector2(shadowOffset.x, yOffset);
            }
        }

        // === Animation Handlers ===

        private void HandleHoverEnter()
        {
            isHovering = true;
            currentScaleTween?.Kill();
            currentScaleTween = rectTransform.DOScale(hoverScale, hoverDuration)
                .SetEase(Ease.OutBack).SetId(tweenId).SetAutoKill(true);

            visualContainer.DOShakeRotation(0.3f, new Vector3(0, 0, 3f), 10, 90f, false)
                .SetEase(Ease.OutQuad).SetId(tweenId);
        }

        private void HandleHoverExit()
        {
            isHovering = false;
            currentScaleTween?.Kill();
            currentScaleTween = rectTransform.DOScale(1f, hoverDuration)
                .SetEase(Ease.OutQuad).SetId(tweenId).SetAutoKill(true);
            visualContainer.DOLocalRotate(Vector3.zero, 0.2f).SetId(tweenId);
        }

        private void HandleSelect()
        {
            selectFeedback?.PlayFeedbacks();
        }

        private void HandleDeselect()
        {
            if (deselectFeedback != null)
                deselectFeedback.PlayFeedbacks();
            else
                rectTransform.DOScale(isHovering ? hoverScale : 1f, 0.15f).SetId(tweenId);
        }

        private void HandleDragStart()
        {
            isDragging = true;
            rectTransform.DOScale(1.1f, 0.1f).SetId(tweenId);
        }

        private void HandleDragEnd()
        {
            isDragging = false;
            rectTransform.DOScale(isHovering ? hoverScale : 1f, 0.2f)
                .SetEase(Ease.OutBack).SetId(tweenId);
        }

        /// <summary>사용 불가 — 좌우 흔들림 후 핸드로 복귀</summary>
        private void HandleCardUseRejected()
        {
            isDragging = false;
            rectTransform.DOShakeAnchorPos(0.4f, new Vector2(20f, 0), 12, 90f, false, true)
                .SetEase(Ease.OutQuad).SetId(tweenId);
            rectTransform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetId(tweenId);
        }
    }
}
