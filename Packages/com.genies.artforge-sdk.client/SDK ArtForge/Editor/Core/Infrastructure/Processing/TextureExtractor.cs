using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using Object = UnityEngine.Object;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Processing
{
    /// <summary>
    /// Responsible for extracting textures from GLB materials and saving them to disk.
    /// Handles embedded textures, GPU readback, and file validation.
    /// </summary>
    internal class TextureExtractor
    {
        private const int MinTextureDimension = 1;
        private const int PngSignatureLength = 8;
        private const int RenderTextureDepth = 0;
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private readonly ILogger _logger;
        private readonly bool _verboseLogging;

        public TextureExtractor(ILogger logger, bool verboseLogging = false)
        {
            _logger = logger;
            _verboseLogging = verboseLogging;
        }

        /// <summary>
        /// Result of texture extraction operation.
        /// </summary>
        public class ExtractionResult
        {
            public int ExtractedCount { get; set; }
            public List<string> ExtractedPaths { get; set; } = new List<string>();
        }

        /// <summary>
        /// Extracts all textures from a material and saves them to the specified directory.
        /// </summary>
        public ExtractionResult ExtractTexturesFromMaterial(Material material, string assetName, string texturesDir)
        {
            if (material == null)
            {
                _logger.Error("[TextureExtractor] Material is null");
                return new ExtractionResult();
            }

            LogInfo($"Extracting textures from material: {material.name}");

            // Ensure directory exists
            AssetPathHelpers.EnsureParentDir(Path.Combine(texturesDir, "dummy.txt"));

            var result = new ExtractionResult();
            HashSet<int> processedTextureIds = new HashSet<int>();

            // Enumerate all textures from material
            foreach (var (propertyName, texture2D) in TexturePropertyMapper.EnumerateTextures(material))
            {
                int textureId = texture2D.GetInstanceID();
                if (processedTextureIds.Contains(textureId))
                {
                    continue;
                }

                processedTextureIds.Add(textureId);

                if (ExtractSingleTexture(texture2D, propertyName, assetName, texturesDir, out string extractedPath))
                {
                    result.ExtractedCount++;
                    result.ExtractedPaths.Add(extractedPath);
                }
            }

            if (result.ExtractedCount > 0)
            {
                _logger.Info($"[TextureExtractor] ✓ Extracted {result.ExtractedCount} texture(s)");
            }
            else
            {
                _logger.Warning($"[TextureExtractor] No textures extracted from material: {material.name}");
            }

            return result;
        }

        /// <summary>
        /// Extracts a single texture and saves it to disk.
        /// </summary>
        private bool ExtractSingleTexture(Texture2D texture, string propertyName, string assetName, string texturesDir, out string outputPath)
        {
            outputPath = null;

            if (texture == null)
            {
                return false;
            }

            LogVerbose($"Processing texture: {texture.name} from property: {propertyName}");

            // Determine output path
            string fileName = TexturePropertyMapper.GetTextureFileName(propertyName, assetName);
            outputPath = AssetPathHelpers.NormalizePath(Path.Combine(texturesDir, fileName));

            // Get source asset path
            string sourceAssetPath = AssetDatabase.GetAssetPath(texture);

            // Check if we need to extract (GLB sub-asset or no asset path)
            bool needsExtraction = IsGlbSubAsset(sourceAssetPath) || string.IsNullOrEmpty(sourceAssetPath);

            if (needsExtraction)
            {
                LogVerbose($"Extracting embedded texture: {sourceAssetPath}");
                return ExtractEmbeddedTexture(texture, outputPath);
            }
            else
            {
                LogVerbose($"Copying texture file: {sourceAssetPath} → {outputPath}");
                return CopyTextureFile(sourceAssetPath, outputPath);
            }
        }

        /// <summary>
        /// Checks if an asset path points to a GLB/GLTF file (sub-asset).
        /// </summary>
        private bool IsGlbSubAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            string extension = Path.GetExtension(assetPath).ToLower();
            return extension == ".glb" || extension == ".gltf";
        }

        /// <summary>
        /// Copies a texture file from one location to another.
        /// </summary>
        private bool CopyTextureFile(string sourceAssetPath, string destAssetPath)
        {
            string fullSourcePath = AssetPathHelpers.ToFullPath(sourceAssetPath);
            string fullDestPath = AssetPathHelpers.ToFullPath(destAssetPath);

            if (!File.Exists(fullSourcePath))
            {
                _logger.Warning($"[TextureExtractor] Source file not found: {sourceAssetPath}");
                return false;
            }

            try
            {
                AssetPathHelpers.EnsureParentDir(destAssetPath);
                File.Copy(fullSourcePath, fullDestPath, true);

                // Validate file
                if (!AssetPathHelpers.IsFileValid(destAssetPath, _logger))
                {
                    _logger.Warning($"[TextureExtractor] Copied file is invalid, trying embedded extraction");
                    return false;
                }

                LogInfo($"✓ Copied texture: {Path.GetFileName(destAssetPath)}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"[TextureExtractor] Failed to copy texture: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Extracts an embedded texture directly from Texture2D pixels.
        /// Uses GPU readback if texture is not CPU-readable.
        /// </summary>
        private bool ExtractEmbeddedTexture(Texture2D texture, string outputAssetPath)
        {
            try
            {
                int width = Mathf.Max(MinTextureDimension, texture.width);
                int height = Mathf.Max(MinTextureDimension, texture.height);

                byte[] pngData = texture.isReadable
                    ? ExtractViaReadableTexture(texture, width, height)
                    : ExtractViaGPUReadback(texture, width, height);

                if (pngData == null || pngData.Length < PngSignatureLength)
                {
                    _logger.Error($"[TextureExtractor] Failed to encode texture: {texture.name}");
                    return false;
                }

                // Validate PNG signature
                if (!IsPngSignatureValid(pngData))
                {
                    _logger.Error($"[TextureExtractor] Invalid PNG signature, refusing to write: {outputAssetPath}");
                    return false;
                }

                // Write to disk
                string fullPath = AssetPathHelpers.ToFullPath(outputAssetPath);
                AssetPathHelpers.EnsureParentDir(outputAssetPath);
                File.WriteAllBytes(fullPath, pngData);

                if (AssetPathHelpers.IsFileValid(outputAssetPath, _logger))
                {
                    LogInfo($"✓ Extracted embedded texture: {Path.GetFileName(outputAssetPath)} ({pngData.Length} bytes)");
                    return true;
                }
                else
                {
                    _logger.Error($"[TextureExtractor] Failed to write texture: {outputAssetPath}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[TextureExtractor] Failed to extract embedded texture: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Extracts texture data via CPU path (for readable textures).
        /// </summary>
        private byte[] ExtractViaReadableTexture(Texture2D texture, int width, int height)
        {
            Texture2D tmp = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            tmp.SetPixels32(texture.GetPixels32());
            tmp.Apply(false, false);
            byte[] pngData = tmp.EncodeToPNG();
            Object.DestroyImmediate(tmp);
            return pngData;
        }

        /// <summary>
        /// Extracts texture data via GPU readback (for non-readable textures and GLB sub-assets).
        /// </summary>
        private byte[] ExtractViaGPUReadback(Texture2D texture, int width, int height)
        {
            RenderTexture previousRT = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(width, height, RenderTextureDepth, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);

            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;

            Texture2D tmp = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            tmp.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            tmp.Apply(false, false);

            RenderTexture.active = previousRT;
            RenderTexture.ReleaseTemporary(rt);

            byte[] pngData = tmp.EncodeToPNG();
            Object.DestroyImmediate(tmp);
            return pngData;
        }

        /// <summary>
        /// Validates PNG file signature.
        /// PNG signature: 89 50 4E 47 0D 0A 1A 0A
        /// </summary>
        private bool IsPngSignatureValid(byte[] data)
        {
            if (data == null || data.Length < PngSignatureLength)
            {
                return false;
            }

            for (int i = 0; i < PngSignature.Length; i++)
            {
                if (data[i] != PngSignature[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void LogInfo(string message)
        {
            _logger.Info($"[TextureExtractor] {message}");
        }

        private void LogVerbose(string message)
        {
            if (_verboseLogging)
            {
                _logger.Info($"[TextureExtractor] {message}");
            }
        }
    }
}
