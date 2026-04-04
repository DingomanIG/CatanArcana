using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcanaCatan.UI.CardHand
{
    /// <summary>
    /// 카드 핸드 테스트용 — 키보드로 카드 추가/제거.
    /// </summary>
    public class CardHandTester : MonoBehaviour
    {
        [SerializeField] private CardHandManager handManager;

        [Header("테스트 모드")]
        [SerializeField] private bool testResourceCards = true;
        [SerializeField] private bool testDevCards = true;

        private ResourceType[] resourceTypes = {
            ResourceType.Wood, ResourceType.Brick,
            ResourceType.Wool, ResourceType.Wheat, ResourceType.Ore
        };

        private DevCardType[] devTypes = {
            DevCardType.Knight, DevCardType.VictoryPoint,
            DevCardType.RoadBuilding, DevCardType.YearOfPlenty,
            DevCardType.Monopoly
        };

        private int resIndex;
        private int devIndex;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Space: 자원 카드 추가 (순환)
            if (keyboard.spaceKey.wasPressedThisFrame && testResourceCards)
            {
                var type = resourceTypes[resIndex % resourceTypes.Length];
                handManager.AddCard(CardData.Resource(type));
                resIndex++;
                Debug.Log($"[Test] 자원 추가: {type} (총 {handManager.CardCount})");
            }

            // D: 발전 카드 추가 (순환)
            if (keyboard.dKey.wasPressedThisFrame && testDevCards)
            {
                var type = devTypes[devIndex % devTypes.Length];
                handManager.AddCard(CardData.Development(type));
                devIndex++;
                Debug.Log($"[Test] 발전카드 추가: {type} (총 {handManager.CardCount})");
            }

            // B: 보너스 카드 추가
            if (keyboard.bKey.wasPressedThisFrame)
            {
                handManager.AddCard(CardData.Bonus(BonusCardType.LongestRoad));
                Debug.Log($"[Test] 최장도로 추가");
            }

            // Backspace: 마지막 카드 제거
            if (keyboard.backspaceKey.wasPressedThisFrame)
            {
                if (handManager.CardCount > 0)
                {
                    var cards = handManager.Cards;
                    handManager.RemoveCard(cards[cards.Count - 1]);
                }
            }

            // R: 선택된 카드 제거
            if (keyboard.rKey.wasPressedThisFrame)
            {
                var selected = handManager.GetSelectedCards();
                foreach (var card in selected)
                    handManager.RemoveCard(card);
                if (selected.Count > 0)
                    Debug.Log($"[Test] {selected.Count}장 제거");
            }
        }
    }
}
