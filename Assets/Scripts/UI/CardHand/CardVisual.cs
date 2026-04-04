using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using System;

namespace ArcanaCatan.UI.CardHand
{
    /// <summary>
    /// 카드 비주얼 담당.
    /// BaseCard의 위치를 Lerp로 부드럽게 따라가며, 호버/선택 시 DOTween 애니메이션.
    /// </summary>
    public class CardVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BaseCard baseCard;
        [SerializeField] private Image cardImage;
        [SerializeField] private Image shadowImage;
        [SerializeField] private RectTransform visualContainer; // 회전 효과용 자식

        [Header("Follow")]
        [SerializeField] private float followSpeed = 15f;

        [Header("Idle Animation")]
        [SerializeField] private float idleRotationAmount = 3f;
        [SerializeField] private float idleSpeed = 1f;

        [Header("Hover Animation")]
        [SerializeField] private float hoverScale = 1.15f;
        [SerializeField] private float hoverDuration = 0.15f;
        [SerializeField] private float hoverRotationAmount = 15f;

        [Header("Select Animation")]
        [SerializeField] private float selectPunchScale = 0.1f;
        [SerializeField] private float selectDuration = 0.2f;

        [Header("Shadow")]
        [SerializeField] private Vector2 shadowOffset = new Vector2(5f, -10f);
        [SerializeField] private float shadowDragOffset = -30f;

        private RectTransform rectTransform;
        private Vector3 targetPosition;
        private bool isHovering;
        private bool isDragging;
        private float idleTimer;
        private Tween currentScaleTween;
        private Tween currentRotationTween;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (visualContainer == null)
                visualContainer = rectTransform;
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
            DOTween.Kill(transform);
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
        }

        private void UnsubscribeEvents()
        {
            baseCard.OnHoverEnter -= HandleHoverEnter;
            baseCard.OnHoverExit -= HandleHoverExit;
            baseCard.OnSelect -= HandleSelect;
            baseCard.OnDeselect -= HandleDeselect;
            baseCard.OnDragStart -= HandleDragStart;
            baseCard.OnDragEnd -= HandleDragEnd;
        }

        private void Update()
        {
            // Idle 회전 (호버/드래그 아닐 때) — visualContainer만 회전
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

        private void HandleHoverEnter()
        {
            isHovering = true;
            currentScaleTween?.Kill();
            currentScaleTween = rectTransform.DOScale(hoverScale, hoverDuration).SetEase(Ease.OutBack);

            // Shake 연출
            visualContainer.DOShakeRotation(0.3f, new Vector3(0, 0, 3f), 10, 90f, false)
                .SetEase(Ease.OutQuad);
        }

        private void HandleHoverExit()
        {
            isHovering = false;
            currentScaleTween?.Kill();
            currentScaleTween = rectTransform.DOScale(1f, hoverDuration).SetEase(Ease.OutQuad);
            visualContainer.DOLocalRotate(Vector3.zero, 0.2f);
        }

        private void HandleSelect()
        {
            rectTransform.DOPunchScale(Vector3.one * selectPunchScale, selectDuration, 6, 0.5f);
        }

        private void HandleDeselect()
        {
            rectTransform.DOScale(isHovering ? hoverScale : 1f, 0.15f);
        }

        private void HandleDragStart()
        {
            isDragging = true;
            rectTransform.DOScale(1.1f, 0.1f);
        }

        private void HandleDragEnd()
        {
            isDragging = false;
            rectTransform.DOScale(isHovering ? hoverScale : 1f, 0.2f).SetEase(Ease.OutBack);
        }
    }
}
