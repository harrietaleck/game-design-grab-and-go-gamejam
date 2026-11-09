using UnityEngine;
using UnityEditor;
using System.IO;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Import
{
    /// <summary>
    /// Asset postprocessor to automatically configure imported 3D models
    /// </summary>
    internal class ModelImportProcessor : AssetPostprocessor
    {
        private ILogger _logger;
        private const float DefaultScale = 1f;
        private const float WearableScale = 1f;
        private const float DecorScale = 1f;

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

        private void OnPreprocessModel()
        {
            try
            {
                // Only process FBX files in GeniesArtForge directories
                if (!IsGeniesArtForgeModel(assetPath))
                {
                    return;
                }

                ModelImporter modelImporter = assetImporter as ModelImporter;
                if (modelImporter == null)
                {
                    return;
                }

                // Fix rotation and scale
                var metadataSettings = AssetMetadataSettingsReader.Read(assetPath, Logger);
                float modelScale = ResolveModelScale(metadataSettings?.BackendAssetTypeId);
                modelImporter.useFileUnits = false;
                modelImporter.globalScale = modelScale;

                // Disable unnecessary features
                modelImporter.importBlendShapes = false;
                modelImporter.importVisibility = false;
                modelImporter.importCameras = false;
                modelImporter.importLights = false;
                modelImporter.importAnimation = false;
                modelImporter.importConstraints = false;

                // Mesh settings
                modelImporter.meshCompression = ModelImporterMeshCompression.Off;
                modelImporter.isReadable = true;
                modelImporter.optimizeMeshPolygons = true;
                modelImporter.optimizeMeshVertices = true;

                // Don't import materials (we'll create our own)
                modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;

                // Apply collider preference from metadata.
                modelImporter.addCollider = metadataSettings?.GenerateCollider ?? false;

                // Apply LOD generation when available (Unity 6.2+).
                #if UNITY_6000_2_OR_NEWER
                modelImporter.generateMeshLods = metadataSettings?.GenerateLODs ?? false;
                #endif

                Logger.Info($"[GeniesArtForge] Configured model import: {Path.GetFileName(assetPath)} | Scale={modelScale} | AssetType={metadataSettings?.BackendAssetTypeId ?? "unknown"}");
            }
            catch (System.Exception ex)
            {
                Logger.Error($"[GeniesArtForge] Error in OnPreprocessModel for {assetPath}: {ex.Message}");
            }
        }

        private static float ResolveModelScale(string backendAssetTypeId)
        {
            if (string.IsNullOrWhiteSpace(backendAssetTypeId))
            {
                return DefaultScale;
            }

            // Decor/prop assets are authored in world-scale units, while wearables are authored smaller.
            // Keep wearable behavior unchanged and normalize decor/prop outputs down to 1x.
            return Job.IsDecorAssetTypeId(backendAssetTypeId) ? DecorScale : WearableScale;
        }

        private void OnPostprocessModel(GameObject gameObject)
        {
            try
            {
                // Only process GeniesArtForge models
                if (!IsGeniesArtForgeModel(assetPath))
                {
                    return;
                }

                if (gameObject == null)
                {
                    return;
                }

                // Ensure zero rotation
                gameObject.transform.rotation = Quaternion.identity;
                gameObject.transform.localRotation = Quaternion.identity;

                // Reset child rotations
                Transform[] allTransforms = gameObject.GetComponentsInChildren<Transform>();
                if (allTransforms != null)
                {
                    foreach (Transform t in allTransforms)
                    {
                        if (t != null)
                        {
                            t.localRotation = Quaternion.identity;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.Error($"[GeniesArtForge] Error in OnPostprocessModel for {assetPath}: {ex.Message}");
            }
        }

        private bool IsGeniesArtForgeModel(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            string normalizedPath = path.Replace('\\', '/');
            string projectRoot = EditorPrefs.GetString("GAF_ProjectRoot", "Assets/Genies/ArtForge");

            return normalizedPath.StartsWith(projectRoot) &&
                   normalizedPath.Contains("/Models/") &&
                   normalizedPath.EndsWith(".fbx");
        }
    }
}
