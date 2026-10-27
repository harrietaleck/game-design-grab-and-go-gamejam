#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Guarantees the gameplay scene (Assets/MainScene.unity) is build scene 0.
/// Runs once when the editor loads scripts, and is also available from the menu.
/// SampleScene is kept in the list but disabled (not deleted) so nothing is lost.
/// </summary>
[InitializeOnLoad]
public static class BuildSceneSetup
{
    public const string MainScenePath = "Assets/MainScene.unity";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    static BuildSceneSetup()
    {
        EditorApplication.delayCall += () => EnsureBuildScenes(logIfUnchanged: false);
    }

    [MenuItem("Grab & Go/Fix Build Scenes")]
    public static void FixFromMenu() => EnsureBuildScenes(logIfUnchanged: true);

    private static void EnsureBuildScenes(bool logIfUnchanged)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        bool mainIsFirstAndEnabled = scenes.Count > 0 && scenes[0].path == MainScenePath && scenes[0].enabled;
        bool sampleDisabled = scenes.All(s => s.path != SampleScenePath || !s.enabled);

        if (mainIsFirstAndEnabled && sampleDisabled)
        {
            if (logIfUnchanged)
                Debug.Log("[BuildSceneSetup] Build scenes already correct: MainScene is scene 0.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) == null)
        {
            Debug.LogWarning($"[BuildSceneSetup] {MainScenePath} not found; build scenes left unchanged.");
            return;
        }

        scenes.RemoveAll(s => s.path == MainScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(MainScenePath, true));
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == SampleScenePath)
                scenes[i] = new EditorBuildSettingsScene(SampleScenePath, false);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[BuildSceneSetup] Build scenes updated: MainScene is scene 0, SampleScene disabled.");
    }
}
#endif
