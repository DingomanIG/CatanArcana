using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcanaCatan.UI.CardHand
{
    /// <summary>
    /// 카드 핸드 테스트용 - 키보드로 카드 추가/제거.
    /// Game 씬에서 CardHandManager에 붙여서 사용.
    /// </summary>
    public class CardHandTester : MonoBehaviour
    {
        [SerializeField] private CardHandManager handManager;

        private DevCardType[] testTypes = {
            DevCardType.Knight,
            DevCardType.VictoryPoint,
            DevCardType.RoadBuilding,
            DevCardType.YearOfPlenty,
            DevCardType.Monopoly
        };

        private int typeIndex;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Space: 카드 추가
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                var type = testTypes[typeIndex % testTypes.Length];
                handManager.AddCard(type);
                typeIndex++;
                Debug.Log($"[CardHandTest] Added {type} (total: {handManager.CardCount})");
            }

            // Backspace: 마지막 카드 제거
            if (keyboard.backspaceKey.wasPressedThisFrame)
            {
                if (handManager.CardCount > 0)
                {
                    var cards = handManager.Cards;
                    handManager.RemoveCard(cards[cards.Count - 1]);
                    Debug.Log($"[CardHandTest] Removed last card (total: {handManager.CardCount - 1})");
                }
            }

            // R: 선택된 카드 제거
            if (keyboard.rKey.wasPressedThisFrame)
            {
                var selected = handManager.GetSelectedCards();
                foreach (var card in selected)
                    handManager.RemoveCard(card);
                if (selected.Count > 0)
                    Debug.Log($"[CardHandTest] Removed {selected.Count} selected cards");
            }
        }
    }
}
