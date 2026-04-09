using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using System.Collections.Generic;

namespace ArcanaCatan.UI.CardHand
{
    /// <summary>
    /// 싱글 핸드 카드 관리.
    /// 부채꼴 배열, 자원 팬 스택, 카테고리 그룹 간격, 호버/드래그.
    /// </summary>
    public class CardHandManager : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject cardPrefab;

        [Header("Layout")]
        [SerializeField] private RectTransform cardContainer;
        [SerializeField] private float cardWidth = 120f;
        [SerializeField] private float cardSpacing = -30f;

        [Header("Category Spacing")]
        [SerializeField] private float categoryGapWidth = 40f;

        [Header("Hand Curve (부채꼴)")]
        [SerializeField] private AnimationCurve curveY = AnimationCurve.EaseInOut(0f, -20f, 1f, -20f);
        [SerializeField] private AnimationCurve curveRotation = AnimationCurve.Linear(0f, 5f, 1f, -5f);
        [SerializeField] private float curveYMultiplier = 1f;
        [SerializeField] private float curveRotationMultiplier = 1f;

        [Header("Animation")]
        [SerializeField] private float dealDuration = 0.3f;
        [SerializeField] private float rearrangeDuration = 0.2f;

        private List<BaseCard> cards = new List<BaseCard>();
        private Dictionary<ResourceType, BaseCard> resourceStacks = new Dictionary<ResourceType, BaseCard>();
        private bool isDirty;
        private BaseCard currentHoveredCard;
        private Canvas rootCanvas;
        private Camera canvasCamera;

        private void Awake()
        {
            var layoutGroup = GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup != null)
                layoutGroup.enabled = false;

            var csf = GetComponent<ContentSizeFitter>();
            if (csf != null)
                csf.enabled = false;

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

        // === Hover ===

        private void UpdateHover()
        {
            if (Mouse.current == null) return;

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

            if (topCard != currentHoveredCard)
            {
                if (currentHoveredCard != null)
                {
                    currentHoveredCard.SetHover(false);
                    SetCardSortingOverride(currentHoveredCard, false);
                }

                currentHoveredCard = topCard;

                if (currentHoveredCard != null)
                {
                    currentHoveredCard.SetHover(true);
                    SetCardSortingOverride(currentHoveredCard, true);
                }

                isDirty = true;
            }
        }

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

        /// <summary>레거시 — DevCardType으로 추가</summary>
        public BaseCard AddCard(DevCardType type)
        {
            return AddCard(CardData.Development(type));
        }

        /// <summary>통합 — CardData로 추가. 자원카드는 팬 스택 처리.</summary>
        public BaseCard AddCard(CardData data)
        {
            if (cardPrefab == null)
            {
                Debug.LogError("[CardHand] cardPrefab이 할당되지 않았습니다!");
                return null;
            }

            // 자원 카드 스택 처리
            if (data.Category == CardCategory.Resource)
            {
                if (resourceStacks.TryGetValue(data.ResourceType, out BaseCard existing))
                {
                    existing.SetStackCount(existing.StackCount + 1);
                    return existing;
                }
            }

            // 새 카드 생성
            GameObject cardObj = Instantiate(cardPrefab, cardContainer);
            BaseCard baseCard = cardObj.GetComponent<BaseCard>();

            int insertIndex = FindInsertIndex(data);
            baseCard.Initialize(this, data, insertIndex);

            CardVisual visual = cardObj.GetComponentInChildren<CardVisual>();
            if (visual != null)
                visual.Initialize(baseCard);

            cards.Insert(insertIndex, baseCard);
            UpdateCardIndices();

            // 자원 스택 등록
            if (data.Category == CardCategory.Resource)
                resourceStacks[data.ResourceType] = baseCard;

            // 딜 애니메이션
            AnimateDeal(baseCard, insertIndex);

            isDirty = true;
            return baseCard;
        }

        /// <summary>정렬 순서에 맞는 삽입 위치</summary>
        private int FindInsertIndex(CardData data)
        {
            int insertIndex = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].CardData != null && cards[i].CardData.SortOrder <= data.SortOrder)
                    insertIndex = i + 1;
            }
            return insertIndex;
        }

        private void AnimateDeal(BaseCard card, int index)
        {
            int count = cards.Count;
            float t = count == 1 ? 0.5f : (float)index / (count - 1);
            float targetX = GetCardX(index, count);
            float targetY = curveY.Evaluate(t) * curveYMultiplier;
            float targetRot = curveRotation.Evaluate(t) * curveRotationMultiplier;

            RectTransform rt = card.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(targetX, targetY - 200f);
            rt.localScale = Vector3.zero;
            rt.localRotation = Quaternion.Euler(0, 0, targetRot);

            rt.DOAnchorPos(new Vector2(targetX, targetY), dealDuration).SetEase(Ease.OutBack);
            rt.DOScale(1f, dealDuration).SetEase(Ease.OutBack);
        }

        public void RemoveCard(BaseCard card)
        {
            if (!cards.Contains(card)) return;

            if (currentHoveredCard == card)
                currentHoveredCard = null;

            // 자원 스택 감소 처리
            if (card.CardData?.Category == CardCategory.Resource && card.StackCount > 1)
            {
                card.SetStackCount(card.StackCount - 1);
                return;
            }

            // 자원 스택 등록 해제
            if (card.CardData?.Category == CardCategory.Resource)
                resourceStacks.Remove(card.CardData.ResourceType);

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

        /// <summary>특정 자원 타입의 스택 카드 가져오기</summary>
        public BaseCard GetResourceStack(ResourceType type)
        {
            resourceStacks.TryGetValue(type, out BaseCard card);
            return card;
        }

        // === Hand Curve ===

        private float GetCardX(int index, int count)
        {
            float step = cardWidth + cardSpacing;

            // 카테고리 전환 횟수 계산 (전체 + 이 인덱스까지)
            int totalGaps = CountCategoryGaps();
            int gapsBefore = CountCategoryGapsBefore(index);

            float totalWidth = (count - 1) * step + totalGaps * categoryGapWidth;
            return -totalWidth / 2f + index * step + gapsBefore * categoryGapWidth;
        }

        private int CountCategoryGaps()
        {
            int gaps = 0;
            for (int i = 1; i < cards.Count; i++)
            {
                if (GetCategory(i) != GetCategory(i - 1))
                    gaps++;
            }
            return gaps;
        }

        private int CountCategoryGapsBefore(int index)
        {
            int gaps = 0;
            for (int i = 1; i <= index && i < cards.Count; i++)
            {
                if (GetCategory(i) != GetCategory(i - 1))
                    gaps++;
            }
            return gaps;
        }

        private CardCategory GetCategory(int index)
        {
            return cards[index].CardData?.Category ?? CardCategory.Resource;
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

        // === Drag (스왑 비활성화 — 정렬 순서 고정) ===

        public void CheckCardSwap(BaseCard draggedCard)
        {
            // 싱글 핸드: 정렬 순서 고정, 스왑 비활성화
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
            isDirty = true;
        }

        private void SetCardSortingOverride(BaseCard card, bool onTop)
        {
            Canvas cardCanvas = card.GetComponent<Canvas>();
            if (onTop)
            {
                if (cardCanvas == null)
                    cardCanvas = card.gameObject.AddComponent<Canvas>();
                cardCanvas.overrideSorting = true;
                cardCanvas.sortingOrder = 100;

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

        // === Dev Card Use ===

        /// <summary>
        /// 발전카드 사용 시도. IGameManager 연동.
        /// 성공 시 카드 제거 + true, 실패 시 false (shake 트리거는 BaseCard에서).
        /// </summary>
        public bool TryUseDevCard(BaseCard card)
        {
            if (card.CardData?.Category != CardCategory.Development) return false;

            var gm = GameServices.GameManager;
            if (gm == null)
            {
                Debug.LogWarning("[CardHand] GameManager 없음 — 테스트 모드에서는 항상 성공");
                RemoveCardWithUseAnimation(card);
                return true;
            }

            if (!gm.IsMyTurn() || gm.CurrentPhase != GamePhase.Action)
                return false;

            bool success = card.CardData.DevCardType switch
            {
                DevCardType.Knight => gm.TryUseKnight(default),
                DevCardType.RoadBuilding => gm.TryUseRoadBuilding(),
                DevCardType.YearOfPlenty => gm.TryUseYearOfPlenty(default, default),
                DevCardType.Monopoly => gm.TryUseMonopoly(default),
                _ => false // VictoryPoint, Hidden 등은 사용 불가
            };

            if (success)
            {
                RemoveCardWithUseAnimation(card);
                return true;
            }

            return false;
        }

        /// <summary>카드 사용 성공 — 위로 날아가며 제거</summary>
        private void RemoveCardWithUseAnimation(BaseCard card)
        {
            if (currentHoveredCard == card)
                currentHoveredCard = null;

            cards.Remove(card);
            SetCardSortingOverride(card, true);

            RectTransform rt = card.RectTransform;

            // 위로 날아가며 사라짐
            rt.DOAnchorPos(rt.anchoredPosition + new Vector2(0, 400f), 0.4f)
                .SetEase(Ease.InBack);
            rt.DOScale(0.3f, 0.4f).SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    Destroy(card.gameObject);
                    UpdateCardIndices();
                    isDirty = true;
                });
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

        /// <summary>총 자원 카드 수 (스택 합산)</summary>
        public int TotalResourceCardCount
        {
            get
            {
                int total = 0;
                foreach (var kv in resourceStacks)
                    total += kv.Value.StackCount;
                return total;
            }
        }
    }
}

