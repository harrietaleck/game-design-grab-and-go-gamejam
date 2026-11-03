#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Creates Assets/Resources/UITheme.asset from the "Sci-Fi UI" skin and "MK Easy Text" (SoftEffects) fonts.</summary>
[InitializeOnLoad]
public static class UIThemeBuilder
{
    private const string AssetPath = "Assets/Resources/UITheme.asset";
    private const string Skin = "Assets/Sci-Fi UI/_SciFi_GUISkin_";
    private const string MkFonts = "Assets/SoftEffects/EditFolder";
    private const int Version = 1;

    static UIThemeBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            var existing = AssetDatabase.LoadAssetAtPath<UITheme>(AssetPath);
            if (existing == null || existing.generatorVersion < Version) Build();
        };
    }

    [MenuItem("Grab & Go/Rebuild UI Theme")]
    public static void Build()
    {
        var theme = AssetDatabase.LoadAssetAtPath<UITheme>(AssetPath);
        bool created = theme == null;
        if (created) theme = ScriptableObject.CreateInstance<UITheme>();

        // Borders chosen from the artwork's bevels (the GUISkin's own borders are IMGUI-sized).
        Set(theme.window, $"{Skin}/Skin_Assets/window/window_transparent.png", new Vector4(110, 110, 110, 110), 6f);
        Set(theme.button, $"{Skin}/Skin_Assets/buttons/button1.png", new Vector4(200, 170, 200, 170), 5f);
        Set(theme.buttonActive, $"{Skin}/Skin_Assets/buttons/button_active.png", new Vector4(200, 170, 200, 170), 5f);
        Set(theme.barFrame, $"{Skin}/Skin_Assets/bars/progress_bar_background.png", new Vector4(40, 60, 40, 60), 4f);
        Set(theme.barFill, $"{Skin}/Bonus_Assets/bars/background_bar1.png", new Vector4(100, 70, 100, 70), 4f);
        Set(theme.barRed, $"{Skin}/Bonus_Assets/bars/bar_red1.png", new Vector4(100, 70, 100, 70), 4f);

        theme.hudFont = AssetDatabase.LoadAssetAtPath<Font>($"{Skin}/Skin_Assets/font/zekton free.ttf");
        theme.purpleFont = AssetDatabase.LoadAssetAtPath<Font>($"{MkFonts}/Coiny-Regular_SEFont_0/Coiny-Regular_se_rep.fontsettings");
        theme.goldFont = AssetDatabase.LoadAssetAtPath<Font>($"{MkFonts}/Coiny-Regular_SEFont_0 1/Coiny-Regular_se_rep.fontsettings");
        theme.neonFont = AssetDatabase.LoadAssetAtPath<Font>($"{MkFonts}/Coiny-Regular_SEFont_0 2/Coiny-Regular_se_rep.fontsettings");
        theme.generatorVersion = Version;

        if (created)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateAsset(theme, AssetPath);
        }
        else EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        Debug.Log($"[UIThemeBuilder] {(created ? "Created" : "Updated")} {AssetPath}: window {(theme.window.texture ? "ok" : "missing")}, " +
                  $"HUD font {(theme.hudFont ? theme.hudFont.name : "missing")}, MK fonts gold/neon/purple " +
                  $"{(theme.goldFont ? "ok" : "-")}/{(theme.neonFont ? "ok" : "-")}/{(theme.purpleFont ? "ok" : "-")}.");
    }

    private static void Set(UITheme.Slice slice, string path, Vector4 border, float borderScale)
    {
        slice.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        slice.border = border;
        slice.borderScale = borderScale;
        if (slice.texture == null) Debug.LogWarning($"[UIThemeBuilder] Missing texture {path}");
    }
}
#endif
