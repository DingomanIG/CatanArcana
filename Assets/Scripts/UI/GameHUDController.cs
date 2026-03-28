using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 인게임 HUD 컨트롤러
/// UIDocument에서 UXML 요소를 바인딩하고 TurnManager 이벤트를 구독하여 UI 갱신
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
    Label diceResultLabel;
    Label diceDetailLabel;

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

    // State
    readonly Dictionary<ulong, VisualElement> playerEntries = new();
    Coroutine diceHideCoroutine;
    const float DICE_DISPLAY_DURATION = 3f;

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
        diceResultLabel = root.Q<Label>("dice-result");
        diceDetailLabel = root.Q<Label>("dice-detail");

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
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnChanged += HandleTurnChanged;
            TurnManager.Instance.OnPhaseChanged += HandlePhaseChanged;
            TurnManager.Instance.OnDiceRolled += HandleDiceRolled;
        }

        if (GameNetworkManager.Instance != null)
        {
            GameNetworkManager.Instance.OnClientConnected += HandleClientConnected;
            GameNetworkManager.Instance.OnClientDisconnected += HandleClientDisconnected;
        }
    }

    void UnsubscribeFromEvents()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnChanged -= HandleTurnChanged;
            TurnManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
            TurnManager.Instance.OnDiceRolled -= HandleDiceRolled;
        }

        if (GameNetworkManager.Instance != null)
        {
            GameNetworkManager.Instance.OnClientConnected -= HandleClientConnected;
            GameNetworkManager.Instance.OnClientDisconnected -= HandleClientDisconnected;
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

    void OnStartGameClicked() => TurnManager.Instance?.StartGame();
    void OnRollDiceClicked() => TurnManager.Instance?.RollDiceServerRpc();
    void OnEndTurnClicked() => TurnManager.Instance?.EndTurnServerRpc();

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

    void HandleTurnChanged(ulong newPlayerId)
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

    void HandleDiceRolled(int totalResult)
    {
        ShowDice(totalResult);
        UpdateActionButtons();
    }

    void HandleClientConnected()
    {
        RebuildPlayerList();
        UpdateActionButtons();
    }

    void HandleClientDisconnected()
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
        if (TurnManager.Instance == null) return;

        turnNumberLabel.text = $"턴: {TurnManager.Instance.TurnNumber.Value}";
        currentPlayerLabel.text = $"현재: {GetPlayerDisplayName(TurnManager.Instance.CurrentTurnPlayerId.Value)}";
        phaseIndicatorLabel.text = GetPhaseDisplayName(TurnManager.Instance.CurrentPhase.Value);
    }

    void UpdateActionButtons()
    {
        if (TurnManager.Instance == null)
        {
            HideAllButtons();
            return;
        }

        var phase = TurnManager.Instance.CurrentPhase.Value;
        bool isMyTurn = TurnManager.Instance.IsMyTurn();
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        SetVisible(btnStartGame, phase == GamePhase.WaitingForPlayers && isHost);
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

    void ShowDice(int total)
    {
        diceResultLabel.text = total.ToString();
        diceDetailLabel.text = $"2d6 = {total}";
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

    // ========================
    // PLAYER LIST
    // ========================

    void RebuildPlayerList()
    {
        playerList.Clear();
        playerEntries.Clear();

        if (NetworkManager.Singleton == null) return;

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            var entry = CreatePlayerEntry(clientId);
            playerList.Add(entry);
            playerEntries[clientId] = entry;
        }

        UpdatePlayerHighlight();
    }

    VisualElement CreatePlayerEntry(ulong clientId)
    {
        var entry = new VisualElement();
        entry.AddToClassList("player-entry");

        var nameLabel = new Label(GetPlayerDisplayName(clientId));
        nameLabel.AddToClassList("player-entry__name");

        var vpLabel = new Label("0 VP");
        vpLabel.AddToClassList("player-entry__vp");
        vpLabel.name = $"player-vp-{clientId}";

        entry.Add(nameLabel);
        entry.Add(vpLabel);
        return entry;
    }

    void UpdatePlayerHighlight()
    {
        if (TurnManager.Instance == null) return;

        ulong currentTurnPlayer = TurnManager.Instance.CurrentTurnPlayerId.Value;
        foreach (var kvp in playerEntries)
        {
            if (kvp.Key == currentTurnPlayer)
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
    public void UpdatePlayerVP(ulong clientId, int vp)
    {
        if (playerEntries.TryGetValue(clientId, out var entry))
        {
            var vpLabel = entry.Q<Label>($"player-vp-{clientId}");
            if (vpLabel != null)
                vpLabel.text = $"{vp} VP";
        }
    }

    // ========================
    // HELPERS
    // ========================

    string GetPlayerDisplayName(ulong clientId)
    {
        bool isLocal = NetworkManager.Singleton != null &&
                       clientId == NetworkManager.Singleton.LocalClientId;
        return isLocal ? $"Player {clientId} (나)" : $"Player {clientId}";
    }

    static string GetPhaseDisplayName(GamePhase phase) => phase switch
    {
        GamePhase.WaitingForPlayers => "대기 중",
        GamePhase.RollDice => "주사위 굴리기",
        GamePhase.Action => "행동",
        GamePhase.GameOver => "게임 종료",
        _ => phase.ToString()
    };
}
