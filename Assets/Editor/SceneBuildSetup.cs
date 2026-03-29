using UnityEditor;
using UnityEngine;

/// <summary>
/// Build Settings에 씬을 자동 등록하는 에디터 도구
/// Tools > ArcanaCatan > Setup Build Scenes
/// </summary>
public class SceneBuildSetup
{
    [MenuItem("Tools/ArcanaCatan/Setup Build Scenes")]
    public static void SetupBuildScenes()
    {
        var scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Lobby.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true),
        };

        EditorBuildSettings.scenes = scenes;
        Debug.Log("[SceneBuildSetup] Build Settings 씬 등록 완료: MainMenu(0), Lobby(1), SampleScene(2)");
    }

    [InitializeOnLoadMethod]
    static void AutoSetup()
    {
        // 씬이 등록 안 되어 있으면 자동 설정
        if (EditorBuildSettings.scenes.Length == 0 ||
            (EditorBuildSettings.scenes.Length == 1 && EditorBuildSettings.scenes[0].path.Contains("SampleScene")))
        {
            SetupBuildScenes();
        }
    }
}
