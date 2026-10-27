#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Assets/Resources/CityDressingSet.asset from the Cartoon City (ithappy) prefabs already in the project,
/// so the runtime CityDresser can load them in the editor and in builds. Runs once if the asset is missing.
/// </summary>
[InitializeOnLoad]
public static class CityDressingSetBuilder
{
    private const string AssetPath = "Assets/Resources/CityDressingSet.asset";
    private const string PackRoot = "Assets/ithappy/Cartoon_City_Free";

    static CityDressingSetBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<CityDressingSet>(AssetPath) == null)
                Build(log: true);
        };
    }

    [MenuItem("Grab & Go/Rebuild City Dressing Set")]
    public static void BuildFromMenu() => Build(log: true);

    private static void Build(bool log)
    {
        if (!AssetDatabase.IsValidFolder(PackRoot))
        {
            Debug.LogWarning($"[CityDressingSetBuilder] {PackRoot} not found; city dressing skipped.");
            return;
        }

        var set = AssetDatabase.LoadAssetAtPath<CityDressingSet>(AssetPath);
        bool created = set == null;
        if (created) set = ScriptableObject.CreateInstance<CityDressingSet>();

        // Daytime scene: skip the *_NightLight building variants.
        set.buildings = Load("Prefabs/Buildings", n => !n.Contains("NightLight"));
        set.sidewalkTiles = Load("Prefabs/Sidewalks");
        set.streetLights = Load("Prefabs/Props", n => n.StartsWith("Spotlight"));
        set.trees = Load("Prefabs/Vegetation", n => n.StartsWith("Palm"));
        set.bushes = Load("Prefabs/Vegetation", n => n.StartsWith("Bush"));
        set.trashCans = Load("Prefabs/Props", n => n.StartsWith("Trash_Can"));
        set.busStops = Load("Prefabs/Props", n => n.StartsWith("Bus_Stop"));
        set.billboards = Load("Prefabs/Billboards", n => n.StartsWith("Billboard"));
        set.trafficLights = Load("Prefabs/Props", n => n.StartsWith("traffic_light"));
        set.fountains = Load("Prefabs/Props", n => n.StartsWith("Fountain"));
        set.skybox = AssetDatabase.LoadAssetAtPath<Material>($"{PackRoot}/Skyboxes/Skybox_5.mat");

        if (created)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateAsset(set, AssetPath);
        }
        else
        {
            EditorUtility.SetDirty(set);
        }
        AssetDatabase.SaveAssets();
        if (log)
            Debug.Log($"[CityDressingSetBuilder] {(created ? "Created" : "Updated")} {AssetPath}: " +
                      $"{set.buildings.Length} buildings, {set.streetLights.Length} lights, {set.trees.Length + set.bushes.Length} plants, " +
                      $"{set.billboards.Length} billboards, skybox {(set.skybox != null ? "found" : "missing")}.");
    }

    private static GameObject[] Load(string subFolder, System.Func<string, bool> filter = null)
    {
        string folder = $"{PackRoot}/{subFolder}";
        if (!AssetDatabase.IsValidFolder(folder)) return new GameObject[0];
        var result = new List<GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            if (filter != null && !filter(name)) continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null) result.Add(go);
        }
        return result.OrderBy(g => g.name).ToArray();
    }
}
#endif
