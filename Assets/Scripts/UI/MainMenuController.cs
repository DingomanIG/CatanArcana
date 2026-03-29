using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Services.Lobbies.Models;

/// <summary>
/// 메인 메뉴 UI 컨트롤러
/// 로컬 플레이 (AI 난이도 선택), 방 생성, 코드 참가, 방 목록 브라우징
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [SerializeField] UIDocument uiDocument;

    // UI Elements
    TextField inputPlayerName;
    TextField inputJoinCode;
    Button btnLocalPlay;
    Button btnCreateRoom;
    Button btnJoinRoom;
    Button btnBrowseRooms;
    Button btnRefreshRooms;
    Button btnCloseRooms;
    Label statusMessage;
    VisualElement roomListPanel;
    ScrollView roomListScroll;

    // AI 난이도 드롭다운
    DropdownField[] aiDropdowns = new DropdownField[3];

    static readonly List<string> DifficultyChoices = new() { "없음", "쉬움", "보통", "어려움" };

    bool networkReady;

    void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        inputPlayerName = root.Q<TextField>("input-player-name");
        inputJoinCode = root.Q<TextField>("input-join-code");
        btnLocalPlay = root.Q<Button>("btn-local-play");
        btnCreateRoom = root.Q<Button>("btn-create-room");
        btnJoinRoom = root.Q<Button>("btn-join-room");
        btnBrowseRooms = root.Q<Button>("btn-browse-rooms");
        btnRefreshRooms = root.Q<Button>("btn-refresh-rooms");
        btnCloseRooms = root.Q<Button>("btn-close-rooms");
        statusMessage = root.Q<Label>("status-message");
        roomListPanel = root.Q<VisualElement>("room-list-panel");
        roomListScroll = root.Q<ScrollView>("room-list-scroll");

        // AI 난이도 드롭다운 초기화
        for (int i = 0; i < 3; i++)
        {
            aiDropdowns[i] = root.Q<DropdownField>($"ai-diff-{i + 1}");
            if (aiDropdowns[i] != null)
            {
                aiDropdowns[i].choices = DifficultyChoices;
                aiDropdowns[i].index = 2; // 기본: 보통(Medium)
            }
        }

        btnLocalPlay.clicked += OnLocalPlay;
        btnCreateRoom.clicked += OnCreateRoom;
        btnJoinRoom.clicked += OnJoinRoom;
        btnBrowseRooms.clicked += OnBrowseRooms;
        btnRefreshRooms.clicked += OnRefreshRooms;
        btnCloseRooms.clicked += OnCloseRooms;

        inputJoinCode.value = "";

        string savedName = PlayerPrefs.GetString("PlayerName", "");
        if (!string.IsNullOrEmpty(savedName))
            inputPlayerName.value = savedName;
    }

    void Start()
    {
        SetNetworkButtonsEnabled(false);
        SetStatus("서비스 연결 중...");
        WaitForServices();
    }

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

    void OnLocalPlay()
    {
        SavePlayerName(GetPlayerName());

        // AI 난이도 수집
        var difficulties = new AIDifficulty[4];
        difficulties[0] = AIDifficulty.None; // 플레이어 본인

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
        flow.GoToGame();
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
        if (!networkReady) return;

        string code = inputJoinCode.value.Trim().ToUpper();
        if (string.IsNullOrEmpty(code))
        {
            SetStatus("참가 코드를 입력하세요", true);
            return;
        }

        string playerName = GetPlayerName();
        SetNetworkButtonsEnabled(false);
        SetStatus("접속 중...");

        bool success = await LobbyManager.Instance.JoinLobbyByCode(code, playerName);
        if (success)
        {
            SetStatus("접속 성공!");
            SavePlayerName(playerName);

            SceneFlowManager.Instance.PlayerName = playerName;
            SceneFlowManager.Instance.IsHosting = false;
            SceneFlowManager.Instance.IsLocalPlay = false;
            SceneFlowManager.Instance.GoToLobby();
        }
        else
        {
            SetStatus("접속 실패 - 코드를 확인하세요", true);
            SetNetworkButtonsEnabled(true);
        }
    }

    async void OnBrowseRooms()
    {
        if (!networkReady) return;
        roomListPanel.RemoveFromClassList("room-list-panel--hidden");
        SetStatus("방 목록 조회 중...");

        var lobbies = await LobbyManager.Instance.GetLobbyList();
        PopulateRoomList(lobbies);
        SetStatus("");
    }

    async void OnRefreshRooms()
    {
        if (!networkReady) return;
        SetStatus("새로고침 중...");
        var lobbies = await LobbyManager.Instance.GetLobbyList();
        PopulateRoomList(lobbies);
        SetStatus("");
    }

    void OnCloseRooms()
    {
        roomListPanel.AddToClassList("room-list-panel--hidden");
    }

    // ========================
    // ROOM LIST
    // ========================

    void PopulateRoomList(List<Lobby> lobbies)
    {
        roomListScroll.Clear();

        if (lobbies == null || lobbies.Count == 0)
        {
            var emptyLabel = new Label("열린 방이 없습니다");
            emptyLabel.AddToClassList("room-list-empty");
            roomListScroll.Add(emptyLabel);
            return;
        }

        foreach (var lobby in lobbies)
        {
            var entry = new VisualElement();
            entry.AddToClassList("room-entry");

            var info = new VisualElement();
            info.AddToClassList("room-entry__info");

            var nameLabel = new Label(lobby.Name);
            nameLabel.AddToClassList("room-entry__name");

            var playersLabel = new Label($"{lobby.Players.Count}/{lobby.MaxPlayers}명");
            playersLabel.AddToClassList("room-entry__players");

            info.Add(nameLabel);
            info.Add(playersLabel);
            entry.Add(info);

            var joinBtn = new Button();
            joinBtn.text = "참가";
            joinBtn.AddToClassList("room-entry__join-btn");

            string lobbyCode = lobby.LobbyCode;
            joinBtn.clicked += () => JoinFromList(lobbyCode);

            entry.Add(joinBtn);
            roomListScroll.Add(entry);
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
        statusMessage.text = message;
        statusMessage.RemoveFromClassList("status-message--error");
        statusMessage.RemoveFromClassList("status-message--success");
        if (isError)
            statusMessage.AddToClassList("status-message--error");
    }

    void SetNetworkButtonsEnabled(bool enabled)
    {
        btnCreateRoom.SetEnabled(enabled);
        btnJoinRoom.SetEnabled(enabled);
        btnBrowseRooms.SetEnabled(enabled);
    }
}
