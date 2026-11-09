using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Import
{
    /// <summary>
    /// Service for importing generated assets into Unity's asset database
    /// Handles copying files from temp locations and creating Asset entities
    /// </summary>
    internal class AssetImporter
    {
        private const int AssetImportRefreshDelayMs = 500;
        private const int IterationSuffixLength = 3;

        private readonly ILogger _logger;
        private readonly string _projectRootPath;

        public AssetImporter(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Get project root from BrandingConstants (remove trailing slash)
            _projectRootPath = BrandingConstants.AssetPaths.Root.TrimEnd('/');
        }

        /// <summary>
        /// Import a generated asset into Unity's project
        /// </summary>
        /// <param name="assetName">Name of the asset</param>
        /// <param name="modelPath">Path to generated FBX file</param>
        /// <param name="texturePaths">Paths to texture files</param>
        /// <param name="referenceImagePath">Path to reference image</param>
        /// <param name="iterationNumber">Iteration number</param>
        /// <returns>Asset entity</returns>
        public async Task<Asset> ImportGeneratedAssetAsync(
            AssetName assetName,
            string modelPath = null,
            string[] texturePaths = null,
            string referenceImagePath = null,
            int? iterationNumber = null,
            JobQualitySettings qualitySettings = null,
            string backendAssetTypeId = null)
        {
            try
            {
                _logger.Info($"Importing asset: {assetName}");

                // Create only folders needed for the imported payload.
                bool includeModelFolders = !string.IsNullOrEmpty(modelPath) ||
                                           (texturePaths != null && texturePaths.Length > 0);
                string assetFolder = CreateAssetFolderStructure(assetName.Value, includeModelFolders);

                // Import model (optional - may be null during approval phase)
                string importedModelPath = null;
                if (!string.IsNullOrEmpty(modelPath))
                {
                    importedModelPath = ImportModel(modelPath, assetFolder);
                }

                // Import textures
                string[] importedTexturePaths = null;
                if (texturePaths != null && texturePaths.Length > 0)
                {
                    importedTexturePaths = ImportTextures(texturePaths, assetFolder, iterationNumber);
                }

                // Import reference image
                string importedReferencePath = null;
                if (!string.IsNullOrEmpty(referenceImagePath))
                {
                    importedReferencePath = ImportReferenceImage(referenceImagePath, assetFolder, iterationNumber);
                    _logger.Info($"Reference image imported: {importedReferencePath}");
                }

                // Refresh asset database to ensure all files are imported
                AssetDatabase.Refresh();

                // Wait for import to complete
                await Task.Delay(AssetImportRefreshDelayMs);

                // Create Asset entity using factory method (only if we have a model)
                Asset asset = null;
                if (!string.IsNullOrEmpty(importedModelPath))
                {
                    var assetResult = Asset.Create(assetName, AssetType.Model);
                    if (!assetResult.IsSuccess)
                    {
                        throw new Exception($"Failed to create asset entity: {assetResult.Error}");
                    }

                    asset = assetResult.Value;
                    asset.SetFilePath(importedModelPath);
                    asset.SetBackendAssetTypeId(backendAssetTypeId);
                    asset.SetPostProcessSettings(qualitySettings);

                    // Save asset entity to repository
                    await GeniesArtForgeService.Assets.AddAsync(asset);
                }

                if (asset != null)
                {
                    _logger.Info($"Asset imported successfully: {assetName}");
                }
                else
                {
                    _logger.Info($"Reference image imported for approval: {assetName}");
                }

                return asset;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to import asset: {ex.Message}", ex);
                throw;
            }
        }

        #region Private Methods

        private string CreateAssetFolderStructure(string assetName, bool includeModelFolders)
        {
            // Setup folder structure: Assets/Genies/ArtForge/{AssetName}/Models, Textures, Source, etc.
            string projectPath = Path.Combine(UnityEngine.Application.dataPath, "..");

            Directory.CreateDirectory(Path.Combine(projectPath, BrandingConstants.AssetPaths.GetRefImagesFolder(assetName).TrimEnd('/')));
            Directory.CreateDirectory(Path.Combine(projectPath, BrandingConstants.AssetPaths.GetRefImagesHistoryFolder(assetName).TrimEnd('/')));

            if (includeModelFolders)
            {
                Directory.CreateDirectory(Path.Combine(projectPath, BrandingConstants.AssetPaths.GetModelsFolder(assetName).TrimEnd('/')));
                Directory.CreateDirectory(Path.Combine(projectPath, BrandingConstants.AssetPaths.GetTexturesFolder(assetName).TrimEnd('/')));
                Directory.CreateDirectory(Path.Combine(projectPath, BrandingConstants.AssetPaths.GetMaterialsFolder(assetName).TrimEnd('/')));
                Directory.CreateDirectory(Path.Combine(projectPath, BrandingConstants.AssetPaths.GetPrefabsFolder(assetName).TrimEnd('/')));
                Directory.CreateDirectory(Path.Combine(projectPath, BrandingConstants.AssetPaths.GetSourceFolder(assetName).TrimEnd('/')));
            }

            // Return the asset root folder (Unity relative)
            return BrandingConstants.AssetPaths.GetAssetRoot(assetName).TrimEnd('/');
        }

        private string ImportModel(string sourcePath, string assetFolder)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                throw new ArgumentException($"Model file not found: {sourcePath}");
            }

            // Extract asset name from folder path (e.g., "Assets/Genies/ArtForge/Models/MyAsset" -> "MyAsset")
            string assetName = Path.GetFileName(assetFolder.TrimEnd('/'));

            // Use asset name for the file instead of the original filename
            string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            string targetFileName = $"{assetName}{extension}";

            string targetPath;
            string fullTargetPath;

            // GLB files from backend go to Source folder, FBX files go to Models folder
            if (extension == ".glb")
            {
                // GLB files go to Source folder
                targetPath = BrandingConstants.AssetPaths.GetSourceGlbPath(assetName);
                fullTargetPath = Path.Combine(UnityEngine.Application.dataPath, "..", targetPath);

                // Ensure Source folder exists
                string sourceDir = Path.GetDirectoryName(fullTargetPath);
                Directory.CreateDirectory(sourceDir);

                _logger.Info($"Importing GLB model to Source folder: {targetPath}");
            }
            else
            {
                // FBX and other formats go to Models folder
                targetPath = Path.Combine(assetFolder, "Models", targetFileName);
                fullTargetPath = Path.Combine(UnityEngine.Application.dataPath, "..", targetPath);

                _logger.Info($"Importing model to Models folder: {targetPath}");
            }

            // Copy file
            File.Copy(sourcePath, fullTargetPath, true);

            _logger.Info($"Imported model: {targetPath}");

            return targetPath;
        }

        private string[] ImportTextures(string[] sourcePaths, string assetFolder, int? iterationNumber = null)
        {
            if (sourcePaths == null || sourcePaths.Length == 0)
            {
                return Array.Empty<string>();
            }

            string assetName = Path.GetFileName(assetFolder.TrimEnd('/'));
            var importedPaths = new string[sourcePaths.Length];

            // Create History folder if we're versioning textures
            string historyFolder = BrandingConstants.AssetPaths.GetRefImagesHistoryFolder(assetName);
            string fullHistoryFolder = Path.Combine(UnityEngine.Application.dataPath, "..", historyFolder);
            if (iterationNumber.HasValue && iterationNumber.Value >= 0)
            {
                Directory.CreateDirectory(fullHistoryFolder);
            }

            // Import each texture
            _logger.Info($"[ImportTextures] Starting import of {sourcePaths.Length} textures for asset '{assetName}' (iteration: {iterationNumber?.ToString() ?? "null"})");

            for (int i = 0; i < sourcePaths.Length; i++)
            {
                string sourcePath = sourcePaths[i];
                if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                {
                    _logger.Warning($"[ImportTextures] Texture #{i} file not found: {sourcePath}");
                    continue;
                }

                string fileName = Path.GetFileName(sourcePath);
                string targetPath = Path.Combine(assetFolder, "Textures", fileName);
                string fullTargetPath = Path.Combine(UnityEngine.Application.dataPath, "..", targetPath);

                _logger.Info($"[ImportTextures] Processing texture #{i}: '{fileName}'");
                _logger.Info($"[ImportTextures]   Source: {sourcePath}");
                _logger.Info($"[ImportTextures]   Target: {fullTargetPath}");
                _logger.Info($"[ImportTextures]   Target exists: {File.Exists(fullTargetPath)}");

                // If target texture exists and we have an iteration number, save it to history first
                if (File.Exists(fullTargetPath) && iterationNumber.HasValue && iterationNumber.Value >= 0)
                {
                    string baseName = Path.GetFileNameWithoutExtension(fileName);
                    string extension = Path.GetExtension(fileName);

                    _logger.Info($"[ImportTextures]   Original baseName: '{baseName}'");

                    // Remove version suffix if present (e.g., "MyAsset_Albedo_00" -> "MyAsset_Albedo")
                    if (baseName.Length >= IterationSuffixLength)
                    {
                        string last3 = baseName.Substring(baseName.Length - IterationSuffixLength);
                        if (last3.StartsWith("_") && char.IsDigit(last3[1]) && char.IsDigit(last3[2]))
                        {
                            string oldBaseName = baseName;
                            baseName = baseName.Substring(0, baseName.Length - IterationSuffixLength);
                            _logger.Info($"[ImportTextures]   Removed version suffix: '{oldBaseName}' -> '{baseName}'");
                        }
                    }

                    // Save to history with iteration number
                    string versionedFileName = $"{baseName}_{iterationNumber.Value:D2}{extension}";
                    string historyPath = Path.Combine(historyFolder, versionedFileName);
                    string fullHistoryPath = Path.Combine(fullHistoryFolder, versionedFileName);

                    _logger.Info($"[ImportTextures]   Saving to history: '{versionedFileName}'");
                    _logger.Info($"[ImportTextures]   History path: {fullHistoryPath}");

                    try
                    {
                        File.Copy(fullTargetPath, fullHistoryPath, true);
                        _logger.Info($"[ImportTextures]   ✓ Saved existing texture to History: {historyPath} (iteration {iterationNumber.Value:D2})");
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning($"[ImportTextures]   ✗ Failed to save texture to History: {ex.Message}");
                    }
                }
                else
                {
                    if (!iterationNumber.HasValue)
                    {
                        _logger.Info($"[ImportTextures]   No iteration number provided - skipping history");
                    }
                    else if (!File.Exists(fullTargetPath))
                    {
                        _logger.Info($"[ImportTextures]   Target doesn't exist - first import, no history needed");
                    }
                }

                // Copy new texture to target location
                File.Copy(sourcePath, fullTargetPath, true);
                importedPaths[i] = targetPath;
                _logger.Info($"[ImportTextures]   ✓ Copied new texture to: {targetPath}");
            }

            _logger.Info($"[ImportTextures] Completed import of {sourcePaths.Length} textures");

            return importedPaths;
        }

        private string ImportReferenceImage(string sourcePath, string assetFolder, int? iterationNumber = null)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                return null;
            }

            string assetName = Path.GetFileName(assetFolder);

            // Create RefImages and History directories
            string refImagesFolder = Path.Combine(assetFolder, "RefImages");
            string historyFolder = Path.Combine(refImagesFolder, "History");
            string fullRefImagesFolder = Path.Combine(UnityEngine.Application.dataPath, "..", refImagesFolder);
            string fullHistoryFolder = Path.Combine(UnityEngine.Application.dataPath, "..", historyFolder);

            Directory.CreateDirectory(fullRefImagesFolder);
            Directory.CreateDirectory(fullHistoryFolder);

            string historyPath = null;

            // Normalize paths for comparison
            string normalizedSourcePath = Path.GetFullPath(sourcePath);
            string currentRefPath = Path.Combine(refImagesFolder, $"{assetName}_ref.png");
            string fullCurrentRefPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", currentRefPath));

            // Check if source is already the current ref image (approved image case)
            bool isAlreadyCurrentRef = string.Equals(normalizedSourcePath, fullCurrentRefPath, StringComparison.OrdinalIgnoreCase);

            if (isAlreadyCurrentRef)
            {
                _logger.Info($"Reference image already in place, skipping copy: {currentRefPath}");
                return currentRefPath; // Already imported, just return the path
            }

            // If iteration number provided, save to History with versioned name
            if (iterationNumber.HasValue)
            {
                string versionedFileName = $"{assetName}_ref_{iterationNumber.Value:D2}.png";
                historyPath = Path.Combine(historyFolder, versionedFileName);
                string fullHistoryPath = Path.Combine(fullHistoryFolder, versionedFileName);
                File.Copy(sourcePath, fullHistoryPath, true);
                _logger.Info($"Saved to History: {historyPath}");
            }

            // Copy to current reference image location
            File.Copy(sourcePath, fullCurrentRefPath, true);
            _logger.Info($"Updated current reference image: {currentRefPath}");

            // Return the history path if versioned, otherwise current ref path
            return historyPath ?? currentRefPath;
        }

        #endregion
    }
}
