using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Processing
{
    /// <summary>
    /// Single source of truth for texture property mapping.
    /// Maps shader property names to texture filenames and provides enumeration utilities.
    /// </summary>
    internal static class TexturePropertyMapper
    {
        /// <summary>
        /// Common texture property names to check (ordered by priority).
        /// </summary>
        private static readonly string[] CommonTextureProperties = new[]
        {
            "_BaseMap", "_MainTex", "_BaseColorMap",
            "_BumpMap", "_NormalMap",
            "_MetallicGlossMap", "_MetallicMap",
            "_RoughnessMap",
            "_OcclusionMap"
        };

    /// <summary>
    /// Maps shader property name to standardized texture filename suffix.
    /// </summary>
    public static string GetTextureFileName(string propertyName, string assetName)
    {
        string suffix = GetTextureSuffix(propertyName);
        return $"{assetName}{suffix}.png";
    }

    /// <summary>
    /// Determines the texture suffix from property name or texture name.
    /// Handles both Unity shader properties (_BaseMap) and GLB texture names (baseColorTexture).
    /// </summary>
    private static string GetTextureSuffix(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            {
                return "_Unknown";
            }

            // Lowercase for case-insensitive matching
            string lowerName = propertyName.ToLower();

        // Unity shader properties (exact match)
        switch (propertyName)
        {
            case "_BaseMap":
            case "_MainTex":
            case "_BaseColorMap":
                return "_Albedo";
            case "_BumpMap":
            case "_NormalMap":
                return "_Normal";
            case "_MetallicGlossMap":
            case "_MetallicMap":
                return "_MetallicSmoothness";
            case "_RoughnessMap":
                return "_Roughness";
            case "_OcclusionMap":
                return "_Occlusion";
        }

        // GLB/GLTF texture naming conventions (case-insensitive substring match)
        if (lowerName.Contains("basecolor") || lowerName.Contains("albedo") || lowerName.Contains("diffuse"))
            {
                return "_Albedo";
            }

            if (lowerName.Contains("normal"))
            {
                return "_Normal";
            }

            if (lowerName.Contains("metallicroughness") || lowerName.Contains("metallic"))
            {
                return "_MetallicSmoothness";
            }

            if (lowerName.Contains("roughness"))
            {
                return "_Roughness";
            }

            if (lowerName.Contains("occlusion") || lowerName.Contains("ao"))
            {
                return "_Occlusion";
            }

            if (lowerName.Contains("emissive") || lowerName.Contains("emission"))
            {
                return "_Emission";
            }

            // Fallback: use the property name as-is
            return $"_{propertyName.TrimStart('_')}";
    }

        /// <summary>
        /// Enumerates all texture properties from a material (dynamic + fallback).
        /// Returns tuples of (propertyName, texture).
        /// </summary>
        public static IEnumerable<(string propertyName, Texture2D texture)> EnumerateTextures(Material material)
        {
            if (material == null)
            {
                yield break;
            }

            HashSet<int> processedTextureIds = new HashSet<int>();

            // First pass: Dynamic shader properties
            if (material.shader != null)
            {
                int propertyCount = material.shader.GetPropertyCount();
                for (int i = 0; i < propertyCount; i++)
                {
                    ShaderPropertyType propertyType = material.shader.GetPropertyType(i);
                    if (propertyType == ShaderPropertyType.Texture)
                    {
                        string propertyName = material.shader.GetPropertyName(i);
                        Texture texture = material.GetTexture(propertyName);

                        if (texture is Texture2D texture2D && !processedTextureIds.Contains(texture2D.GetInstanceID()))
                        {
                            processedTextureIds.Add(texture2D.GetInstanceID());
                            yield return (propertyName, texture2D);
                        }
                    }
                }
            }

            // Second pass: Common fallback properties (in case dynamic enumeration missed something)
            foreach (string propertyName in CommonTextureProperties)
            {
                if (material.HasProperty(propertyName))
                {
                    Texture texture = material.GetTexture(propertyName);
                    if (texture is Texture2D texture2D && !processedTextureIds.Contains(texture2D.GetInstanceID()))
                    {
                        processedTextureIds.Add(texture2D.GetInstanceID());
                        yield return (propertyName, texture2D);
                    }
                }
            }
        }

        /// <summary>
        /// Gets all common texture property names (for fallback checks).
        /// </summary>
        public static IReadOnlyList<string> GetCommonTextureProperties()
        {
            return CommonTextureProperties;
        }

        /// <summary>
        /// Checks if a property name is a known texture property.
        /// </summary>
        public static bool IsTextureProperty(string propertyName)
        {
            foreach (string common in CommonTextureProperties)
            {
                if (common == propertyName)
                {
                    return true;
                }
            }
            return false;
        }
    }
}

