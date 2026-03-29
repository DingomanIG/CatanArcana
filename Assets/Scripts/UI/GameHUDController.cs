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

    // Bonus Status
    Label longestRoadLabel;
    Label largestArmyLabel;

    // Action Buttons
    Button btnStartGame;
    Button btnRollDice;
    Button btnEndTurn;
    Button btnBuild;
    Button btnTrade;
    Button btnBuyDevCard;
    Button btnDevCardHand;

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
    VisualElement devCardOverlay;
    VisualElement resourceSelectOverlay;
    Button btnCloseBuild;
    Button btnCloseTrade;
    Button btnRules;
    Button btnCloseRules;
    Button btnCloseDevCard;
    Button btnCancelResourceSelect;

    // Build Buttons
    Button btnBuildRoad;
    Button btnBuildSettlement;
    Button btnBuildCity;
    Button btnBuildDevCard;

    // Resource Select
    Label resourceSelectTitle;
    Button btnSelectWood;
    Button btnSelectBrick;
    Button btnSelectWool;
    Button btnSelectWheat;
    Button btnSelectOre;

    // Steal Overlay
    VisualElement stealOverlay;
    VisualElement stealPlayerList;

    // Dev Card Hand
    ScrollView devCardHand;

    // Dice dot elements (3x3 grid per die)
    VisualElement[,] die1Dots;
    VisualElement[,] die2Dots;

    // State
    readonly Dictionary<int, VisualElement> playerEntries = new();
    Coroutine diceHideCoroutine;
    const float DICE_DISPLAY_DURATION = 3f;

    // 자원 선택 상태
    enum ResourceSelectMode { None, YearOfPlenty1, YearOfPlenty2, Monopoly }
    ResourceSelectMode resourceSelectMode = ResourceSelectMode.None;
    ResourceType yearOfPlentyFirstChoice;

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

    IGameManager gm;
    IGameManager GM => gm ??= GameServices.GameManager;

    void OnEnable()
    {
        CacheUIElements();
        RegisterButtonCallbacks();
    }

    void Start()
    {
        gm = GameServices.GameManager;
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

        longestRoadLabel = root.Q<Label>("longest-road-label");
        largestArmyLabel = root.Q<Label>("largest-army-label");

        btnStartGame = root.Q<Button>("btn-start-game");
        btnRollDice = root.Q<Button>("btn-roll-dice");
        btnEndTurn = root.Q<Button>("btn-end-turn");
        btnBuild = root.Q<Button>("btn-build");
        btnTrade = root.Q<Button>("btn-trade");
        btnBuyDevCard = root.Q<Button>("btn-buy-devcard");
        btnDevCardHand = root.Q<Button>("btn-devcard-hand");

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
        devCardOverlay = root.Q<VisualElement>("devcard-overlay");
        resourceSelectOverlay = root.Q<VisualElement>("resource-select-overlay");
        btnCloseBuild = root.Q<Button>("btn-close-build");
        btnCloseTrade = root.Q<Button>("btn-close-trade");
        btnRules = root.Q<Button>("btn-rules");
        btnCloseRules = root.Q<Button>("btn-close-rules");
        btnCloseDevCard = root.Q<Button>("btn-close-devcard");
        btnCancelResourceSelect = root.Q<Button>("btn-cancel-resource-select");

        btnBuildRoad = root.Q<Button>("btn-build-road");
        btnBuildSettlement = root.Q<Button>("btn-build-settlement");
        btnBuildCity = root.Q<Button>("btn-build-city");
        btnBuildDevCard = root.Q<Button>("btn-build-devcard");

        resourceSelectTitle = root.Q<Label>("resource-select-title");
        btnSelectWood = root.Q<Button>("btn-select-wood");
        btnSelectBrick = root.Q<Button>("btn-select-brick");
        btnSelectWool = root.Q<Button>("btn-select-wool");
        btnSelectWheat = root.Q<Button>("btn-select-wheat");
        btnSelectOre = root.Q<Button>("btn-select-ore");

        devCardHand = root.Q<ScrollView>("devcard-hand");

        stealOverlay = root.Q<VisualElement>("steal-overlay");
        stealPlayerList = root.Q<VisualElement>("steal-player-list");
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
            GM.OnResourceChanged += HandleResourceChanged;
            GM.OnVPChanged += HandleVPChanged;
            GM.OnDevCardPurchased += HandleDevCardPurchased;
            GM.OnDevCardUsed += HandleDevCardUsed;
            GM.OnLongestRoadChanged += HandleLongestRoadChanged;
            GM.OnLargestArmyChanged += HandleLargestArmyChanged;
            GM.OnRobberMoved += HandleRobberMoved;
            GM.OnRobberSteal += HandleRobberSteal;
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
            GM.OnResourceChanged -= HandleResourceChanged;
            GM.OnVPChanged -= HandleVPChanged;
            GM.OnDevCardPurchased -= HandleDevCardPurchased;
            GM.OnDevCardUsed -= HandleDevCardUsed;
            GM.OnLongestRoadChanged -= HandleLongestRoadChanged;
            GM.OnLargestArmyChanged -= HandleLargestArmyChanged;
            GM.OnRobberMoved -= HandleRobberMoved;
            GM.OnRobberSteal -= HandleRobberSteal;
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
        btnDevCardHand.clicked += OnDevCardHandClicked;
        btnCloseBuild.clicked += OnCloseBuildClicked;
        btnCloseTrade.clicked += OnCloseTradeClicked;
        btnRules.clicked += OnRulesClicked;
        btnCloseRules.clicked += OnCloseRulesClicked;
        btnCloseDevCard.clicked += OnCloseDevCardClicked;
        btnCancelResourceSelect.clicked += OnCancelResourceSelect;

        btnBuildRoad.clicked += () =>
        {
            buildOverlay.AddToClassList("overlay--hidden");
            GM?.EnterBuildMode(BuildMode.PlacingRoad);
        };
        btnBuildSettlement.clicked += () =>
        {
            buildOverlay.AddToClassList("overlay--hidden");
            GM?.EnterBuildMode(BuildMode.PlacingSettlement);
        };
        btnBuildCity.clicked += () =>
        {
            buildOverlay.AddToClassList("overlay--hidden");
            GM?.EnterBuildMode(BuildMode.PlacingCity);
        };
        btnBuildDevCard.clicked += () =>
        {
            GM?.TryBuyDevCard();
            buildOverlay.AddToClassList("overlay--hidden");
        };

        // 자원 선택 버튼
        btnSelectWood.clicked += () => OnResourceSelected(ResourceType.Wood);
        btnSelectBrick.clicked += () => OnResourceSelected(ResourceType.Brick);
        btnSelectWool.clicked += () => OnResourceSelected(ResourceType.Wool);
        btnSelectWheat.clicked += () => OnResourceSelected(ResourceType.Wheat);
        btnSelectOre.clicked += () => OnResourceSelected(ResourceType.Ore);
    }

    void OnStartGameClicked() => GM?.StartGame();
    void OnRollDiceClicked() => GM?.RollDice();
    void OnEndTurnClicked() => GM?.EndTurn();

    void OnBuildClicked() => buildOverlay.RemoveFromClassList("overlay--hidden");
    void OnTradeClicked() => tradeOverlay.RemoveFromClassList("overlay--hidden");

    void OnBuyDevCardClicked()
    {
        GM?.TryBuyDevCard();
    }

    void OnDevCardHandClicked()
    {
        RefreshDevCardHand();
        devCardOverlay.RemoveFromClassList("overlay--hidden");
    }

    void OnCloseBuildClicked() => buildOverlay.AddToClassList("overlay--hidden");
    void OnCloseTradeClicked() => tradeOverlay.AddToClassList("overlay--hidden");
    void OnRulesClicked() => rulesOverlay.RemoveFromClassList("overlay--hidden");
    void OnCloseRulesClicked() => rulesOverlay.AddToClassList("overlay--hidden");
    void OnCloseDevCardClicked() => devCardOverlay.AddToClassList("overlay--hidden");

    void OnCancelResourceSelect()
    {
        resourceSelectMode = ResourceSelectMode.None;
        resourceSelectOverlay.AddToClassList("overlay--hidden");
    }

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

        if (newPhase == GamePhase.StealResource)
            ShowStealOverlay();
        else
            stealOverlay?.AddToClassList("overlay--hidden");
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

    void HandleResourceChanged(int playerIndex, ResourceType type, int newCount)
    {
        if (playerIndex == GM.LocalPlayerIndex)
            UpdateResource(type, newCount);
    }

    void HandleVPChanged(int playerIndex, int vp)
    {
        UpdatePlayerVP(playerIndex, vp);
    }

    void HandleDevCardPurchased(int playerIndex, DevCardType cardType)
    {
        if (playerIndex == GM.LocalPlayerIndex)
            Debug.Log($"[HUD] 발전카드 구매: {cardType}");
    }

    void HandleDevCardUsed(int playerIndex, DevCardType cardType)
    {
        if (playerIndex == GM.LocalPlayerIndex)
            Debug.Log($"[HUD] 발전카드 사용: {cardType}");
    }

    void HandleLongestRoadChanged(int playerIndex, bool gained)
    {
        UpdateBonusStatus();
    }

    void HandleLargestArmyChanged(int playerIndex, bool gained)
    {
        UpdateBonusStatus();
    }

    void HandleRobberMoved(HexCoord newCoord)
    {
        var gridView = FindObjectOfType<HexGridView>();
        if (gridView != null)
            gridView.MoveRobberVisual(newCoord);
    }

    void HandleRobberSteal(int thief, int victim, ResourceType resource)
    {
        Debug.Log($"[HUD] {GM.GetPlayerName(thief)}이 {GM.GetPlayerName(victim)}에게서 {resource} 약탈!");
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
        UpdateBonusStatus();
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
        SetVisible(btnRollDice, phase == GamePhase.RollDice);
        SetVisible(btnEndTurn, phase == GamePhase.Action);

        bool actionPhase = phase == GamePhase.Action;
        SetVisible(btnBuild, actionPhase);
        SetVisible(btnTrade, actionPhase);
        SetVisible(btnBuyDevCard, actionPhase);
        SetVisible(btnDevCardHand, actionPhase);

        btnBuild.SetEnabled(actionPhase);
        btnTrade.SetEnabled(actionPhase);
        btnBuyDevCard.SetEnabled(actionPhase);
        btnDevCardHand.SetEnabled(actionPhase);
    }

    void HideAllButtons()
    {
        SetVisible(btnStartGame, false);
        SetVisible(btnRollDice, false);
        SetVisible(btnEndTurn, false);
        SetVisible(btnBuild, false);
        SetVisible(btnTrade, false);
        SetVisible(btnBuyDevCard, false);
        SetVisible(btnDevCardHand, false);
    }

    static void SetVisible(VisualElement element, bool visible)
    {
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // ========================
    // BONUS STATUS
    // ========================

    void UpdateBonusStatus()
    {
        if (GM == null) return;

        int roadHolder = GM.GetLongestRoadHolder();
        if (roadHolder >= 0)
        {
            longestRoadLabel.text = $"최장교역로: {GM.GetPlayerName(roadHolder)} ({GM.GetLongestRoadLength(roadHolder)})";
            longestRoadLabel.RemoveFromClassList("bonus-label--hidden");
        }
        else
        {
            longestRoadLabel.AddToClassList("bonus-label--hidden");
        }

        int armyHolder = GM.GetLargestArmyHolder();
        if (armyHolder >= 0)
        {
            var state = GM.GetPlayerState(armyHolder);
            largestArmyLabel.text = $"최대기사단: {GM.GetPlayerName(armyHolder)} ({state.KnightsPlayed})";
            largestArmyLabel.RemoveFromClassList("bonus-label--hidden");
        }
        else
        {
            largestArmyLabel.AddToClassList("bonus-label--hidden");
        }
    }

    // ========================
    // DEV CARD HAND
    // ========================

    void RefreshDevCardHand()
    {
        devCardHand.Clear();

        if (GM == null) return;

        var state = GM.GetPlayerState(GM.LocalPlayerIndex);
        if (state == null || state.DevCards.Count == 0)
        {
            var emptyLabel = new Label("보유한 발전카드가 없습니다.");
            emptyLabel.AddToClassList("placeholder-text");
            devCardHand.Add(emptyLabel);
            return;
        }

        foreach (var card in state.DevCards)
        {
            if (card.IsUsed) continue;

            var entry = new VisualElement();
            entry.AddToClassList("devcard-entry");

            var info = new VisualElement();
            info.AddToClassList("devcard-entry__info");

            var nameLabel = new Label(GetDevCardName(card.Type));
            nameLabel.AddToClassList("devcard-entry__name");

            var descLabel = new Label(GetDevCardDesc(card.Type));
            descLabel.AddToClassList("devcard-entry__desc");

            info.Add(nameLabel);
            info.Add(descLabel);
            entry.Add(info);

            // 승리점 카드는 사용 버튼 없음
            if (card.Type != DevCardType.VictoryPoint)
            {
                var useBtn = new Button();
                useBtn.text = "사용";
                useBtn.AddToClassList("devcard-entry__btn");

                bool canUse = !state.HasUsedDevCardThisTurn
                              && card.CanUseOnTurn(GM.TurnNumber)
                              && GM.CurrentPhase == GamePhase.Action;

                useBtn.SetEnabled(canUse);
                if (!canUse) entry.AddToClassList("devcard-entry--disabled");

                var capturedCard = card;
                useBtn.clicked += () => UseDevCard(capturedCard);

                entry.Add(useBtn);
            }

            devCardHand.Add(entry);
        }
    }

    void UseDevCard(DevelopmentCard card)
    {
        devCardOverlay.AddToClassList("overlay--hidden");

        switch (card.Type)
        {
            case DevCardType.Knight:
                // 첫 호출: 카드 소비 + SelectingKnightTarget 상태 진입
                // BuildModeController가 타일 클릭 감지 후 TryUseKnight(coord) 재호출
                GM?.TryUseKnight(default);
                break;

            case DevCardType.RoadBuilding:
                GM?.TryUseRoadBuilding();
                break;

            case DevCardType.YearOfPlenty:
                OpenResourceSelect(ResourceSelectMode.YearOfPlenty1, "풍년: 첫 번째 자원 선택");
                break;

            case DevCardType.Monopoly:
                OpenResourceSelect(ResourceSelectMode.Monopoly, "독점: 자원 선택");
                break;
        }
    }

    // ========================
    // RESOURCE SELECT
    // ========================

    void OpenResourceSelect(ResourceSelectMode mode, string title)
    {
        resourceSelectMode = mode;
        resourceSelectTitle.text = title;
        resourceSelectOverlay.RemoveFromClassList("overlay--hidden");
    }

    void OnResourceSelected(ResourceType type)
    {
        switch (resourceSelectMode)
        {
            case ResourceSelectMode.YearOfPlenty1:
                yearOfPlentyFirstChoice = type;
                resourceSelectMode = ResourceSelectMode.YearOfPlenty2;
                resourceSelectTitle.text = "풍년: 두 번째 자원 선택";
                break;

            case ResourceSelectMode.YearOfPlenty2:
                resourceSelectOverlay.AddToClassList("overlay--hidden");
                GM?.TryUseYearOfPlenty(yearOfPlentyFirstChoice, type);
                resourceSelectMode = ResourceSelectMode.None;
                break;

            case ResourceSelectMode.Monopoly:
                resourceSelectOverlay.AddToClassList("overlay--hidden");
                GM?.TryUseMonopoly(type);
                resourceSelectMode = ResourceSelectMode.None;
                break;
        }
    }

    // ========================
    // STEAL
    // ========================

    void ShowStealOverlay()
    {
        if (stealOverlay == null || stealPlayerList == null) return;

        stealPlayerList.Clear();

        var candidates = GM?.GetRobberStealCandidates();
        if (candidates == null || candidates.Count == 0) return;

        foreach (int playerIndex in candidates)
        {
            var state = GM.GetPlayerState(playerIndex);
            var btn = new Button();
            btn.text = $"{GM.GetPlayerName(playerIndex)} (자원 {state.TotalResourceCount}장)";
            btn.AddToClassList("steal-player-btn");

            int captured = playerIndex;
            btn.clicked += () =>
            {
                GM.TryStealFromPlayer(captured);
                stealOverlay.AddToClassList("overlay--hidden");
            };

            stealPlayerList.Add(btn);
        }

        stealOverlay.RemoveFromClassList("overlay--hidden");
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
    // PUBLIC API
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
        GamePhase.InitialPlacement => "초기 배치",
        GamePhase.MoveRobber => "도적 이동",
        GamePhase.StealResource => "자원 약탈",
        GamePhase.GameOver => "게임 종료",
        _ => phase.ToString()
    };

    static string GetDevCardName(DevCardType type) => type switch
    {
        DevCardType.Knight => "기사",
        DevCardType.VictoryPoint => "승리점",
        DevCardType.RoadBuilding => "도로건설",
        DevCardType.YearOfPlenty => "풍년",
        DevCardType.Monopoly => "독점",
        _ => type.ToString()
    };

    static string GetDevCardDesc(DevCardType type) => type switch
    {
        DevCardType.Knight => "도적을 이동합니다",
        DevCardType.VictoryPoint => "즉시 1 승리점",
        DevCardType.RoadBuilding => "도로 2개를 무료로 건설",
        DevCardType.YearOfPlenty => "원하는 자원 2개 획득",
        DevCardType.Monopoly => "선택한 자원을 전부 약탈",
        _ => ""
    };
}
