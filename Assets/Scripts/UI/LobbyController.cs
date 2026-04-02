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
    Button[] btnAddAI = new Button[4];
    Button[] btnRemoveAI = new Button[4];

    // AI 난이도 순환 (추가 버튼 클릭 시)
    static readonly AIDifficulty[] AI_LEVEL_CYCLE = {
        AIDifficulty.Lv1, AIDifficulty.Lv3, AIDifficulty.Lv5,
        AIDifficulty.Lv7, AIDifficulty.Lv9
    };

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
        {
            slots[i] = root.Q<VisualElement>($"slot-{i}");
            btnAddAI[i] = root.Q<Button>($"btn-ai-{i}");
            btnRemoveAI[i] = root.Q<Button>($"btn-rm-ai-{i}");

            int idx = i; // 클로저 캡처
            btnAddAI[i]?.RegisterCallback<ClickEvent>(evt =>
            {
                SFXManager.Instance?.Play(SFXType.ButtonClick);
                OnAddAI(idx);
            });
            btnRemoveAI[i]?.RegisterCallback<ClickEvent>(evt =>
            {
                SFXManager.Instance?.Play(SFXType.ButtonClick);
                OnRemoveAI(idx);
            });
        }

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
        var lobbyMgr = LobbyManager.Instance;
        var lobby = lobbyMgr?.CurrentLobby;
        if (lobby == null)
        {
            lobbyStatus.text = "로비 연결 끊김";
            return;
        }

        lobbyTitle.text = lobby.Name;
        lobbyCode.text = lobby.LobbyCode ?? "------";

        bool isHost = IsHost(lobby);
        var aiSlots = lobbyMgr.GetAISlots();
        int humanCount = lobby.Players.Count;

        // 슬롯 배치: 접속자 먼저 → 나머지는 AI or 빈 자리
        // 빈 슬롯 인덱스 = humanCount 이후
        for (int i = 0; i < 4; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;

            var nameLabel = slot.Q<Label>(className: "player-slot__name");
            var badge = slot.Q<Label>(className: "player-slot__badge");

            // AI 버튼 기본 숨김
            HideAIButtons(i);

            if (i < humanCount)
            {
                // 실제 접속 플레이어
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
            else if (aiSlots[i] != AIDifficulty.None)
            {
                // AI 슬롯
                int lvl = (int)aiSlots[i];
                string aiName = lvl > 0 && lvl < AI_NAMES.Length ? AI_NAMES[lvl] : "AI";
                slot.AddToClassList("player-slot--filled");
                nameLabel.text = $"{aiName} (Lv{lvl})";
                nameLabel.RemoveFromClassList("player-slot__name--empty");
                badge.text = "AI";
                badge.RemoveFromClassList("player-slot__badge--hidden");

                // 호스트만 난이도 변경 + 제거 버튼 표시
                if (isHost)
                {
                    ShowAddAIButton(i); // 난이도 순환 버튼으로 재활용
                    if (btnAddAI[i] != null) btnAddAI[i].text = $"Lv{lvl}";
                    ShowRemoveAIButton(i);
                }
            }
            else
            {
                // 빈 자리
                slot.RemoveFromClassList("player-slot--filled");
                nameLabel.text = "빈 자리";
                nameLabel.AddToClassList("player-slot__name--empty");
                badge.AddToClassList("player-slot__badge--hidden");

                // 호스트만 추가 버튼 표시
                if (isHost)
                {
                    ShowAddAIButton(i);
                    if (btnAddAI[i] != null) btnAddAI[i].text = "+AI";
                }
            }
        }

        int totalCount = lobbyMgr.GetTotalPlayerCount();

        btnStartGame.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
        btnStartGame.SetEnabled(totalCount >= 2);

        if (isHost)
        {
            if (totalCount < 2)
                lobbyStatus.text = $"최소 2명이 필요합니다 (현재 {totalCount}명)";
            else
                lobbyStatus.text = $"플레이어 {totalCount}명 준비 완료!";
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
            // AI 슬롯 정보를 SceneFlowManager에 전달
            var lobbyMgr = LobbyManager.Instance;
            if (lobbyMgr != null)
            {
                flow.AIDifficulties = lobbyMgr.GetAISlots();
                flow.LocalPlayerCount = lobbyMgr.GetTotalPlayerCount();
            }
            Debug.Log($"[Lobby] 네트워크 게임 시작 — 총 {flow.LocalPlayerCount}명 (AI 포함)");
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
    // AI SLOT HANDLERS
    // ========================

    async void OnAddAI(int slotIndex)
    {
        var lobbyMgr = LobbyManager.Instance;
        if (lobbyMgr == null) return;

        // 이미 AI가 있으면 난이도 순환, 없으면 기본 Lv5로 추가
        var currentSlots = lobbyMgr.GetAISlots();
        AIDifficulty next = AIDifficulty.Lv5;
        if (currentSlots[slotIndex] != AIDifficulty.None)
        {
            // 현재 레벨의 다음 레벨로 순환
            int idx = System.Array.IndexOf(AI_LEVEL_CYCLE, currentSlots[slotIndex]);
            next = AI_LEVEL_CYCLE[(idx + 1) % AI_LEVEL_CYCLE.Length];
        }

        await lobbyMgr.SetAISlot(slotIndex, next);
        RefreshNetworkLobbyUI();
    }

    async void OnRemoveAI(int slotIndex)
    {
        var lobbyMgr = LobbyManager.Instance;
        if (lobbyMgr == null) return;
        await lobbyMgr.SetAISlot(slotIndex, AIDifficulty.None);
        RefreshNetworkLobbyUI();
    }

    void HideAIButtons(int i)
    {
        if (btnAddAI[i] != null) btnAddAI[i].AddToClassList("btn-add-ai--hidden");
        if (btnRemoveAI[i] != null) btnRemoveAI[i].AddToClassList("btn-add-ai--hidden");
    }

    void ShowAddAIButton(int i)
    {
        if (btnAddAI[i] != null) btnAddAI[i].RemoveFromClassList("btn-add-ai--hidden");
    }

    void ShowRemoveAIButton(int i)
    {
        if (btnRemoveAI[i] != null) btnRemoveAI[i].RemoveFromClassList("btn-add-ai--hidden");
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
