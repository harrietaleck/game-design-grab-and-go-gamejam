#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Creates Assets/Scenes/FrontEnd.unity: the canvas that hosts FrontEndUI, plus a live backdrop made from visual
/// copies of Claire, AJ and the road in MainScene. Built additively, so the scene open in the editor is left alone.
/// </summary>
public static class FrontEndSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/FrontEnd.unity";
    private const float RigStartZ = 130f; // far enough along for the street to exist behind the camera

    [MenuItem("Grab & Go/Rebuild Front End Scene")]
    public static void Build()
    {
        var mainScene = SceneManager.GetSceneByPath(BuildSceneSetup.MainScenePath);
        bool openedMain = !mainScene.isLoaded;
        if (openedMain) mainScene = EditorSceneManager.OpenScene(BuildSceneSetup.MainScenePath, OpenSceneMode.Additive);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var previous = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);

        BuildBackdrop(mainScene);

        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.957f, 0.839f);
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(50f, 330f, 0f);

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
        canvasGo.AddComponent<FrontEndUI>().menuAmbience = FindAmbience();

        var city = AssetDatabase.LoadAssetAtPath<CityDressingSet>("Assets/Resources/CityDressingSet.asset");
        if (city != null && city.skybox != null) RenderSettings.skybox = city.skybox;

        EditorSceneManager.SaveScene(scene, ScenePath);
        SceneManager.SetActiveScene(previous);
        EditorSceneManager.CloseScene(scene, true);
        if (openedMain) EditorSceneManager.CloseScene(mainScene, true);
        BuildSceneSetup.FixFromMenu();
        Debug.Log($"[FrontEndSceneBuilder] {ScenePath} created.");
    }

    private static void BuildBackdrop(Scene mainScene)
    {
        var rig = new GameObject("Runner Rig");
        rig.transform.position = new Vector3(0f, 0f, RigStartZ);
        rig.AddComponent<FrontEndDiorama>();

        var claire = Find<PlayerLaneMovement>(mainScene);
        var aj = Find<AIThiefLaneDodge>(mainScene);
        AddRunner(claire.gameObject, rig.transform, new Vector3(1.5f, claire.transform.position.y, 0f));
        AddRunner(aj.gameObject, rig.transform, new Vector3(-1.5f, aj.transform.position.y, 3.2f));

        var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
        cameraGo.transform.SetParent(rig.transform, false);
        var camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 55f;
        camera.farClipPlane = 400f;
        cameraGo.AddComponent<AudioListener>();
        cameraGo.transform.localPosition = new Vector3(-5.5f, 1.3f, 10.5f);
        cameraGo.transform.LookAt(rig.transform.TransformPoint(new Vector3(-1.2f, 2.6f, -2f)));

        // The road spawner in MainScene uses a RoadTile object in that scene as its template.
        var source = Find<RoadSpawner>(mainScene);
        var tile = Object.Instantiate(source.roadTilePrefab);
        tile.name = "RoadTile";
        tile.transform.position = new Vector3(0f, 0f, -1000f);
        SceneManager.MoveGameObjectToScene(tile, SceneManager.GetActiveScene());

        var spawnerGo = new GameObject("RoadSpawner");
        var spawner = spawnerGo.AddComponent<RoadSpawner>();
        spawner.roadTilePrefab = tile;
        spawner.tileLength = source.tileLength;
        spawner.initialTiles = source.initialTiles;
        spawner.spawnDistanceAhead = source.spawnDistanceAhead;
        spawner.despawnDistanceBehind = source.despawnDistanceBehind;
        spawner.maxTiles = 40;
        spawner.spawnObstaclesWithTiles = false;
        spawner.handlerTransform = rig.transform;
        spawnerGo.AddComponent<CityDresser>().despawnBehind = 80f;
    }

    private static T Find<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<T>(true)).First(c => c != null);

    /// <summary>A copy of a character that keeps only its model and running animation.</summary>
    private static void AddRunner(GameObject source, Transform rig, Vector3 localPosition)
    {
        var copy = Object.Instantiate(source, rig);
        copy.name = source.name;
        copy.transform.SetLocalPositionAndRotation(localPosition, Quaternion.identity);
        copy.SetActive(true);
        for (int pass = 0; pass < 2; pass++)
            foreach (var mb in copy.GetComponentsInChildren<MonoBehaviour>(true).Reverse())
                if (mb != null) Object.DestroyImmediate(mb);
        foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var rb in copy.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
        foreach (var a in copy.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(a);
        foreach (var animator in copy.GetComponentsInChildren<Animator>(true)) animator.applyRootMotion = false;
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
