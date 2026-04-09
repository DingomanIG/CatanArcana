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

        [Header("Selection")]
        [SerializeField] private float selectionOffsetY = 30f;

        // Events for CardVisual
        public event Action OnHoverEnter;
        public event Action OnHoverExit;
        public event Action OnSelect;
        public event Action OnDeselect;
        public event Action<Vector2> OnDragUpdate;
        public event Action OnDragStart;
        public event Action OnDragEnd;
        public event Action<int> OnStackCountChanged;

        public bool IsSelected { get; private set; }
        public bool IsDragging { get; private set; }
        public bool IsHovering { get; private set; }

        /// <summary>자원 카드 스택 수량 (1 = 단일 카드)</summary>
        public int StackCount { get; private set; } = 1;

        /// <summary>스택 수량 변경</summary>
        public void SetStackCount(int count)
        {
            StackCount = Mathf.Max(0, count);
            OnStackCountChanged?.Invoke(StackCount);
        }

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
            IsDragging = false;
            GetComponent<CanvasGroup>().blocksRaycasts = true;
            OnDragEnd?.Invoke();
            handManager?.OnCardDragEnd(this);
        }

        public float SelectionOffsetY => selectionOffsetY;
    }
}
