using System.Collections.Generic;

/// <summary>
/// 발전카드 덱 - 셔플 + 뽑기
/// </summary>
public class DevCardDeck
{
    readonly List<DevCardType> cards = new();

    public int RemainingCount => cards.Count;

    public DevCardDeck()
    {
        // 카탄 기본 25장
        for (int i = 0; i < 14; i++) cards.Add(DevCardType.Knight);
        for (int i = 0; i < 5; i++) cards.Add(DevCardType.VictoryPoint);
        for (int i = 0; i < 2; i++) cards.Add(DevCardType.RoadBuilding);
        for (int i = 0; i < 2; i++) cards.Add(DevCardType.YearOfPlenty);
        for (int i = 0; i < 2; i++) cards.Add(DevCardType.Monopoly);

        Shuffle();
    }

    void Shuffle()
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }

    /// <summary>카드 1장 뽑기. 없으면 null</summary>
    public DevCardType? Draw()
    {
        if (cards.Count == 0) return null;
        var card = cards[cards.Count - 1];
        cards.RemoveAt(cards.Count - 1);
        return card;
    }
}
