#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Prepares the police asset packs for the game and creates Assets/Resources/PoliceSet.asset:
/// converts the packs' built-in materials to URP, makes a Humanoid copy of Claire's run clip for the officer rig
/// (her own Generic import is left alone), and references the prefabs. Runs once if the asset is missing or old.
/// </summary>
[InitializeOnLoad]
public static class PoliceSetBuilder
{
    private const string AssetPath = "Assets/Resources/PoliceSet.asset";
    private const string OfficerRoot = "Assets/Police_officer";
    private const string VehicleRoot = "Assets/Police Car & Helicopter";
    private const string RunSource = "Assets/Claire_character1/claire@Running.fbx";
    private const string RunCopy = OfficerRoot + "/Animations/PoliceOfficer@Running.fbx";
    private const string RunController = OfficerRoot + "/Animations/PoliceOfficerRun.controller";
    private const string IdleSource = "Assets/Claire_character1/claire@Victory Idle.fbx";
    private const string IdleCopy = OfficerRoot + "/Animations/PoliceOfficer@Idle.fbx";
    private const int Version = 2;

    static PoliceSetBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var existing = AssetDatabase.LoadAssetAtPath<PoliceSet>(AssetPath);
            if (existing == null || existing.generatorVersion < Version)
                Build();
        };
    }

    [MenuItem("Grab & Go/Rebuild Police Set")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(OfficerRoot) || !AssetDatabase.IsValidFolder(VehicleRoot))
        {
            Debug.LogWarning("[PoliceSetBuilder] Police asset packs not found; police set skipped.");
            return;
        }

        int converted = ConvertMaterialsToUrp(OfficerRoot) + ConvertMaterialsToUrp(VehicleRoot);
        var controller = BuildRunController();

        var set = AssetDatabase.LoadAssetAtPath<PoliceSet>(AssetPath);
        bool created = set == null;
        if (created) set = ScriptableObject.CreateInstance<PoliceSet>();

        var officer = AssetDatabase.LoadAssetAtPath<GameObject>($"{OfficerRoot}/Prefab/police_officer.prefab");
        set.officerPrefabs = officer != null ? new[] { officer } : new GameObject[0];
        set.officerRunController = controller;
        set.policeCar = AssetDatabase.LoadAssetAtPath<GameObject>($"{VehicleRoot}/Prefabs/Interceptor.prefab");
        set.helicopter = AssetDatabase.LoadAssetAtPath<GameObject>($"{VehicleRoot}/Prefabs/Helicopter.prefab");
        set.generatorVersion = Version;

        if (created) AssetDatabase.CreateAsset(set, AssetPath);
        else EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PoliceSetBuilder] {AssetPath} ready ({converted} materials converted to URP).");
    }

    private static int ConvertMaterialsToUrp(string folder)
    {
        var urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) return 0;
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat == null || mat.shader == urpLit) continue;

            bool twoSided = mat.shader.name.Contains("2sided");
            Texture albedo = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Texture normal = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            Texture metallic = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
            Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

            mat.shader = urpLit;
            mat.SetTexture("_BaseMap", albedo);
            mat.SetColor("_BaseColor", color);
            mat.SetTexture("_BumpMap", normal);
            if (normal != null) mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", metallic);
            if (metallic != null)
            {
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Smoothness", 1f); // the map's alpha holds the smoothness
            }
            else
            {
                mat.SetFloat("_Smoothness", 0.3f);
            }
            mat.SetFloat("_Cull", twoSided ? 0f : 2f);
            EditorUtility.SetDirty(mat);
            count++;
        }
        return count;
    }

    private static RuntimeAnimatorController BuildRunController()
    {
        if (!AssetDatabase.IsValidFolder($"{OfficerRoot}/Animations"))
            AssetDatabase.CreateFolder(OfficerRoot, "Animations");

        var run = ImportHumanoidCopy(RunSource, RunCopy);
        if (run == null) return null;
        var idle = ImportHumanoidCopy(IdleSource, IdleCopy);

        if (File.Exists(RunController)) AssetDatabase.DeleteAsset(RunController);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(RunController);
        var machine = controller.layers[0].stateMachine;
        machine.AddState("Run").motion = run; // first state added is the default
        if (idle != null) machine.AddState("Idle").motion = idle;
        return controller;
    }

    private static AnimationClip ImportHumanoidCopy(string source, string copy)
    {
        if (AssetDatabase.LoadMainAssetAtPath(source) == null) return null;
        if (AssetDatabase.LoadMainAssetAtPath(copy) == null && !AssetDatabase.CopyAsset(source, copy))
            return null;

        var importer = (ModelImporter)AssetImporter.GetAtPath(copy);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        var clips = importer.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.loopTime = true;
            c.lockRootRotation = true;
            c.lockRootHeightY = true;
            c.lockRootPositionXZ = true;
            c.keepOriginalOrientation = true;
            c.keepOriginalPositionY = true;
            c.keepOriginalPositionXZ = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();

        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(copy))
            if (o is AnimationClip a && !a.name.StartsWith("__preview")) return a;
        return null;
    }
}
#endif
