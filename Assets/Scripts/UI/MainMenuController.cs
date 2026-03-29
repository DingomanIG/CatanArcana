using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Services.Lobbies.Models;

/// <summary>
/// 메인 메뉴 UI 컨트롤러
/// 방 생성, 코드 참가, 방 목록 브라우징
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [SerializeField] UIDocument uiDocument;

    // UI Elements
    TextField inputPlayerName;
    TextField inputJoinCode;
    Button btnCreateRoom;
    Button btnJoinRoom;
    Button btnBrowseRooms;
    Button btnRefreshRooms;
    Button btnCloseRooms;
    Label statusMessage;
    VisualElement roomListPanel;
    ScrollView roomListScroll;

    void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        inputPlayerName = root.Q<TextField>("input-player-name");
        inputJoinCode = root.Q<TextField>("input-join-code");
        btnCreateRoom = root.Q<Button>("btn-create-room");
        btnJoinRoom = root.Q<Button>("btn-join-room");
        btnBrowseRooms = root.Q<Button>("btn-browse-rooms");
        btnRefreshRooms = root.Q<Button>("btn-refresh-rooms");
        btnCloseRooms = root.Q<Button>("btn-close-rooms");
        statusMessage = root.Q<Label>("status-message");
        roomListPanel = root.Q<VisualElement>("room-list-panel");
        roomListScroll = root.Q<ScrollView>("room-list-scroll");

        btnCreateRoom.clicked += OnCreateRoom;
        btnJoinRoom.clicked += OnJoinRoom;
        btnBrowseRooms.clicked += OnBrowseRooms;
        btnRefreshRooms.clicked += OnRefreshRooms;
        btnCloseRooms.clicked += OnCloseRooms;

        // placeholder 텍스트 효과
        inputJoinCode.value = "";
    }

    void Start()
    {
        SetStatus("서비스 연결 중...");
        WaitForServices();
    }

    async void WaitForServices()
    {
        // GameNetworkManager가 초기화될 때까지 대기
        while (GameNetworkManager.Instance == null || !GameNetworkManager.Instance.IsInitialized)
        {
            await System.Threading.Tasks.Task.Delay(200);
        }
        SetStatus("준비 완료!");
        await System.Threading.Tasks.Task.Delay(1000);
        SetStatus("");
    }

    // ========================
    // BUTTON HANDLERS
    // ========================

    async void OnCreateRoom()
    {
        string playerName = GetPlayerName();
        SetButtonsEnabled(false);
        SetStatus("방 생성 중...");

        var lobby = await LobbyManager.Instance.CreateLobby(playerName);
        if (lobby != null)
        {
            SetStatus($"방 생성 완료! Code: {lobby.LobbyCode}");
            SavePlayerName(playerName);

            // 로비 씬으로 전환
            SceneFlowManager.Instance.PlayerName = playerName;
            SceneFlowManager.Instance.IsHosting = true;
            SceneFlowManager.Instance.GoToLobby();
        }
        else
        {
            SetStatus("방 생성 실패", true);
            SetButtonsEnabled(true);
        }
    }

    async void OnJoinRoom()
    {
        string code = inputJoinCode.value.Trim().ToUpper();
        if (string.IsNullOrEmpty(code))
        {
            SetStatus("참가 코드를 입력하세요", true);
            return;
        }

        string playerName = GetPlayerName();
        SetButtonsEnabled(false);
        SetStatus("접속 중...");

        bool success = await LobbyManager.Instance.JoinLobbyByCode(code, playerName);
        if (success)
        {
            SetStatus("접속 성공!");
            SavePlayerName(playerName);

            SceneFlowManager.Instance.PlayerName = playerName;
            SceneFlowManager.Instance.IsHosting = false;
            SceneFlowManager.Instance.GoToLobby();
        }
        else
        {
            SetStatus("접속 실패 - 코드를 확인하세요", true);
            SetButtonsEnabled(true);
        }
    }

    async void OnBrowseRooms()
    {
        roomListPanel.RemoveFromClassList("room-list-panel--hidden");
        SetStatus("방 목록 조회 중...");

        var lobbies = await LobbyManager.Instance.GetLobbyList();
        PopulateRoomList(lobbies);
        SetStatus("");
    }

    async void OnRefreshRooms()
    {
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
        SetButtonsEnabled(false);
        SetStatus("접속 중...");

        bool success = await LobbyManager.Instance.JoinLobbyByCode(lobbyCode, playerName);
        if (success)
        {
            SavePlayerName(playerName);
            SceneFlowManager.Instance.PlayerName = playerName;
            SceneFlowManager.Instance.IsHosting = false;
            SceneFlowManager.Instance.GoToLobby();
        }
        else
        {
            SetStatus("접속 실패", true);
            SetButtonsEnabled(true);
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

    void SetButtonsEnabled(bool enabled)
    {
        btnCreateRoom.SetEnabled(enabled);
        btnJoinRoom.SetEnabled(enabled);
        btnBrowseRooms.SetEnabled(enabled);
    }
}
