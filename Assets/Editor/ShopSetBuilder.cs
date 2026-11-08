#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Creates Assets/Resources/ShopSet.asset from the skateboard and bag packs, converts the boards to URP, and adds
/// a "Skate" state to Claire's and AJ's animators. Runs from the menu.
/// </summary>
public static class ShopSetBuilder
{
    private const string AssetPath = "Assets/Resources/ShopSet.asset";
    private const string Boards = "Assets/SkateBoard";
    private const string Bags = "Assets/LowPoly Fantasy Bags/Prefab";
    private const string Anim = "Assets/animation_Police";

    [MenuItem("Grab & Go/Rebuild Shop Set")]
    public static void Build()
    {
        PoliceSetBuilder.ConvertMaterialsToUrp($"{Boards}/Material");

        var set = AssetDatabase.LoadAssetAtPath<ShopSet>(AssetPath);
        bool created = set == null;
        if (created) set = ScriptableObject.CreateInstance<ShopSet>();
        set.boards = LoadPrefabs($"{Boards}/Prefabs", _ => true);
        // Claire's and AJ's original bags are left out; "Bloody" does not suit a shop item.
        set.bags = LoadPrefabs(Bags, n => n != "Bag.1_Red" && n != "Bag.4" && !n.Contains("Bloody"));
        if (created) AssetDatabase.CreateAsset(set, AssetPath);
        else EditorUtility.SetDirty(set);

        AddSkateState("Assets/ClaireControllerAnimation.controller", $"{Anim}/Ch35_nonPBR@Skateboarding.fbx");
        AddSkateState("Assets/AJControllerAnimation.controller", $"{Anim}/Ch35_nonPBR@Skateboarding1.fbx");

        // The game controls position and jump height, so those are removed from the clips.
        var jump = BakeInPlace("Jump", "Player Jump", keepHeight: false);
        var doubleJump = BakeInPlace("Jumping Up", "Player Double Jump", keepHeight: false);
        var slide = BakeInPlace("Soccer Tackle", "Player Slide", keepHeight: true);
        var duck = BakeInPlace("Ducking", "Player Duck", keepHeight: true);
        foreach (var path in new[] { "Assets/ClaireControllerAnimation.controller", "Assets/AJControllerAnimation.controller" })
        {
            SetState(path, "Jump", jump);
            SetState(path, "DoubleJump", doubleJump);
            SetState(path, "Slide", slide);
            SetState(path, "Duck", duck);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[ShopSetBuilder] {AssetPath} ready: {set.boards.Length} boards, {set.bags.Length} bags.");
    }

    private static GameObject[] LoadPrefabs(string folder, System.Func<string, bool> keep) =>
        AssetDatabase.FindAssets("t:Prefab", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p)
            .Where(p => keep(System.IO.Path.GetFileNameWithoutExtension(p)))
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .ToArray();

    /// <summary>Saves a copy of a Mixamo clip with the hips held in place (and optionally at a fixed height).</summary>
    private static AnimationClip BakeInPlace(string sourceName, string clipName, bool keepHeight)
    {
        var source = PoliceSetBuilder.FirstClip($"{Anim}/Ch35_nonPBR@{sourceName}.fbx");
        if (source == null) return null;

        string folder = $"{Anim}/Clips";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Anim, "Clips");
        string path = $"{folder}/{clipName}.anim";
        var clip = Object.Instantiate(source);
        clip.name = clipName;

        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.path != "mixamorig:Hips" || binding.type != typeof(Transform)) continue;
            bool flatten = binding.propertyName == "m_LocalPosition.x" || binding.propertyName == "m_LocalPosition.z"
                           || (binding.propertyName == "m_LocalPosition.y" && !keepHeight);
            if (!flatten) continue;
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, clip.length, curve.Evaluate(0f)));
        }

        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(clip, existing);
            existing.name = clipName;
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static void SetState(string controllerPath, string stateName, AnimationClip clip)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null || clip == null) return;
        var machine = controller.layers[0].stateMachine;
        var existing = machine.states.FirstOrDefault(s => s.state.name == stateName).state;
        if (existing != null) existing.motion = clip;
        else machine.AddState(stateName).motion = clip;
        EditorUtility.SetDirty(controller);
    }

    private static void AddSkateState(string controllerPath, string clipPath)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        var clip = PoliceSetBuilder.LoopGeneric(clipPath);
        if (controller == null || clip == null) return;

        var machine = controller.layers[0].stateMachine;
        var existing = machine.states.FirstOrDefault(s => s.state.name == "Skate").state;
        if (existing != null) existing.motion = clip;
        else machine.AddState("Skate").motion = clip;
        EditorUtility.SetDirty(controller);
    }
}
#endif
