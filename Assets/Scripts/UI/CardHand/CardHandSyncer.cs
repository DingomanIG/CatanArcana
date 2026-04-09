using UnityEngine;
using System.Collections.Generic;

namespace ArcanaCatan.UI.CardHand
{
    /// <summary>
    /// IGameManager 이벤트 → CardHandManager 동기화.
    /// 자원 변화, 발전카드 구매, 디스카드 모드 등을 핸드에 반영.
    /// </summary>
    public class CardHandSyncer : MonoBehaviour
    {
        [SerializeField] private CardHandManager handManager;

        [Header("Animation")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private float hexSize = 1f;

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

        // 타일 → 핸드 날아오기 연출용 큐 (타일 이벤트가 먼저 → 자원 이벤트에서 소비)
        private Queue<Vector3> pendingFlyOrigins = new Queue<Vector3>();

        private void Update()
        {
            // GameManager가 늦게 초기화될 수 있으므로 매 프레임 체크
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
            // 초기 상태를 먼저 동기화 (prevResources 세팅)
            // → 이후 이벤트 구독 시 delta 계산이 정확해짐
            SyncInitialState();

            gm.OnResourceChanged += HandleResourceChanged;
            gm.OnDevCardPurchased += HandleDevCardPurchased;
            gm.OnDiscardRequired += HandleDiscardRequired;
            gm.OnLongestRoadChanged += HandleLongestRoadChanged;
            gm.OnLargestArmyChanged += HandleLargestArmyChanged;
            gm.OnResourceGainedFromTile += HandleResourceGainedFromTile;
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
            gm.OnResourceGainedFromTile -= HandleResourceGainedFromTile;
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

            // 보너스 카드 초기 동기화
            if (ps.HasLongestRoad)
                handManager.AddCard(CardData.Bonus(BonusCardType.LongestRoad));
            if (ps.HasLargestArmy)
                handManager.AddCard(CardData.Bonus(BonusCardType.LargestArmy));
        }

        // === Event Handlers ===

        /// <summary>자원 변화 → 핸드 카드 증감</summary>
        private void HandleResourceChanged(int playerIndex, ResourceType type, int newAmount)
        {
            // 로컬 플레이어만 처리
            if (playerIndex != gm.LocalPlayerIndex) return;
            if (type == ResourceType.None || type == ResourceType.Sea) return;

            int prev = prevResources.ContainsKey(type) ? prevResources[type] : 0;
            int delta = newAmount - prev;
            prevResources[type] = newAmount;

            if (delta > 0)
            {
                // 자원 획득 — 카드 추가 (스택 증가)
                for (int i = 0; i < delta; i++)
                {
                    if (pendingFlyOrigins.Count > 0 && worldCamera != null)
                        handManager.AddCardFromWorld(CardData.Resource(type), pendingFlyOrigins.Dequeue(), worldCamera);
                    else
                        handManager.AddCard(CardData.Resource(type));
                }
            }
            else if (delta < 0)
            {
                // 자원 소모 — 카드 제거 (스택 감소)
                var stack = handManager.GetResourceStack(type);
                if (stack != null)
                {
                    for (int i = 0; i < -delta; i++)
                        handManager.RemoveCard(stack);
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

        /// <summary>타일에서 자원 획득 → 월드 좌표를 fly-in 큐에 등록</summary>
        private void HandleResourceGainedFromTile(int playerIndex, ResourceType type, HexCoord tileCoord)
        {
            if (playerIndex != gm.LocalPlayerIndex) return;
            Vector3 worldPos = tileCoord.ToWorldPosition(hexSize);
            pendingFlyOrigins.Enqueue(worldPos);
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
