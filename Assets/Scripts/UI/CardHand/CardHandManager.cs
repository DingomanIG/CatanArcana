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
    /// 부채꼴 배열, 개별 카드 인스턴스, 카테고리 그룹 간격, 호버/드래그.
    /// 자원카드도 한 장씩 개별 오브젝트로 관리 (스택 없음).
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

        [Header("Prefabs — Victory Point")]
        [SerializeField] private GameObject prefabDevChapel;
        [SerializeField] private GameObject prefabDevLibrary;
        [SerializeField] private GameObject prefabDevMarket;
        [SerializeField] private GameObject prefabDevGreatHall;
        [SerializeField] private GameObject prefabDevUniversity;

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

        [Header("Discard Selection")]
        [SerializeField] private float discardOffsetY = 150f;

        private List<BaseCard> cards = new List<BaseCard>();
        private Dictionary<BonusCardType, BaseCard> bonusCards = new Dictionary<BonusCardType, BaseCard>();
        private bool isDirty;
        private BaseCard currentHoveredCard;
        private Canvas rootCanvas;
        private Camera canvasCamera;

        private void Awake()
        {
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

            // 호버/선택 카드 회전 강제 (Canvas 리빌드 후에도 보장)
            if (currentHoveredCard != null)
                currentHoveredCard.RectTransform.localRotation = Quaternion.identity;
            foreach (var card in cards)
            {
                if (card.IsSelected)
                    card.RectTransform.localRotation = Quaternion.identity;
            }
        }

        // === Hover ===

        private void UpdateHover()
        {
            BaseCard topCard = null;

            if (Pointer.current != null)
            {
                Vector2 mouseScreenPos = Pointer.current.position.ReadValue();

                for (int i = cards.Count - 1; i >= 0; i--)
                {
                    RectTransform rt = cards[i].RectTransform;
                    if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, mouseScreenPos, canvasCamera))
                    {
                        topCard = cards[i];
                        break;
                    }
                }
            }

            // 현재 호버 카드가 리스트에서 제거됐으면 강제 해제
            if (currentHoveredCard != null && !cards.Contains(currentHoveredCard))
            {
                currentHoveredCard.SetHover(false);
                SetCardSortingOverride(currentHoveredCard, false);
                currentHoveredCard = null;
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
                    DevCardType.Chapel       => prefabDevChapel,
                    DevCardType.Library      => prefabDevLibrary,
                    DevCardType.Market       => prefabDevMarket,
                    DevCardType.GreatHall    => prefabDevGreatHall,
                    DevCardType.University   => prefabDevUniversity,
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

        /// <summary>통합 — CardData로 추가. 모든 카드 개별 인스턴스.</summary>
        public BaseCard AddCard(CardData data)
        {
            var prefab = GetPrefabForCard(data);
            if (prefab == null)
            {
                Debug.LogError($"[CardHand] 프리팹 없음: {data?.DisplayName ?? "null"}");
                return null;
            }

            GameObject cardObj = Instantiate(prefab, cardContainer);
            BaseCard baseCard = cardObj.GetComponent<BaseCard>();

            int insertIndex = FindInsertIndex(data);
            baseCard.Initialize(this, data, insertIndex);

            CardVisual visual = cardObj.GetComponentInChildren<CardVisual>();
            if (visual != null)
                visual.Initialize(baseCard);

            cards.Insert(insertIndex, baseCard);
            UpdateCardIndices();

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
            rt.anchoredPosition = new Vector2(targetX, targetY + 300f);
            rt.localScale = Vector3.zero;
            rt.localRotation = Quaternion.Euler(0, 0, targetRot);

            rt.DOAnchorPos(new Vector2(targetX, targetY), dealDuration)
                .SetEase(Ease.OutBack).SetId(tid * 4 + 0);
            rt.DOScale(1f, dealDuration)
                .SetEase(Ease.OutBack).SetId(tid * 4 + 2);
        }

        public void RemoveCard(BaseCard card)
        {
            if (!cards.Contains(card)) return;

            if (currentHoveredCard == card)
                currentHoveredCard = null;

            cards.Remove(card);
            RectTransform rt = card.GetComponent<RectTransform>();
            int tid = rt.GetInstanceID();
            DOTween.Kill(tid * 4 + 0);
            DOTween.Kill(tid * 4 + 1);
            DOTween.Kill(tid * 4 + 2);

            rt.DOAnchorPos(rt.anchoredPosition + new Vector2(0, 200f), 0.3f)
                .SetEase(Ease.InBack).SetId(tid * 4 + 0);
            rt.DOScale(0f, 0.3f).SetEase(Ease.InBack).SetId(tid * 4 + 2)
                .OnComplete(() =>
                {
                    Destroy(card.gameObject);
                    UpdateCardIndices();
                    isDirty = true;
                });
        }

        /// <summary>특정 자원 타입의 카드 1장 찾기 (제거용)</summary>
        public BaseCard FindResourceCard(ResourceType type)
        {
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                if (cards[i].CardData?.Category == CardCategory.Resource
                    && cards[i].CardData.ResourceType == type)
                    return cards[i];
            }
            return null;
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

        /// <summary>컨테이너 폭에 맞춰 카드 간격을 동적으로 계산</summary>
        private float GetDynamicStep(int count)
        {
            float defaultStep = cardWidth + cardSpacing;
            if (count <= 1) return defaultStep;

            // rootCanvas 기준 화면 폭 사용 (컨테이너 rect가 불안정할 수 있음)
            float canvasWidth = rootCanvas != null
                ? (rootCanvas.transform as RectTransform).rect.width
                : Screen.width;
            float availableWidth = canvasWidth * 0.4f; // 화면의 40% 사용

            int totalGaps = CountCategoryGaps();
            float gapTotal = totalGaps * categoryGapWidth;
            // 전체 폭 = 카드간 간격 + 마지막 카드 폭 + 카테고리 갭
            float neededWidth = (count - 1) * defaultStep + cardWidth + gapTotal;

            if (neededWidth <= availableWidth)
                return defaultStep;

            // 카드 폭과 갭을 빼고 남은 공간을 간격으로 분배
            float maxStep = (availableWidth - cardWidth - gapTotal) / Mathf.Max(1, count - 1);
            return Mathf.Min(defaultStep, maxStep);
        }

        private float GetCardX(int index, int count)
        {
            float step = GetDynamicStep(count);

            int totalGaps = CountCategoryGaps();
            int gapsBefore = CountCategoryGapsBefore(index);

            // 카테고리 갭도 축소 비율 적용
            float defaultStep = cardWidth + cardSpacing;
            float ratio = defaultStep > 0 ? step / defaultStep : 1f;
            float dynamicGap = categoryGapWidth * ratio;

            float totalWidth = (count - 1) * step + totalGaps * dynamicGap;
            return -totalWidth / 2f + index * step + gapsBefore * dynamicGap;
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
                float t = count == 1 ? 0.5f : (float)i / (count - 1);

                float xPos = GetCardX(i, count);
                float yOffset = curveY.Evaluate(t) * curveYMultiplier;
                float zRotation = curveRotation.Evaluate(t) * curveRotationMultiplier;

                RectTransform rt = cards[i].RectTransform;

                // 선택 → 위로 올라감
                if (cards[i].IsSelected)
                    yOffset += discardOffsetY;

                Vector2 targetPos = new Vector2(xPos, yOffset);
                // 선택 또는 호버 시 회전 제거 (똑바로)
                float targetRot = (cards[i].IsSelected || cards[i].IsHovering) ? 0f : zRotation;

                DOTween.Kill(rt.GetInstanceID() * 4 + 0);
                DOTween.Kill(rt.GetInstanceID() * 4 + 1);

                rt.DOAnchorPos(targetPos, rearrangeDuration)
                    .SetEase(Ease.OutQuad)
                    .SetId(rt.GetInstanceID() * 4 + 0);

                if (cards[i].IsHovering || cards[i].IsSelected)
                {
                    // 호버/선택: 즉시 회전 0 (트윈 충돌 방지)
                    DOTween.Kill(rt.GetInstanceID() * 4 + 1);
                    rt.localRotation = Quaternion.Euler(0, 0, 0);
                }
                else
                {
                    rt.DOLocalRotate(new Vector3(0, 0, targetRot), rearrangeDuration)
                        .SetEase(Ease.OutQuad)
                        .SetId(rt.GetInstanceID() * 4 + 1);
                }
            }
        }

        private void UpdateCardIndices()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].cardIndex = i;
                cards[i].transform.SetSiblingIndex(i);
            }
        }

        // === Event Handlers ===

        public void OnCardSelected(BaseCard card) => isDirty = true;
        public void OnCardDeselected(BaseCard card) => isDirty = true;

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

        // === Dev Card Selection & Use ===

        /// <summary>현재 선택된 발전카드 (1장만)</summary>
        public BaseCard SelectedDevCard { get; private set; }

        /// <summary>발전카드 선택/해제 알림 (선택된 카드 or null)</summary>
        public event Action<BaseCard> OnDevCardSelectionChanged;

        /// <summary>발전카드 사용 요청 (대상 선택이 필요한 카드용). HUD에서 구독.</summary>
        public event Action<DevCardType, BaseCard> OnDevCardUseRequested;

        /// <summary>발전카드 선택됨 (BaseCard에서 호출)</summary>
        public void OnDevCardSelected(BaseCard card)
        {
            // 기존 선택 해제 (1장만 허용)
            if (SelectedDevCard != null && SelectedDevCard != card)
                SelectedDevCard.ForceDeselect();

            SelectedDevCard = card;
            OnDevCardSelectionChanged?.Invoke(card);
            isDirty = true;
        }

        /// <summary>발전카드 선택 해제됨 (BaseCard에서 호출)</summary>
        public void OnDevCardDeselected(BaseCard card)
        {
            if (SelectedDevCard == card)
                SelectedDevCard = null;
            OnDevCardSelectionChanged?.Invoke(null);
            isDirty = true;
        }

        /// <summary>선택된 발전카드 사용 시도. HUD "사용" 버튼에서 호출.</summary>
        public bool TryUseSelectedDevCard()
        {
            if (SelectedDevCard == null) return false;
            return TryUseDevCard(SelectedDevCard);
        }

        /// <summary>
        /// 발전카드 사용 시도. IGameManager 연동.
        /// 즉시 실행 가능한 카드(RoadBuilding)는 바로 처리.
        /// 대상 선택 필요한 카드(Knight/YearOfPlenty/Monopoly)는 이벤트 발행.
        /// </summary>
        public bool TryUseDevCard(BaseCard card)
        {
            if (card.CardData?.Category != CardCategory.Development) return false;

            var gm = GameServices.GameManager;
            if (gm == null)
            {
                Debug.LogWarning("[CardHand] GameManager 없음 — 테스트 모드에서는 항상 성공");
                card.NotifyCardUsed();
                RemoveCardWithUseAnimation(card);
                return true;
            }

            if (!gm.IsMyTurn() || gm.CurrentPhase != GamePhase.Action)
            {
                card.NotifyCardUseRejected();
                return false;
            }

            if (!card.CardData.CanUseOnTurn(gm.TurnNumber))
            {
                card.NotifyCardUseRejected();
                return false;
            }

            switch (card.CardData.DevCardType)
            {
                case DevCardType.RoadBuilding:
                    if (gm.TryUseRoadBuilding())
                    {
                        card.NotifyCardUsed();
                        ClearDevCardSelection();
                        RemoveCardWithUseAnimation(card);
                        return true;
                    }
                    card.NotifyCardUseRejected();
                    return false;

                case DevCardType.Knight:
                case DevCardType.YearOfPlenty:
                case DevCardType.Monopoly:
                    OnDevCardUseRequested?.Invoke(card.CardData.DevCardType, card);
                    ClearDevCardSelection();
                    return true;

                default:
                    card.NotifyCardUseRejected();
                    return false;
            }
        }

        /// <summary>대상 선택 완료 후 호출 — 카드 제거 연출</summary>
        public void ConfirmDevCardUsed(BaseCard card)
        {
            card.NotifyCardUsed();
            RemoveCardWithUseAnimation(card);
        }

        /// <summary>발전카드 선택 해제 (내부용)</summary>
        private void ClearDevCardSelection()
        {
            if (SelectedDevCard != null)
            {
                SelectedDevCard.ForceDeselect();
                SelectedDevCard = null;
                OnDevCardSelectionChanged?.Invoke(null);
            }
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
            DOTween.Kill(tid * 4 + 0);
            DOTween.Kill(tid * 4 + 1);
            DOTween.Kill(tid * 4 + 2);

            rt.DOAnchorPos(rt.anchoredPosition + new Vector2(0, 400f), 0.4f)
                .SetEase(Ease.InBack).SetId(tid * 4 + 0);
            rt.DOScale(0.3f, 0.4f).SetEase(Ease.InBack).SetId(tid * 4 + 2)
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
                {
                    card.ForceDeselect();
                }
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
                    card.ForceDeselect();
            }

            OnDiscardModeChanged?.Invoke(false);
            isDirty = true;
        }

        /// <summary>아직 더 선택 가능한지</summary>
        public bool CanSelectMoreDiscard()
        {
            return CurrentDiscardSelected < RequiredDiscardCount;
        }

        /// <summary>디스카드 모드에서 카드 선택/해제 처리</summary>
        public void OnDiscardCardToggled(BaseCard card, bool selected)
        {
            if (CurrentSelectionMode != SelectionMode.MultiSelect_Discard) return;
            if (card.CardData?.Category != CardCategory.Resource) return;

            CurrentDiscardSelected += selected ? 1 : -1;
            CurrentDiscardSelected = Mathf.Clamp(CurrentDiscardSelected, 0, RequiredDiscardCount);
            OnDiscardSelectionChanged?.Invoke(CurrentDiscardSelected, RequiredDiscardCount);
        }

        /// <summary>디스카드 확인 — 선택된 카드 날아가는 연출 + 리스트에서 제거. GM 자원 감소는 HUD에서 별도 호출.</summary>
        public bool ConfirmDiscard()
        {
            if (CurrentSelectionMode != SelectionMode.MultiSelect_Discard) return false;
            if (CurrentDiscardSelected < RequiredDiscardCount) return false;

            // 선택된 카드 연출 제거 (리스트에서도 즉시 빼서 Syncer 이중 제거 방지)
            var selected = new List<BaseCard>(cards.FindAll(c => c.IsSelected && c.CardData?.Category == CardCategory.Resource));
            foreach (var card in selected)
            {
                if (currentHoveredCard == card)
                    currentHoveredCard = null;
                cards.Remove(card);
                AnimateDiscardRemove(card);
            }

            UpdateCardIndices();
            ExitDiscardMode();
            return true;
        }

        /// <summary>디스카드 카드 위로 날아가며 사라지는 연출</summary>
        private void AnimateDiscardRemove(BaseCard card)
        {
            SetCardSortingOverride(card, true);
            RectTransform rt = card.RectTransform;
            int tid = rt.GetInstanceID();
            DOTween.Kill(tid * 4 + 0);
            DOTween.Kill(tid * 4 + 1);
            DOTween.Kill(tid * 4 + 2);

            var cg = card.GetComponent<CanvasGroup>();
            if (cg == null) cg = card.gameObject.AddComponent<CanvasGroup>();

            // 위로 날아가며 축소 + 페이드아웃
            rt.DOAnchorPos(rt.anchoredPosition + new Vector2(0, 300f), 0.4f)
                .SetEase(Ease.InBack).SetId(tid * 4 + 0);
            rt.DOScale(0.3f, 0.4f).SetEase(Ease.InBack).SetId(tid * 4 + 2);
            cg.DOFade(0f, 0.35f).SetEase(Ease.InQuad).SetId(tid * 4 + 1)
                .OnComplete(() =>
                {
                    Destroy(card.gameObject);
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
            {
                if (card.IsSelected)
                    card.ForceDeselect();
            }
        }

        /// <summary>총 자원 카드 수 (개별 카드이므로 단순 카운트)</summary>
        public int TotalResourceCardCount
        {
            get
            {
                int total = 0;
                foreach (var card in cards)
                {
                    if (card.CardData?.Category == CardCategory.Resource)
                        total++;
                }
                return total;
            }
        }
    }
}
