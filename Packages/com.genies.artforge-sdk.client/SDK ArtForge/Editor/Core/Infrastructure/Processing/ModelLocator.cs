using UnityEngine;
using UnityEditor;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Processing
{
    /// <summary>
    /// Responsible for locating model files (GLB/FBX) in the project.
    /// Handles priority and fallback logic.
    /// </summary>
    internal class ModelLocator
    {
        private readonly ILogger _logger;

        public ModelLocator(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Result of model location.
        /// </summary>
        public class ModelLocation
        {
            public GameObject ModelAsset { get; set; }
            public string AssetPath { get; set; }
            public ModelFormatOptions Options { get; set; }
            public bool Found => ModelAsset != null;
        }

        /// <summary>
        /// Locates a model file for the given asset name.
        /// Priority: Source GLB (backend) > Models FBX (local) > Models GLB (fallback).
        /// </summary>
        public ModelLocation LocateModel(string assetName, bool pivotAtBase = true, bool generateCollider = false)
        {
            // Priority 1: Source GLB (backend models)
            string sourceGlbPath = BrandingConstants.AssetPaths.GetSourceGlbPath(assetName);
            if (AssetPathHelpers.FileExists(sourceGlbPath))
            {
                _logger.Info($"[ModelLocator] Found GLB in Source folder: {sourceGlbPath}");
                
                // Ensure import is complete
                AssetDatabase.ImportAsset(sourceGlbPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                
                GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(sourceGlbPath);
                if (modelAsset != null)
                {
                    return new ModelLocation
                    {
                        ModelAsset = modelAsset,
                        AssetPath = sourceGlbPath,
                        Options = ModelFormatOptions.ForGLB(generateCollider)
                    };
                }
            }

            // Priority 2: Models FBX (local generation)
            string fbxPath = BrandingConstants.AssetPaths.GetFbxPath(assetName);
            if (AssetPathHelpers.FileExists(fbxPath))
            {
                _logger.Info($"[ModelLocator] Found FBX in Models folder: {fbxPath}");
                
                GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (modelAsset != null)
                {
                    return new ModelLocation
                    {
                        ModelAsset = modelAsset,
                        AssetPath = fbxPath,
                        Options = ModelFormatOptions.ForFBX(pivotAtBase, generateCollider)
                    };
                }
            }

            // Priority 3: Models GLB (fallback)
            string glbPath = BrandingConstants.AssetPaths.GetGlbPath(assetName);
            if (AssetPathHelpers.FileExists(glbPath))
            {
                _logger.Info($"[ModelLocator] Found GLB in Models folder (fallback): {glbPath}");
                
                GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
                if (modelAsset != null)
                {
                    return new ModelLocation
                    {
                        ModelAsset = modelAsset,
                        AssetPath = glbPath,
                        Options = ModelFormatOptions.ForGLB(generateCollider)
                    };
                }
            }

            // Not found
            _logger.Error($"[ModelLocator] No model file found for asset: {assetName}");
            _logger.Error($"[ModelLocator] Tried: {sourceGlbPath}, {fbxPath}, {glbPath}");
            
            return new ModelLocation
            {
                ModelAsset = null,
                AssetPath = null,
                Options = null
            };
        }
    }
}
