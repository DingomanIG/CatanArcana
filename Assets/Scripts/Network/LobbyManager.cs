using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Header("Settings")]
    public string lobbyName = "CatanRoom";
    public int maxPlayers = 4;

    public Lobby CurrentLobby { get; private set; }

    public event Action<Lobby> OnLobbyCreated;
    public event Action<Lobby> OnLobbyJoined;
    public event Action<List<Lobby>> OnLobbyListUpdated;

    float heartbeatTimer;
    float lobbyPollTimer;
    const float HEARTBEAT_INTERVAL = 15f;
    const float LOBBY_POLL_INTERVAL = 2f;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        HandleHeartbeat();
        HandleLobbyPoll();
    }

    /// <summary>
    /// 로비 생성 (호스트)
    /// </summary>
    public async Task<Lobby> CreateLobby(string playerName)
    {
        try
        {
            string joinCode = await GameNetworkManager.Instance.StartHost();
            if (joinCode == null) return null;

            var options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = CreatePlayer(playerName),
                Data = new Dictionary<string, DataObject>
                {
                    { "JoinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCode) }
                }
            };

            CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            Debug.Log($"[Lobby] 방 생성: {CurrentLobby.Name} (Code: {CurrentLobby.LobbyCode})");
            OnLobbyCreated?.Invoke(CurrentLobby);
            return CurrentLobby;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] 방 생성 실패: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 로비 코드로 참가 (클라이언트)
    /// </summary>
    public async Task<bool> JoinLobbyByCode(string lobbyCode, string playerName)
    {
        try
        {
            var options = new JoinLobbyByCodeOptions
            {
                Player = CreatePlayer(playerName)
            };

            CurrentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, options);
            string relayJoinCode = CurrentLobby.Data["JoinCode"].Value;

            bool connected = await GameNetworkManager.Instance.StartClient(relayJoinCode);
            if (connected)
            {
                Debug.Log($"[Lobby] 방 참가: {CurrentLobby.Name}");
                OnLobbyJoined?.Invoke(CurrentLobby);
            }
            return connected;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] 방 참가 실패: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 로비 목록 조회
    /// </summary>
    public async Task<List<Lobby>> GetLobbyList()
    {
        try
        {
            var options = new QueryLobbiesOptions
            {
                Count = 20,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                }
            };

            var response = await LobbyService.Instance.QueryLobbiesAsync(options);
            OnLobbyListUpdated?.Invoke(response.Results);
            return response.Results;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] 목록 조회 실패: {e.Message}");
            return new List<Lobby>();
        }
    }

    public async void LeaveLobby()
    {
        if (CurrentLobby == null) return;

        try
        {
            await LobbyService.Instance.RemovePlayerAsync(CurrentLobby.Id,
                Unity.Services.Authentication.AuthenticationService.Instance.PlayerId);
            GameNetworkManager.Instance.Disconnect();
            CurrentLobby = null;
            Debug.Log("[Lobby] 방 퇴장");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] 퇴장 실패: {e.Message}");
        }
    }

    Player CreatePlayer(string name)
    {
        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "Name", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, name) }
            }
        };
    }

    /// <summary>
    /// 호스트가 로비를 유지하기 위한 하트비트
    /// </summary>
    void HandleHeartbeat()
    {
        if (CurrentLobby == null) return;
        if (!IsHost()) return;

        heartbeatTimer -= Time.deltaTime;
        if (heartbeatTimer <= 0f)
        {
            heartbeatTimer = HEARTBEAT_INTERVAL;
            LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
        }
    }

    /// <summary>
    /// 로비 상태 폴링
    /// </summary>
    async void HandleLobbyPoll()
    {
        if (CurrentLobby == null) return;

        lobbyPollTimer -= Time.deltaTime;
        if (lobbyPollTimer <= 0f)
        {
            lobbyPollTimer = LOBBY_POLL_INTERVAL;
            try
            {
                CurrentLobby = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Lobby] 폴링 실패: {e.Message}");
                CurrentLobby = null;
            }
        }
    }

    bool IsHost()
    {
        return CurrentLobby != null &&
               CurrentLobby.HostId == Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
    }
}
