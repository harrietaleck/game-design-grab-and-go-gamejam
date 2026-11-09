using System.IO;
using UnityEngine;
using UnityEditor;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Processing
{
    /// <summary>
    /// Orchestrates Unity asset processing: materials, textures, and prefabs.
    /// </summary>
    internal class UnityAssetProcessor
    {
        private const float DefaultGlobalScale = 1.0f;

        private readonly ILogger _logger;
        private readonly ModelLocator _modelLocator;
        private readonly MaterialBuilder _materialBuilder;
        private readonly PrefabBuilder _prefabBuilder;

        public UnityAssetProcessor(ILogger logger, bool verboseLogging = false)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            _modelLocator = new ModelLocator(logger);
            _materialBuilder = new MaterialBuilder(logger, verboseLogging);
            _prefabBuilder = new PrefabBuilder(logger);
        }

        /// <summary>
        /// Creates material and prefab for a generated asset.
        /// Single entry point for asset processing.
        /// </summary>
        /// <param name="assetName">Name of the asset</param>
        /// <param name="pivotAtBase">If true, places pivot at the bottom of the model. If false, centers it.</param>
        /// <param name="generateCollider">If true, adds mesh colliders to generated prefab meshes.</param>
        public void CreateMaterialAndPrefab(string assetName, bool pivotAtBase = true, bool generateCollider = false)
        {
            try
            {
                _logger.Info($"[UnityAssetProcessor] Processing asset: {assetName}");

                // Define paths
                string materialPath = BrandingConstants.AssetPaths.GetMaterialPath(assetName);
                string prefabPath = BrandingConstants.AssetPaths.GetPrefabPath(assetName);
                string texturesDir = BrandingConstants.AssetPaths.GetTexturesFolder(assetName);

                // Ensure directories exist
                AssetPathHelpers.EnsureParentDir(materialPath);
                AssetPathHelpers.EnsureParentDir(prefabPath);

                // Step 1: Locate model
                var modelLocation = _modelLocator.LocateModel(assetName, pivotAtBase, generateCollider);
                if (!modelLocation.Found)
                {
                    _logger.Error($"[UnityAssetProcessor] Failed to locate model for asset: {assetName}");
                    return;
                }

                // Step 1.5: Convert GLB to FBX if source is GLB
                if (modelLocation.AssetPath.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Info($"[UnityAssetProcessor] Converting GLB to FBX for better Unity compatibility...");

                    // IMPORTANT: Extract textures from GLB BEFORE converting to FBX
                    // FBX export doesn't preserve texture slots properly
                    _logger.Info($"[UnityAssetProcessor] Extracting textures from GLB before conversion...");
                    ExtractTexturesFromGlbFile(modelLocation.AssetPath);

                    // Force AssetDatabase refresh to ensure extracted textures are available
                    AssetDatabase.Refresh();
                    _logger.Info($"[UnityAssetProcessor] Textures extracted and refreshed, proceeding with FBX conversion...");

                    string fbxPath = ConvertGlbToFbx(assetName, modelLocation.AssetPath, pivotAtBase);

                    if (!string.IsNullOrEmpty(fbxPath))
                    {
                        // Update model location to use the new FBX for geometry
                        GameObject fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                        if (fbxModel != null)
                        {
                            modelLocation.ModelAsset = fbxModel;
                            modelLocation.AssetPath = fbxPath;
                            modelLocation.Options = ModelFormatOptions.ForFBX(pivotAtBase, generateCollider);
                            _logger.Info($"[UnityAssetProcessor] ✓ Converted to FBX: {fbxPath}");
                        }
                    }
                }

                // Step 2: Build material (using FBX for geometry, but textures already extracted from GLB)
                var materialResult = _materialBuilder.BuildMaterial(
                    modelLocation.ModelAsset,
                    assetName,
                    modelLocation.Options,
                    materialPath,
                    texturesDir
                );

                if (materialResult == null || materialResult.Material == null)
                {
                    _logger.Error($"[UnityAssetProcessor] Failed to build material for asset: {assetName}");
                    return;
                }

                // Step 3: Create prefab
                _prefabBuilder.CreatePrefab(
                    modelLocation.ModelAsset,
                    materialResult.Material,
                    prefabPath,
                    modelLocation.Options
                );

                // Step 4: Save and refresh (once at the end)
                AssetDatabase.SaveAssets();
                // NOTE: AssetDatabase.Refresh() is NOT called here to avoid infinite import loops
                // The caller (QueueService) is responsible for calling Refresh() once at the end

                _logger.Info($"[UnityAssetProcessor] ✓ Processed asset: {assetName}");
                _logger.Info($"[UnityAssetProcessor]   Material: {materialPath}");
                _logger.Info($"[UnityAssetProcessor]   Prefab: {prefabPath}");
                if (materialResult.TexturesExtracted > 0)
                {
                    _logger.Info($"[UnityAssetProcessor]   Extracted {materialResult.TexturesExtracted} texture(s)");
                }
            }
            catch (System.Exception e)
            {
                _logger.Error($"[UnityAssetProcessor] Error processing asset '{assetName}': {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// Public static method to convert GLB to FBX (for context menu use).
        /// </summary>
        public static string ConvertGlbToFbxFile(string glbAssetPath, bool pivotAtBase = true)
        {
            var logger = new Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging.UnityLogger();
            var processor = new UnityAssetProcessor(logger);

            // Extract asset name from path
            string fileName = Path.GetFileNameWithoutExtension(glbAssetPath);

            logger.Info($"[UnityAssetProcessor] Converting GLB to FBX: {glbAssetPath}");
            string fbxPath = processor.ConvertGlbToFbx(fileName, glbAssetPath, pivotAtBase);

            if (!string.IsNullOrEmpty(fbxPath))
            {
                logger.Info($"[UnityAssetProcessor] ✓ FBX created: {fbxPath}");
            }
            else
            {
                logger.Error($"[UnityAssetProcessor] Failed to convert GLB to FBX");
            }

            return fbxPath;
        }

        /// <summary>
        /// Public static method to extract textures from a GLB file (for context menu use).
        /// </summary>
        public static void ExtractTexturesFromGlbFile(string glbAssetPath)
        {
            try
            {
                var logger = new UnityLogger();

                // Load the GLB GameObject
                GameObject glbModel = AssetPathHelpers.LoadOrImport<GameObject>(glbAssetPath, forceImport: true);
                if (glbModel == null)
                {
                    logger.Error($"[UnityAssetProcessor] Failed to load GLB file: {glbAssetPath}");
                    return;
                }

                // Extract asset name from path
                string assetName = System.IO.Path.GetFileNameWithoutExtension(glbAssetPath);

                // Try to determine asset name from folder structure
                string[] pathParts = glbAssetPath.Split('/');
                for (int i = 0; i < pathParts.Length; i++)
                {
                    if (pathParts[i] == "Models" && i + 1 < pathParts.Length)
                    {
                        assetName = pathParts[i + 1];
                        break;
                    }
                }

                // Get textures directory
                string texturesDir = BrandingConstants.AssetPaths.GetTexturesFolder(assetName);

                // Create extractor
                TextureExtractor extractor = new TextureExtractor(logger, verboseLogging: false);

                // Find renderers
                Renderer[] renderers = glbModel.GetComponentsInChildren<Renderer>();
                if (renderers == null || renderers.Length == 0)
                {
                    logger.Warning($"[UnityAssetProcessor] No renderers found in GLB model: {glbAssetPath}");
                    return;
                }

                int totalExtracted = 0;

                // Extract textures from all materials
                foreach (Renderer renderer in renderers)
                {
                    Material[] materials = renderer.sharedMaterials;
                    if (materials != null && materials.Length > 0)
                    {
                        foreach (Material material in materials)
                        {
                            if (material != null)
                            {
                                logger.Info($"[UnityAssetProcessor] Extracting textures from material: {material.name}");
                                var result = extractor.ExtractTexturesFromMaterial(material, assetName, texturesDir);
                                totalExtracted += result.ExtractedCount;
                            }
                        }
                    }
                }

                // Refresh to import extracted textures
                AssetDatabase.Refresh();

                logger.Info($"[UnityAssetProcessor] ✓ Extracted {totalExtracted} texture(s) from GLB: {glbAssetPath}");
            }
            catch (System.Exception ex)
            {
                var logger = new UnityLogger();
                logger.Error($"[UnityAssetProcessor] Failed to extract textures from GLB file {glbAssetPath}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Converts a GLB file to FBX format for better Unity compatibility.
        /// Creates the FBX in the Models folder.
        /// </summary>
        /// <param name="assetName">Name of the asset</param>
        /// <param name="glbPath">Path to the source GLB file</param>
        /// <param name="pivotAtBase">Whether to place pivot at base</param>
        /// <returns>Path to the converted FBX file, or null if conversion failed</returns>
        public string ConvertGlbToFbx(string assetName, string glbPath, bool pivotAtBase)
        {
            try
            {
                // Define output FBX path in Models folder
                string fbxPath = BrandingConstants.AssetPaths.GetFbxPath(assetName);
                string fullFbxPath = Path.Combine(UnityEngine.Application.dataPath, "..", fbxPath);

                // Ensure Models directory exists
                string modelsDir = Path.GetDirectoryName(fullFbxPath);
                Directory.CreateDirectory(modelsDir);

                // Load the GLB model
                AssetDatabase.ImportAsset(glbPath, ImportAssetOptions.ForceUpdate);
                GameObject glbModel = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
                if (glbModel == null)
                {
                    var allAssets = AssetDatabase.LoadAllAssetsAtPath(glbPath);
                    foreach (var asset in allAssets)
                    {
                        glbModel = asset as GameObject;
                        if (glbModel != null)
                        {
                            break;
                        }
                    }
                }

                if (glbModel == null)
                {
                    _logger.Error($"[UnityAssetProcessor] Failed to load GLB model: {glbPath}");
                    return null;
                }

                // Instantiate the model in the scene
                GameObject instance = PrefabUtility.InstantiatePrefab(glbModel) as GameObject;
                if (instance == null)
                {
                    instance = Object.Instantiate(glbModel);
                }

                instance.name = assetName;

                // Reset rotation to ensure FBX doesn't inherit GLB rotation
                instance.transform.rotation = Quaternion.identity;
                instance.transform.position = Vector3.zero;
                instance.transform.localScale = Vector3.one;

                // Apply pivot adjustment if needed
                if (pivotAtBase)
                {
                    // Calculate bounds and adjust pivot to base
                    Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        Bounds combinedBounds = renderers[0].bounds;
                        foreach (var renderer in renderers)
                        {
                            combinedBounds.Encapsulate(renderer.bounds);
                        }

                        // Move children down so pivot is at the base
                        Vector3 offset = new Vector3(0, -combinedBounds.min.y, 0);
                        foreach (Transform child in instance.transform)
                        {
                            child.position += offset;
                        }
                    }
                }

                try
                {
                    // Use Unity's FBX Exporter (com.unity.formats.fbx)
                    UnityEditor.Formats.Fbx.Exporter.ModelExporter.ExportObject(fullFbxPath, instance);
                }
                finally
                {
                    // Clean up instantiated object
                    Object.DestroyImmediate(instance);
                }

                // Refresh and reimport the FBX
                AssetDatabase.Refresh();
                AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

                // Configure FBX import settings
                ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer != null)
                {
                    importer.importAnimation = false;
                    importer.importBlendShapes = false;
                    importer.importCameras = false;
                    importer.importLights = false;
                    importer.materialImportMode = ModelImporterMaterialImportMode.None; // We'll create our own materials

                    // Fix rotation issue - bake axis conversion into mesh
                    importer.bakeAxisConversion = true;

                    // Ensure proper scale
                    importer.globalScale = DefaultGlobalScale;
                    importer.useFileScale = false;

                    // Save and force synchronous reimport
                    AssetDatabase.StartAssetEditing();
                    try
                    {
                        importer.SaveAndReimport();
                    }
                    finally
                    {
                        AssetDatabase.StopAssetEditing();
                    }

                    // Force another import to make sure settings are applied
                    AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                }

                _logger.Info($"[UnityAssetProcessor] ✓ GLB converted to FBX: {fbxPath}");
                return fbxPath;
            }
            catch (System.Exception ex)
            {
                _logger.Error($"[UnityAssetProcessor] Failed to convert GLB to FBX: {ex.Message}");
                return null;
            }
        }
    }
}
