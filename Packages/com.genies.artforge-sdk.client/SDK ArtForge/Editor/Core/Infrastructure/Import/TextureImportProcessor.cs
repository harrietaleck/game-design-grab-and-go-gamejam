using UnityEngine;
using UnityEditor;
using System.IO;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Import
{
    /// <summary>
    /// Asset postprocessor to automatically configure imported textures
    /// </summary>
    internal class TextureImportProcessor : AssetPostprocessor
    {
        private const float NormalMapHeightScale = 0.01f;
        private const int MaxTextureSize = 2048;

        private ILogger _logger;

        private ILogger Logger
        {
            get
            {
                if (_logger == null)
                {
                    _logger = GeniesArtForgeService.Logger ?? new UnityLogger();
                }

                return _logger;
            }
        }
        private void OnPreprocessTexture()
        {
            try
            {
                // Only process textures in GeniesArtForge directories
                if (!IsGeniesArtForgeTexture(assetPath))
                {
                    return;
                }

                TextureImporter textureImporter = assetImporter as TextureImporter;
                if (textureImporter == null)
                {
                    return;
                }

                string fileName = Path.GetFileName(assetPath).ToLower();

                // Configure based on texture type
                if (fileName.Contains("normal") || fileName.Contains("_n."))
                {
                    // Normal map settings
                    textureImporter.textureType = TextureImporterType.NormalMap;
                    textureImporter.sRGBTexture = false;

                    // "Create from Grayscale" = true
                    textureImporter.convertToNormalmap = true;
                    
                    // "Filtering" = Smooth
                    textureImporter.normalmapFilter = TextureImporterNormalFilter.Standard; 
                    
                    // "Bumpiness" = 0.01
                    textureImporter.heightmapScale = NormalMapHeightScale;
                }
                else
                {
                    // Albedo and other color textures
                    textureImporter.textureType = TextureImporterType.Default;
                    textureImporter.sRGBTexture = true;
                }

                // Quality settings from metadata.json (fallback to default).
                int maxTextureSize = MaxTextureSize;
                var metadataSettings = AssetMetadataSettingsReader.Read(assetPath, Logger);
                if (metadataSettings?.TextureResolution != null && metadataSettings.TextureResolution.Value > 0)
                {
                    maxTextureSize = metadataSettings.TextureResolution.Value;
                }

                textureImporter.maxTextureSize = maxTextureSize;
                textureImporter.textureCompression = TextureImporterCompression.Compressed;
            }
            catch (System.Exception ex)
            {
                Logger.Error($"[GeniesArtForge] Error in OnPreprocessTexture for {assetPath}: {ex.Message}");
            }
        }

        private bool IsGeniesArtForgeTexture(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            string normalizedPath = path.Replace('\\', '/');

            // Check if it's a valid image file
            if (!(normalizedPath.EndsWith(".png") || normalizedPath.EndsWith(".jpg") || normalizedPath.EndsWith(".jpeg")))
            {
                return false;
            }

            // Check if it's in GeniesArtForge directories
            string projectRoot = EditorPrefs.GetString("GAF_ProjectRoot", "Assets/Genies/ArtForge");

            return normalizedPath.StartsWith(projectRoot) &&
                   (normalizedPath.Contains("/Textures/") || normalizedPath.Contains("/Materials/"));
        }
    }
}
