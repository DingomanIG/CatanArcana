using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

namespace ArcanaCatan.UI.CardHand
{
    /// <summary>
    /// 카드 로직 담당 (Canvas 위).
    /// 호버는 CardHandManager가 직접 판별, 클릭은 포인터 이벤트.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class BaseCard : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler
    {
        [Header("Data")]
        public int cardIndex;

        /// <summary>통합 카드 데이터</summary>
        public CardData CardData { get; private set; }

        // Events for CardVisual
        public event Action OnHoverEnter;
        public event Action OnHoverExit;
        public event Action OnSelect;
        public event Action OnDeselect;

        /// <summary>카드 사용 성공 (날아가기 연출 트리거)</summary>
        public event Action OnCardUsed;
        /// <summary>카드 사용 실패 (shake 연출 트리거)</summary>
        public event Action OnCardUseRejected;

        public bool IsSelected { get; private set; }
        public bool IsHovering { get; private set; }

        private CardHandManager handManager;
        private RectTransform rectTransform;

        public RectTransform RectTransform => rectTransform;

        /// <summary>레거시 초기화 (DevCardType)</summary>
        public void Initialize(CardHandManager manager, DevCardType type, int index)
        {
            Initialize(manager, CardData.Development(type), index);
        }

        /// <summary>통합 카드 데이터로 초기화</summary>
        public void Initialize(CardHandManager manager, CardData data, int index)
        {
            handManager = manager;
            CardData = data;
            cardIndex = index;
            rectTransform = GetComponent<RectTransform>();
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

            // 매니저에게 물어봐서 이 카드가 최상위인지 확인
            if (!handManager.IsTopmostCardAt(eventData.position, this)) return;

            // 디스카드 모드: 자원카드만 선택/해제 토글
            if (handManager.CurrentSelectionMode == CardHandManager.SelectionMode.MultiSelect_Discard)
            {
                if (CardData?.Category != CardCategory.Resource) return;

                if (IsSelected)
                {
                    IsSelected = false;
                    OnDeselect?.Invoke();
                    handManager?.OnDiscardCardToggled(this, false);
                }
                else
                {
                    if (!handManager.CanSelectMoreDiscard()) return;
                    IsSelected = true;
                    OnSelect?.Invoke();
                    handManager?.OnDiscardCardToggled(this, true);
                }
                handManager?.OnCardSelected(this);
                return;
            }

            // 보너스카드는 선택 불가
            if (CardData?.Category == CardCategory.Bonus) return;

            // 발전카드: 사용 가능할 때만 선택 허용
            if (CardData?.Category == CardCategory.Development)
            {
                var gm = GameServices.GameManager;
                if (gm != null)
                {
                    if (!gm.IsMyTurn() || gm.CurrentPhase != GamePhase.Action)
                        return;
                    if (!CardData.CanUseOnTurn(gm.TurnNumber))
                        return;
                }

                // 토글
                if (IsSelected)
                {
                    IsSelected = false;
                    OnDeselect?.Invoke();
                    handManager?.OnDevCardDeselected(this);
                }
                else
                {
                    IsSelected = true;
                    OnSelect?.Invoke();
                    handManager?.OnDevCardSelected(this);
                }
                return;
            }

            // 자원카드: 일반 모드에서는 선택 불가 (디스카드 모드에서만 선택)
        }

        /// <summary>선택 강제 해제 (매니저에서 호출)</summary>
        public void ForceDeselect()
        {
            if (!IsSelected) return;
            IsSelected = false;
            OnDeselect?.Invoke();
        }

        /// <summary>사용 성공 이벤트 발행 (매니저에서 호출)</summary>
        public void NotifyCardUsed() => OnCardUsed?.Invoke();

        /// <summary>사용 거부 이벤트 발행 (매니저에서 호출)</summary>
        public void NotifyCardUseRejected() => OnCardUseRejected?.Invoke();
    }
}
