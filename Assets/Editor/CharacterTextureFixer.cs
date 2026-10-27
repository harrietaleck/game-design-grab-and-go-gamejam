#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Claire and AJ are Mixamo FBX files with their textures embedded. Unity does not apply embedded textures
/// until they are extracted, so both characters rendered white. This extracts each character's textures
/// once into a "Textures" folder next to the model (the same as the importer's "Extract Textures" button),
/// marks normal maps correctly and re-imports the character's FBX files so their materials pick the textures up.
/// Runs automatically once; also available from the menu.
/// </summary>
[InitializeOnLoad]
public static class CharacterTextureFixer
{
    private static readonly (string folder, string baseModel)[] Characters =
    {
        ("Assets/Claire_character1", "Assets/Claire_character1/claire.fbx"),
        ("Assets/AJ_character2", "Assets/AJ_character2/Aj.fbx"),
    };

    static CharacterTextureFixer()
    {
        EditorApplication.delayCall += () => Fix(force: false);
    }

    [MenuItem("Grab & Go/Fix Character Textures")]
    public static void FixFromMenu() => Fix(force: true);

    private static void Fix(bool force)
    {
        foreach (var (folder, baseModel) in Characters)
        {
            if (!File.Exists(baseModel)) continue;
            string texFolder = $"{folder}/Textures";
            bool hasTextures = AssetDatabase.IsValidFolder(texFolder) &&
                               AssetDatabase.FindAssets("t:Texture2D", new[] { texFolder }).Length > 0;
            if (hasTextures && !force) continue;

            var importer = AssetImporter.GetAtPath(baseModel) as ModelImporter;
            if (importer == null) continue;

            if (!AssetDatabase.IsValidFolder(texFolder))
                AssetDatabase.CreateFolder(folder, "Textures");

            bool extracted = importer.ExtractTextures(texFolder);
            AssetDatabase.Refresh();
            Debug.Log($"[CharacterTextureFixer] {Path.GetFileName(baseModel)}: textures {(extracted ? "extracted" : "not extracted (none embedded?)")} to {texFolder}");

            // Normal maps must be imported as normal maps.
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { texFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.ToLowerInvariant().Contains("normal")) continue;
                if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.SaveAndReimport();
                }
            }

            // Re-import every FBX of this character (model + animation files) so materials find the textures.
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
#endif
