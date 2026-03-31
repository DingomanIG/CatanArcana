using UnityEngine;
using UnityEngine.UIElements;
using Unity.Services.Lobbies.Models;

/// <summary>
/// 로비(대기실) UI 컨트롤러
/// 플레이어 슬롯, 게임 시작, 나가기
/// </summary>
public class LobbyController : MonoBehaviour
{
    [SerializeField] UIDocument uiDocument;

    // UI Elements
    Label lobbyTitle;
    Label lobbyCode;
    Label lobbyStatus;
    Button btnStartGame;
    Button btnLeave;
    VisualElement playerSlots;
    VisualElement[] slots = new VisualElement[4];

    float pollTimer;
    const float POLL_INTERVAL = 1.5f;

    void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        lobbyTitle = root.Q<Label>("lobby-title");
        lobbyCode = root.Q<Label>("lobby-code");
        lobbyStatus = root.Q<Label>("lobby-status");
        btnStartGame = root.Q<Button>("btn-start-game");
        btnLeave = root.Q<Button>("btn-leave");
        playerSlots = root.Q<VisualElement>("player-slots");

        for (int i = 0; i < 4; i++)
            slots[i] = root.Q<VisualElement>($"slot-{i}");

        btnStartGame.clicked += () => { SFXManager.Instance?.Play(SFXType.ButtonClick); OnStartGame(); };
        btnLeave.clicked += () => { SFXManager.Instance?.Play(SFXType.ButtonClick); OnLeave(); };
    }

    void Start()
    {
        RefreshLobbyUI();
    }

    void Update()
    {
        // 주기적으로 로비 상태 갱신
        pollTimer -= Time.deltaTime;
        if (pollTimer <= 0f)
        {
            pollTimer = POLL_INTERVAL;
            RefreshLobbyUI();
        }
    }

    // ========================
    // UI REFRESH
    // ========================

    void RefreshLobbyUI()
    {
        var lobby = LobbyManager.Instance?.CurrentLobby;
        if (lobby == null)
        {
            lobbyStatus.text = "로비 연결 끊김";
            return;
        }

        // 헤더
        lobbyTitle.text = lobby.Name;
        lobbyCode.text = lobby.LobbyCode ?? "------";

        // 플레이어 슬롯
        for (int i = 0; i < 4; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;

            var nameLabel = slot.Q<Label>(className: "player-slot__name");
            var badge = slot.Q<Label>(className: "player-slot__badge");

            if (i < lobby.Players.Count)
            {
                var player = lobby.Players[i];
                string pName = GetPlayerDisplayName(player);

                slot.AddToClassList("player-slot--filled");
                nameLabel.text = pName;
                nameLabel.RemoveFromClassList("player-slot__name--empty");

                // 호스트 표시
                if (player.Id == lobby.HostId)
                {
                    badge.text = "HOST";
                    badge.RemoveFromClassList("player-slot__badge--hidden");
                }
                else
                {
                    badge.AddToClassList("player-slot__badge--hidden");
                }
            }
            else
            {
                slot.RemoveFromClassList("player-slot--filled");
                nameLabel.text = "빈 자리";
                nameLabel.AddToClassList("player-slot__name--empty");
                badge.AddToClassList("player-slot__badge--hidden");
            }
        }

        // 버튼 상태
        bool isHost = IsHost(lobby);
        int playerCount = lobby.Players.Count;

        btnStartGame.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
        btnStartGame.SetEnabled(playerCount >= 2);

        // 상태 메시지
        if (isHost)
        {
            if (playerCount < 2)
                lobbyStatus.text = "최소 2명이 필요합니다 (현재 1명)";
            else
                lobbyStatus.text = $"플레이어 {playerCount}명 준비 완료!";
        }
        else
        {
            lobbyStatus.text = "호스트가 게임을 시작할 때까지 대기 중...";
        }
    }

    // ========================
    // BUTTON HANDLERS
    // ========================

    void OnStartGame()
    {
        lobbyStatus.text = "게임 시작 중...";
        btnStartGame.SetEnabled(false);
        btnLeave.SetEnabled(false);

        // 게임 씬으로 전환
        SceneFlowManager.Instance.GoToGame();
    }

    void OnLeave()
    {
        LobbyManager.Instance?.LeaveLobby();
        SceneFlowManager.Instance.GoToMainMenu();
    }

    // ========================
    // HELPERS
    // ========================

    string GetPlayerDisplayName(Player player)
    {
        if (player.Data != null &&
            player.Data.TryGetValue("Name", out var nameData))
        {
            return nameData.Value;
        }
        return $"Player {player.Id[..6]}";
    }

    bool IsHost(Lobby lobby)
    {
        return lobby.HostId ==
               Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
    }
}
