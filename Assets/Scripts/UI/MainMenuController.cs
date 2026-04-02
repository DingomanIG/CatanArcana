using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Services.Lobbies.Models;

/// <summary>
/// 메인 메뉴 UI 컨트롤러
/// 사이드바 탭 (ROOMS / PLAY / STORE / PROFILE) 전환
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [SerializeField] UIDocument uiDocument;

    // Sidebar tabs
    Button tabRooms, tabPlay, tabStore, tabProfile;
    Button[] sidebarTabs;

    // Content panels
    VisualElement panelRooms, panelPlay, panelStore, panelProfile;
    VisualElement[] contentPanels;

    // User bar
    TextField inputPlayerName;

    // ROOMS panel
    Button roomsTabOpen, roomsTabSpectate;
    ScrollView roomListScroll;
    Button btnCreateRoom, btnJoinRoom;
    TextField inputRoomCode;

    // PLAY panel
    Button playTabAI, playTabCasual, playTabRanked;
    Label playP1Name;
    DropdownField[] aiDropdowns = new DropdownField[3];
    Button btnInvite, btnStartGame;

    // Status
    Label statusMessage;

    static readonly List<string> DifficultyChoices = new()
    {
        "없음",
        "Lv1 덕",
        "Lv2 잼미니",
        "Lv3 또리",
        "Lv4 지피",
        "Lv5 수리",
        "Lv6 그룩",
        "Lv7 아르카",
        "Lv8 페이큰",
        "Lv9 클로디"
    };

    bool networkReady;

    void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        // Sidebar
        tabRooms = root.Q<Button>("tab-rooms");
        tabPlay = root.Q<Button>("tab-play");
        tabStore = root.Q<Button>("tab-store");
        tabProfile = root.Q<Button>("tab-profile");
        sidebarTabs = new[] { tabRooms, tabPlay, tabStore, tabProfile };

        // Panels
        panelRooms = root.Q<VisualElement>("panel-rooms");
        panelPlay = root.Q<VisualElement>("panel-play");
        panelStore = root.Q<VisualElement>("panel-store");
        panelProfile = root.Q<VisualElement>("panel-profile");
        contentPanels = new[] { panelRooms, panelPlay, panelStore, panelProfile };

        // User bar
        inputPlayerName = root.Q<TextField>("input-player-name");

        // ROOMS panel elements
        roomsTabOpen = root.Q<Button>("rooms-tab-open");
        roomsTabSpectate = root.Q<Button>("rooms-tab-spectate");
        roomListScroll = root.Q<ScrollView>("room-list-scroll");
        btnCreateRoom = root.Q<Button>("btn-create-room");
        btnJoinRoom = root.Q<Button>("btn-join-room");
        inputRoomCode = root.Q<TextField>("input-room-code");

        // PLAY panel elements
        playTabAI = root.Q<Button>("play-tab-ai");
        playTabCasual = root.Q<Button>("play-tab-casual");
        playTabRanked = root.Q<Button>("play-tab-ranked");
        playP1Name = root.Q<Label>("play-p1-name");
        btnInvite = root.Q<Button>("btn-invite");
        btnStartGame = root.Q<Button>("btn-start-game");

        // AI dropdowns
        for (int i = 0; i < 3; i++)
        {
            aiDropdowns[i] = root.Q<DropdownField>($"ai-diff-{i + 1}");
            if (aiDropdowns[i] != null)
            {
                aiDropdowns[i].choices = DifficultyChoices;
                aiDropdowns[i].index = 5;
            }
        }

        // Status
        statusMessage = root.Q<Label>("status-message");

        // === SIDEBAR TAB EVENTS ===
        tabRooms.clicked += () => { SFXManager.Instance?.Play(SFXType.ButtonClick); SwitchTab(0); };
        tabPlay.clicked += () => { SFXManager.Instance?.Play(SFXType.ButtonClick); SwitchTab(1); };
        tabStore.clicked += () => { SFXManager.Instance?.Play(SFXType.ButtonClick); SwitchTab(2); };
        tabProfile.clicked += () => { SFXManager.Instance?.Play(SFXType.ButtonClick); SwitchTab(3); };

        // === ROOMS SUB-TAB EVENTS ===
        roomsTabOpen.clicked += () => { SFXManager.Instance?.Play(SFXType.ButtonClick); SetSubTab(roomsTabOpen, roomsTabSpectate); OnRefreshRooms(); };
        roomsTabSpectate.clicked += () => { SFXManager.Instance?.Play(SFXType.ButtonClick); SetSubTab(roomsTabSpectate, roomsTabOpen); };

        // === PLAY SUB-TAB EVENTS ===
        playTabAI.clicked += () => { SFXManager.Instance?.Play(SFXType.ButtonClick); SetPlaySubTab(playTabAI); };
        playTabCasual.clicked += () => { SFXManager.Instance?.Play(SFXType.ButtonClick); SetPlaySubTab(playTabCasual); };
        playTabRanked.clicked += () => { SFXManager.Instance?.Play(SFXType.ButtonClick); SetPlaySubTab(playTabRanked); };

        // === BUTTON EVENTS ===
        btnCreateRoom.clicked += () => { SFXManager.Instance?.Play(SFXType.ButtonClick); OnCreateRoom(); };
        btnJoinRoom.clicked += () => { SFXManager.Instance?.Play(SFXType.ButtonClick); OnJoinRoom(); };
        btnInvite.clicked += () => { SFXManager.Instance?.Play(SFXType.ButtonClick); SetStatus("초대 기능은 준비 중입니다"); };
        btnStartGame.clicked += () => { SFXManager.Instance?.Play(SFXType.ButtonClick); OnStartGame(); };

        // Load saved name
        string savedName = PlayerPrefs.GetString("PlayerName", "");
        if (!string.IsNullOrEmpty(savedName))
            inputPlayerName.value = savedName;

        // P1 name follows input
        inputPlayerName.RegisterValueChangedCallback(evt => { if (playP1Name != null) playP1Name.text = evt.newValue; });
        if (playP1Name != null) playP1Name.text = inputPlayerName.value;
    }

    void Start()
    {
        SetNetworkButtonsEnabled(false);
        SetStatus("서비스 연결 중...");
        WaitForServices();
    }

    // ========================
    // TAB SWITCHING
    // ========================

    void SwitchTab(int index)
    {
        for (int i = 0; i < sidebarTabs.Length; i++)
        {
            if (i == index)
            {
                sidebarTabs[i].AddToClassList("sidebar__tab--active");
                contentPanels[i].RemoveFromClassList("content-panel--hidden");
            }
            else
            {
                sidebarTabs[i].RemoveFromClassList("sidebar__tab--active");
                contentPanels[i].AddToClassList("content-panel--hidden");
            }
        }
    }

    void SetSubTab(Button active, Button inactive)
    {
        active.AddToClassList("sub-tab--active");
        inactive.RemoveFromClassList("sub-tab--active");
    }

    void SetPlaySubTab(Button active)
    {
        var all = new[] { playTabAI, playTabCasual, playTabRanked };
        foreach (var tab in all)
        {
            if (tab == active)
                tab.AddToClassList("sub-tab--active");
            else
                tab.RemoveFromClassList("sub-tab--active");
        }
    }

    // ========================
    // SERVICES
    // ========================

    async void WaitForServices()
    {
        if (GameNetworkManager.Instance == null)
        {
            SetStatus("로컬 플레이만 가능");
            return;
        }

        int timeout = 30;
        while (!GameNetworkManager.Instance.IsInitialized && timeout > 0)
        {
            await System.Threading.Tasks.Task.Delay(200);
            timeout--;
        }

        if (GameNetworkManager.Instance.IsInitialized)
        {
            networkReady = true;
            SetNetworkButtonsEnabled(true);
            SetStatus("준비 완료!");
            await System.Threading.Tasks.Task.Delay(1000);
            SetStatus("");
        }
        else
        {
            SetStatus("온라인 서비스 연결 실패 - 로컬 플레이만 가능");
        }
    }

    // ========================
    // BUTTON HANDLERS
    // ========================

    void OnStartGame()
    {
        SavePlayerName(GetPlayerName());

        var difficulties = new AIDifficulty[4];
        difficulties[0] = AIDifficulty.None;

        int activeCount = 1;
        for (int i = 0; i < 3; i++)
        {
            int idx = aiDropdowns[i] != null ? aiDropdowns[i].index : 2;
            difficulties[i + 1] = (AIDifficulty)idx;
            if (idx > 0) activeCount++;
        }

        if (activeCount < 2)
        {
            SetStatus("최소 AI 1명은 참가해야 합니다", true);
            return;
        }

        var flow = SceneFlowManager.Instance;
        flow.PlayerName = GetPlayerName();
        flow.IsLocalPlay = true;
        flow.LocalPlayerCount = activeCount;
        flow.AIDifficulties = difficulties;
        flow.GoToLobby();
    }

    async void OnCreateRoom()
    {
        if (!networkReady) return;

        string playerName = GetPlayerName();
        SetNetworkButtonsEnabled(false);
        SetStatus("방 생성 중...");

        var lobby = await LobbyManager.Instance.CreateLobby(playerName);
        if (lobby != null)
        {
            SetStatus($"방 생성 완료! Code: {lobby.LobbyCode}");
            SavePlayerName(playerName);

            SceneFlowManager.Instance.PlayerName = playerName;
            SceneFlowManager.Instance.IsHosting = true;
            SceneFlowManager.Instance.IsLocalPlay = false;
            SceneFlowManager.Instance.GoToLobby();
        }
        else
        {
            SetStatus("방 생성 실패", true);
            SetNetworkButtonsEnabled(true);
        }
    }

    async void OnJoinRoom()
    {
        if (!networkReady)
        {
            SetStatus("온라인 서비스가 준비되지 않았습니다", true);
            return;
        }

        // 코드 입력이 있으면 직접 참가
        string code = inputRoomCode?.value?.Trim().ToUpper();
        if (!string.IsNullOrEmpty(code))
        {
            JoinFromList(code);
            return;
        }

        // 코드 없으면 방 목록 조회
        SetStatus("방 목록 조회 중...");
        var lobbies = await LobbyManager.Instance.GetLobbyList();
        PopulateRoomList(lobbies);
    }

    async void OnRefreshRooms()
    {
        if (!networkReady) return;
        SetStatus("새로고침 중...");
        var lobbies = await LobbyManager.Instance.GetLobbyList();
        PopulateRoomList(lobbies);
        SetStatus("");
    }

    // ========================
    // ROOM LIST
    // ========================

    void PopulateRoomList(List<Lobby> lobbies)
    {
        roomListScroll.Clear();
        SetStatus("");

        if (lobbies == null || lobbies.Count == 0)
        {
            var emptyLabel = new Label("열린 방이 없습니다");
            emptyLabel.AddToClassList("room-list-empty");
            roomListScroll.Add(emptyLabel);
            return;
        }

        foreach (var lobby in lobbies)
        {
            var row = new VisualElement();
            row.AddToClassList("room-table__row");

            var mode = new Label("Base");
            mode.AddToClassList("room-table__cell");
            mode.AddToClassList("room-table__cell--mode");

            var map = new Label("Base");
            map.AddToClassList("room-table__cell");
            map.AddToClassList("room-table__cell--map");

            var timer = new Label("60s");
            timer.AddToClassList("room-table__cell");
            timer.AddToClassList("room-table__cell--timer");

            var players = new Label($"{lobby.Players.Count}/{lobby.MaxPlayers}");
            players.AddToClassList("room-table__cell");
            players.AddToClassList("room-table__cell--players");

            row.Add(mode);
            row.Add(map);
            row.Add(timer);
            row.Add(players);

            string lobbyCode = lobby.LobbyCode;
            row.RegisterCallback<ClickEvent>(evt => JoinFromList(lobbyCode));

            roomListScroll.Add(row);
        }
    }

    async void JoinFromList(string lobbyCode)
    {
        string playerName = GetPlayerName();
        SetNetworkButtonsEnabled(false);
        SetStatus("접속 중...");

        bool success = await LobbyManager.Instance.JoinLobbyByCode(lobbyCode, playerName);
        if (success)
        {
            SavePlayerName(playerName);
            SceneFlowManager.Instance.PlayerName = playerName;
            SceneFlowManager.Instance.IsHosting = false;
            SceneFlowManager.Instance.IsLocalPlay = false;
            SceneFlowManager.Instance.GoToLobby();
        }
        else
        {
            SetStatus("접속 실패", true);
            SetNetworkButtonsEnabled(true);
        }
    }

    // ========================
    // HELPERS
    // ========================

    string GetPlayerName()
    {
        string name = inputPlayerName.value.Trim();
        return string.IsNullOrEmpty(name) ? "Player" : name;
    }

    void SavePlayerName(string name)
    {
        PlayerPrefs.SetString("PlayerName", name);
        PlayerPrefs.Save();
    }

    void SetStatus(string message, bool isError = false)
    {
        if (statusMessage == null) return;
        statusMessage.text = message;
        statusMessage.RemoveFromClassList("status-message--error");
        statusMessage.RemoveFromClassList("status-message--success");
        if (isError)
            statusMessage.AddToClassList("status-message--error");
    }

    void SetNetworkButtonsEnabled(bool enabled)
    {
        btnCreateRoom?.SetEnabled(enabled);
        btnJoinRoom?.SetEnabled(enabled);
    }
}
