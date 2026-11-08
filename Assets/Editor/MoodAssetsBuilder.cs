#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Imports the title art as a sprite and creates the transparent material used for particles and light beams.</summary>
public static class MoodAssetsBuilder
{
    private const string Art = "Assets/Resources/SplashArt.png";
    private const string Fx = "Assets/Resources/FxParticle.mat";

    [MenuItem("Grab & Go/Rebuild Mood Assets")]
    public static void Build()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Art);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        var material = AssetDatabase.LoadAssetAtPath<Material>(Fx);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material, Fx);
        }
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        Debug.Log("[MoodAssetsBuilder] Title art and particle material ready.");
    }
}
#endif
