using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 인게임 HUD 컨트롤러
/// IGameManager 인터페이스를 통해 로컬/네트워크 모드 모두 지원
/// </summary>
public class GameHUDController : MonoBehaviour
{
    [SerializeField] UIDocument uiDocument;

    // Top Bar
    Label turnNumberLabel;
    Label currentPlayerLabel;
    Label phaseIndicatorLabel;

    // Player Panel
    VisualElement playerList;

    // Action Buttons
    Button btnStartGame;
    Button btnRollDice;
    Button btnEndTurn;
    Button btnBuild;
    Button btnTrade;
    Button btnBuyDevCard;

    // Dice Display
    VisualElement diceDisplay;
    VisualElement die1Face;
    VisualElement die2Face;
    Label diceResultLabel;

    // Resource Counts
    Label resWoodCount;
    Label resBrickCount;
    Label resWoolCount;
    Label resWheatCount;
    Label resOreCount;

    // Overlays
    VisualElement buildOverlay;
    VisualElement tradeOverlay;
    VisualElement rulesOverlay;
    Button btnCloseBuild;
    Button btnCloseTrade;
    Button btnRules;
    Button btnCloseRules;

    // Build Buttons
    Button btnBuildRoad;
    Button btnBuildSettlement;
    Button btnBuildCity;
    Button btnBuildDevCard;

    // Dice dot elements (3x3 grid per die)
    VisualElement[,] die1Dots;
    VisualElement[,] die2Dots;

    // State
    readonly Dictionary<int, VisualElement> playerEntries = new();
    Coroutine diceHideCoroutine;
    const float DICE_DISPLAY_DURATION = 3f;

    // Dice face patterns: which dots (row,col) are visible for each value 1-6
    static readonly bool[][,] DiceDotPatterns = new bool[][,]
    {
        null, // index 0 unused
        new bool[,] { // 1
            { false, false, false },
            { false, true,  false },
            { false, false, false }
        },
        new bool[,] { // 2
            { false, false, true  },
            { false, false, false },
            { true,  false, false }
        },
        new bool[,] { // 3
            { false, false, true  },
            { false, true,  false },
            { true,  false, false }
        },
        new bool[,] { // 4
            { true,  false, true  },
            { false, false, false },
            { true,  false, true  }
        },
        new bool[,] { // 5
            { true,  false, true  },
            { false, true,  false },
            { true,  false, true  }
        },
        new bool[,] { // 6
            { true,  false, true  },
            { true,  false, true  },
            { true,  false, true  }
        },
    };

    IGameManager GM => GameServices.GameManager;

    void OnEnable()
    {
        CacheUIElements();
        RegisterButtonCallbacks();
        SubscribeToEvents();
        RefreshAllUI();
    }

    void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    // ========================
    // INITIALIZATION
    // ========================

    void CacheUIElements()
    {
        var root = uiDocument.rootVisualElement;

        turnNumberLabel = root.Q<Label>("turn-number");
        currentPlayerLabel = root.Q<Label>("current-player");
        phaseIndicatorLabel = root.Q<Label>("phase-indicator");

        playerList = root.Q<VisualElement>("player-list");

        btnStartGame = root.Q<Button>("btn-start-game");
        btnRollDice = root.Q<Button>("btn-roll-dice");
        btnEndTurn = root.Q<Button>("btn-end-turn");
        btnBuild = root.Q<Button>("btn-build");
        btnTrade = root.Q<Button>("btn-trade");
        btnBuyDevCard = root.Q<Button>("btn-buy-devcard");

        diceDisplay = root.Q<VisualElement>("dice-display");
        die1Face = root.Q<VisualElement>("die-1");
        die2Face = root.Q<VisualElement>("die-2");
        diceResultLabel = root.Q<Label>("dice-result");

        die1Dots = BuildDieDots(die1Face);
        die2Dots = BuildDieDots(die2Face);

        resWoodCount = root.Q<Label>("res-wood-count");
        resBrickCount = root.Q<Label>("res-brick-count");
        resWoolCount = root.Q<Label>("res-wool-count");
        resWheatCount = root.Q<Label>("res-wheat-count");
        resOreCount = root.Q<Label>("res-ore-count");

        buildOverlay = root.Q<VisualElement>("build-overlay");
        tradeOverlay = root.Q<VisualElement>("trade-overlay");
        rulesOverlay = root.Q<VisualElement>("rules-overlay");
        btnCloseBuild = root.Q<Button>("btn-close-build");
        btnCloseTrade = root.Q<Button>("btn-close-trade");
        btnRules = root.Q<Button>("btn-rules");
        btnCloseRules = root.Q<Button>("btn-close-rules");

        btnBuildRoad = root.Q<Button>("btn-build-road");
        btnBuildSettlement = root.Q<Button>("btn-build-settlement");
        btnBuildCity = root.Q<Button>("btn-build-city");
        btnBuildDevCard = root.Q<Button>("btn-build-devcard");
    }

    // ========================
    // EVENTS
    // ========================

    void SubscribeToEvents()
    {
        if (GM != null)
        {
            GM.OnTurnChanged += HandleTurnChanged;
            GM.OnPhaseChanged += HandlePhaseChanged;
            GM.OnDiceRolled += HandleDiceRolled;
            GM.OnPlayerListChanged += HandlePlayerListChanged;
        }
    }

    void UnsubscribeFromEvents()
    {
        if (GM != null)
        {
            GM.OnTurnChanged -= HandleTurnChanged;
            GM.OnPhaseChanged -= HandlePhaseChanged;
            GM.OnDiceRolled -= HandleDiceRolled;
            GM.OnPlayerListChanged -= HandlePlayerListChanged;
        }
    }

    // ========================
    // BUTTON CALLBACKS
    // ========================

    void RegisterButtonCallbacks()
    {
        btnStartGame.clicked += OnStartGameClicked;
        btnRollDice.clicked += OnRollDiceClicked;
        btnEndTurn.clicked += OnEndTurnClicked;
        btnBuild.clicked += OnBuildClicked;
        btnTrade.clicked += OnTradeClicked;
        btnBuyDevCard.clicked += OnBuyDevCardClicked;
        btnCloseBuild.clicked += OnCloseBuildClicked;
        btnCloseTrade.clicked += OnCloseTradeClicked;
        btnRules.clicked += OnRulesClicked;
        btnCloseRules.clicked += OnCloseRulesClicked;

        btnBuildRoad.clicked += () => Debug.Log("[HUD] 도로 건설 (미구현)");
        btnBuildSettlement.clicked += () => Debug.Log("[HUD] 마을 건설 (미구현)");
        btnBuildCity.clicked += () => Debug.Log("[HUD] 도시 건설 (미구현)");
        btnBuildDevCard.clicked += () => Debug.Log("[HUD] 발전카드 구매 (미구현)");
    }

    void OnStartGameClicked() => GM?.StartGame();
    void OnRollDiceClicked() => GM?.RollDice();
    void OnEndTurnClicked() => GM?.EndTurn();

    void OnBuildClicked() => buildOverlay.RemoveFromClassList("overlay--hidden");
    void OnTradeClicked() => tradeOverlay.RemoveFromClassList("overlay--hidden");
    void OnBuyDevCardClicked() => Debug.Log("[HUD] 발전카드 구매 (미구현)");

    void OnCloseBuildClicked() => buildOverlay.AddToClassList("overlay--hidden");
    void OnCloseTradeClicked() => tradeOverlay.AddToClassList("overlay--hidden");
    void OnRulesClicked() => rulesOverlay.RemoveFromClassList("overlay--hidden");
    void OnCloseRulesClicked() => rulesOverlay.AddToClassList("overlay--hidden");

    // ========================
    // EVENT HANDLERS
    // ========================

    void HandleTurnChanged(int playerIndex)
    {
        UpdateTopBar();
        UpdateActionButtons();
        UpdatePlayerHighlight();
    }

    void HandlePhaseChanged(GamePhase newPhase)
    {
        UpdateTopBar();
        UpdateActionButtons();

        if (newPhase == GamePhase.RollDice)
            HideDice();
    }

    void HandleDiceRolled(int die1, int die2, int total)
    {
        ShowDice(die1, die2, total);
        UpdateActionButtons();
    }

    void HandlePlayerListChanged()
    {
        RebuildPlayerList();
        UpdateActionButtons();
    }

    // ========================
    // UI UPDATES
    // ========================

    void RefreshAllUI()
    {
        UpdateTopBar();
        UpdateActionButtons();
        RebuildPlayerList();
        HideDice();
        UpdateResourceDisplay(0, 0, 0, 0, 0);
    }

    void UpdateTopBar()
    {
        if (GM == null) return;

        turnNumberLabel.text = $"턴: {GM.TurnNumber}";
        currentPlayerLabel.text = $"현재: {GM.GetPlayerName(GM.CurrentPlayerIndex)}";
        phaseIndicatorLabel.text = GetPhaseDisplayName(GM.CurrentPhase);
    }

    void UpdateActionButtons()
    {
        if (GM == null)
        {
            HideAllButtons();
            return;
        }

        var phase = GM.CurrentPhase;
        bool isMyTurn = GM.IsMyTurn();

        SetVisible(btnStartGame, phase == GamePhase.WaitingForPlayers && GM.IsHost);
        SetVisible(btnRollDice, phase == GamePhase.RollDice && isMyTurn);
        SetVisible(btnEndTurn, phase == GamePhase.Action && isMyTurn);

        bool actionPhase = phase == GamePhase.Action;
        SetVisible(btnBuild, actionPhase);
        SetVisible(btnTrade, actionPhase);
        SetVisible(btnBuyDevCard, actionPhase);

        btnBuild.SetEnabled(isMyTurn && actionPhase);
        btnTrade.SetEnabled(isMyTurn && actionPhase);
        btnBuyDevCard.SetEnabled(isMyTurn && actionPhase);
    }

    void HideAllButtons()
    {
        SetVisible(btnStartGame, false);
        SetVisible(btnRollDice, false);
        SetVisible(btnEndTurn, false);
        SetVisible(btnBuild, false);
        SetVisible(btnTrade, false);
        SetVisible(btnBuyDevCard, false);
    }

    static void SetVisible(VisualElement element, bool visible)
    {
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // ========================
    // DICE
    // ========================

    void ShowDice(int die1, int die2, int total)
    {
        SetDieFace(die1Dots, die1);
        SetDieFace(die2Dots, die2);
        diceResultLabel.text = total.ToString();
        diceDisplay.RemoveFromClassList("dice-display--hidden");

        if (diceHideCoroutine != null)
            StopCoroutine(diceHideCoroutine);
        diceHideCoroutine = StartCoroutine(HideDiceAfterDelay());
    }

    void HideDice()
    {
        diceDisplay.AddToClassList("dice-display--hidden");
        if (diceHideCoroutine != null)
        {
            StopCoroutine(diceHideCoroutine);
            diceHideCoroutine = null;
        }
    }

    IEnumerator HideDiceAfterDelay()
    {
        yield return new WaitForSeconds(DICE_DISPLAY_DURATION);
        HideDice();
    }

    /// <summary>주사위 면에 3x3 도트 그리드 생성</summary>
    static VisualElement[,] BuildDieDots(VisualElement dieFace)
    {
        var dots = new VisualElement[3, 3];
        for (int row = 0; row < 3; row++)
        {
            var rowElement = new VisualElement();
            rowElement.AddToClassList("die-row");
            for (int col = 0; col < 3; col++)
            {
                var dot = new VisualElement();
                dot.AddToClassList("die-dot");
                dot.AddToClassList("die-dot--hidden");
                rowElement.Add(dot);
                dots[row, col] = dot;
            }
            dieFace.Add(rowElement);
        }
        return dots;
    }

    /// <summary>주사위 값(1-6)에 맞는 도트 패턴 표시</summary>
    static void SetDieFace(VisualElement[,] dots, int value)
    {
        value = Mathf.Clamp(value, 1, 6);
        var pattern = DiceDotPatterns[value];
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                if (pattern[row, col])
                    dots[row, col].RemoveFromClassList("die-dot--hidden");
                else
                    dots[row, col].AddToClassList("die-dot--hidden");
            }
        }
    }

    // ========================
    // PLAYER LIST
    // ========================

    void RebuildPlayerList()
    {
        playerList.Clear();
        playerEntries.Clear();

        if (GM == null) return;

        for (int i = 0; i < GM.PlayerCount; i++)
        {
            var entry = CreatePlayerEntry(i);
            playerList.Add(entry);
            playerEntries[i] = entry;
        }

        UpdatePlayerHighlight();
    }

    VisualElement CreatePlayerEntry(int playerIndex)
    {
        var entry = new VisualElement();
        entry.AddToClassList("player-entry");

        var nameLabel = new Label(GM.GetPlayerName(playerIndex));
        nameLabel.AddToClassList("player-entry__name");

        var vpLabel = new Label("0 VP");
        vpLabel.AddToClassList("player-entry__vp");
        vpLabel.name = $"player-vp-{playerIndex}";

        entry.Add(nameLabel);
        entry.Add(vpLabel);
        return entry;
    }

    void UpdatePlayerHighlight()
    {
        if (GM == null) return;

        int current = GM.CurrentPlayerIndex;
        foreach (var kvp in playerEntries)
        {
            if (kvp.Key == current)
                kvp.Value.AddToClassList("player-entry--active");
            else
                kvp.Value.RemoveFromClassList("player-entry--active");
        }
    }

    // ========================
    // PUBLIC API (향후 시스템 연동용)
    // ========================

    /// <summary>자원 UI 일괄 업데이트</summary>
    public void UpdateResourceDisplay(int wood, int brick, int wool, int wheat, int ore)
    {
        resWoodCount.text = wood.ToString();
        resBrickCount.text = brick.ToString();
        resWoolCount.text = wool.ToString();
        resWheatCount.text = wheat.ToString();
        resOreCount.text = ore.ToString();
    }

    /// <summary>개별 자원 업데이트</summary>
    public void UpdateResource(ResourceType type, int count)
    {
        Label target = type switch
        {
            ResourceType.Wood => resWoodCount,
            ResourceType.Brick => resBrickCount,
            ResourceType.Wool => resWoolCount,
            ResourceType.Wheat => resWheatCount,
            ResourceType.Ore => resOreCount,
            _ => null
        };
        if (target != null) target.text = count.ToString();
    }

    /// <summary>플레이어 승리점 업데이트</summary>
    public void UpdatePlayerVP(int playerIndex, int vp)
    {
        if (playerEntries.TryGetValue(playerIndex, out var entry))
        {
            var vpLabel = entry.Q<Label>($"player-vp-{playerIndex}");
            if (vpLabel != null)
                vpLabel.text = $"{vp} VP";
        }
    }

    // ========================
    // HELPERS
    // ========================

    static string GetPhaseDisplayName(GamePhase phase) => phase switch
    {
        GamePhase.WaitingForPlayers => "대기 중",
        GamePhase.RollDice => "주사위 굴리기",
        GamePhase.Action => "행동",
        GamePhase.GameOver => "게임 종료",
        _ => phase.ToString()
    };
}
