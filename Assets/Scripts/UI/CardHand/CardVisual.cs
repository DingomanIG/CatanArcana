using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using System;
using System.Collections.Generic;

namespace ArcanaCatan.UI.CardHand
{
    /// <summary>
    /// 카드 비주얼 담당.
    /// 호버/선택 애니메이션, 팬 스택 서브카드, 수량 배지, 카테고리 테두리 색상.
    /// </summary>
    public class CardVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BaseCard baseCard;
        [SerializeField] private Image cardImage;
        [SerializeField] private Image shadowImage;
        [SerializeField] private RectTransform visualContainer;

        [Header("Border")]
        [SerializeField] private Image borderImage;

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

        [Header("Fan Stack")]
        [SerializeField] private float fanOffsetX = 5f;
        [SerializeField] private float fanOffsetY = 0f;
        [SerializeField] private int maxFanCards = 5;
        [SerializeField] private Vector2 fanBorderSize = new Vector2(124, 184);
        [SerializeField] private Vector2 fanBgSize = new Vector2(116, 176);
        [SerializeField] private Color fanBgColor = new Color(0.15f, 0.15f, 0.2f, 0.95f);

        [Header("Category Border Colors")]
        [SerializeField] private Color resourceBorderColor = new Color(0.3f, 0.7f, 0.35f, 1f);
        [SerializeField] private Color developmentBorderColor = new Color(0.55f, 0.3f, 0.75f, 1f);
        [SerializeField] private Color bonusBorderColor = new Color(0.9f, 0.75f, 0.3f, 1f);

        private RectTransform rectTransform;
        private bool isHovering;
        private bool isDragging;
        private float idleTimer;
        private Tween currentScaleTween;

        // Fan stack
        private List<GameObject> fanCards = new List<GameObject>();
        private int currentFanCount;
        private GameObject badgeObj;
        private Text badgeText;

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
            ApplyCategoryBorderColor();
            UpdateFanVisuals(card.StackCount);
        }

        private void SubscribeEvents()
        {
            baseCard.OnHoverEnter += HandleHoverEnter;
            baseCard.OnHoverExit += HandleHoverExit;
            baseCard.OnSelect += HandleSelect;
            baseCard.OnDeselect += HandleDeselect;
            baseCard.OnDragStart += HandleDragStart;
            baseCard.OnDragEnd += HandleDragEnd;
            baseCard.OnStackCountChanged += HandleStackCountChanged;
        }

        private void UnsubscribeEvents()
        {
            baseCard.OnHoverEnter -= HandleHoverEnter;
            baseCard.OnHoverExit -= HandleHoverExit;
            baseCard.OnSelect -= HandleSelect;
            baseCard.OnDeselect -= HandleDeselect;
            baseCard.OnDragStart -= HandleDragStart;
            baseCard.OnDragEnd -= HandleDragEnd;
            baseCard.OnStackCountChanged -= HandleStackCountChanged;
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

            // Shadow (팬 스택 오프셋 반영)
            if (shadowImage != null)
            {
                float fanExtraX = currentFanCount * fanOffsetX;
                float fanExtraY = currentFanCount * fanOffsetY;
                float yOffset = isDragging ? shadowDragOffset : shadowOffset.y;
                shadowImage.rectTransform.localPosition = new Vector2(
                    shadowOffset.x + fanExtraX,
                    yOffset + fanExtraY);
            }
        }

        // === Category Border ===

        private void ApplyCategoryBorderColor()
        {
            if (borderImage == null)
            {
                // Border Image 자동 탐색
                if (visualContainer != null)
                {
                    Transform borderTf = visualContainer.Find("Border");
                    if (borderTf != null)
                        borderImage = borderTf.GetComponent<Image>();
                }
            }

            if (borderImage == null || baseCard.CardData == null) return;

            borderImage.color = baseCard.CardData.Category switch
            {
                CardCategory.Resource => resourceBorderColor,
                CardCategory.Development => developmentBorderColor,
                CardCategory.Bonus => bonusBorderColor,
                _ => borderImage.color
            };
        }

        // === Fan Stack Visuals ===

        private void HandleStackCountChanged(int count)
        {
            UpdateFanVisuals(count);
        }

        private void UpdateFanVisuals(int count)
        {
            // 기존 서브카드 제거
            foreach (var fc in fanCards)
            {
                if (fc != null) Destroy(fc);
            }
            fanCards.Clear();

            // 서브카드 생성 (뒤에 겹침)
            int subCount = Mathf.Min(count - 1, maxFanCards);
            currentFanCount = subCount;
            for (int i = 0; i < subCount; i++)
            {
                var sub = new GameObject($"FanCard_{i}");
                sub.transform.SetParent(transform, false);
                sub.transform.SetAsFirstSibling();

                var rt = sub.AddComponent<RectTransform>();
                rt.sizeDelta = rectTransform.sizeDelta;
                rt.anchoredPosition = new Vector2(
                    (i + 1) * fanOffsetX,
                    (i + 1) * fanOffsetY
                );

                // 테두리
                var borderSub = new GameObject("Border");
                borderSub.transform.SetParent(sub.transform, false);
                var borderRT = borderSub.AddComponent<RectTransform>();
                borderRT.sizeDelta = fanBorderSize;
                var borderImg = borderSub.AddComponent<Image>();
                if (baseCard.CardData != null)
                {
                    borderImg.color = baseCard.CardData.Category switch
                    {
                        CardCategory.Resource => resourceBorderColor,
                        CardCategory.Development => developmentBorderColor,
                        CardCategory.Bonus => bonusBorderColor,
                        _ => new Color(0.9f, 0.75f, 0.3f, 1f)
                    };
                }
                borderImg.raycastTarget = false;

                // 배경
                var bgSub = new GameObject("Background");
                bgSub.transform.SetParent(sub.transform, false);
                var bgRT = bgSub.AddComponent<RectTransform>();
                bgRT.sizeDelta = fanBgSize;
                var bgImg = bgSub.AddComponent<Image>();
                bgImg.color = fanBgColor;
                bgImg.raycastTarget = false;

                fanCards.Add(sub);
            }

            // Shadow를 항상 맨 뒤로
            if (shadowImage != null)
                shadowImage.transform.SetAsFirstSibling();

            // 배지 업데이트
            UpdateBadge(count);
        }

        private void UpdateBadge(int count)
        {
            if (count <= 1)
            {
                if (badgeObj != null)
                    badgeObj.SetActive(false);
                return;
            }

            if (badgeObj == null)
                CreateBadge();

            badgeObj.SetActive(true);
            badgeText.text = $"x{count}";
        }

        private void CreateBadge()
        {
            badgeObj = new GameObject("StackBadge");
            badgeObj.transform.SetParent(visualContainer != null ? visualContainer : transform, false);

            // 배경 원
            var bgImg = badgeObj.AddComponent<Image>();
            bgImg.color = new Color(0.9f, 0.2f, 0.2f, 1f);
            bgImg.raycastTarget = false;

            var rt = badgeObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(32, 24);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(-5f, 5f);

            // 텍스트
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(badgeObj.transform, false);
            var textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            badgeText = textObj.AddComponent<Text>();
            badgeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            badgeText.fontSize = 14;
            badgeText.fontStyle = FontStyle.Bold;
            badgeText.color = Color.white;
            badgeText.alignment = TextAnchor.MiddleCenter;
            badgeText.raycastTarget = false;
        }

        // === Animation Handlers ===

        private void HandleHoverEnter()
        {
            isHovering = true;
            currentScaleTween?.Kill();
            currentScaleTween = rectTransform.DOScale(hoverScale, hoverDuration).SetEase(Ease.OutBack);

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
