#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Prepares the third-party packs for the game: converts their materials to URP, sets up the officer and K9
/// animators, and creates Assets/Resources/PoliceSet.asset and JewelrySet.asset.
/// Runs once if an asset is missing or old, and from the menu.
/// </summary>
[InitializeOnLoad]
public static class PoliceSetBuilder
{
    private const string AssetPath = "Assets/Resources/PoliceSet.asset";
    private const string JewelryPath = "Assets/Resources/JewelrySet.asset";
    private const string OfficerRoot = "Assets/Police_officer";
    private const string VehicleRoot = "Assets/Police Car & Helicopter";
    private const string GunRoot = "Assets/Nokobot/Modern Guns - Handgun";
    private const string DogRoot = "Assets/RSG_DogsPack/HDRP";
    private const string JewelryRoot = "Assets/jewelry_shop";
    private const string AnimRoot = "Assets/animation_Police";
    private const string RunSource = "Assets/Claire_character1/claire@Running.fbx";
    private const string RunCopy = OfficerRoot + "/Animations/PoliceOfficer@Running.fbx";
    private const string OldIdleCopy = OfficerRoot + "/Animations/PoliceOfficer@Idle.fbx";
    private const string RunController = OfficerRoot + "/Animations/PoliceOfficerRun.controller";
    private const string DogController = DogRoot + "/Animations/PoliceDog.controller";
    private const int Version = 3;

    private static readonly (string prefab, int weight, int value)[] JewelryItems =
    {
        ("SM_ring Variant", 6, 10),
        ("SM_watch Variant", 4, 15),
        ("SM_necklace Variant", 3, 20),
        ("SM_diamond Variant", 2, 30),
        ("SM_crown Variant", 1, 50),
    };

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

        int converted = ConvertMaterialsToUrp(OfficerRoot) + ConvertMaterialsToUrp(VehicleRoot)
                        + ConvertMaterialsToUrp(GunRoot + "/Materials");
        ConvertDogMaterials();
        var controller = BuildOfficerController();

        var set = AssetDatabase.LoadAssetAtPath<PoliceSet>(AssetPath);
        bool created = set == null;
        if (created) set = ScriptableObject.CreateInstance<PoliceSet>();

        var officer = AssetDatabase.LoadAssetAtPath<GameObject>($"{OfficerRoot}/Prefab/police_officer.prefab");
        set.officerPrefabs = officer != null ? new[] { officer } : new GameObject[0];
        set.officerRunController = controller;
        set.handgun = AssetDatabase.LoadAssetAtPath<GameObject>($"{GunRoot}/_Prefabs/Handgun Black/M1911 Handgun_Black.prefab");
        set.policeCar = AssetDatabase.LoadAssetAtPath<GameObject>($"{VehicleRoot}/Prefabs/Interceptor.prefab");
        set.helicopter = AssetDatabase.LoadAssetAtPath<GameObject>($"{VehicleRoot}/Prefabs/Helicopter.prefab");
        set.policeDog = AssetDatabase.LoadAssetAtPath<GameObject>($"{DogRoot}/Prefabs/P_GermanShepherd.prefab");
        set.dogController = BuildDogController();
        set.generatorVersion = Version;

        if (created) AssetDatabase.CreateAsset(set, AssetPath);
        else EditorUtility.SetDirty(set);

        BuildJewelrySet();
        AssetDatabase.SaveAssets();
        Debug.Log($"[PoliceSetBuilder] {AssetPath} ready ({converted} materials converted to URP).");
    }

    public static int ConvertMaterialsToUrp(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return 0;
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            if (ConvertToUrp(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)))) count++;
        return count;
    }

    private static bool ConvertToUrp(Material mat)
    {
        var urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null || mat == null || mat.shader == urpLit) return false;

        bool twoSided = mat.shader.name.Contains("2sided");
        Texture albedo = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
        Texture normal = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
        Texture metallic = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
        Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
        float metalness = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
        float gloss = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.3f;

        mat.shader = urpLit;
        mat.SetTexture("_BaseMap", albedo);
        mat.SetColor("_BaseColor", color);
        mat.SetTexture("_BumpMap", normal);
        if (normal != null) mat.EnableKeyword("_NORMALMAP");
        mat.SetTexture("_MetallicGlossMap", metallic);
        mat.SetFloat("_Metallic", metalness);
        if (metallic != null) mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        mat.SetFloat("_Smoothness", metallic != null ? 1f : gloss); // a metallic map's alpha holds the smoothness
        mat.SetFloat("_Cull", twoSided ? 0f : 2f);
        EditorUtility.SetDirty(mat);
        return true;
    }

    /// <summary>The dog pack ships HDRP materials only; rebuild them for URP from its textures.</summary>
    private static void ConvertDogMaterials()
    {
        var urpLit = Shader.Find("Universal Render Pipeline/Lit");
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{DogRoot}/Textures/T_GermanShepherd_B.png");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{DogRoot}/Textures/T_GermanShepherd_N.png");
        if (urpLit == null || albedo == null) return;

        foreach (var name in new[] { "M_GermanShepherd", "M_GermanShepherd_Transparent" })
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{DogRoot}/Materials/{name}.mat");
            if (mat == null || mat.shader == urpLit) continue;
            mat.shader = urpLit;
            mat.SetTexture("_BaseMap", albedo);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", normal);
            if (normal != null) mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_Smoothness", 0.2f);
            if (name.EndsWith("Transparent")) // fur cards: cut out by the texture's alpha
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.5f);
                mat.SetFloat("_Cull", 0f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = 2450;
            }
            EditorUtility.SetDirty(mat);
        }
    }

    private static void BuildJewelrySet()
    {
        if (!AssetDatabase.IsValidFolder(JewelryRoot)) return;
        var items = new List<JewelrySet.Item>();
        foreach (var (prefabName, weight, value) in JewelryItems)
        {
            foreach (string guid in AssetDatabase.FindAssets($"\"{prefabName}\" t:Prefab", new[] { JewelryRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != prefabName) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials) ConvertToUrp(m);
                items.Add(new JewelrySet.Item { prefab = prefab, weight = weight, value = value });
                break;
            }
        }

        var set = AssetDatabase.LoadAssetAtPath<JewelrySet>(JewelryPath);
        bool created = set == null;
        if (created) set = ScriptableObject.CreateInstance<JewelrySet>();
        set.items = items.ToArray();
        if (created) AssetDatabase.CreateAsset(set, JewelryPath);
        else EditorUtility.SetDirty(set);
    }

    private static RuntimeAnimatorController BuildOfficerController()
    {
        if (!AssetDatabase.IsValidFolder($"{OfficerRoot}/Animations"))
            AssetDatabase.CreateFolder(OfficerRoot, "Animations");
        if (AssetDatabase.LoadMainAssetAtPath(OldIdleCopy) != null) AssetDatabase.DeleteAsset(OldIdleCopy);

        if (AssetDatabase.LoadMainAssetAtPath(RunSource) == null) return null;
        if (AssetDatabase.LoadMainAssetAtPath(RunCopy) == null && !AssetDatabase.CopyAsset(RunSource, RunCopy)) return null;

        var run = ImportHumanoid(RunCopy);
        if (run == null) return null;
        var idle = ImportHumanoid($"{AnimRoot}/Ch35_nonPBR@Pistol Idle.fbx");
        var kneel = ImportHumanoid($"{AnimRoot}/Ch35_nonPBR@Pistol Kneeling Idle.fbx");
        var fire = ImportHumanoid($"{AnimRoot}/Ch35_nonPBR@Crouch Rapid Fire.fbx");

        if (File.Exists(RunController)) AssetDatabase.DeleteAsset(RunController);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(RunController);
        var machine = controller.layers[0].stateMachine;
        machine.AddState("Run").motion = run; // first state added is the default
        if (idle != null) machine.AddState("Idle").motion = idle;
        if (kneel != null) machine.AddState("Kneel").motion = kneel;
        if (fire != null) machine.AddState("Fire").motion = fire;
        return controller;
    }

    private static AnimationClip ImportHumanoid(string path)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) == null) return null;
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        LoopInPlace(importer);
        return FirstClip(path);
    }

    private static RuntimeAnimatorController BuildDogController()
    {
        var run = LoopGeneric($"{DogRoot}/Animations/A_Run_Loop.fbx");
        var idle = LoopGeneric($"{DogRoot}/Animations/A_Idle_Breathing.fbx");
        if (run == null) return null;

        if (File.Exists(DogController)) AssetDatabase.DeleteAsset(DogController);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(DogController);
        var machine = controller.layers[0].stateMachine;
        machine.AddState("Run").motion = run;
        if (idle != null) machine.AddState("Idle").motion = idle;
        return controller;
    }

    public static AnimationClip LoopGeneric(string path)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) == null) return null;
        LoopInPlace((ModelImporter)AssetImporter.GetAtPath(path));
        return FirstClip(path);
    }

    private static void LoopInPlace(ModelImporter importer)
    {
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
    }

    public static AnimationClip FirstClip(string path)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is AnimationClip a && !a.name.StartsWith("__preview")) return a;
        return null;
    }
}
#endif
