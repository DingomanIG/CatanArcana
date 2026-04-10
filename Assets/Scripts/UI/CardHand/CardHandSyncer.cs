using UnityEngine;
using System.Collections.Generic;

namespace ArcanaCatan.UI.CardHand
{
    /// <summary>
    /// IGameManager 이벤트 → CardHandManager 동기화.
    /// 자원 변화, 발전카드 구매, 디스카드 모드 등을 핸드에 반영.
    /// 자원카드는 개별 인스턴스로 관리 (스택 없음).
    /// </summary>
    public class CardHandSyncer : MonoBehaviour
    {
        [SerializeField] private CardHandManager handManager;

        private IGameManager gm;
        private bool subscribed;

        // 이전 자원 상태 추적 (변화량 계산용)
        private Dictionary<ResourceType, int> prevResources = new Dictionary<ResourceType, int>
        {
            { ResourceType.Wood, 0 },
            { ResourceType.Brick, 0 },
            { ResourceType.Wool, 0 },
            { ResourceType.Wheat, 0 },
            { ResourceType.Ore, 0 },
        };

        private void Update()
        {
            if (!subscribed && GameServices.GameManager != null)
            {
                gm = GameServices.GameManager;
                Subscribe();
            }
        }

        private void OnDestroy()
        {
            if (subscribed) Unsubscribe();
        }

        private void Subscribe()
        {
            SyncInitialState();

            gm.OnResourceChanged += HandleResourceChanged;
            gm.OnDevCardPurchased += HandleDevCardPurchased;
            gm.OnDiscardRequired += HandleDiscardRequired;
            gm.OnLongestRoadChanged += HandleLongestRoadChanged;
            gm.OnLargestArmyChanged += HandleLargestArmyChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (gm == null) return;
            gm.OnResourceChanged -= HandleResourceChanged;
            gm.OnDevCardPurchased -= HandleDevCardPurchased;
            gm.OnDiscardRequired -= HandleDiscardRequired;
            gm.OnLongestRoadChanged -= HandleLongestRoadChanged;
            gm.OnLargestArmyChanged -= HandleLargestArmyChanged;
            subscribed = false;
        }

        /// <summary>게임 시작 시 현재 자원 + 보너스 상태를 핸드에 반영</summary>
        private void SyncInitialState()
        {
            var ps = gm.GetPlayerState(gm.LocalPlayerIndex);
            if (ps == null) return;

            foreach (var kv in ps.Resources)
            {
                if (kv.Key == ResourceType.None || kv.Key == ResourceType.Sea) continue;

                for (int i = 0; i < kv.Value; i++)
                    handManager.AddCard(CardData.Resource(kv.Key));

                prevResources[kv.Key] = kv.Value;
            }

            if (ps.HasLongestRoad)
                handManager.AddCard(CardData.Bonus(BonusCardType.LongestRoad));
            if (ps.HasLargestArmy)
                handManager.AddCard(CardData.Bonus(BonusCardType.LargestArmy));
        }

        /// <summary>외부에서 카드를 직접 제거한 경우 prevResources 동기화 (이중 제거 방지)</summary>
        public void AdjustPrevResource(ResourceType type, int removedCount)
        {
            if (prevResources.ContainsKey(type))
                prevResources[type] = Mathf.Max(0, prevResources[type] - removedCount);
        }

        // === Event Handlers ===

        /// <summary>자원 변화 → 개별 카드 추가/제거</summary>
        private void HandleResourceChanged(int playerIndex, ResourceType type, int newAmount)
        {
            if (playerIndex != gm.LocalPlayerIndex) return;
            if (type == ResourceType.None || type == ResourceType.Sea) return;

            int prev = prevResources.ContainsKey(type) ? prevResources[type] : 0;
            int delta = newAmount - prev;
            prevResources[type] = newAmount;

            if (delta > 0)
            {
                // 자원 획득 — 개별 카드 추가
                for (int i = 0; i < delta; i++)
                    handManager.AddCard(CardData.Resource(type));
            }
            else if (delta < 0)
            {
                // 자원 소모 — 개별 카드 제거
                for (int i = 0; i < -delta; i++)
                {
                    var card = handManager.FindResourceCard(type);
                    if (card != null)
                        handManager.RemoveCard(card);
                }
            }
        }

        /// <summary>발전카드 구매 → 핸드에 추가</summary>
        private void HandleDevCardPurchased(int playerIndex, DevCardType type)
        {
            if (playerIndex != gm.LocalPlayerIndex) return;
            handManager.AddCard(CardData.Development(type));
        }

        /// <summary>도적 디스카드 요구 → 다중 선택 모드 진입</summary>
        private void HandleDiscardRequired(int playerIndex, int discardCount)
        {
            if (playerIndex != gm.LocalPlayerIndex) return;
            handManager.EnterDiscardMode(discardCount);
        }

        /// <summary>최장도로 변경 → 보너스 카드 추가/제거</summary>
        private void HandleLongestRoadChanged(int playerIndex, bool gained)
        {
            if (playerIndex != gm.LocalPlayerIndex) return;

            if (gained)
                handManager.AddBonusCard(BonusCardType.LongestRoad);
            else
                handManager.RemoveBonusCard(BonusCardType.LongestRoad);
        }

        /// <summary>최강기사 변경 → 보너스 카드 추가/제거</summary>
        private void HandleLargestArmyChanged(int playerIndex, bool gained)
        {
            if (playerIndex != gm.LocalPlayerIndex) return;

            if (gained)
                handManager.AddBonusCard(BonusCardType.LargestArmy);
            else
                handManager.RemoveBonusCard(BonusCardType.LargestArmy);
        }
    }
}
