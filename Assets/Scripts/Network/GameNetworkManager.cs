using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class GameNetworkManager : MonoBehaviour
{
    public static GameNetworkManager Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("최대 플레이어 수 (호스트 포함)")]
    public int maxPlayers = 4;

    public string JoinCode { get; private set; }
    public bool IsInitialized { get; private set; }

    public event Action OnClientConnected;
    public event Action OnClientDisconnected;
    public event Action<string> OnJoinCodeGenerated;

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

    async void Start()
    {
        await InitializeServices();
    }

    async Task InitializeServices()
    {
        try
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            IsInitialized = true;
            Debug.Log($"[Network] 초기화 완료. Player ID: {AuthenticationService.Instance.PlayerId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Network] 초기화 실패: {e.Message}");
        }
    }

    /// <summary>
    /// 호스트로 게임 시작 (Relay 할당 + NetworkManager 시작)
    /// </summary>
    public async Task<string> StartHost()
    {
        if (!IsInitialized)
        {
            Debug.LogError("[Network] 서비스 미초기화");
            return null;
        }

        try
        {
            // Relay 할당
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // Transport 설정
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            NetworkManager.Singleton.StartHost();
            RegisterCallbacks();

            Debug.Log($"[Network] 호스트 시작. Join Code: {JoinCode}");
            OnJoinCodeGenerated?.Invoke(JoinCode);
            return JoinCode;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Network] 호스트 시작 실패: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 클라이언트로 게임 참가
    /// </summary>
    public async Task<bool> StartClient(string joinCode)
    {
        if (!IsInitialized)
        {
            Debug.LogError("[Network] 서비스 미초기화");
            return false;
        }

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData
            );

            NetworkManager.Singleton.StartClient();
            RegisterCallbacks();

            Debug.Log($"[Network] 클라이언트 접속. Code: {joinCode}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Network] 접속 실패: {e.Message}");
            return false;
        }
    }

    public void Disconnect()
    {
        NetworkManager.Singleton.Shutdown();
        Debug.Log("[Network] 연결 해제");
    }

    void RegisterCallbacks()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnect;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
    }

    void OnClientConnect(ulong clientId)
    {
        Debug.Log($"[Network] 클라이언트 접속: {clientId}");
        OnClientConnected?.Invoke();
    }

    void OnClientDisconnect(ulong clientId)
    {
        Debug.Log($"[Network] 클라이언트 퇴장: {clientId}");
        OnClientDisconnected?.Invoke();
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnect;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
        }
    }
}
