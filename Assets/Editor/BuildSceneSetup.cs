#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Guarantees the front-end scene is build scene 0 and the gameplay scene (Assets/MainScene.unity) follows it.
/// Without a front-end scene, MainScene is scene 0. Runs when the editor loads scripts, and from the menu.
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
        bool hasFrontEnd = AssetDatabase.LoadAssetAtPath<SceneAsset>(FrontEndSceneBuilder.ScenePath) != null;
        int mainIndex = hasFrontEnd ? 1 : 0;
        bool frontEndFirst = !hasFrontEnd || (scenes.Count > 0 && scenes[0].path == FrontEndSceneBuilder.ScenePath && scenes[0].enabled);
        bool mainInPlace = scenes.Count > mainIndex && scenes[mainIndex].path == MainScenePath && scenes[mainIndex].enabled;
        bool sampleDisabled = scenes.All(s => s.path != SampleScenePath || !s.enabled);

        if (frontEndFirst && mainInPlace && sampleDisabled)
        {
            if (logIfUnchanged)
                Debug.Log("[BuildSceneSetup] Build scenes already correct.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) == null)
        {
            Debug.LogWarning($"[BuildSceneSetup] {MainScenePath} not found; build scenes left unchanged.");
            return;
        }

        scenes.RemoveAll(s => s.path == MainScenePath);
        scenes.RemoveAll(s => s.path == FrontEndSceneBuilder.ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(MainScenePath, true));
        if (hasFrontEnd) scenes.Insert(0, new EditorBuildSettingsScene(FrontEndSceneBuilder.ScenePath, true));
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == SampleScenePath)
                scenes[i] = new EditorBuildSettingsScene(SampleScenePath, false);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[BuildSceneSetup] Build scenes updated.");
    }
}
#endif
