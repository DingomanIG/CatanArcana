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
    [SerializeField] VisualTreeAsset opponentCardTemplate;

    // Top Bar
    Label turnNumberLabel;
    Label currentPlayerLabel;
    Label phaseIndicatorLabel;

    // Opponent Bar
    VisualElement opponentBar;

    // Action Panel (container)
    VisualElement actionPanel;

    // Player Stats (bottom bar)
    Label statResTotalLabel;
    Label statDevTotalLabel;
    Label statRoadLabel;
    Label statKnightLabel;
    Label statVpLabel;

    // Action Buttons
    Button btnStartGame;
    Button btnRollDice;
    Button btnEndTurn;
    Button btnTrade;
    Button btnBuyDevCard;
    Button btnDevCardHand;

    // Build Section Header
    VisualElement buildHeader;

    // Status Labels
    Label labelInitialPlacement;
    Label labelMoveRobber;

    // Dice Display
    VisualElement diceDisplay;
    VisualElement die1Face;
    VisualElement die2Face;
    Label diceResultLabel;
    Label dicePlayerName;
    VisualElement numberToken;
    VisualElement tokenDots;

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
    VisualElement tradeOverlay;
    VisualElement rulesOverlay;
    VisualElement devCardOverlay;
    VisualElement resourceSelectOverlay;
    VisualElement volumeOverlay;
    VisualElement optionsOverlay;
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
    Label nowPlayingLabel;

    // Build Buttons
    Button btnBuildRoad;
    Button btnBuildSettlement;
    Button btnBuildCity;

    // Build Count Labels
    Label buildRoadCount;
    Label buildSettlementCount;
    Label buildCityCount;
    Label buildDevCardCount;

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
    Button btnExecuteBankTrade;
    Label bankTradeSummaryLabel;

    // Player Trade Proposal UI
    VisualElement playerTermsPanel;
    VisualElement playerResponsesPanel;
    VisualElement tradeResponseList;
    Button btnSendProposal;
    Button btnCancelProposal;

    // Proposal state
    readonly Dictionary<int, bool?> proposalResponses = new(); // null=pending, true=수락, false=거절
    Dictionary<ResourceType, int> pendingOffer;
    Dictionary<ResourceType, int> pendingRequest;

    // Incoming Trade Overlay (AI → 인간 수신)
    VisualElement incomingTradeOverlay;
    Label incomingTradeFrom;
    VisualElement incomingTradeOfferGrid;
    VisualElement incomingTradeRequestGrid;
    Button btnIncomingAccept;
    Button btnIncomingDecline;

    // Steal Overlay
    VisualElement stealOverlay;
    VisualElement stealPlayerList;

    // Bank Resources
    Label bankWood, bankBrick, bankWool, bankWheat, bankOre, bankDevCard;

    // Event Log
    ScrollView eventLogScroll;
    readonly Dictionary<int, Dictionary<ResourceType, int>> pendingLogDeltas = new();
    Coroutine logFlushCoroutine;

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

    // Dev Card Quick Slot Bar
    VisualElement devCardQuickSlotBar;

    // Dice dot elements (3x3 grid per die)
    VisualElement[,] die1Dots;
    VisualElement[,] die2Dots;

    // State
    readonly Dictionary<int, OpponentCardUI> opponentCards = new();
    readonly Dictionary<(int player, ResourceType res), int> lastKnownResources = new();

    static readonly Color[] PlayerColors =
    {
        new(0.77f, 0.36f, 0.24f), // Red
        new(0.29f, 0.48f, 0.77f), // Blue
        new(0.83f, 0.66f, 0.27f), // Gold
        new(0.6f, 0.2f, 0.8f),   // Purple
    };

    class OpponentCardUI
    {
        public VisualElement card;
        public Label vpLabel;
        public Label resCountLabel;
        public Label devCountLabel;
        public Label roadValueLabel;
        public Label roadTextLabel;
        public Label knightValueLabel;
        public Label knightTextLabel;
        public VisualElement statusBar;
        public Label statusText;
    }

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
        InvokeRepeating(nameof(UpdateNowPlaying), 0.5f, 1f);
    }

    void OnDisable()
    {
        UnsubscribeFromEvents();
        CancelInvoke(nameof(UpdateNowPlaying));
    }

    void UpdateNowPlaying()
    {
        if (nowPlayingLabel == null) return;
        var bgm = BGMManager.Instance;
        if (bgm == null || string.IsNullOrEmpty(bgm.CurrentClipName))
        {
            nowPlayingLabel.text = "♪ —";
            return;
        }
        int sec = Mathf.CeilToInt(bgm.RemainingTime);
        int m = sec / 60;
        int s = sec % 60;
        nowPlayingLabel.text = $"♪ {bgm.CurrentClipName}  {m}:{s:D2}";
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

        opponentBar = root.Q<VisualElement>("opponent-bar");

        actionPanel = root.Q<VisualElement>("action-panel");

        statResTotalLabel = root.Q<Label>("stat-res-total");
        statDevTotalLabel = root.Q<Label>("stat-dev-total");
        statRoadLabel = root.Q<Label>("stat-road");
        statKnightLabel = root.Q<Label>("stat-knight");
        statVpLabel = root.Q<Label>("stat-vp");

        btnStartGame = root.Q<Button>("btn-start-game");
        labelInitialPlacement = root.Q<Label>("label-initial-placement");
        btnRollDice = root.Q<Button>("btn-roll-dice");
        labelMoveRobber = root.Q<Label>("label-move-robber");
        btnEndTurn = root.Q<Button>("btn-end-turn");
        btnTrade = root.Q<Button>("btn-trade");
        btnBuyDevCard = root.Q<Button>("btn-buy-devcard");
        btnDevCardHand = root.Q<Button>("btn-devcard-hand");
        buildHeader = root.Q<VisualElement>("build-header");

        diceDisplay = root.Q<VisualElement>("dice-display");
        die1Face = root.Q<VisualElement>("die-1");
        die2Face = root.Q<VisualElement>("die-2");
        diceResultLabel = root.Q<Label>("dice-result");
        dicePlayerName = root.Q<Label>("dice-player-name");
        numberToken = root.Q<VisualElement>("number-token");
        tokenDots = root.Q<VisualElement>("token-dots");

        die1Dots = BuildDieDots(die1Face);
        die2Dots = BuildDieDots(die2Face);

        resWoodCount = root.Q<Label>("res-wood-count");
        resBrickCount = root.Q<Label>("res-brick-count");
        resWoolCount = root.Q<Label>("res-wool-count");
        resWheatCount = root.Q<Label>("res-wheat-count");
        resOreCount = root.Q<Label>("res-ore-count");

        tradeOverlay = root.Q<VisualElement>("trade-overlay");
        rulesOverlay = root.Q<VisualElement>("rules-overlay");
        devCardOverlay = root.Q<VisualElement>("devcard-overlay");
        resourceSelectOverlay = root.Q<VisualElement>("resource-select-overlay");
        btnCloseTrade = root.Q<Button>("btn-close-trade");
        btnRules = root.Q<Button>("btn-rules");
        btnCloseRules = root.Q<Button>("btn-close-rules");
        btnCloseDevCard = root.Q<Button>("btn-close-devcard");
        btnCancelResourceSelect = root.Q<Button>("btn-cancel-resource-select");

        btnBuildRoad = root.Q<Button>("btn-build-road");
        btnBuildSettlement = root.Q<Button>("btn-build-settlement");
        btnBuildCity = root.Q<Button>("btn-build-city");

        buildRoadCount = root.Q<Label>("build-road-count");
        buildSettlementCount = root.Q<Label>("build-settlement-count");
        buildCityCount = root.Q<Label>("build-city-count");
        buildDevCardCount = root.Q<Label>("build-devcard-count");

        resourceSelectTitle = root.Q<Label>("resource-select-title");
        btnSelectWood = root.Q<Button>("btn-select-wood");
        btnSelectBrick = root.Q<Button>("btn-select-brick");
        btnSelectWool = root.Q<Button>("btn-select-wool");
        btnSelectWheat = root.Q<Button>("btn-select-wheat");
        btnSelectOre = root.Q<Button>("btn-select-ore");

        devCardHand = root.Q<ScrollView>("devcard-hand");
        devCardQuickSlotBar = root.Q<VisualElement>("devcard-quickslot-bar");

        btnTradeTabBank = root.Q<Button>("btn-trade-tab-bank");
        btnTradeTabPlayer = root.Q<Button>("btn-trade-tab-player");
        bankTradeSection = root.Q<VisualElement>("bank-trade-section");
        playerTradeSection = root.Q<VisualElement>("player-trade-section");
        tradeGiveGrid = root.Q<VisualElement>("trade-give-grid");
        tradeReceiveGrid = root.Q<VisualElement>("trade-receive-grid");
        tradeOfferGrid = root.Q<VisualElement>("trade-offer-grid");
        tradeRequestGrid = root.Q<VisualElement>("trade-request-grid");
        btnExecuteBankTrade = root.Q<Button>("btn-execute-bank-trade");
        bankTradeSummaryLabel = root.Q<Label>("bank-trade-summary");

        playerTermsPanel = root.Q<VisualElement>("player-trade-terms");
        playerResponsesPanel = root.Q<VisualElement>("player-trade-responses");
        tradeResponseList = root.Q<VisualElement>("trade-response-list");
        btnSendProposal = root.Q<Button>("btn-send-proposal");
        btnCancelProposal = root.Q<Button>("btn-cancel-proposal");

        incomingTradeOverlay = root.Q<VisualElement>("incoming-trade-overlay");
        incomingTradeFrom = root.Q<Label>("incoming-trade-from");
        incomingTradeOfferGrid = root.Q<VisualElement>("incoming-trade-offer-grid");
        incomingTradeRequestGrid = root.Q<VisualElement>("incoming-trade-request-grid");
        btnIncomingAccept = root.Q<Button>("btn-incoming-accept");
        btnIncomingDecline = root.Q<Button>("btn-incoming-decline");

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

        // Event Log
        eventLogScroll = root.Q<ScrollView>("event-log-scroll");

        // Bank Resources
        bankWood = root.Q<Label>("bank-wood");
        bankBrick = root.Q<Label>("bank-brick");
        bankWool = root.Q<Label>("bank-wool");
        bankWheat = root.Q<Label>("bank-wheat");
        bankOre = root.Q<Label>("bank-ore");
        bankDevCard = root.Q<Label>("bank-devcard");

        // Now Playing
        nowPlayingLabel = root.Q<Label>("now-playing");

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
            GM.OnBuildingPlaced += HandleBuildingPlaced;
            GM.OnRoadPlaced += HandleRoadPlaced;
            GM.OnIncomingTradeProposal += HandleIncomingTradeProposal;
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
            GM.OnBuildingPlaced -= HandleBuildingPlaced;
            GM.OnRoadPlaced -= HandleRoadPlaced;
            GM.OnIncomingTradeProposal -= HandleIncomingTradeProposal;
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
        btnTrade.clicked += OnTradeClicked;
        btnBuyDevCard.clicked += OnBuyDevCardClicked;
        btnDevCardHand.clicked += OnDevCardHandClicked;
        btnCloseTrade.clicked += OnCloseTradeClicked;
        btnRules.clicked += OnRulesClicked;
        btnCloseRules.clicked += OnCloseRulesClicked;
        btnCloseDevCard.clicked += OnCloseDevCardClicked;
        btnCancelResourceSelect.clicked += OnCancelResourceSelect;
        btnCloseTurnOrder.clicked += () => turnOrderOverlay.AddToClassList("overlay--hidden");
        btnResultMenu.clicked += OnResultMenuClicked;
        btnResultRematch.clicked += OnResultRematchClicked;

        btnBuildRoad.clicked += () => GM?.EnterBuildMode(BuildMode.PlacingRoad);
        btnBuildSettlement.clicked += () => GM?.EnterBuildMode(BuildMode.PlacingSettlement);
        btnBuildCity.clicked += () => GM?.EnterBuildMode(BuildMode.PlacingCity);

        // 거래 탭 및 실행
        btnTradeTabBank.clicked += () => SwitchTradeTab(true);
        btnTradeTabPlayer.clicked += () => SwitchTradeTab(false);
        btnExecuteBankTrade.clicked += OnExecuteBankTrade;
        btnSendProposal.clicked += OnSendProposalClicked;
        btnCancelProposal.clicked += OnCancelProposalClicked;
        btnIncomingAccept.clicked += () =>
        {
            GM?.RespondToIncomingTrade(true);
            incomingTradeOverlay.AddToClassList("overlay--hidden");
        };
        btnIncomingDecline.clicked += () =>
        {
            GM?.RespondToIncomingTrade(false);
            incomingTradeOverlay.AddToClassList("overlay--hidden");
        };

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

        // 음량 슬라이더 - BGMManager 연동
        if (BGMManager.Instance != null)
            sliderBgm.value = BGMManager.Instance.volume * 100f;
        sliderBgm.RegisterValueChangedCallback(evt =>
        {
            bgmValueLabel.text = Mathf.RoundToInt(evt.newValue).ToString();
            if (BGMManager.Instance != null)
                BGMManager.Instance.SetVolume(evt.newValue / 100f);
        });
        sliderSfx.RegisterValueChangedCallback(evt =>
        {
            sfxValueLabel.text = Mathf.RoundToInt(evt.newValue).ToString();
        });
    }

    void OnStartGameClicked() => GM?.StartGame();
    void OnRollDiceClicked() => GM?.RollDice();
    void OnEndTurnClicked() => GM?.EndTurn();

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
        string who = GM.GetPlayerName(playerIndex);
        AddEventLog($"--- {who}의 턴 (턴 {GM.TurnNumber}) ---", "system");

        UpdateTopBar();
        UpdateActionButtons();
        UpdateOpponentHighlight();
        RefreshDevCardQuickSlots();
    }

    void HandlePhaseChanged(GamePhase newPhase)
    {
        UpdateTopBar();
        UpdateActionButtons();
        UpdateOpponentHighlight();
        RefreshDevCardQuickSlots();

        if (newPhase == GamePhase.InitialPlacement)
            ShowTurnOrderOverlay();

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

        string who = GM.GetPlayerName(GM.CurrentPlayerIndex);
        AddEventLog($"{who} 주사위 [{die1}]+[{die2}] = {total}", "dice");

        if (total == 7)
        {
            ShowToast("robber", "도적 출현! 자원 7장 이상 보유자는 절반 폐기");
            AddEventLog("도적 출현!", "robber");
        }
    }

    void HandlePlayerListChanged()
    {
        RebuildOpponentBar();
        UpdateActionButtons();
    }

    void HandleResourceChanged(int playerIndex, ResourceType type, int newCount)
    {
        var key = (playerIndex, type);
        int prev = lastKnownResources.GetValueOrDefault(key, 0);
        lastKnownResources[key] = newCount;

        int delta = newCount - prev;
        if (delta != 0)
        {
            // 이벤트 로그: 프레임 단위 배치 처리 (같은 자원 합산)
            BufferResourceLog(playerIndex, type, delta);

            // 상대 카드 status bar에 자원 증감 표시
            string resName = GetResourceName(type);
            ShowOpponentResourceDelta(playerIndex, resName, delta);
        }

        if (playerIndex == GM.LocalPlayerIndex)
        {
            UpdateResource(type, newCount);
            UpdateBuildCounts();
            UpdatePlayerStats();
        }
        UpdateOpponentCard(playerIndex);
        UpdateBankResources();
    }

    void HandleVPChanged(int playerIndex, int vp)
    {
        UpdateOpponentCard(playerIndex);
        if (playerIndex == GM.LocalPlayerIndex)
            UpdatePlayerStats();
    }

    void HandleDevCardPurchased(int playerIndex, DevCardType cardType)
    {
        string who = GM.GetPlayerName(playerIndex);
        AddEventLog($"{who} 발전카드 구매", "devcard");

        if (playerIndex == GM.LocalPlayerIndex)
        {
            Debug.Log($"[HUD] 발전카드 구매: {cardType}");
            UpdateBuildCounts();
            UpdatePlayerStats();
            RefreshDevCardQuickSlots();
        }
        UpdateOpponentCard(playerIndex);
    }

    void HandleDevCardUsed(int playerIndex, DevCardType cardType)
    {
        string who = GM.GetPlayerName(playerIndex);
        AddEventLog($"{who} {GetDevCardName(cardType)} 사용", "devcard");

        if (cardType == DevCardType.Knight)
            ShowToast("knight", $"{who}이(가) 기사 카드를 사용했습니다!");

        UpdateOpponentCard(playerIndex);
        if (playerIndex == GM.LocalPlayerIndex)
        {
            UpdatePlayerStats();
            RefreshDevCardQuickSlots();
        }
    }

    void HandleLongestRoadChanged(int playerIndex, bool gained)
    {
        UpdateAllOpponentCards();
        UpdatePlayerStats();
        if (gained)
        {
            string who = GM.GetPlayerName(playerIndex);
            ShowToast("longest-road", $"{who}이(가) 최장교역로를 획득! (+2점)");
            AddEventLog($"{who} 최장교역로 획득! (+2점)", "bonus");
        }
    }

    void HandleLargestArmyChanged(int playerIndex, bool gained)
    {
        UpdateAllOpponentCards();
        UpdatePlayerStats();
        if (gained)
        {
            string who = GM.GetPlayerName(playerIndex);
            ShowToast("largest-army", $"{who}이(가) 최대기사단을 획득! (+2점)");
            AddEventLog($"{who} 최대기사단 획득! (+2점)", "bonus");
        }
    }

    void HandleRobberMoved(HexCoord newCoord)
    {
        string who = GM?.GetPlayerName(GM.CurrentPlayerIndex) ?? "?";
        AddEventLog($"{who} 도적 이동", "robber");

        var gridView = FindObjectOfType<HexGridView>();
        if (gridView != null)
            gridView.MoveRobberVisual(newCoord);
    }

    void HandleRobberSteal(int thief, int victim, ResourceType resource)
    {
        string thiefName = GM.GetPlayerName(thief);
        string victimName = GM.GetPlayerName(victim);
        ShowToast("robber", $"{thiefName}이(가) {victimName}에게서 자원을 약탈!");
        AddEventLog($"{thiefName} -> {victimName} 약탈", "robber");
    }

    void HandleBankTrade(int player, ResourceType gave, ResourceType received, int rate)
    {
        string who = GM.GetPlayerName(player);
        ShowToast("trade", $"{who}: {GetResourceName(gave)}×{rate} → {GetResourceName(received)}×1");
        AddEventLog($"{who} 은행거래: {GetResourceName(gave)}x{rate} -> {GetResourceName(received)}x1", "trade");
        tradeOverlay.AddToClassList("overlay--hidden");
    }

    void HandlePlayerTrade(int player1, int player2)
    {
        string name1 = GM.GetPlayerName(player1);
        string name2 = GM.GetPlayerName(player2);
        ShowToast("trade", $"{name1} ↔ {name2} 거래 성사!");
        AddEventLog($"{name1} <-> {name2} 거래 성사", "trade");
        tradeOverlay.AddToClassList("overlay--hidden");
    }

    void HandleIncomingTradeProposal(int proposer, Dictionary<ResourceType, int> offerToHuman, Dictionary<ResourceType, int> requestFromHuman)
    {
        if (incomingTradeOverlay == null) return;

        incomingTradeFrom.text = $"{GM?.GetPlayerName(proposer) ?? "?"}의 제안";

        BuildIncomingResourceDisplay(incomingTradeOfferGrid, offerToHuman);
        BuildIncomingResourceDisplay(incomingTradeRequestGrid, requestFromHuman);

        incomingTradeOverlay.RemoveFromClassList("overlay--hidden");
    }

    void BuildIncomingResourceDisplay(VisualElement grid, Dictionary<ResourceType, int> amounts)
    {
        grid.Clear();
        foreach (var kv in amounts)
        {
            if (kv.Value <= 0) continue;
            var btn = new Button { text = $"{GetResourceName(kv.Key)}\n×{kv.Value}" };
            btn.AddToClassList("trade-res-btn");
            btn.AddToClassList("trade-res-btn--selected");
            btn.SetEnabled(false);
            grid.Add(btn);
        }
    }

    // ========================
    // UI UPDATES
    // ========================

    void RefreshAllUI()
    {
        // 주사위 패널은 첫 굴림 전까지 숨김
        if (diceDisplay != null)
            SetVisible(diceDisplay, false);

        UpdateTopBar();
        UpdateActionButtons();
        RebuildOpponentBar();
        UpdateResourceDisplay(0, 0, 0, 0, 0);
        UpdatePlayerStats();
        UpdateBuildCounts();
        UpdateBankResources();
        RefreshDevCardQuickSlots();
        AddEventLog("게임 대기중...", "system");
    }

    void UpdatePlayerStats()
    {
        if (GM == null) return;
        var state = GM.GetPlayerState(GM.LocalPlayerIndex);
        if (state == null) return;

        if (statResTotalLabel != null) statResTotalLabel.text = state.TotalResourceCount.ToString();
        if (statDevTotalLabel != null) statDevTotalLabel.text = state.DevCards.Count.ToString();

        int roadLen = GM.GetLongestRoadLength(GM.LocalPlayerIndex);
        statRoadLabel.text = roadLen.ToString();
        statKnightLabel.text = state.KnightsPlayed.ToString();
        statVpLabel.text = state.VictoryPoints.ToString();
    }

    void UpdateBuildCounts()
    {
        if (GM == null) return;
        var state = GM.GetPlayerState(GM.LocalPlayerIndex);
        if (state == null) return;

        if (buildRoadCount != null)
            buildRoadCount.text = Mathf.Min(AffordableCount(state, BuildingCosts.Road), state.RoadsRemaining).ToString();
        if (buildSettlementCount != null)
            buildSettlementCount.text = Mathf.Min(AffordableCount(state, BuildingCosts.Settlement), state.SettlementsRemaining).ToString();
        if (buildCityCount != null)
            buildCityCount.text = Mathf.Min(AffordableCount(state, BuildingCosts.City), state.CitiesRemaining).ToString();
        if (buildDevCardCount != null)
            buildDevCardCount.text = Mathf.Min(AffordableCount(state, BuildingCosts.DevelopmentCard), GM.DevCardDeckRemaining).ToString();
    }

    /// <summary>현재 자원으로 건설 가능한 횟수 계산</summary>
    static int AffordableCount(PlayerState state, Dictionary<ResourceType, int> cost)
    {
        int min = int.MaxValue;
        foreach (var kv in cost)
        {
            int have = state.Resources.ContainsKey(kv.Key) ? state.Resources[kv.Key] : 0;
            min = Mathf.Min(min, have / kv.Value);
        }
        return min == int.MaxValue ? 0 : min;
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

        // 행동 패널 항상 표시
        SetVisible(actionPanel, true);

        SetVisible(btnStartGame, phase == GamePhase.WaitingForPlayers && GM.IsHost);

        // 초기 배치 안내
        bool initialPhase = isMyTurn && phase == GamePhase.InitialPlacement;
        SetVisible(labelInitialPlacement, initialPhase);
        if (initialPhase)
        {
            labelInitialPlacement.text = GM.CurrentBuildMode == BuildMode.PlacingRoad
                ? "도로를 배치하세요" : "마을을 배치하세요";
        }

        SetVisible(btnRollDice, isMyTurn && phase == GamePhase.RollDice);
        SetVisible(labelMoveRobber, isMyTurn && (phase == GamePhase.MoveRobber || phase == GamePhase.StealResource));
        SetVisible(btnEndTurn, isMyTurn && phase == GamePhase.Action);

        bool actionPhase = isMyTurn && phase == GamePhase.Action;

        // 건설/거래/카드보기는 항상 표시, 내 턴 Action일 때만 활성화
        btnBuildRoad.SetEnabled(actionPhase);
        btnBuildSettlement.SetEnabled(actionPhase);
        btnBuildCity.SetEnabled(actionPhase);
        btnBuyDevCard.SetEnabled(actionPhase);
        btnTrade.SetEnabled(actionPhase);
        btnDevCardHand.SetEnabled(phase != GamePhase.WaitingForPlayers && phase != GamePhase.GameOver);
    }

    void HideAllButtons()
    {
        // 액션 패널은 항상 표시, 상태 버튼만 숨김
        SetVisible(btnStartGame, false);
        SetVisible(labelInitialPlacement, false);
        SetVisible(btnRollDice, false);
        SetVisible(labelMoveRobber, false);
        SetVisible(btnEndTurn, false);
        btnBuildRoad.SetEnabled(false);
        btnBuildSettlement.SetEnabled(false);
        btnBuildCity.SetEnabled(false);
        btnBuyDevCard.SetEnabled(false);
        btnTrade.SetEnabled(false);
        btnDevCardHand.SetEnabled(false); // HideAllButtons는 GM==null일 때만 호출
    }

    static void SetVisible(VisualElement element, bool visible)
    {
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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
    // DEV CARD QUICK SLOT BAR
    // ========================

    void RefreshDevCardQuickSlots()
    {
        if (devCardQuickSlotBar == null || GM == null) return;
        devCardQuickSlotBar.Clear();

        var state = GM.GetPlayerState(GM.LocalPlayerIndex);
        if (state == null || state.DevCards.Count == 0)
        {
            SetVisible(devCardQuickSlotBar, false);
            return;
        }

        // 종류별 카운트 집계 (미사용만)
        var counts = new Dictionary<DevCardType, int>();
        foreach (var card in state.DevCards)
        {
            if (card.IsUsed) continue;
            counts.TryGetValue(card.Type, out int cnt);
            counts[card.Type] = cnt + 1;
        }

        if (counts.Count == 0)
        {
            SetVisible(devCardQuickSlotBar, false);
            return;
        }

        bool isMyTurn = GM.IsMyTurn();
        bool canUseCard = isMyTurn
            && !state.HasUsedDevCardThisTurn
            && (GM.CurrentPhase == GamePhase.Action || GM.CurrentPhase == GamePhase.RollDice);

        // 표시 순서: 기사 → 도로건설 → 풍년 → 독점 → 승리점
        DevCardType[] order = { DevCardType.Knight, DevCardType.RoadBuilding, DevCardType.YearOfPlenty, DevCardType.Monopoly, DevCardType.VictoryPoint };
        foreach (var type in order)
        {
            if (!counts.TryGetValue(type, out int count)) continue;

            var btn = new Button();
            btn.AddToClassList("devcard-slot-btn");
            if (type == DevCardType.Knight) btn.AddToClassList("devcard-slot-btn--knight");

            var countLabel = new Label(count.ToString());
            countLabel.AddToClassList("devcard-slot-count");

            var nameLabel = new Label(GetDevCardName(type));
            nameLabel.AddToClassList("devcard-slot-name");

            btn.Add(countLabel);
            btn.Add(nameLabel);

            bool isVP = type == DevCardType.VictoryPoint;
            bool usable = canUseCard && !isVP;

            // 사용 불가 이유 힌트
            if (!isVP && isMyTurn && !canUseCard)
            {
                string hint = state.HasUsedDevCardThisTurn ? "(이미 사용)" :
                              GM.CurrentPhase == GamePhase.RollDice ? "(주사위 전 가능)" : "";
                if (!string.IsNullOrEmpty(hint))
                {
                    var hintLabel = new Label(hint);
                    hintLabel.AddToClassList("devcard-slot-hint");
                    btn.Add(hintLabel);
                }
            }

            if (!usable) btn.AddToClassList("devcard-slot-btn--disabled");
            btn.SetEnabled(usable);

            if (usable)
            {
                // 해당 타입의 첫 번째 사용 가능한 카드 찾기
                var capturedType = type;
                btn.clicked += () =>
                {
                    var card = GM.GetPlayerState(GM.LocalPlayerIndex)?.DevCards
                        .Find(c => !c.IsUsed && c.Type == capturedType && c.CanUseOnTurn(GM.TurnNumber));
                    if (card != null) UseDevCard(card);
                };
            }

            devCardQuickSlotBar.Add(btn);
        }

        SetVisible(devCardQuickSlotBar, true);
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
        UpdateBankTradeSummary();
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
                bankReceiveSelected = null;
                BuildBankGiveButtons();
                BuildBankReceiveButtons();
                UpdateBankTradeSummary();
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
            int bankStock = GM?.GetBankResourceCount(res) ?? 0;
            var btn = new Button();
            btn.text = $"{GetResourceName(res)}\n(재고 {bankStock})";
            btn.AddToClassList("trade-res-btn");
            btn.SetEnabled(bankGiveSelected.HasValue && bankGiveSelected != res && bankStock > 0);

            if (bankReceiveSelected == res)
                btn.AddToClassList("trade-res-btn--selected");

            var captured = res;
            btn.clicked += () =>
            {
                bankReceiveSelected = captured;
                BuildBankReceiveButtons();
                UpdateBankTradeSummary();
                UpdateBankTradeExecuteButton();
            };

            tradeReceiveGrid.Add(btn);
        }
    }

    void UpdateBankTradeSummary()
    {
        if (bankTradeSummaryLabel == null) return;

        if (bankGiveSelected.HasValue && bankReceiveSelected.HasValue)
        {
            int rate = GM?.GetTradeRate(bankGiveSelected.Value) ?? 4;
            bankTradeSummaryLabel.text =
                $"{GetResourceName(bankGiveSelected.Value)} ×{rate}  →  {GetResourceName(bankReceiveSelected.Value)} ×1";
        }
        else if (bankGiveSelected.HasValue)
        {
            int rate = GM?.GetTradeRate(bankGiveSelected.Value) ?? 4;
            bankTradeSummaryLabel.text = $"{GetResourceName(bankGiveSelected.Value)} ×{rate}  →  ?";
        }
        else
        {
            bankTradeSummaryLabel.text = "";
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
        proposalResponses.Clear();
        pendingOffer = null;
        pendingRequest = null;

        foreach (var res in AllResources)
        {
            playerOfferAmounts[res] = 0;
            playerRequestAmounts[res] = 0;
        }

        BuildAmountGrid(tradeOfferGrid, playerOfferAmounts, true);
        BuildAmountGrid(tradeRequestGrid, playerRequestAmounts, false);
        UpdateSendProposalButton();
        ShowPlayerTradeTerms();
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
                    UpdateSendProposalButton();
                }
            };
            btnPlus.clicked += () =>
            {
                int max = isOffer ? myState.Resources[capturedRes] : 99;
                if (amounts[capturedRes] < max)
                {
                    amounts[capturedRes]++;
                    BuildAmountGrid(grid, amounts, isOffer);
                    UpdateSendProposalButton();
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

    void UpdateSendProposalButton()
    {
        bool hasOffer = false;
        bool hasRequest = false;
        foreach (var kv in playerOfferAmounts) if (kv.Value > 0) { hasOffer = true; break; }
        foreach (var kv in playerRequestAmounts) if (kv.Value > 0) { hasRequest = true; break; }
        btnSendProposal?.SetEnabled(hasOffer && hasRequest);
    }

    void ShowPlayerTradeTerms()
    {
        playerTermsPanel?.RemoveFromClassList("trade-section--hidden");
        playerResponsesPanel?.AddToClassList("trade-section--hidden");
    }

    void ShowPlayerTradeResponses()
    {
        playerTermsPanel?.AddToClassList("trade-section--hidden");
        playerResponsesPanel?.RemoveFromClassList("trade-section--hidden");
    }

    void OnSendProposalClicked()
    {
        if (GM == null) return;

        pendingOffer = new Dictionary<ResourceType, int>();
        pendingRequest = new Dictionary<ResourceType, int>();
        foreach (var kv in playerOfferAmounts) if (kv.Value > 0) pendingOffer[kv.Key] = kv.Value;
        foreach (var kv in playerRequestAmounts) if (kv.Value > 0) pendingRequest[kv.Key] = kv.Value;
        if (pendingOffer.Count == 0 || pendingRequest.Count == 0) return;

        proposalResponses.Clear();

        for (int i = 0; i < GM.PlayerCount; i++)
        {
            if (i == GM.LocalPlayerIndex) continue;

            if (GM.IsPlayerAI(i))
            {
                // AI가 받는 것 = 내가 주는 것(pendingOffer), AI가 줘야 하는 것 = 내가 원하는 것(pendingRequest)
                bool accepts = AIBoardEvaluator.ShouldAcceptTradeOffer(i, GM, pendingOffer, pendingRequest);
                proposalResponses[i] = accepts;
            }
            else
            {
                proposalResponses[i] = null; // 인간: 버튼으로 직접 응답
            }
        }

        ShowPlayerTradeResponses();
        BuildResponseList();
    }

    void OnCancelProposalClicked()
    {
        proposalResponses.Clear();
        pendingOffer = null;
        pendingRequest = null;
        ShowPlayerTradeTerms();
        BuildAmountGrid(tradeOfferGrid, playerOfferAmounts, true);
        BuildAmountGrid(tradeRequestGrid, playerRequestAmounts, false);
        UpdateSendProposalButton();
    }

    void BuildResponseList()
    {
        tradeResponseList.Clear();
        if (GM == null) return;

        bool anyAccepted = false;
        bool anyPending = false;

        foreach (var kv in proposalResponses)
        {
            int playerIndex = kv.Key;
            bool? response = kv.Value;

            var row = new VisualElement();
            row.AddToClassList("trade-response-row");
            row.style.borderLeftColor = PlayerColors[playerIndex % PlayerColors.Length];

            var nameLabel = new Label(GM.GetPlayerName(playerIndex));
            nameLabel.AddToClassList("trade-response-row__name");
            row.Add(nameLabel);

            if (response == null) // 인간 플레이어 대기 중
            {
                anyPending = true;
                var acceptBtn = new Button { text = "✓ 수락" };
                acceptBtn.AddToClassList("trade-response-row__status");
                acceptBtn.AddToClassList("trade-response--pending");
                var declineBtn = new Button { text = "✗ 거절" };
                declineBtn.AddToClassList("trade-response-row__status");
                declineBtn.AddToClassList("trade-response--pending");
                declineBtn.style.marginLeft = 4;

                int captured = playerIndex;
                acceptBtn.clicked += () => { proposalResponses[captured] = true; BuildResponseList(); };
                declineBtn.clicked += () => { proposalResponses[captured] = false; BuildResponseList(); };

                row.Add(acceptBtn);
                row.Add(declineBtn);
            }
            else if (response == true)
            {
                anyAccepted = true;
                var statusBtn = new Button { text = "✓ 수락" };
                statusBtn.AddToClassList("trade-response-row__status");
                statusBtn.AddToClassList("trade-response--accepted");

                int captured = playerIndex;
                statusBtn.clicked += () => OnSelectTradePartner(captured);
                row.Add(statusBtn);
            }
            else
            {
                var statusLabel = new Label("✗ 거절");
                statusLabel.AddToClassList("trade-response-row__status");
                statusLabel.AddToClassList("trade-response--declined");
                row.Add(statusLabel);
            }

            tradeResponseList.Add(row);
        }

        if (!anyAccepted && !anyPending)
        {
            var msg = new Label("모든 플레이어가 거절했습니다.");
            msg.AddToClassList("trade-subtitle");
            msg.style.marginTop = 8;
            tradeResponseList.Add(msg);
        }
    }

    void OnSelectTradePartner(int targetPlayerIndex)
    {
        if (pendingOffer == null || pendingRequest == null) return;

        if (!GM.TryPlayerTrade(targetPlayerIndex, pendingOffer, pendingRequest))
        {
            ShowToast("trade", "거래 실패: 자원이 부족합니다");
            proposalResponses[targetPlayerIndex] = false;
            BuildResponseList();
        }
        // 성공 시 HandlePlayerTrade 이벤트가 overlay를 닫음
    }

    // ========================
    // DICE
    // ========================

    void ShowDice(int die1, int die2, int total)
    {
        // 첫 굴림 시 주사위 패널 표시 (이후 계속 보임)
        if (diceDisplay != null)
            SetVisible(diceDisplay, true);

        SetDieFace(die1Dots, die1);
        SetDieFace(die2Dots, die2);
        diceResultLabel.text = total.ToString();
        UpdateNumberToken(total);

        // 현재 플레이어 이름과 색상 표시
        if (GM != null && dicePlayerName != null)
        {
            int current = GM.CurrentPlayerIndex;
            dicePlayerName.text = GM.GetPlayerName(current);
            dicePlayerName.style.backgroundColor = PlayerColors[current % PlayerColors.Length];
        }
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

    /// <summary>숫자 토큰 스타일 업데이트 (카탄 확률 표시)</summary>
    void UpdateNumberToken(int total)
    {
        if (numberToken == null || tokenDots == null) return;

        Color bgColor, fontColor, dotColor;
        int dotCount;

        if (total == 7)
        {
            bgColor = Color.black;
            fontColor = Color.white;
            dotColor = Color.black;
            dotCount = 0;
        }
        else if (total == 6 || total == 8)
        {
            bgColor = new Color(0.97f, 0.97f, 0.97f);
            fontColor = new Color(0.8f, 0.1f, 0.1f);
            dotColor = new Color(0.8f, 0.1f, 0.1f);
            dotCount = 5;
        }
        else if (total == 5 || total == 9)
        {
            bgColor = new Color(0.97f, 0.97f, 0.97f);
            fontColor = Color.black;
            dotColor = Color.black;
            dotCount = 4;
        }
        else if (total == 4 || total == 10)
        {
            bgColor = new Color(0.97f, 0.97f, 0.97f);
            fontColor = Color.black;
            dotColor = Color.black;
            dotCount = 3;
        }
        else if (total == 3 || total == 11)
        {
            bgColor = new Color(0.97f, 0.97f, 0.97f);
            fontColor = Color.black;
            dotColor = Color.black;
            dotCount = 2;
        }
        else
        {
            bgColor = new Color(0.97f, 0.97f, 0.97f);
            fontColor = Color.black;
            dotColor = Color.black;
            dotCount = 1;
        }

        numberToken.style.backgroundColor = bgColor;
        diceResultLabel.style.color = fontColor;

        tokenDots.Clear();
        tokenDots.style.display = dotCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;

        for (int i = 0; i < dotCount; i++)
        {
            var dot = new VisualElement();
            dot.style.width = 4;
            dot.style.height = 4;
            dot.style.backgroundColor = dotColor;
            dot.style.borderTopLeftRadius = 2;
            dot.style.borderTopRightRadius = 2;
            dot.style.borderBottomLeftRadius = 2;
            dot.style.borderBottomRightRadius = 2;
            dot.style.marginLeft = 1;
            dot.style.marginRight = 1;
            tokenDots.Add(dot);
        }
    }

    // ========================
    // OPPONENT BAR
    // ========================

    void RebuildOpponentBar()
    {
        if (opponentBar == null) return;
        opponentBar.Clear();
        opponentCards.Clear();
        if (GM == null) return;

        for (int i = 0; i < GM.PlayerCount; i++)
        {
            if (i == GM.LocalPlayerIndex) continue;
            var ui = CreateOpponentCard(i);
            opponentBar.Add(ui.card);
            opponentCards[i] = ui;
        }

        UpdateAllOpponentCards();
        UpdateOpponentHighlight();
    }

    OpponentCardUI CreateOpponentCard(int playerIndex)
    {
        var ui = new OpponentCardUI();

        var instance = opponentCardTemplate.Instantiate();
        ui.card = instance.Q<VisualElement>("opponent-card");

        // Header
        var header = ui.card.Q<VisualElement>("opponent-header");
        header.style.backgroundColor = PlayerColors[playerIndex % PlayerColors.Length];
        ui.card.Q<Label>("opponent-name").text = GM.GetPlayerName(playerIndex);

        ui.vpLabel = ui.card.Q<Label>("opponent-vp");
        ui.resCountLabel = ui.card.Q<Label>("opponent-res-value");
        ui.devCountLabel = ui.card.Q<Label>("opponent-dev-value");
        ui.roadValueLabel = ui.card.Q<Label>("opponent-road-value");
        ui.roadTextLabel = ui.card.Q<Label>("opponent-road-label");
        ui.knightValueLabel = ui.card.Q<Label>("opponent-knight-value");
        ui.knightTextLabel = ui.card.Q<Label>("opponent-knight-label");
        ui.statusBar = ui.card.Q<VisualElement>("opponent-status");
        ui.statusText = ui.card.Q<Label>("opponent-status-text");

        return ui;
    }

    void UpdateAllOpponentCards()
    {
        foreach (var kvp in opponentCards)
            UpdateOpponentCard(kvp.Key);
    }

    void UpdateOpponentCard(int playerIndex)
    {
        if (!opponentCards.TryGetValue(playerIndex, out var ui) || GM == null) return;

        var state = GM.GetPlayerState(playerIndex);
        if (state == null) return;

        var colorNormal = new Color(0.91f, 0.86f, 0.78f);
        var colorRed = new Color(0.77f, 0.36f, 0.24f);
        var colorGold = new Color(0.83f, 0.66f, 0.27f);

        ui.vpLabel.text = $"{state.VictoryPoints} VP";

        int resCount = state.TotalResourceCount;
        ui.resCountLabel.text = resCount.ToString();
        ui.resCountLabel.style.color = resCount >= 7 ? colorRed : colorNormal;

        ui.devCountLabel.text = state.DevCards.Count.ToString();

        int roadLen = GM.GetLongestRoadLength(playerIndex);
        ui.roadValueLabel.text = roadLen.ToString();
        ui.roadTextLabel.text = state.HasLongestRoad ? "최장\n도로" : "도로";
        ui.roadValueLabel.style.color = state.HasLongestRoad ? colorGold : colorNormal;

        ui.knightValueLabel.text = state.KnightsPlayed.ToString();
        ui.knightTextLabel.text = state.HasLargestArmy ? "최강\n기사" : "기사";
        ui.knightValueLabel.style.color = state.HasLargestArmy ? colorGold : colorNormal;
    }

    void UpdateOpponentHighlight()
    {
        if (GM == null) return;
        int current = GM.CurrentPlayerIndex;

        foreach (var kvp in opponentCards)
        {
            bool isActive = kvp.Key == current;

            if (isActive)
                kvp.Value.card.AddToClassList("opponent-card--active");
            else
                kvp.Value.card.RemoveFromClassList("opponent-card--active");

            // 자원 델타 표시 중이면 건드리지 않음
            bool showingDelta = opponentStatusTimers.ContainsKey(kvp.Key);
            if (!showingDelta)
            {
                kvp.Value.statusBar.style.display = isActive ? DisplayStyle.Flex : DisplayStyle.None;
                if (isActive)
                {
                    kvp.Value.statusBar.style.backgroundColor = StatusYellow;
                    kvp.Value.statusText.text = GetStatusText(GM.CurrentPhase);
                }
            }
        }
    }

    static readonly Color StatusGreen = new(0.2f, 0.6f, 0.2f);
    static readonly Color StatusRed = new(0.7f, 0.2f, 0.15f);
    static readonly Color StatusYellow = new(1f, 0.7f, 0f);
    readonly Dictionary<int, Coroutine> opponentStatusTimers = new();
    readonly Dictionary<int, List<(string resName, int delta)>> pendingDeltas = new();

    void ShowOpponentResourceDelta(int playerIndex, string resName, int delta)
    {
        if (!opponentCards.TryGetValue(playerIndex, out var ui)) return;

        // 기존 타이머 취소
        if (opponentStatusTimers.TryGetValue(playerIndex, out var existing) && existing != null)
            StopCoroutine(existing);

        if (!pendingDeltas.ContainsKey(playerIndex))
            pendingDeltas[playerIndex] = new List<(string, int)>();
        pendingDeltas[playerIndex].Add((resName, delta));

        // +와 -를 분리해서 순차 표시
        opponentStatusTimers[playerIndex] = StartCoroutine(ShowDeltaSequence(playerIndex));
    }

    IEnumerator ShowDeltaSequence(int playerIndex)
    {
        if (!opponentCards.TryGetValue(playerIndex, out var ui)) yield break;

        // 한 프레임 대기 (같은 프레임 델타 합산)
        yield return null;

        var deltas = pendingDeltas.GetValueOrDefault(playerIndex);
        if (deltas == null || deltas.Count == 0) yield break;

        // 같은 자원 합산
        var aggregated = new Dictionary<string, int>();
        foreach (var (name, d) in deltas)
        {
            if (aggregated.ContainsKey(name)) aggregated[name] += d;
            else aggregated[name] = d;
        }

        var gains = new List<string>();
        var losses = new List<string>();
        foreach (var kv in aggregated)
        {
            if (kv.Value > 0) gains.Add($"+{kv.Value} {kv.Key}");
            else if (kv.Value < 0) losses.Add($"{kv.Value} {kv.Key}");
        }

        // 획득 먼저 표시
        if (gains.Count > 0)
        {
            ui.statusText.text = string.Join(", ", gains);
            ui.statusBar.style.backgroundColor = StatusGreen;
            ui.statusBar.style.display = DisplayStyle.Flex;
            yield return new WaitForSeconds(1.5f);
        }

        // 손실 표시
        if (losses.Count > 0)
        {
            ui.statusText.text = string.Join(", ", losses);
            ui.statusBar.style.backgroundColor = StatusRed;
            ui.statusBar.style.display = DisplayStyle.Flex;
            yield return new WaitForSeconds(1.5f);
        }

        // 원래 상태로 복귀
        pendingDeltas.Remove(playerIndex);

        bool isActive = GM != null && GM.CurrentPlayerIndex == playerIndex;
        if (isActive)
        {
            ui.statusBar.style.backgroundColor = StatusYellow;
            ui.statusText.text = GetStatusText(GM.CurrentPhase);
        }
        else
        {
            ui.statusBar.style.display = DisplayStyle.None;
        }

        opponentStatusTimers.Remove(playerIndex);
    }

    static string GetStatusText(GamePhase phase) => phase switch
    {
        GamePhase.InitialPlacement => "배치중 ...",
        GamePhase.RollDice => "주사위 굴리는중 ...",
        GamePhase.Action => "행동중 ...",
        GamePhase.MoveRobber => "도적 이동 ...",
        GamePhase.StealResource => "약탈중 ...",
        _ => ""
    };

    void HandleBuildingPlaced(int playerIndex, int vertexId, BuildingType type)
    {
        string who = GM.GetPlayerName(playerIndex);
        string building = type == BuildingType.Settlement ? "마을" : "도시";
        int vp = type == BuildingType.Settlement ? 1 : 2;
        AddEventLog($"{who} {building} 건설 (+{vp}점)", "build");

        UpdateOpponentCard(playerIndex);
        if (playerIndex == GM.LocalPlayerIndex)
        {
            UpdatePlayerStats();
            UpdateBuildCounts();
        }
    }

    void HandleRoadPlaced(int playerIndex, int edgeId)
    {
        string who = GM.GetPlayerName(playerIndex);
        AddEventLog($"{who} 도로 건설", "build");

        UpdateOpponentCard(playerIndex);
        if (playerIndex == GM.LocalPlayerIndex)
        {
            UpdatePlayerStats();
            UpdateBuildCounts();
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

    void UpdateBankResources()
    {
        if (GM == null || bankWood == null) return;
        bankWood.text = GM.GetBankResourceCount(ResourceType.Wood).ToString();
        bankBrick.text = GM.GetBankResourceCount(ResourceType.Brick).ToString();
        bankWool.text = GM.GetBankResourceCount(ResourceType.Wool).ToString();
        bankWheat.text = GM.GetBankResourceCount(ResourceType.Wheat).ToString();
        bankOre.text = GM.GetBankResourceCount(ResourceType.Ore).ToString();
        if (bankDevCard != null)
            bankDevCard.text = GM.DevCardDeckRemaining.ToString();
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
    // EVENT LOG
    // ========================

    const int MAX_LOG_ENTRIES = 100;

    void BufferResourceLog(int playerIndex, ResourceType type, int delta)
    {
        if (!pendingLogDeltas.ContainsKey(playerIndex))
            pendingLogDeltas[playerIndex] = new Dictionary<ResourceType, int>();

        var dict = pendingLogDeltas[playerIndex];
        if (dict.ContainsKey(type)) dict[type] += delta;
        else dict[type] = delta;

        if (logFlushCoroutine == null)
            logFlushCoroutine = StartCoroutine(FlushResourceLogs());
    }

    IEnumerator FlushResourceLogs()
    {
        yield return null; // 한 프레임 대기 (같은 프레임 델타 합산)

        foreach (var kv in pendingLogDeltas)
        {
            string who = GM.GetPlayerName(kv.Key);

            var gains = new List<string>();
            var losses = new List<string>();
            foreach (var res in kv.Value)
            {
                if (res.Value > 0) gains.Add($"+{res.Value} {GetResourceName(res.Key)}");
                else if (res.Value < 0) losses.Add($"{res.Value} {GetResourceName(res.Key)}");
            }

            if (gains.Count > 0)
                AddEventLog($"{who} {string.Join(", ", gains)}", "resource");
            if (losses.Count > 0)
                AddEventLog($"{who} {string.Join(", ", losses)}", "robber");
        }

        pendingLogDeltas.Clear();
        logFlushCoroutine = null;
    }

    void AddEventLog(string message, string cssModifier = "")
    {
        if (eventLogScroll == null) return;

        var entry = new VisualElement();
        entry.AddToClassList("event-log-entry");

        var label = new Label(message);
        label.AddToClassList("event-log__text");
        if (!string.IsNullOrEmpty(cssModifier))
            label.AddToClassList($"event-log__text--{cssModifier}");

        entry.Add(label);
        eventLogScroll.Add(entry);

        // 최대 엔트리 제한
        while (eventLogScroll.childCount > MAX_LOG_ENTRIES)
            eventLogScroll.RemoveAt(0);

        // 자동 스크롤
        eventLogScroll.schedule.Execute(() =>
            eventLogScroll.scrollOffset = new Vector2(0, float.MaxValue));
    }

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
