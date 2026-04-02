using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>
/// 씬 전환 관리 (MainMenu → Lobby → Game)
/// DontDestroyOnLoad 싱글톤
/// - 로컬 모드: SceneManager.LoadScene 직접 사용
/// - 네트워크 모드: NetworkManager.SceneManager로 동기화된 씬 전환 (호스트만 호출)
/// </summary>
public class SceneFlowManager : MonoBehaviour
{
    public static SceneFlowManager Instance { get; private set; }

    public const string SCENE_MAIN_MENU = "MainMenu";
    public const string SCENE_LOBBY = "Lobby";
    public const string SCENE_GAME = "SampleScene";

    // 씬 간 전달 데이터
    public string PlayerName { get; set; } = "Player";
    public bool IsHosting { get; set; }
    public bool IsLocalPlay { get; set; }
    public int LocalPlayerCount { get; set; } = 4;
    public AIDifficulty[] AIDifficulties { get; set; } = { AIDifficulty.None, AIDifficulty.Lv5, AIDifficulty.Lv5, AIDifficulty.Lv5 };

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

    public void GoToMainMenu()
    {
        // 네트워크 정리 후 메인 메뉴로
        if (!IsLocalPlay && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        SceneManager.LoadScene(SCENE_MAIN_MENU);
    }

    public void GoToLobby()
    {
        SceneManager.LoadScene(SCENE_LOBBY);
    }

    /// <summary>
    /// 게임 씬으로 전환
    /// - 로컬: 직접 LoadScene
    /// - 네트워크: NetworkManager.SceneManager로 동기화된 전환 (호스트만)
    /// </summary>
    public void GoToGame()
    {
        if (!IsLocalPlay && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            // 네트워크 동기화 씬 전환 — 모든 클라이언트가 함께 이동
            NetworkManager.Singleton.SceneManager.LoadScene(SCENE_GAME, LoadSceneMode.Single);
            Debug.Log("[SceneFlow] 네트워크 씬 전환: Game (호스트)");
        }
        else if (IsLocalPlay)
        {
            SceneManager.LoadScene(SCENE_GAME);
        }
        // 클라이언트는 호스트의 씬 전환을 자동으로 따라감
    }
}
