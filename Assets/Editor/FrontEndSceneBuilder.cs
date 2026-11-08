#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Creates Assets/Scenes/FrontEnd.unity (camera, light, event system and the canvas that hosts FrontEndUI).
/// Built additively, so the scene open in the editor is left alone.
/// </summary>
public static class FrontEndSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/FrontEnd.unity";

    [MenuItem("Grab & Go/Rebuild Front End Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);

        var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        cameraGo.transform.SetPositionAndRotation(new Vector3(0f, 2f, -8f), Quaternion.Euler(-8f, 0f, 0f));
        cameraGo.AddComponent<AudioListener>();

        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var events = new GameObject("EventSystem");
        events.AddComponent<EventSystem>();
        events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

        var canvasGo = new GameObject("FrontEndCanvas") { layer = LayerMask.NameToLayer("UI") };
        canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 600f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var ui = canvasGo.AddComponent<FrontEndUI>();
        ui.menuAmbience = FindAmbience();

        var city = AssetDatabase.LoadAssetAtPath<CityDressingSet>("Assets/Resources/CityDressingSet.asset");
        if (city != null && city.skybox != null) RenderSettings.skybox = city.skybox;

        EditorSceneManager.SaveScene(scene, ScenePath);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
        EditorSceneManager.CloseScene(scene, true);
        BuildSceneSetup.FixFromMenu();
        Debug.Log($"[FrontEndSceneBuilder] {ScenePath} created.");
    }

    private static AudioClip FindAmbience()
    {
        var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Sounds" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        string path = clips.FirstOrDefault(p => p.ToLowerInvariant().Contains("ambience") || p.ToLowerInvariant().Contains("city"))
                      ?? clips.FirstOrDefault();
        return path != null ? AssetDatabase.LoadAssetAtPath<AudioClip>(path) : null;
    }
}
#endif
