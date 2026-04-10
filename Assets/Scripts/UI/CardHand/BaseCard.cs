using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

namespace ArcanaCatan.UI.CardHand
{
    /// <summary>
    /// 카드 로직 담당 (Canvas 위).
    /// 호버는 CardHandManager가 직접 판별, 클릭/드래그는 포인터 이벤트.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class BaseCard : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Data")]
        public DevCardType cardType;    // 레거시 호환
        public int cardIndex;

        /// <summary>통합 카드 데이터</summary>
        public CardData CardData { get; private set; }

        [Header("Card Use (발전카드)")]
        [SerializeField] private float useThresholdRatio = 0.6f; // 화면 높이의 60%

        // Events for CardVisual
        public event Action OnHoverEnter;
        public event Action OnHoverExit;
        public event Action OnSelect;
        public event Action OnDeselect;
        public event Action<Vector2> OnDragUpdate;
        public event Action OnDragStart;
        public event Action OnDragEnd;

        /// <summary>카드 사용 성공 (날아가기 연출 트리거)</summary>
        public event Action OnCardUsed;
        /// <summary>카드 사용 실패 (shake 연출 트리거)</summary>
        public event Action OnCardUseRejected;

        public bool IsSelected { get; private set; }
        public bool IsDragging { get; private set; }
        public bool IsHovering { get; private set; }

        private CardHandManager handManager;
        private RectTransform rectTransform;
        private Canvas parentCanvas;
        private Vector2 dragOffset;

        public RectTransform RectTransform => rectTransform;

        /// <summary>레거시 초기화 (DevCardType)</summary>
        public void Initialize(CardHandManager manager, DevCardType type, int index)
        {
            Initialize(manager, CardData.Development(type), index);
            cardType = type;
        }

        /// <summary>통합 카드 데이터로 초기화</summary>
        public void Initialize(CardHandManager manager, CardData data, int index)
        {
            handManager = manager;
            CardData = data;
            cardIndex = index;
            rectTransform = GetComponent<RectTransform>();
            parentCanvas = GetComponentInParent<Canvas>();
        }

        /// <summary>매니저에서 호출 — 호버 시작</summary>
        public void SetHover(bool hover)
        {
            if (hover == IsHovering) return;
            IsHovering = hover;
            if (hover)
                OnHoverEnter?.Invoke();
            else
                OnHoverExit?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (IsDragging) return;

            // 매니저에게 물어봐서 이 카드가 최상위인지 확인
            if (!handManager.IsTopmostCardAt(eventData.position, this)) return;

            // 디스카드 모드: 자원카드만 선택/해제 토글
            if (handManager.CurrentSelectionMode == CardHandManager.SelectionMode.MultiSelect_Discard)
            {
                if (CardData?.Category != CardCategory.Resource) return;

                if (IsSelected)
                {
                    // 올라간 카드 클릭 → 내려옴
                    IsSelected = false;
                    OnDeselect?.Invoke();
                    handManager?.OnDiscardCardToggled(this, false);
                }
                else
                {
                    // 핸드 카드 클릭 → 올라감
                    if (!handManager.CanSelectMoreDiscard()) return;
                    IsSelected = true;
                    OnSelect?.Invoke();
                    handManager?.OnDiscardCardToggled(this, true);
                }
                handManager?.OnCardSelected(this);
                return;
            }

            // 일반 모드
            IsSelected = !IsSelected;
            if (IsSelected)
            {
                OnSelect?.Invoke();
                handManager?.OnCardSelected(this);
            }
            else
            {
                OnDeselect?.Invoke();
                handManager?.OnCardDeselected(this);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (CardData != null && !CardData.IsDraggable) return;
            // 디스카드 모드에서는 드래그 비활성화
            if (handManager.CurrentSelectionMode == CardHandManager.SelectionMode.MultiSelect_Discard) return;
            IsDragging = true;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform.parent as RectTransform,
                eventData.position,
                parentCanvas.worldCamera,
                out Vector2 localPoint);
            dragOffset = (Vector2)rectTransform.localPosition - localPoint;

            GetComponent<CanvasGroup>().blocksRaycasts = false;
            OnDragStart?.Invoke();
            handManager?.OnCardDragStart(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform.parent as RectTransform,
                eventData.position,
                parentCanvas.worldCamera,
                out Vector2 localPoint);

            rectTransform.localPosition = localPoint + dragOffset;
            OnDragUpdate?.Invoke(eventData.delta);
            handManager?.CheckCardSwap(this);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging) return;
            IsDragging = false;
            GetComponent<CanvasGroup>().blocksRaycasts = true;

            // 발전카드: 상단 임계선 초과 시 사용 시도
            if (CardData?.Category == CardCategory.Development)
            {
                float screenY = eventData.position.y;
                float threshold = Screen.height * useThresholdRatio;

                if (screenY >= threshold)
                {
                    bool success = handManager != null && handManager.TryUseDevCard(this);
                    if (success)
                    {
                        OnCardUsed?.Invoke();
                        return;
                    }
                    else
                    {
                        OnCardUseRejected?.Invoke();
                    }
                }
            }

            OnDragEnd?.Invoke();
            handManager?.OnCardDragEnd(this);
        }

        /// <summary>선택 강제 해제 (매니저에서 호출)</summary>
        public void ForceDeselect()
        {
            if (!IsSelected) return;
            IsSelected = false;
            OnDeselect?.Invoke();
        }
    }
}
