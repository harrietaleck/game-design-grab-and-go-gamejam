using System.IO;
using UnityEngine;
using UnityEditor;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Processing
{
    /// <summary>
    /// Responsible for creating and configuring Unity materials.
    /// Handles both GLB material extraction and FBX material creation from scratch.
    /// </summary>
    internal class MaterialBuilder
    {
        private const float WorkflowModeSpecular = 0.0f;

        private readonly ILogger _logger;
        private readonly TextureExtractor _textureExtractor;

        public MaterialBuilder(ILogger logger, bool verboseLogging = false)
        {
            _logger = logger;
            _textureExtractor = new TextureExtractor(logger, verboseLogging);
        }

        /// <summary>
        /// Result of material building operation.
        /// </summary>
        public class BuildResult
        {
            public Material Material { get; set; }
            public string MaterialPath { get; set; }
            public int TexturesExtracted { get; set; }
        }

        /// <summary>
        /// Builds a material for the given model.
        /// For GLB: extracts embedded material and textures.
        /// For FBX: creates material from texture files.
        /// </summary>
        public BuildResult BuildMaterial(GameObject modelAsset, string assetName, ModelFormatOptions options,
            string materialPath, string texturesDir)
        {
            if (modelAsset == null)
            {
                _logger.Error("[MaterialBuilder] Model asset is null");
                return null;
            }

            AssetPathHelpers.EnsureParentDir(materialPath);

            if (options.ExtractMaterialsAndTextures)
            {
                return BuildFromGlb(modelAsset, assetName, materialPath, texturesDir);
            }
            else
            {
                return BuildFromTextures(assetName, materialPath, texturesDir);
            }
        }

        /// <summary>
        /// Extracts material and textures from a GLB model.
        /// </summary>
        private BuildResult BuildFromGlb(GameObject modelAsset, string assetName, string materialPath, string texturesDir)
        {
            _logger.Info($"[MaterialBuilder] Extracting material from GLB model: {modelAsset.name}");

            // Find renderers
            Renderer[] renderers = modelAsset.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
            {
                _logger.Warning("[MaterialBuilder] No renderers found in GLB, creating default material");
                return BuildFromTextures(assetName, materialPath, texturesDir);
            }

            // Find first valid material
            Material sourceMaterial = FindFirstMaterial(renderers);
            if (sourceMaterial == null)
            {
                _logger.Warning("[MaterialBuilder] No material found in GLB, creating default material");
                return BuildFromTextures(assetName, materialPath, texturesDir);
            }

            _logger.Info($"[MaterialBuilder] Found source material: {sourceMaterial.name}");

            // Extract textures
            var extractionResult = _textureExtractor.ExtractTexturesFromMaterial(sourceMaterial, assetName, texturesDir);

            // Copy material
            Material extractedMaterial = new Material(sourceMaterial);
            extractedMaterial.name = "MAT_" + assetName;

            // Update material with extracted texture paths
            UpdateMaterialTextures(extractedMaterial, assetName, texturesDir);
            ApplyUrpLitSpecularWorkflow(extractedMaterial);

            // Save material
            AssetDatabase.CreateAsset(extractedMaterial, materialPath);

            // Reload to ensure texture references are persisted
            Material savedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (savedMaterial == null)
            {
                _logger.Warning($"[MaterialBuilder] Failed to reload saved material, using extracted material");
                savedMaterial = extractedMaterial;
            }

            _logger.Info($"[MaterialBuilder] ✓ Created material: {materialPath}");

            return new BuildResult
            {
                Material = savedMaterial,
                MaterialPath = materialPath,
                TexturesExtracted = extractionResult.ExtractedCount
            };
        }

        /// <summary>
        /// Creates a material from existing texture files.
        /// </summary>
        private BuildResult BuildFromTextures(string assetName, string materialPath, string texturesDir)
        {
            _logger.Info($"[MaterialBuilder] Creating material from textures: {assetName}");

            // Find shader
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                _logger.Warning("[MaterialBuilder] URP Lit shader not found, falling back to Standard");
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                _logger.Error("[MaterialBuilder] No compatible shader found");
                return null;
            }

            // Create material
            Material material = new Material(shader);
            material.name = "MAT_" + assetName;

            // Assign textures
            UpdateMaterialTextures(material, assetName, texturesDir);
            ApplyUrpLitSpecularWorkflow(material);

            // Save material
            AssetDatabase.CreateAsset(material, materialPath);

            _logger.Info($"[MaterialBuilder] ✓ Created material: {materialPath}");

            return new BuildResult
            {
                Material = material,
                MaterialPath = materialPath,
                TexturesExtracted = 0
            };
        }

        /// <summary>
        /// Updates material texture properties with extracted textures from disk.
        /// </summary>
        private void UpdateMaterialTextures(Material material, string assetName, string texturesDir)
        {
            int assignedCount = 0;

            foreach (string propertyName in TexturePropertyMapper.GetCommonTextureProperties())
            {
                if (!material.HasProperty(propertyName))
                {
                    continue;
                }

                // Try to find texture with any naming convention
                Texture2D texture = FindTextureForProperty(propertyName, assetName, texturesDir);

                if (texture != null)
                {
                    material.SetTexture(propertyName, texture);
                    assignedCount++;
                }
            }

            // Enable normal map keyword if needed
            bool hasNormalMap = false;
            if (material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") != null)
            {
                hasNormalMap = true;
            }
            else if (material.HasProperty("_NormalMap") && material.GetTexture("_NormalMap") != null)
            {
                hasNormalMap = true;
            }
            
            if (hasNormalMap)
            {
                material.EnableKeyword("_NORMALMAP");
            }

            if (assignedCount > 0)
            {
                _logger.Info($"[MaterialBuilder] ✓ Assigned {assignedCount} texture(s) to material");
            }
            else
            {
                _logger.Warning($"[MaterialBuilder] No textures assigned to material");
            }
        }

        /// <summary>
        /// Finds a texture file for a given property, checking multiple naming conventions.
        /// </summary>
        private Texture2D FindTextureForProperty(string propertyName, string assetName, string texturesDir)
        {
            // First try: Standard naming convention (e.g., "d14_Albedo.png")
            string standardFileName = TexturePropertyMapper.GetTextureFileName(propertyName, assetName);
            string standardPath = AssetPathHelpers.NormalizePath(Path.Combine(texturesDir, standardFileName));
            
            Texture2D texture = AssetPathHelpers.LoadOrImport<Texture2D>(standardPath, forceImport: true);
            if (texture != null)
            {
                return texture;
            }

            // Second try: Search for any matching texture in the directory
            string fullTexturesDir = AssetPathHelpers.ToFullPath(texturesDir);
            if (!Directory.Exists(fullTexturesDir))
            {
                return null;
            }

            string[] textureFiles = Directory.GetFiles(fullTexturesDir, "*.png");
            string searchPattern = GetTextureSearchPattern(propertyName);

            foreach (string filePath in textureFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath).ToLower();
                
                if (fileName.Contains(searchPattern))
                {
                    string assetPath = AssetPathHelpers.NormalizePath(Path.Combine(texturesDir, Path.GetFileName(filePath)));
                    texture = AssetPathHelpers.LoadOrImport<Texture2D>(assetPath, forceImport: true);
                    if (texture != null)
                    {
                        return texture;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a search pattern for finding textures by type (lowercase).
        /// </summary>
        private string GetTextureSearchPattern(string propertyName)
        {
            return propertyName switch
            {
                "_BaseMap" or "_MainTex" or "_BaseColorMap" => "basecolor",
                "_BumpMap" or "_NormalMap" => "normal",
                "_MetallicGlossMap" or "_MetallicMap" => "metallic",
                "_RoughnessMap" => "roughness",
                "_OcclusionMap" => "occlusion",
                _ => propertyName.ToLower().TrimStart('_')
            };
        }

        /// <summary>
        /// Finds the first valid material from renderers.
        /// </summary>
        private Material FindFirstMaterial(Renderer[] renderers)
        {
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                if (materials != null && materials.Length > 0)
                {
                    foreach (Material mat in materials)
                    {
                        if (mat != null)
                        {
                            return mat;
                        }
                    }
                }
            }
            return null;
        }

        private void ApplyUrpLitSpecularWorkflow(Material material)
        {
            if (material?.shader == null || material.shader.name != "Universal Render Pipeline/Lit")
            {
                return;
            }

            // 0 = Specular, 1 = Metallic
            if (material.HasProperty("_WorkflowMode"))
            {
                material.SetFloat("_WorkflowMode", WorkflowModeSpecular);
            }

            SetKeyword(material, "_SPECULAR_SETUP", true);
            SetKeyword(material, "_METALLICSPECGLOSSMAP", false);
            SetKeyword(material, "_SPECGLOSSMAP", material.GetTexture("_SpecGlossMap") != null);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }
    }
}
