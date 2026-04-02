using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 게임 씬 진입 시 모드에 따라 적절한 GameManager를 생성/등록
/// - 로컬 플레이: LocalGameManager 생성
/// - 온라인 플레이: NetworkGameManager 프리팹 스폰 (호스트) / 자동 수신 (클라이언트)
///
/// Game 씬(SampleScene)에 배치할 것.
/// LocalGameManager가 씬에 이미 있다면 그것을 사용 (기존 호환).
/// </summary>
[DefaultExecutionOrder(-200)]
public class GameBootstrapper : MonoBehaviour
{
    [Header("네트워크 모드용")]
    [SerializeField] GameObject networkGameManagerPrefab;

    void Awake()
    {
        var flow = SceneFlowManager.Instance;

        if (flow == null || flow.IsLocalPlay)
        {
            SetupLocalGame();
        }
        else
        {
            SetupNetworkGame();
        }
    }

    void SetupLocalGame()
    {
        // 씬에 이미 LocalGameManager가 있으면 그대로 사용
        var existing = FindFirstObjectByType<LocalGameManager>();
        if (existing != null)
        {
            Debug.Log("[GameBootstrapper] 기존 LocalGameManager 사용");
            return;
        }

        // 없으면 새로 생성
        var go = new GameObject("LocalGameManager");
        go.AddComponent<LocalGameManager>();
        Debug.Log("[GameBootstrapper] LocalGameManager 생성 완료");
    }

    void SetupNetworkGame()
    {
        // 로컬 모드용 LocalGameManager가 씬에 있으면 비활성화
        var localMgr = FindFirstObjectByType<LocalGameManager>();
        if (localMgr != null)
        {
            localMgr.gameObject.SetActive(false);
            Debug.Log("[GameBootstrapper] 로컬 모드 LGM 비활성화");
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[GameBootstrapper] NetworkManager가 없습니다!");
            return;
        }

        // 호스트만 NetworkGameManager 스폰
        if (NetworkManager.Singleton.IsServer)
        {
            if (networkGameManagerPrefab == null)
            {
                Debug.LogError("[GameBootstrapper] networkGameManagerPrefab이 할당되지 않았습니다!");
                return;
            }

            var instance = Instantiate(networkGameManagerPrefab);
            instance.GetComponent<NetworkObject>().Spawn();
            Debug.Log("[GameBootstrapper] NetworkGameManager 스폰 완료 (호스트)");
        }
        else
        {
            Debug.Log("[GameBootstrapper] 클라이언트 대기 — NetworkGameManager 자동 수신 예정");
        }
    }
}
