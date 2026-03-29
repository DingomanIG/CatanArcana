using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환 관리 (MainMenu → Lobby → Game)
/// DontDestroyOnLoad 싱글톤
/// </summary>
public class SceneFlowManager : MonoBehaviour
{
    public static SceneFlowManager Instance { get; private set; }

    public const string SCENE_MAIN_MENU = "MainMenu";
    public const string SCENE_LOBBY = "Lobby";
    public const string SCENE_GAME = "Game";

    // 씬 간 전달 데이터
    public string PlayerName { get; set; } = "Player";
    public bool IsHosting { get; set; }

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
        SceneManager.LoadScene(SCENE_MAIN_MENU);
    }

    public void GoToLobby()
    {
        SceneManager.LoadScene(SCENE_LOBBY);
    }

    public void GoToGame()
    {
        SceneManager.LoadScene(SCENE_GAME);
    }
}
