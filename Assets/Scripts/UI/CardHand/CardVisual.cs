using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using MoreMountains.Feedbacks;

namespace ArcanaCatan.UI.CardHand
{
    /// <summary>
    /// 카드 비주얼 담당.
    /// 스케일/이동/회전 → DOTween, 나머지 효과(색상/파티클/사운드 등) → Feel 피드백.
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

        [Header("Hover Animation (DOTween)")]
        [SerializeField] private float hoverScale = 1.15f;
        [SerializeField] private float hoverDuration = 0.15f;
        [SerializeField] private float hoverRotationAmount = 15f;

        [Header("Feel — 추가 효과 (색상/파티클/사운드 등)")]
        [Tooltip("호버 시작 추가 효과")]
        [SerializeField] private MMF_Player hoverEnterFeedback;
        [Tooltip("호버 해제 추가 효과")]
        [SerializeField] private MMF_Player hoverExitFeedback;
        [Tooltip("선택 시 추가 효과")]
        [SerializeField] private MMF_Player selectFeedback;
        [Tooltip("선택 해제 추가 효과")]
        [SerializeField] private MMF_Player deselectFeedback;

        [Header("Feel — 발전카드 테두리")]
        [Tooltip("사용 가능할 때 테두리 효과 (루프)")]
        [SerializeField] private MMF_Player usableBorderFeedback;
        [Tooltip("사용 불가능해질 때 테두리 해제")]
        [SerializeField] private MMF_Player usableBorderStopFeedback;

        [Header("Feel — 발전카드 사용")]
        [Tooltip("사용 성공 추가 효과")]
        [SerializeField] private MMF_Player cardUsedFeedback;
        [Tooltip("사용 거부 추가 효과")]
        [SerializeField] private MMF_Player cardUseRejectedFeedback;

        [Header("Dev Card Use Button")]
        [Tooltip("발전카드 선택 시 표시되는 '사용' 버튼 (프리팹에 배치)")]
        [SerializeField] private Button useButton;

        [Header("Shadow")]
        [SerializeField] private Vector2 shadowOffset = new Vector2(5f, -10f);

        private RectTransform rectTransform;
        private bool isHovering;
        private bool isUsableBorderPlaying;
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

            if (useButton != null)
            {
                useButton.gameObject.SetActive(false);
                useButton.onClick.AddListener(OnUseButtonClicked);
            }
        }

        private void OnEnable()
        {
            if (baseCard == null) return;
            UnsubscribeEvents();
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
            if (baseCard != null) UnsubscribeEvents();
            baseCard = card;
            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            baseCard.OnHoverEnter += HandleHoverEnter;
            baseCard.OnHoverExit += HandleHoverExit;
            baseCard.OnSelect += HandleSelect;
            baseCard.OnDeselect += HandleDeselect;
            baseCard.OnCardUsed += HandleCardUsed;
            baseCard.OnCardUseRejected += HandleCardUseRejected;
        }

        private void UnsubscribeEvents()
        {
            baseCard.OnHoverEnter -= HandleHoverEnter;
            baseCard.OnHoverExit -= HandleHoverExit;
            baseCard.OnSelect -= HandleSelect;
            baseCard.OnDeselect -= HandleDeselect;
            baseCard.OnCardUsed -= HandleCardUsed;
            baseCard.OnCardUseRejected -= HandleCardUseRejected;
        }

        private void Update()
        {
            // Idle 회전 (호버 아닐 때)
            if (!isHovering)
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
            if (isHovering)
            {
                Vector3 mousePos = Pointer.current != null ? (Vector3)Pointer.current.position.ReadValue() : Vector3.zero;
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
                shadowImage.rectTransform.localPosition = shadowOffset;

            // 발전카드 사용 가능 테두리 효과
            UpdateUsableBorder();
        }

        // === Usable Border ===

        private void UpdateUsableBorder()
        {
            if (usableBorderFeedback == null) return;
            if (baseCard?.CardData?.Category != CardCategory.Development)
            {
                StopUsableBorder();
                return;
            }

            var gm = GameServices.GameManager;
            bool usable = gm != null
                && gm.IsMyTurn()
                && gm.CurrentPhase == GamePhase.Action
                && baseCard.CardData.CanUseOnTurn(gm.TurnNumber);

            if (usable && !isUsableBorderPlaying)
            {
                isUsableBorderPlaying = true;
                usableBorderFeedback.PlayFeedbacks();
            }
            else if (!usable && isUsableBorderPlaying)
            {
                StopUsableBorder();
            }
        }

        private void StopUsableBorder()
        {
            if (!isUsableBorderPlaying) return;
            isUsableBorderPlaying = false;
            usableBorderFeedback?.StopFeedbacks();
            usableBorderStopFeedback?.PlayFeedbacks();
        }

        // === Animation Handlers ===

        private void HandleHoverEnter()
        {
            isHovering = true;
            // DOTween: 스케일
            currentScaleTween?.Kill();
            currentScaleTween = rectTransform.DOScale(hoverScale, hoverDuration)
                .SetEase(Ease.OutBack).SetId(tweenId).SetAutoKill(true);
            // Feel: 추가 효과 (shake 등은 여기서)
            hoverEnterFeedback?.PlayFeedbacks();
        }

        private void HandleHoverExit()
        {
            isHovering = false;
            // DOTween: 스케일 복구
            currentScaleTween?.Kill();
            currentScaleTween = rectTransform.DOScale(1f, hoverDuration)
                .SetEase(Ease.OutQuad).SetId(tweenId).SetAutoKill(true);
            visualContainer.DOLocalRotate(Vector3.zero, 0.2f).SetId(tweenId);
            // Feel: 추가 효과
            hoverExitFeedback?.PlayFeedbacks();
        }

        private void HandleSelect()
        {
            // Feel: 추가 효과
            selectFeedback?.PlayFeedbacks();
            ShowUseButton(true);
        }

        private void HandleDeselect()
        {
            ShowUseButton(false);
            // Feel: 추가 효과
            deselectFeedback?.PlayFeedbacks();
        }

        private void HandleCardUsed()
        {
            ShowUseButton(false);
            StopUsableBorder();
            // Feel: 추가 효과
            cardUsedFeedback?.PlayFeedbacks();
        }

        private void HandleCardUseRejected()
        {
            // DOTween: 좌우 흔들림
            rectTransform.DOShakeAnchorPos(0.4f, new Vector2(20f, 0), 12, 90f, false, true)
                .SetEase(Ease.OutQuad).SetId(tweenId);
            rectTransform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetId(tweenId);
            // Feel: 추가 효과
            cardUseRejectedFeedback?.PlayFeedbacks();
        }

        // === Use Button ===

        private void ShowUseButton(bool show)
        {
            if (useButton == null) return;
            if (show && baseCard?.CardData?.Category != CardCategory.Development)
                return;
            useButton.gameObject.SetActive(show);
        }

        private void OnUseButtonClicked()
        {
            if (baseCard == null) return;
            var manager = baseCard.GetComponentInParent<CardHandManager>();
            manager?.TryUseSelectedDevCard();
        }
    }
}
