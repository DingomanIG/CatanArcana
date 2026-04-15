/// <summary>발전카드 타입</summary>
public enum DevCardType
{
    Knight,         // 기사 (14장)
    RoadBuilding,   // 도로건설 (2장)
    YearOfPlenty,   // 풍년 (2장)
    Monopoly,       // 독점 (2장)
    // 승점 카드 5종 (각 1장)
    Chapel,         // 예배당
    Library,        // 도서관
    Market,         // 시장
    GreatHall,      // 대회당
    University,     // 대학
    Hidden          // 상대방 카드 (종류 비공개)
}

/// <summary>승점 카드 판별 헬퍼</summary>
public static class DevCardTypeExtensions
{
    public static bool IsVictoryPoint(this DevCardType type) =>
        type == DevCardType.Chapel || type == DevCardType.Library ||
        type == DevCardType.Market || type == DevCardType.GreatHall ||
        type == DevCardType.University;
}

/// <summary>발전카드 사용 상태 (다단계 효과용)</summary>
public enum DevCardUseState
{
    None,
    SelectingKnightTarget,  // 기사: 도적 이동 대상 선택
    PlacingFreeRoad1,       // 도로건설: 첫 번째 도로
    PlacingFreeRoad2,       // 도로건설: 두 번째 도로
}

/// <summary>
/// 발전카드 인스턴스 - 구매 턴 추적
/// </summary>
public class DevelopmentCard
{
    public DevCardType Type { get; }
    public int PurchasedOnTurn { get; }
    public bool IsUsed { get; set; }

    public DevelopmentCard(DevCardType type, int purchasedOnTurn)
    {
        Type = type;
        PurchasedOnTurn = purchasedOnTurn;
    }

    /// <summary>이번 턴에 사용 가능한지 (구매한 턴에는 사용 불가)</summary>
    public bool CanUseOnTurn(int currentTurn)
    {
        return !IsUsed && PurchasedOnTurn < currentTurn;
    }
}
