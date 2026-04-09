using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using System;
using System.Collections.Generic;

namespace ArcanaCatan.UI.CardHand
{
    /// <summary>
    /// 싱글 핸드 카드 관리.
    /// 부채꼴 배열, 자원 팬 스택, 카테고리 그룹 간격, 호버/드래그.
    /// </summary>
    public class CardHandManager : MonoBehaviour
    {
        [Header("Prefabs — Fallback")]
        [SerializeField] private GameObject cardPrefab;

        [Header("Prefabs — Resource")]
        [SerializeField] private GameObject prefabResWood;
        [SerializeField] private GameObject prefabResBrick;
        [SerializeField] private GameObject prefabResWool;
        [SerializeField] private GameObject prefabResWheat;
        [SerializeField] private GameObject prefabResOre;

        [Header("Prefabs — Development")]
        [SerializeField] private GameObject prefabDevKnight;
        [SerializeField] private GameObject prefabDevRoadBuilding;
        [SerializeField] private GameObject prefabDevYearOfPlenty;
        [SerializeField] private GameObject prefabDevMonopoly;
        [SerializeField] private GameObject prefabDevVictoryPoint;

        [Header("Prefabs — Bonus")]
        [SerializeField] private GameObject prefabBonusLongestRoad;
        [SerializeField] private GameObject prefabBonusLargestArmy;

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
        private Dictionary<BonusCardType, BaseCard> bonusCards = new Dictionary<BonusCardType, BaseCard>();
        private bool isDirty;
        private BaseCard currentHoveredCard;
        private Canvas rootCanvas;
        private Camera canvasCamera;

        private void Awake()
        {
            // WebGL 최적화: DOTween 재활용 활성화
            DOTween.SetTweensCapacity(200, 50);

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

        // === Prefab Lookup ===

        /// <summary>CardData에 맞는 프리팹 반환. 없으면 fallback cardPrefab 사용.</summary>
        private GameObject GetPrefabForCard(CardData data)
        {
            if (data == null) return cardPrefab;

            GameObject prefab = data.Category switch
            {
                CardCategory.Resource => data.ResourceType switch
                {
                    ResourceType.Wood  => prefabResWood,
                    ResourceType.Brick => prefabResBrick,
                    ResourceType.Wool  => prefabResWool,
                    ResourceType.Wheat => prefabResWheat,
                    ResourceType.Ore   => prefabResOre,
                    _ => null
                },
                CardCategory.Development => data.DevCardType switch
                {
                    DevCardType.Knight       => prefabDevKnight,
                    DevCardType.RoadBuilding => prefabDevRoadBuilding,
                    DevCardType.YearOfPlenty => prefabDevYearOfPlenty,
                    DevCardType.Monopoly     => prefabDevMonopoly,
                    DevCardType.VictoryPoint => prefabDevVictoryPoint,
                    _ => null
                },
                CardCategory.Bonus => data.BonusType switch
                {
                    BonusCardType.LongestRoad => prefabBonusLongestRoad,
                    BonusCardType.LargestArmy => prefabBonusLargestArmy,
                    _ => null
                },
                _ => null
            };

            return prefab != null ? prefab : cardPrefab;
        }

        // === Card Management ===

        /// <summary>레거시 — DevCardType으로 추가</summary>
        public BaseCard AddCard(DevCardType type)
        {
            return AddCard(CardData.Development(type));
        }

        /// <summary>
        /// 월드 좌표에서 날아오는 카드 추가.
        /// worldPos를 Canvas 로컬 좌표로 변환 → 카드를 거기서 시작 → 목표 위치로 이동.
        /// </summary>
        public BaseCard AddCardFromWorld(CardData data, Vector3 worldPos, Camera worldCamera)
        {
            var card = AddCardInternal(data, skipDealAnim: true);
            if (card == null) return null;

            // 스택 증가 케이스: 이미 존재하는 카드 → fly-in 불필요 (배지만 증가)
            if (data.Category == CardCategory.Resource
                && resourceStacks.TryGetValue(data.ResourceType, out var existing)
                && existing == card && card.StackCount > 1)
            {
                return card;
            }

            // 월드 → 스크린 → Canvas 로컬 좌표
            Vector2 screenPos = worldCamera.WorldToScreenPoint(worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                cardContainer, screenPos,
                rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera,
                out Vector2 startLocal);

            // fly-in 목표 위치 계산
            RectTransform rt = card.RectTransform;
            int idx = cards.IndexOf(card);
            int count = cards.Count;
            float t = count == 1 ? 0.5f : (float)idx / (count - 1);
            float targetX = GetCardX(idx, count);
            float targetY = curveY.Evaluate(t) * curveYMultiplier;
            int tweenId = rt.GetInstanceID();

            // ID 체계: +0=position, +1=rotation, +2=scale
            DOTween.Kill(tweenId + 0);
            DOTween.Kill(tweenId + 2);

            rt.anchoredPosition = startLocal;
            rt.localScale = Vector3.one * 0.3f;

            rt.DOAnchorPos(new Vector2(targetX, targetY), 0.5f)
                .SetEase(Ease.OutQuart).SetId(tweenId + 0);
            rt.DOScale(1f, 0.5f)
                .SetEase(Ease.OutBack).SetId(tweenId + 2);

            return card;
        }

        /// <summary>통합 — CardData로 추가. 자원카드는 팬 스택 처리.</summary>
        public BaseCard AddCard(CardData data) => AddCardInternal(data, skipDealAnim: false);

        private BaseCard AddCardInternal(CardData data, bool skipDealAnim)
        {
            var prefab = GetPrefabForCard(data);
            if (prefab == null)
            {
                Debug.LogError($"[CardHand] 프리팹 없음: {data?.DisplayName ?? "null"}");
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

            // 새 카드 생성 — 타입별 프리팹 사용
            GameObject cardObj = Instantiate(prefab, cardContainer);
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

            // 딜 애니메이션 (fly-in 사용 시 건너뜀)
            if (!skipDealAnim)
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
            int tid = rt.GetInstanceID();
            rt.anchoredPosition = new Vector2(targetX, targetY - 200f);
            rt.localScale = Vector3.zero;
            rt.localRotation = Quaternion.Euler(0, 0, targetRot);

            // ID 체계: +0=position, +1=rotation, +2=scale
            rt.DOAnchorPos(new Vector2(targetX, targetY), dealDuration)
                .SetEase(Ease.OutBack).SetId(tid + 0);
            rt.DOScale(1f, dealDuration)
                .SetEase(Ease.OutBack).SetId(tid + 2);
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
            int tid = rt.GetInstanceID();
            DOTween.Kill(tid + 0);
            DOTween.Kill(tid + 1);
            DOTween.Kill(tid + 2);

            rt.DOAnchorPos(rt.anchoredPosition + new Vector2(0, 200f), 0.3f)
                .SetEase(Ease.InBack).SetId(tid + 0);
            rt.DOScale(0f, 0.3f).SetEase(Ease.InBack).SetId(tid + 2)
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

        // === Bonus Cards ===

        /// <summary>보너스 카드 추가 (최장도로/최강기사). 중복 방지.</summary>
        public BaseCard AddBonusCard(BonusCardType type)
        {
            if (bonusCards.ContainsKey(type)) return bonusCards[type];

            var card = AddCard(CardData.Bonus(type));
            if (card != null)
                bonusCards[type] = card;
            return card;
        }

        /// <summary>보너스 카드 제거 (다른 플레이어가 탈취 시)</summary>
        public void RemoveBonusCard(BonusCardType type)
        {
            if (!bonusCards.TryGetValue(type, out BaseCard card)) return;
            bonusCards.Remove(type);
            RemoveCard(card);
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
            {
                cards[i].cardIndex = i;
                // Hierarchy 순서 = 렌더 순서. 왼쪽(index 0)이 뒤, 오른쪽이 앞.
                cards[i].transform.SetSiblingIndex(i);
            }
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
            int tid = rt.GetInstanceID();
            DOTween.Kill(tid + 0);
            DOTween.Kill(tid + 1);
            DOTween.Kill(tid + 2);

            // 위로 날아가며 사라짐
            rt.DOAnchorPos(rt.anchoredPosition + new Vector2(0, 400f), 0.4f)
                .SetEase(Ease.InBack).SetId(tid + 0);
            rt.DOScale(0.3f, 0.4f).SetEase(Ease.InBack).SetId(tid + 2)
                .OnComplete(() =>
                {
                    Destroy(card.gameObject);
                    UpdateCardIndices();
                    isDirty = true;
                });
        }

        // === Discard Mode ===

        public enum SelectionMode { Normal, MultiSelect_Discard }

        public SelectionMode CurrentSelectionMode { get; private set; } = SelectionMode.Normal;
        public int RequiredDiscardCount { get; private set; }
        public int CurrentDiscardSelected { get; private set; }

        /// <summary>디스카드 수량 변경 시 알림 (selected, required)</summary>
        public event Action<int, int> OnDiscardSelectionChanged;
        /// <summary>디스카드 모드 진입/해제 알림</summary>
        public event Action<bool> OnDiscardModeChanged;

        /// <summary>도적 디스카드 모드 진입</summary>
        public void EnterDiscardMode(int discardCount)
        {
            CurrentSelectionMode = SelectionMode.MultiSelect_Discard;
            RequiredDiscardCount = discardCount;
            CurrentDiscardSelected = 0;

            // 기존 선택 해제
            foreach (var card in cards)
            {
                if (card.IsSelected)
                    card.SetHover(false); // deselect 트리거
            }

            OnDiscardModeChanged?.Invoke(true);
            Debug.Log($"[CardHand] 디스카드 모드 진입 — {discardCount}장 선택 필요");
        }

        /// <summary>디스카드 모드 해제</summary>
        public void ExitDiscardMode()
        {
            CurrentSelectionMode = SelectionMode.Normal;
            RequiredDiscardCount = 0;
            CurrentDiscardSelected = 0;

            foreach (var card in cards)
            {
                if (card.IsSelected)
                    card.SetHover(false);
            }

            OnDiscardModeChanged?.Invoke(false);
            isDirty = true;
        }

        /// <summary>디스카드 모드에서 카드 선택 처리 (CardHandManager 내부에서 호출)</summary>
        public void OnDiscardCardToggled(BaseCard card, bool selected)
        {
            if (CurrentSelectionMode != SelectionMode.MultiSelect_Discard) return;
            if (card.CardData?.Category != CardCategory.Resource) return;

            CurrentDiscardSelected += selected ? 1 : -1;
            CurrentDiscardSelected = Mathf.Clamp(CurrentDiscardSelected, 0, RequiredDiscardCount);
            OnDiscardSelectionChanged?.Invoke(CurrentDiscardSelected, RequiredDiscardCount);
        }

        /// <summary>디스카드 확인 — 선택된 자원 제거</summary>
        public bool ConfirmDiscard()
        {
            if (CurrentSelectionMode != SelectionMode.MultiSelect_Discard) return false;
            if (CurrentDiscardSelected < RequiredDiscardCount) return false;

            var selected = GetSelectedCards();
            foreach (var card in selected)
            {
                if (card.CardData?.Category == CardCategory.Resource)
                    RemoveCard(card);
            }

            ExitDiscardMode();
            return true;
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

