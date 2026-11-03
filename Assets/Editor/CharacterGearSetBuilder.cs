#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Assets/Resources/CharacterGearSet.asset if missing or outdated.
/// Claire: Bag.1_Red (rounded backpack, matches her outfit). AJ: Bag.4 (rucksack).
/// Both models have their shoulder straps on local +Z, which faces the wearer's back.
/// </summary>
[InitializeOnLoad]
public static class CharacterGearSetBuilder
{
    private const string AssetPath = "Assets/Resources/CharacterGearSet.asset";
    private const string BagFolder = "Assets/LowPoly Fantasy Bags/Prefab";
    private const int Version = 2; // v2: Claire Bag.5 (a round shoulder bag) -> Bag.1_Red backpack

    static CharacterGearSetBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            var existing = AssetDatabase.LoadAssetAtPath<CharacterGearSet>(AssetPath);
            if (existing == null || existing.generatorVersion < Version) Build();
        };
    }

    [MenuItem("Grab & Go/Rebuild Character Gear Set")]
    public static void Build()
    {
        var set = AssetDatabase.LoadAssetAtPath<CharacterGearSet>(AssetPath);
        bool created = set == null;
        if (created) set = ScriptableObject.CreateInstance<CharacterGearSet>();

        set.claire.prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{BagFolder}/Bag.1_Red.prefab");
        set.claire.height = 0.42f;
        set.generatorVersion = Version;
        set.aj.prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{BagFolder}/Bag.4.prefab");

        if (created)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateAsset(set, AssetPath);
        }
        else EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Debug.Log($"[CharacterGearSetBuilder] {(created ? "Created" : "Updated")} {AssetPath}: Claire {(set.claire.prefab ? set.claire.prefab.name : "none")}, AJ {(set.aj.prefab ? set.aj.prefab.name : "none")}.");
    }
}
#endif
