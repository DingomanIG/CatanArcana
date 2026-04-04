using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using System.Collections.Generic;

namespace ArcanaCatan.UI.CardHand
{
    /// <summary>
    /// 카드 핸드(손패) 관리.
    /// 부채꼴 배열, 카드 추가/제거, 호버 감지, 드래그 스왑.
    /// 호버는 매니저가 Update에서 직접 판별 (겹침 문제 해결).
    /// </summary>
    public class CardHandManager : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject cardPrefab;

        [Header("Layout")]
        [SerializeField] private RectTransform cardContainer;
        [SerializeField] private float cardWidth = 120f;
        [SerializeField] private float cardSpacing = -30f; // 카드 겹침 (음수)

        [Header("Hand Curve (부채꼴)")]
        [SerializeField] private AnimationCurve curveY = AnimationCurve.EaseInOut(0f, -20f, 1f, -20f);
        [SerializeField] private AnimationCurve curveRotation = AnimationCurve.Linear(0f, 5f, 1f, -5f);
        [SerializeField] private float curveYMultiplier = 1f;
        [SerializeField] private float curveRotationMultiplier = 1f;

        [Header("Animation")]
        [SerializeField] private float dealDuration = 0.3f;
        [SerializeField] private float rearrangeDuration = 0.2f;

        private List<BaseCard> cards = new List<BaseCard>();
        private bool isDirty;
        private BaseCard currentHoveredCard;
        private Canvas rootCanvas;
        private Camera canvasCamera;

        private void Awake()
        {
            // LayoutGroup 비활성화 — 위치를 직접 계산
            var layoutGroup = GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup != null)
                layoutGroup.enabled = false;

            // ContentSizeFitter도 비활성화
            var csf = GetComponent<ContentSizeFitter>();
            if (csf != null)
                csf.enabled = false;

            // 커브가 비어있을 때만 기본값 설정
            if (curveY.keys.Length == 0)
            {
                curveY = new AnimationCurve(
                    new Keyframe(0f, -15f),
                    new Keyframe(0.5f, 0f),
                    new Keyframe(1f, -15f));
            }
            if (curveRotation.keys.Length == 0)
            {
                curveRotation = new AnimationCurve(
                    new Keyframe(0f, 8f),
                    new Keyframe(0.5f, 0f),
                    new Keyframe(1f, -8f));
            }
        }

        private void Start()
        {
            rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
            canvasCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : rootCanvas.worldCamera;
        }

        private void Update()
        {
            UpdateHover();
        }

        private void LateUpdate()
        {
            if (isDirty)
            {
                ApplyHandCurve();
                isDirty = false;
            }
        }

        /// <summary>
        /// 매 프레임 마우스 위치로 최상위 카드 판별하여 호버 처리.
        /// 오른쪽(나중) 카드가 위에 렌더링되므로, 역순으로 검사하여 첫 히트 = 최상위.
        /// </summary>
        private void UpdateHover()
        {
            if (Mouse.current == null) return;

            // 드래그 중이면 호버 처리 안 함
            if (cards.Exists(c => c.IsDragging))
            {
                if (currentHoveredCard != null)
                {
                    currentHoveredCard.SetHover(false);
                    SetCardSortingOverride(currentHoveredCard, false);
                    currentHoveredCard = null;
                }
                return;
            }

            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

            BaseCard topCard = null;

            // 역순 검사: 오른쪽(높은 인덱스) 카드가 위에 렌더링됨
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                if (cards[i].IsDragging) continue;

                RectTransform rt = cards[i].RectTransform;
                if (RectTransformUtility.RectangleContainsScreenPoint(rt, mouseScreenPos, canvasCamera))
                {
                    topCard = cards[i];
                    break;
                }
            }

            // 호버 상태 변경
            if (topCard != currentHoveredCard)
            {
                // 이전 카드 호버 해제
                if (currentHoveredCard != null)
                {
                    currentHoveredCard.SetHover(false);
                    SetCardSortingOverride(currentHoveredCard, false);
                }

                currentHoveredCard = topCard;

                // 새 카드 호버 진입
                if (currentHoveredCard != null)
                {
                    currentHoveredCard.SetHover(true);
                    SetCardSortingOverride(currentHoveredCard, true);
                }

                isDirty = true;
            }
        }

        /// <summary>특정 스크린 위치에서 이 카드가 최상위인지 확인 (클릭 검증용)</summary>
        public bool IsTopmostCardAt(Vector2 screenPos, BaseCard card)
        {
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                if (cards[i].IsDragging) continue;
                RectTransform rt = cards[i].RectTransform;
                if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, canvasCamera))
                    return cards[i] == card;
            }
            return false;
        }

        // === Card Management ===

        public BaseCard AddCard(DevCardType type)
        {
            if (cardPrefab == null)
            {
                Debug.LogError("[CardHand] cardPrefab이 할당되지 않았습니다!");
                return null;
            }

            GameObject cardObj = Instantiate(cardPrefab, cardContainer);
            BaseCard baseCard = cardObj.GetComponent<BaseCard>();
            baseCard.Initialize(this, type, cards.Count);

            CardVisual visual = cardObj.GetComponentInChildren<CardVisual>();
            if (visual != null)
                visual.Initialize(baseCard);

            cards.Add(baseCard);

            // 부채꼴 목표 위치 계산
            int count = cards.Count;
            int index = count - 1;
            float t = count == 1 ? 0.5f : (float)index / (count - 1);
            float targetX = GetCardX(index, count);
            float targetY = curveY.Evaluate(t) * curveYMultiplier;
            float targetRot = curveRotation.Evaluate(t) * curveRotationMultiplier;

            // 아래에서 올라오는 딜 애니메이션
            RectTransform rt = cardObj.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(targetX, targetY - 200f);
            rt.localScale = Vector3.zero;
            rt.localRotation = Quaternion.Euler(0, 0, targetRot);

            rt.DOAnchorPos(new Vector2(targetX, targetY), dealDuration).SetEase(Ease.OutBack);
            rt.DOScale(1f, dealDuration).SetEase(Ease.OutBack);

            // 기존 카드들도 재배치 (새 카드 추가로 간격 변경)
            isDirty = true;
            return baseCard;
        }

        public void RemoveCard(BaseCard card)
        {
            if (!cards.Contains(card)) return;

            if (currentHoveredCard == card)
                currentHoveredCard = null;

            cards.Remove(card);
            RectTransform rt = card.GetComponent<RectTransform>();

            rt.DOAnchorPos(rt.anchoredPosition + new Vector2(0, 200f), 0.3f).SetEase(Ease.InBack);
            rt.DOScale(0f, 0.3f).SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    Destroy(card.gameObject);
                    UpdateCardIndices();
                    isDirty = true;
                });
        }

        // === Hand Curve ===

        /// <summary>카드 i의 부채꼴 X 위치 계산 (중앙 정렬)</summary>
        private float GetCardX(int index, int count)
        {
            float step = cardWidth + cardSpacing;
            float totalWidth = (count - 1) * step;
            return -totalWidth / 2f + index * step;
        }

        private void ApplyHandCurve()
        {
            int count = cards.Count;
            if (count == 0) return;

            for (int i = 0; i < count; i++)
            {
                if (cards[i].IsDragging) continue;

                float t = count == 1 ? 0.5f : (float)i / (count - 1);

                float xPos = GetCardX(i, count);
                float yOffset = curveY.Evaluate(t) * curveYMultiplier;
                float zRotation = curveRotation.Evaluate(t) * curveRotationMultiplier;

                RectTransform rt = cards[i].RectTransform;

                if (cards[i].IsSelected)
                    yOffset += cards[i].SelectionOffsetY;

                // 기존 트윈 kill 후 새로 생성
                DOTween.Kill(rt.GetInstanceID() + 0);
                DOTween.Kill(rt.GetInstanceID() + 1);

                rt.DOAnchorPos(new Vector2(xPos, yOffset), rearrangeDuration)
                    .SetEase(Ease.OutQuad)
                    .SetId(rt.GetInstanceID() + 0);

                rt.DOLocalRotate(new Vector3(0, 0, zRotation), rearrangeDuration)
                    .SetEase(Ease.OutQuad)
                    .SetId(rt.GetInstanceID() + 1);
            }
        }

        // === Drag & Swap ===

        public void CheckCardSwap(BaseCard draggedCard)
        {
            int dragIndex = cards.IndexOf(draggedCard);
            if (dragIndex < 0) return;

            float dragX = draggedCard.RectTransform.anchoredPosition.x;
            int count = cards.Count;

            if (dragIndex > 0)
            {
                float neighborX = GetCardX(dragIndex - 1, count);
                if (dragX < neighborX)
                {
                    SwapCards(dragIndex, dragIndex - 1);
                    return;
                }
            }

            if (dragIndex < count - 1)
            {
                float neighborX = GetCardX(dragIndex + 1, count);
                if (dragX > neighborX)
                {
                    SwapCards(dragIndex, dragIndex + 1);
                }
            }
        }

        private void SwapCards(int indexA, int indexB)
        {
            (cards[indexA], cards[indexB]) = (cards[indexB], cards[indexA]);
            cards[indexA].transform.SetSiblingIndex(indexA);
            cards[indexB].transform.SetSiblingIndex(indexB);
            UpdateCardIndices();
            isDirty = true;
        }

        private void UpdateCardIndices()
        {
            for (int i = 0; i < cards.Count; i++)
                cards[i].cardIndex = i;
        }

        // === Event Handlers ===

        public void OnCardSelected(BaseCard card) => isDirty = true;
        public void OnCardDeselected(BaseCard card) => isDirty = true;

        public void OnCardDragStart(BaseCard card)
        {
            SetCardSortingOverride(card, true);
        }

        public void OnCardDragEnd(BaseCard card)
        {
            SetCardSortingOverride(card, false);

            // isDirty가 ApplyHandCurve에서 전부 처리

            isDirty = true;
        }

        /// <summary>
        /// 카드에 Canvas override를 추가/제거하여 렌더 순서만 변경.
        /// sibling 순서를 건드리지 않으므로 LayoutGroup 배치에 영향 없음.
        /// </summary>
        private void SetCardSortingOverride(BaseCard card, bool onTop)
        {
            Canvas cardCanvas = card.GetComponent<Canvas>();
            if (onTop)
            {
                if (cardCanvas == null)
                    cardCanvas = card.gameObject.AddComponent<Canvas>();
                cardCanvas.overrideSorting = true;
                cardCanvas.sortingOrder = 100;

                // Canvas 추가 시 GraphicRaycaster도 필요
                if (card.GetComponent<GraphicRaycaster>() == null)
                    card.gameObject.AddComponent<GraphicRaycaster>();
            }
            else
            {
                if (cardCanvas != null)
                {
                    cardCanvas.overrideSorting = false;
                    cardCanvas.sortingOrder = 0;
                }
            }
        }

        // === Public API ===

        public int CardCount => cards.Count;
        public IReadOnlyList<BaseCard> Cards => cards;

        public List<BaseCard> GetSelectedCards()
        {
            return cards.FindAll(c => c.IsSelected);
        }

        public void DeselectAll()
        {
            foreach (var card in cards)
                card.SetHover(false);
        }
    }
}
