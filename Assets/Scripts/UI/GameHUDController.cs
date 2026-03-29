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

    // Utility Bar (Left)
    Button btnOptions;
    Button btnVolume;

    // Overlays
    VisualElement buildOverlay;
    VisualElement tradeOverlay;
    VisualElement rulesOverlay;
    VisualElement devCardOverlay;
    VisualElement resourceSelectOverlay;
    VisualElement volumeOverlay;
    VisualElement optionsOverlay;
    Button btnCloseBuild;
    Button btnCloseTrade;
    Button btnRules;
    Button btnCloseRules;
    Button btnCloseDevCard;
    Button btnCancelResourceSelect;
    Button btnCloseVolume;
    Button btnCloseOptions;
    Button btnOptionRules;
    Button btnOptionSurrender;
    Button btnOptionMainMenu;

    // Volume
    Slider sliderBgm;
    Slider sliderSfx;
    Label bgmValueLabel;
    Label sfxValueLabel;

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

    // Trade UI
    Button btnTradeTabBank;
    Button btnTradeTabPlayer;
    VisualElement bankTradeSection;
    VisualElement playerTradeSection;
    VisualElement tradeGiveGrid;
    VisualElement tradeReceiveGrid;
    VisualElement tradeOfferGrid;
    VisualElement tradeRequestGrid;
    VisualElement tradePlayerGrid;
    Button btnExecuteBankTrade;
    Button btnExecutePlayerTrade;

    // Steal Overlay
    VisualElement stealOverlay;
    VisualElement stealPlayerList;

    // Toast Notifications
    VisualElement toastContainer;
    const float TOAST_DURATION = 3f;

    // Turn Order Overlay
    VisualElement turnOrderOverlay;
    VisualElement turnOrderList;
    Button btnCloseTurnOrder;

    // Result Overlay
    VisualElement resultOverlay;
    Label resultTitle;
    Label resultWinner;
    VisualElement resultRanking;
    Button btnResultMenu;
    Button btnResultRematch;

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

    // 은행 거래 선택 상태
    ResourceType? bankGiveSelected;
    ResourceType? bankReceiveSelected;

    // 플레이어 거래 상태
    readonly Dictionary<ResourceType, int> playerOfferAmounts = new();
    readonly Dictionary<ResourceType, int> playerRequestAmounts = new();
    int playerTradeTarget = -1;

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

        toastContainer = root.Q<VisualElement>("toast-container");

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

        btnTradeTabBank = root.Q<Button>("btn-trade-tab-bank");
        btnTradeTabPlayer = root.Q<Button>("btn-trade-tab-player");
        bankTradeSection = root.Q<VisualElement>("bank-trade-section");
        playerTradeSection = root.Q<VisualElement>("player-trade-section");
        tradeGiveGrid = root.Q<VisualElement>("trade-give-grid");
        tradeReceiveGrid = root.Q<VisualElement>("trade-receive-grid");
        tradeOfferGrid = root.Q<VisualElement>("trade-offer-grid");
        tradeRequestGrid = root.Q<VisualElement>("trade-request-grid");
        tradePlayerGrid = root.Q<VisualElement>("trade-player-grid");
        btnExecuteBankTrade = root.Q<Button>("btn-execute-bank-trade");
        btnExecutePlayerTrade = root.Q<Button>("btn-execute-player-trade");

        stealOverlay = root.Q<VisualElement>("steal-overlay");
        stealPlayerList = root.Q<VisualElement>("steal-player-list");

        // Turn Order Overlay
        turnOrderOverlay = root.Q<VisualElement>("turn-order-overlay");
        turnOrderList = root.Q<VisualElement>("turn-order-list");
        btnCloseTurnOrder = root.Q<Button>("btn-close-turn-order");

        resultOverlay = root.Q<VisualElement>("result-overlay");
        resultTitle = root.Q<Label>("result-title");
        resultWinner = root.Q<Label>("result-winner");
        resultRanking = root.Q<VisualElement>("result-ranking");
        btnResultMenu = root.Q<Button>("btn-result-menu");
        btnResultRematch = root.Q<Button>("btn-result-rematch");

        // Utility Bar
        btnOptions = root.Q<Button>("btn-options");
        btnVolume = root.Q<Button>("btn-volume");

        // Volume Overlay
        volumeOverlay = root.Q<VisualElement>("volume-overlay");
        sliderBgm = root.Q<Slider>("slider-bgm");
        sliderSfx = root.Q<Slider>("slider-sfx");
        bgmValueLabel = root.Q<Label>("bgm-value");
        sfxValueLabel = root.Q<Label>("sfx-value");
        btnCloseVolume = root.Q<Button>("btn-close-volume");

        // Options Overlay
        optionsOverlay = root.Q<VisualElement>("options-overlay");
        btnCloseOptions = root.Q<Button>("btn-close-options");
        btnOptionRules = root.Q<Button>("btn-option-rules");
        btnOptionSurrender = root.Q<Button>("btn-option-surrender");
        btnOptionMainMenu = root.Q<Button>("btn-option-mainmenu");
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
            GM.OnBankTrade += HandleBankTrade;
            GM.OnPlayerTrade += HandlePlayerTrade;
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
            GM.OnBankTrade -= HandleBankTrade;
            GM.OnPlayerTrade -= HandlePlayerTrade;
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
        btnCloseTurnOrder.clicked += () => turnOrderOverlay.AddToClassList("overlay--hidden");
        btnResultMenu.clicked += OnResultMenuClicked;
        btnResultRematch.clicked += OnResultRematchClicked;

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

        // 거래 탭 및 실행
        btnTradeTabBank.clicked += () => SwitchTradeTab(true);
        btnTradeTabPlayer.clicked += () => SwitchTradeTab(false);
        btnExecuteBankTrade.clicked += OnExecuteBankTrade;
        btnExecutePlayerTrade.clicked += OnExecutePlayerTrade;

        // 자원 선택 버튼
        btnSelectWood.clicked += () => OnResourceSelected(ResourceType.Wood);
        btnSelectBrick.clicked += () => OnResourceSelected(ResourceType.Brick);
        btnSelectWool.clicked += () => OnResourceSelected(ResourceType.Wool);
        btnSelectWheat.clicked += () => OnResourceSelected(ResourceType.Wheat);
        btnSelectOre.clicked += () => OnResourceSelected(ResourceType.Ore);

        // 유틸리티 바
        btnOptions.clicked += OnOptionsClicked;
        btnVolume.clicked += OnVolumeClicked;
        btnCloseVolume.clicked += () => volumeOverlay.AddToClassList("overlay--hidden");
        btnCloseOptions.clicked += () => optionsOverlay.AddToClassList("overlay--hidden");
        btnOptionRules.clicked += () =>
        {
            optionsOverlay.AddToClassList("overlay--hidden");
            rulesOverlay.RemoveFromClassList("overlay--hidden");
        };
        btnOptionSurrender.clicked += OnSurrenderClicked;
        btnOptionMainMenu.clicked += OnOptionMainMenuClicked;

        // 음량 슬라이더
        sliderBgm.RegisterValueChangedCallback(evt =>
        {
            bgmValueLabel.text = Mathf.RoundToInt(evt.newValue).ToString();
            AudioListener.volume = evt.newValue / 100f;
        });
        sliderSfx.RegisterValueChangedCallback(evt =>
        {
            sfxValueLabel.text = Mathf.RoundToInt(evt.newValue).ToString();
        });
    }

    void OnStartGameClicked() => GM?.StartGame();
    void OnRollDiceClicked() => GM?.RollDice();
    void OnEndTurnClicked() => GM?.EndTurn();

    void OnBuildClicked() => buildOverlay.RemoveFromClassList("overlay--hidden");

    void OnTradeClicked()
    {
        SwitchTradeTab(true);
        tradeOverlay.RemoveFromClassList("overlay--hidden");
    }

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
    void OnOptionsClicked() => optionsOverlay.RemoveFromClassList("overlay--hidden");
    void OnVolumeClicked() => volumeOverlay.RemoveFromClassList("overlay--hidden");

    void OnSurrenderClicked()
    {
        optionsOverlay.AddToClassList("overlay--hidden");
        // TODO: 기권 로직 구현
        ShowToast("robber", "기권했습니다.");
    }

    void OnOptionMainMenuClicked()
    {
        optionsOverlay.AddToClassList("overlay--hidden");
        if (SceneFlowManager.Instance != null)
            SceneFlowManager.Instance.GoToMainMenu();
    }

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

        if (newPhase == GamePhase.InitialPlacement)
            ShowTurnOrderOverlay();

        if (newPhase == GamePhase.RollDice)
            HideDice();

        if (newPhase == GamePhase.StealResource)
            ShowStealOverlay();
        else
            stealOverlay?.AddToClassList("overlay--hidden");

        if (newPhase == GamePhase.GameOver)
            ShowResultScreen();
    }

    void HandleDiceRolled(int die1, int die2, int total)
    {
        ShowDice(die1, die2, total);
        UpdateActionButtons();

        if (total == 7)
            ShowToast("robber", "도적 출현! 자원 7장 이상 보유자는 절반 폐기");
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
        if (cardType == DevCardType.Knight)
        {
            string who = GM.GetPlayerName(playerIndex);
            ShowToast("knight", $"{who}이(가) 기사 카드를 사용했습니다!");
        }
    }

    void HandleLongestRoadChanged(int playerIndex, bool gained)
    {
        UpdateBonusStatus();
        if (gained)
        {
            string who = GM.GetPlayerName(playerIndex);
            ShowToast("longest-road", $"{who}이(가) 최장교역로를 획득! (+2점)");
        }
    }

    void HandleLargestArmyChanged(int playerIndex, bool gained)
    {
        UpdateBonusStatus();
        if (gained)
        {
            string who = GM.GetPlayerName(playerIndex);
            ShowToast("largest-army", $"{who}이(가) 최대기사단을 획득! (+2점)");
        }
    }

    void HandleRobberMoved(HexCoord newCoord)
    {
        var gridView = FindObjectOfType<HexGridView>();
        if (gridView != null)
            gridView.MoveRobberVisual(newCoord);
    }

    void HandleRobberSteal(int thief, int victim, ResourceType resource)
    {
        string thiefName = GM.GetPlayerName(thief);
        string victimName = GM.GetPlayerName(victim);
        ShowToast("robber", $"{thiefName}이(가) {victimName}에게서 자원을 약탈!");
    }

    void HandleBankTrade(int player, ResourceType gave, ResourceType received, int rate)
    {
        string who = GM.GetPlayerName(player);
        ShowToast("trade", $"{who}: {GetResourceName(gave)}×{rate} → {GetResourceName(received)}×1");
        tradeOverlay.AddToClassList("overlay--hidden");
    }

    void HandlePlayerTrade(int player1, int player2)
    {
        string name1 = GM.GetPlayerName(player1);
        string name2 = GM.GetPlayerName(player2);
        ShowToast("trade", $"{name1} ↔ {name2} 거래 성사!");
        tradeOverlay.AddToClassList("overlay--hidden");
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
    // TRADE
    // ========================

    static readonly ResourceType[] AllResources =
    {
        ResourceType.Wood, ResourceType.Brick, ResourceType.Wool,
        ResourceType.Wheat, ResourceType.Ore
    };

    void SwitchTradeTab(bool bankTab)
    {
        if (bankTab)
        {
            btnTradeTabBank.AddToClassList("trade-tab--active");
            btnTradeTabPlayer.RemoveFromClassList("trade-tab--active");
            bankTradeSection.RemoveFromClassList("trade-section--hidden");
            playerTradeSection.AddToClassList("trade-section--hidden");
            RefreshBankTradeUI();
        }
        else
        {
            btnTradeTabBank.RemoveFromClassList("trade-tab--active");
            btnTradeTabPlayer.AddToClassList("trade-tab--active");
            bankTradeSection.AddToClassList("trade-section--hidden");
            playerTradeSection.RemoveFromClassList("trade-section--hidden");
            RefreshPlayerTradeUI();
        }
    }

    // --- 은행 거래 ---

    void RefreshBankTradeUI()
    {
        bankGiveSelected = null;
        bankReceiveSelected = null;
        BuildBankGiveButtons();
        BuildBankReceiveButtons();
        UpdateBankTradeExecuteButton();
    }

    void BuildBankGiveButtons()
    {
        tradeGiveGrid.Clear();
        if (GM == null) return;

        var state = GM.GetPlayerState(GM.LocalPlayerIndex);

        foreach (var res in AllResources)
        {
            int rate = GM.GetTradeRate(res);
            int owned = state.Resources[res];

            var btn = new Button();
            btn.text = $"{GetResourceName(res)}\n×{rate} ({owned})";
            btn.AddToClassList("trade-res-btn");
            btn.SetEnabled(owned >= rate);

            if (bankGiveSelected == res)
                btn.AddToClassList("trade-res-btn--selected");

            var captured = res;
            btn.clicked += () =>
            {
                bankGiveSelected = captured;
                BuildBankGiveButtons();
                BuildBankReceiveButtons();
                UpdateBankTradeExecuteButton();
            };

            tradeGiveGrid.Add(btn);
        }
    }

    void BuildBankReceiveButtons()
    {
        tradeReceiveGrid.Clear();

        foreach (var res in AllResources)
        {
            var btn = new Button();
            btn.text = GetResourceName(res);
            btn.AddToClassList("trade-res-btn");
            btn.SetEnabled(bankGiveSelected.HasValue && bankGiveSelected != res);

            if (bankReceiveSelected == res)
                btn.AddToClassList("trade-res-btn--selected");

            var captured = res;
            btn.clicked += () =>
            {
                bankReceiveSelected = captured;
                BuildBankReceiveButtons();
                UpdateBankTradeExecuteButton();
            };

            tradeReceiveGrid.Add(btn);
        }
    }

    void UpdateBankTradeExecuteButton()
    {
        btnExecuteBankTrade.SetEnabled(bankGiveSelected.HasValue && bankReceiveSelected.HasValue);
    }

    void OnExecuteBankTrade()
    {
        if (!bankGiveSelected.HasValue || !bankReceiveSelected.HasValue) return;
        GM?.TryBankTrade(bankGiveSelected.Value, bankReceiveSelected.Value);
    }

    // --- 플레이어 거래 ---

    void RefreshPlayerTradeUI()
    {
        playerOfferAmounts.Clear();
        playerRequestAmounts.Clear();
        playerTradeTarget = -1;

        foreach (var res in AllResources)
        {
            playerOfferAmounts[res] = 0;
            playerRequestAmounts[res] = 0;
        }

        BuildAmountGrid(tradeOfferGrid, playerOfferAmounts, true);
        BuildAmountGrid(tradeRequestGrid, playerRequestAmounts, false);
        BuildPlayerSelectionButtons();
        UpdatePlayerTradeExecuteButton();
    }

    void BuildAmountGrid(VisualElement grid, Dictionary<ResourceType, int> amounts, bool isOffer)
    {
        grid.Clear();
        if (GM == null) return;

        var myState = GM.GetPlayerState(GM.LocalPlayerIndex);

        foreach (var res in AllResources)
        {
            var row = new VisualElement();
            row.AddToClassList("trade-amount-row");

            var icon = new VisualElement();
            icon.AddToClassList("trade-amount-row__icon");
            icon.AddToClassList($"resource-icon--{res.ToString().ToLower()}");

            var nameLabel = new Label(GetResourceName(res));
            nameLabel.AddToClassList("trade-amount-row__name");

            var countLabel = new Label(amounts[res].ToString());
            countLabel.AddToClassList("trade-amount-row__count");

            var btnMinus = new Button { text = "-" };
            btnMinus.AddToClassList("trade-amount-btn");
            btnMinus.SetEnabled(amounts[res] > 0);

            int maxAmount = isOffer ? myState.Resources[res] : 99;
            var btnPlus = new Button { text = "+" };
            btnPlus.AddToClassList("trade-amount-btn");
            btnPlus.SetEnabled(amounts[res] < maxAmount);

            var capturedRes = res;
            btnMinus.clicked += () =>
            {
                if (amounts[capturedRes] > 0)
                {
                    amounts[capturedRes]--;
                    BuildAmountGrid(grid, amounts, isOffer);
                    UpdatePlayerTradeExecuteButton();
                }
            };
            btnPlus.clicked += () =>
            {
                int max = isOffer ? myState.Resources[capturedRes] : 99;
                if (amounts[capturedRes] < max)
                {
                    amounts[capturedRes]++;
                    BuildAmountGrid(grid, amounts, isOffer);
                    UpdatePlayerTradeExecuteButton();
                }
            };

            row.Add(icon);
            row.Add(nameLabel);
            row.Add(btnMinus);
            row.Add(countLabel);
            row.Add(btnPlus);
            grid.Add(row);
        }
    }

    void BuildPlayerSelectionButtons()
    {
        tradePlayerGrid.Clear();
        if (GM == null) return;

        for (int i = 0; i < GM.PlayerCount; i++)
        {
            if (i == GM.LocalPlayerIndex) continue;

            var btn = new Button();
            btn.text = GM.GetPlayerName(i);
            btn.AddToClassList("trade-player-btn");

            if (playerTradeTarget == i)
                btn.AddToClassList("trade-player-btn--selected");

            int captured = i;
            btn.clicked += () =>
            {
                playerTradeTarget = captured;
                BuildPlayerSelectionButtons();
                UpdatePlayerTradeExecuteButton();
            };

            tradePlayerGrid.Add(btn);
        }
    }

    void UpdatePlayerTradeExecuteButton()
    {
        bool hasOffer = false;
        bool hasRequest = false;
        foreach (var kv in playerOfferAmounts) if (kv.Value > 0) { hasOffer = true; break; }
        foreach (var kv in playerRequestAmounts) if (kv.Value > 0) { hasRequest = true; break; }

        btnExecutePlayerTrade.SetEnabled(hasOffer && hasRequest && playerTradeTarget >= 0);
    }

    void OnExecutePlayerTrade()
    {
        if (playerTradeTarget < 0) return;

        var offer = new Dictionary<ResourceType, int>();
        var request = new Dictionary<ResourceType, int>();

        foreach (var kv in playerOfferAmounts)
            if (kv.Value > 0) offer[kv.Key] = kv.Value;
        foreach (var kv in playerRequestAmounts)
            if (kv.Value > 0) request[kv.Key] = kv.Value;

        if (offer.Count == 0 || request.Count == 0) return;

        if (!GM.TryPlayerTrade(playerTradeTarget, offer, request))
            ShowToast("trade", "거래 실패: 상대방의 자원이 부족합니다");
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

    static string GetResourceName(ResourceType type) => type switch
    {
        ResourceType.Wood => "목재",
        ResourceType.Brick => "벽돌",
        ResourceType.Wool => "양모",
        ResourceType.Wheat => "밀",
        ResourceType.Ore => "광석",
        _ => type.ToString()
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

    // ========================
    // TOAST NOTIFICATIONS
    // ========================

    static readonly Dictionary<string, string> ToastIcons = new()
    {
        { "robber",       "!" },
        { "knight",       "K" },
        { "longest-road", "R" },
        { "largest-army", "A" },
        { "trade",        "T" },
    };

    void ShowToast(string type, string message)
    {
        if (toastContainer == null) return;

        var toast = new VisualElement();
        toast.AddToClassList("toast");
        toast.AddToClassList($"toast--{type}");

        var icon = new Label(ToastIcons.GetValueOrDefault(type, "!"));
        icon.AddToClassList("toast__icon");

        var text = new Label(message);
        text.AddToClassList("toast__text");

        toast.Add(icon);
        toast.Add(text);
        toastContainer.Add(toast);

        StartCoroutine(FadeOutToast(toast));
    }

    IEnumerator FadeOutToast(VisualElement toast)
    {
        yield return new WaitForSeconds(TOAST_DURATION);
        toast.AddToClassList("toast--fade-out");
        yield return new WaitForSeconds(0.5f);
        toast.RemoveFromHierarchy();
    }

    // ========================
    // TURN ORDER
    // ========================

    void ShowTurnOrderOverlay()
    {
        if (turnOrderOverlay == null || turnOrderList == null || GM == null) return;

        turnOrderList.Clear();

        int first = GM.FirstPlayerIndex;
        int count = GM.PlayerCount;

        for (int i = 0; i < count; i++)
        {
            int playerIndex = (first + i) % count;

            var entry = new VisualElement();
            entry.AddToClassList("turn-order-entry");
            if (i == 0) entry.AddToClassList("turn-order-entry--first");

            var rank = new Label($"{i + 1}");
            rank.AddToClassList("turn-order-entry__rank");

            var nameLabel = new Label(GM.GetPlayerName(playerIndex));
            nameLabel.AddToClassList("turn-order-entry__name");

            entry.Add(rank);
            entry.Add(nameLabel);

            if (i == 0)
            {
                var tag = new Label("선플레이어");
                tag.AddToClassList("turn-order-entry__tag");
                entry.Add(tag);
            }

            turnOrderList.Add(entry);
        }

        turnOrderOverlay.RemoveFromClassList("overlay--hidden");
    }

    // ========================
    // RESULT SCREEN
    // ========================

    void ShowResultScreen()
    {
        if (GM == null) return;

        // 플레이어 VP 수집 + 정렬
        var rankings = new List<(int index, string name, PlayerState state)>();
        for (int i = 0; i < GM.PlayerCount; i++)
        {
            var state = GM.GetPlayerState(i);
            if (state != null)
                rankings.Add((i, GM.GetPlayerName(i), state));
        }
        rankings.Sort((a, b) => b.state.VictoryPoints.CompareTo(a.state.VictoryPoints));

        // 우승자
        if (rankings.Count > 0)
        {
            var winner = rankings[0];
            resultWinner.text = $"{winner.name} 승리! ({winner.state.VictoryPoints}점)";
        }

        // 랭킹 빌드
        resultRanking.Clear();
        int rank = 1;
        foreach (var (index, name, state) in rankings)
        {
            var row = new VisualElement();
            row.AddToClassList("result-row");
            if (rank == 1)
                row.AddToClassList("result-row--winner");

            var rankLabel = new Label(rank == 1 ? "👑" : $"{rank}");
            rankLabel.AddToClassList("result-row__rank");

            var nameLabel = new Label(name);
            nameLabel.AddToClassList("result-row__name");

            var vpLabel = new Label($"{state.VictoryPoints}점");
            vpLabel.AddToClassList("result-row__vp");

            var detail = BuildVPDetail(state);
            var detailLabel = new Label(detail);
            detailLabel.AddToClassList("result-row__detail");

            row.Add(rankLabel);
            row.Add(nameLabel);
            row.Add(vpLabel);
            row.Add(detailLabel);
            resultRanking.Add(row);
            rank++;
        }

        resultOverlay.RemoveFromClassList("overlay--hidden");
    }

    static string BuildVPDetail(PlayerState state)
    {
        var parts = new List<string>();
        int settlements = 0, cities = 0;
        foreach (var v in state.OwnedVertices)
        {
            if (v.Building == BuildingType.Settlement) settlements++;
            else if (v.Building == BuildingType.City) cities++;
        }
        if (settlements > 0) parts.Add($"마을{settlements}");
        if (cities > 0) parts.Add($"도시{cities}");
        if (state.HasLongestRoad) parts.Add("최장로");
        if (state.HasLargestArmy) parts.Add("최대군");

        int vpCards = 0;
        foreach (var c in state.DevCards)
            if (c.Type == DevCardType.VictoryPoint) vpCards++;
        if (vpCards > 0) parts.Add($"VP카드{vpCards}");

        return string.Join(" / ", parts);
    }

    void OnResultMenuClicked()
    {
        if (SceneFlowManager.Instance != null)
            SceneFlowManager.Instance.GoToMainMenu();
    }

    void OnResultRematchClicked()
    {
        resultOverlay.AddToClassList("overlay--hidden");
        GM?.StartGame();
    }
}
