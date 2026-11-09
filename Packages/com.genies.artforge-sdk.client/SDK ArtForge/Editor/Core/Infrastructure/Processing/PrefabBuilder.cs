using System.IO;
using UnityEngine;
using UnityEditor;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using Object = UnityEngine.Object;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Processing
{
    /// <summary>
    /// Responsible for creating Unity prefabs from model assets.
    /// Handles mesh extraction for GLB models and applies format-specific transformations.
    /// </summary>
    internal class PrefabBuilder
    {
        private readonly ILogger _logger;

        public PrefabBuilder(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Creates a prefab from a model asset.
        /// </summary>
        public void CreatePrefab(GameObject modelAsset, Material material, string prefabPath, ModelFormatOptions options)
        {
            if (modelAsset == null)
            {
                _logger.Error("[PrefabBuilder] Model asset is null");
                return;
            }

            if (material == null)
            {
                _logger.Error("[PrefabBuilder] Material is null");
                return;
            }

            AssetPathHelpers.EnsureParentDir(prefabPath);

            GameObject processedModel = options.ExtractMeshAssets
                ? ExtractAndBuildModel(modelAsset, options)
                : modelAsset;

            if (processedModel == null)
            {
                _logger.Error("[PrefabBuilder] Failed to process model");
                return;
            }

            BuildPrefabHierarchy(processedModel, material, prefabPath, options);

            _logger.Info($"[PrefabBuilder] ✓ Created prefab: {prefabPath}");
        }

        /// <summary>
        /// Extracts meshes from GLB and creates a new GameObject hierarchy.
        /// </summary>
        private GameObject ExtractAndBuildModel(GameObject glbGameObject, ModelFormatOptions options)
        {
            _logger.Info($"[PrefabBuilder] Extracting meshes from GLB: {glbGameObject.name}");

            MeshFilter[] meshFilters = glbGameObject.GetComponentsInChildren<MeshFilter>();
            if (meshFilters == null || meshFilters.Length == 0)
            {
                _logger.Warning("[PrefabBuilder] No meshes found in GLB");
                return null;
            }

            GameObject extractedModel = new GameObject(glbGameObject.name);

            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter.sharedMesh == null)
                {
                    continue;
                }

                ProcessMesh(meshFilter, glbGameObject, extractedModel, i, meshFilters.Length);
            }

            _logger.Info($"[PrefabBuilder] Extracted {meshFilters.Length} mesh(es)");
            return extractedModel;
        }

        /// <summary>
        /// Processes a single mesh from GLB: extracts, saves as asset, and adds to model.
        /// </summary>
        private void ProcessMesh(MeshFilter sourceMeshFilter, GameObject glbRoot, GameObject targetModel, int index, int totalMeshes)
        {
            Mesh sourceMesh = sourceMeshFilter.sharedMesh;

            // Extract asset name from GLB root
            string assetName = glbRoot.name;

            // Create mesh copy
            Mesh extractedMesh = Object.Instantiate(sourceMesh);
            extractedMesh.name = totalMeshes == 1 ? assetName : $"{assetName}_{index}";

            // Save mesh asset
            string meshPath = BrandingConstants.AssetPaths.GetModelsFolder(assetName) + extractedMesh.name + ".asset";
            AssetDatabase.CreateAsset(extractedMesh, meshPath);

            // Create GameObject for this mesh
            string meshObjectName = sourceMeshFilter.gameObject.name;
            if (string.IsNullOrEmpty(meshObjectName) || meshObjectName == glbRoot.name)
            {
                meshObjectName = extractedMesh.name;
            }

            GameObject meshObject = new GameObject(meshObjectName);
            meshObject.transform.SetParent(targetModel.transform);

            // Preserve transform
            Transform glbRootTransform = glbRoot.transform;
            Transform sourceMeshTransform = sourceMeshFilter.transform;

            if (sourceMeshTransform != glbRootTransform)
            {
                meshObject.transform.localPosition = sourceMeshTransform.localPosition;
                meshObject.transform.localRotation = sourceMeshTransform.localRotation;
                meshObject.transform.localScale = sourceMeshTransform.localScale;
            }
            else
            {
                meshObject.transform.localPosition = Vector3.zero;
                meshObject.transform.localRotation = Quaternion.identity;
                meshObject.transform.localScale = Vector3.one;
            }

            // Add components
            MeshFilter newMeshFilter = meshObject.AddComponent<MeshFilter>();
            newMeshFilter.sharedMesh = extractedMesh;

            MeshRenderer newMeshRenderer = meshObject.AddComponent<MeshRenderer>();

            // Copy renderer properties
            MeshRenderer sourceRenderer = sourceMeshFilter.GetComponent<MeshRenderer>();
            if (sourceRenderer != null)
            {
                newMeshRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                newMeshRenderer.receiveShadows = sourceRenderer.receiveShadows;
                newMeshRenderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
                newMeshRenderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
            }
        }

        /// <summary>
        /// Builds the final prefab hierarchy with material assignment and transformations.
        /// </summary>
        private void BuildPrefabHierarchy(GameObject modelAsset, Material material, string prefabPath, ModelFormatOptions options)
        {
            GameObject prefabInstance = InstantiateModel(modelAsset, options);
            if (prefabInstance == null)
            {
                _logger.Error("[PrefabBuilder] Failed to instantiate model");
                return;
            }

            // Create root container
            GameObject prefabRoot = new GameObject(prefabInstance.name);
            prefabInstance.transform.SetParent(prefabRoot.transform);

            // Apply transformations
            ApplyTransformations(prefabInstance, options);

            // Apply materials
            ApplyMaterial(prefabInstance, material);

            // Apply collider settings from quality configuration.
            if (options.AddMeshColliders)
            {
                EnsureMeshColliders(prefabInstance);
            }

            // Save prefab
            PrefabUtility.SaveAsPrefabAssetAndConnect(prefabRoot, prefabPath, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(prefabRoot);

            // Cleanup temporary extracted GameObject
            if (options.ExtractMeshAssets && modelAsset != null)
            {
                Object.DestroyImmediate(modelAsset);
                _logger.Info("[PrefabBuilder] Cleaned up temporary extracted GameObject");
            }
        }

        /// <summary>
        /// Instantiates the model asset appropriately based on format.
        /// </summary>
        private GameObject InstantiateModel(GameObject modelAsset, ModelFormatOptions options)
        {
            if (options.ExtractMeshAssets)
            {
                // GLB extracted model: use regular Instantiate
                return Object.Instantiate(modelAsset);
            }
            else
            {
                // FBX prefab asset: use prefab instantiation
                return PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            }
        }

        /// <summary>
        /// Applies format-specific transformations (rotation, pivot adjustment).
        /// </summary>
        private void ApplyTransformations(GameObject model, ModelFormatOptions options)
        {
            // Always reset rotation to identity for FBX models (rotation should be baked into mesh)
            model.transform.localRotation = Quaternion.identity;

            // Keep scale at 1 (scaling handled by ModelImportProcessor)
            model.transform.localScale = Vector3.one;

            // Apply pivot adjustment (FBX models)
            if (options.AdjustPivotToBase)
            {
                Vector3 pivotOffset = CalculateBasePivotOffset(model);
                model.transform.localPosition = pivotOffset;
            }
            else
            {
                model.transform.localPosition = Vector3.zero;
            }
        }

        /// <summary>
        /// Calculates the offset needed to place the model's pivot at its base.
        /// </summary>
        private Vector3 CalculateBasePivotOffset(GameObject model)
        {
            try
            {
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
                if (renderers == null || renderers.Length == 0)
                {
                    return Vector3.zero;
                }

                // Force Unity to update bounds
                foreach (Renderer r in renderers)
                {
                    r.enabled = false;
                    r.enabled = true;
                }

                // Calculate combined bounds
                Bounds totalBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    totalBounds.Encapsulate(renderers[i].bounds);
                }

                // Calculate Y offset to move bottom to origin
                float yOffset = -totalBounds.min.y;
                return new Vector3(0, yOffset, 0);
            }
            catch (System.Exception e)
            {
                _logger.Warning($"[PrefabBuilder] Failed to calculate base pivot offset: {e.Message}");
                return Vector3.zero;
            }
        }

        /// <summary>
        /// Applies material to all renderers in the model.
        /// </summary>
        private void ApplyMaterial(GameObject model, Material material)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
            }
        }

        /// <summary>
        /// Ensures each mesh object has a MeshCollider when collider generation is enabled.
        /// </summary>
        private void EnsureMeshColliders(GameObject model)
        {
            MeshFilter[] meshFilters = model.GetComponentsInChildren<MeshFilter>();
            foreach (MeshFilter meshFilter in meshFilters)
            {
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                if (meshFilter.GetComponent<MeshCollider>() != null)
                {
                    continue;
                }

                var collider = meshFilter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = meshFilter.sharedMesh;
            }

            SkinnedMeshRenderer[] skinnedRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer skinnedRenderer in skinnedRenderers)
            {
                if (skinnedRenderer == null || skinnedRenderer.sharedMesh == null)
                {
                    continue;
                }

                if (skinnedRenderer.GetComponent<MeshCollider>() != null)
                {
                    continue;
                }

                var collider = skinnedRenderer.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = skinnedRenderer.sharedMesh;
            }
        }
    }
}
