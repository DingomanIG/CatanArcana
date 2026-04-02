using UnityEngine;
using UnityEngine.UIElements;
using Unity.Services.Lobbies.Models;

/// <summary>
/// 로비(대기실) UI 컨트롤러
/// 로컬 플레이 / 네트워크 모드 모두 지원
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

    static readonly string[] AI_NAMES = {
        "", "덕", "잼미니", "또리", "지피", "수리", "그룩", "아르카", "페이큰", "클로디"
    };

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

        // 로비 코드 클릭 시 클립보드 복사
        lobbyCode.RegisterCallback<ClickEvent>(evt =>
        {
            string code = lobbyCode.text;
            if (!string.IsNullOrEmpty(code) && code != "------" && code != "LOCAL")
            {
                GUIUtility.systemCopyBuffer = code;
                lobbyStatus.text = $"코드 복사됨: {code}";
                SFXManager.Instance?.Play(SFXType.ButtonClick);
            }
        });
    }

    void Start()
    {
        var flow = SceneFlowManager.Instance;
        if (flow != null && flow.IsLocalPlay)
            SetupLocalLobby();
        else
            RefreshNetworkLobbyUI();
    }

    void Update()
    {
        var flow = SceneFlowManager.Instance;
        if (flow != null && flow.IsLocalPlay) return; // 로컬은 폴링 불필요

        pollTimer -= Time.deltaTime;
        if (pollTimer <= 0f)
        {
            pollTimer = POLL_INTERVAL;
            RefreshNetworkLobbyUI();
        }
    }

    // ========================
    // LOCAL PLAY LOBBY
    // ========================

    void SetupLocalLobby()
    {
        var flow = SceneFlowManager.Instance;

        lobbyTitle.text = "로컬 플레이";
        lobbyCode.text = "LOCAL";

        // 플레이어 슬롯 채우기
        for (int i = 0; i < 4; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;

            var nameLabel = slot.Q<Label>(className: "player-slot__name");
            var badge = slot.Q<Label>(className: "player-slot__badge");

            if (i == 0)
            {
                // P1 = 인간 플레이어
                slot.AddToClassList("player-slot--filled");
                nameLabel.text = flow.PlayerName;
                nameLabel.RemoveFromClassList("player-slot__name--empty");
                badge.text = "YOU";
                badge.RemoveFromClassList("player-slot__badge--hidden");
            }
            else if (flow.AIDifficulties != null && i < flow.AIDifficulties.Length && flow.AIDifficulties[i] != AIDifficulty.None)
            {
                // AI 슬롯
                slot.AddToClassList("player-slot--filled");
                int lvl = (int)flow.AIDifficulties[i];
                string aiName = lvl > 0 && lvl < AI_NAMES.Length ? AI_NAMES[lvl] : "AI";
                nameLabel.text = $"{aiName} (Lv{lvl})";
                nameLabel.RemoveFromClassList("player-slot__name--empty");
                badge.text = "AI";
                badge.RemoveFromClassList("player-slot__badge--hidden");
            }
            else
            {
                slot.RemoveFromClassList("player-slot--filled");
                nameLabel.text = "빈 자리";
                nameLabel.AddToClassList("player-slot__name--empty");
                badge.AddToClassList("player-slot__badge--hidden");
            }
        }

        btnStartGame.style.display = DisplayStyle.Flex;
        btnStartGame.SetEnabled(true);
        lobbyStatus.text = $"플레이어 {flow.LocalPlayerCount}명 준비 완료!";
    }

    // ========================
    // NETWORK LOBBY
    // ========================

    void RefreshNetworkLobbyUI()
    {
        var lobby = LobbyManager.Instance?.CurrentLobby;
        if (lobby == null)
        {
            lobbyStatus.text = "로비 연결 끊김";
            return;
        }

        lobbyTitle.text = lobby.Name;
        lobbyCode.text = lobby.LobbyCode ?? "------";

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

        bool isHost = IsHost(lobby);
        int playerCount = lobby.Players.Count;

        btnStartGame.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
        btnStartGame.SetEnabled(playerCount >= 2);

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

        var flow = SceneFlowManager.Instance;
        if (flow != null && !flow.IsLocalPlay)
        {
            // 네트워크 모드: 로비 하트비트 중지 후 동기화된 씬 전환
            // LobbyManager는 DontDestroyOnLoad이므로 씬 전환 후에도 유지
            Debug.Log("[Lobby] 네트워크 게임 시작 — 동기화 씬 전환");
        }

        flow.GoToGame();
    }

    void OnLeave()
    {
        var flow = SceneFlowManager.Instance;
        if (flow != null && !flow.IsLocalPlay)
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
