using System.Collections.Generic;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Helpers;
using UnityEditor;

namespace Genies.Sdk.ArtForge.Editor.Components.ModelGenerator
{
    /// <summary>
    /// Plain C# model representing all inputs and selections in the Model Generator.
    /// Single source of truth for the current state.
    /// Automatically persists name and description to EditorPrefs.
    /// </summary>
    internal sealed class ModelGeneratorState
    {
        private const string PrefKeyAssetName = "GAF_ModelGenerator_AssetName";
        private const string PrefKeyDescription = "GAF_ModelGenerator_Description";
        private const string PrefKeyBackendCategory = "GAF_ModelGenerator_BackendCategory";
        private const string PrefKeyBackendSpecificType = "GAF_ModelGenerator_BackendSpecificType";
        private const string PrefKeyBackendSubcategory = "GAF_ModelGenerator_BackendSubcategory";

        private string _assetName = "";
        private string _description = "";
        private string _backendCategory = AssetCategories.Wearable;
        private string _backendSpecificType = "";
        private string _backendSubcategory = "";

        // Basic Information (with persistence)
        public string AssetName
        {
            get => _assetName;
            set
            {
                if (_assetName != value)
                {
                    _assetName = value;
                    EditorPrefs.SetString(PrefKeyAssetName, value);
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    EditorPrefs.SetString(PrefKeyDescription, value);
                }
            }
        }

        // Backend Mode Selection
        public string BackendCategory
        {
            get => _backendCategory;
            set
            {
                string normalized = NormalizeBackendCategory(value);
                if (_backendCategory != normalized)
                {
                    _backendCategory = normalized;
                    EditorPrefs.SetString(PrefKeyBackendCategory, normalized);
                }
            }
        }

        public string BackendSpecificType
        {
            get => _backendSpecificType;
            set
            {
                string normalized = value ?? "";
                if (_backendSpecificType != normalized)
                {
                    _backendSpecificType = normalized;
                    EditorPrefs.SetString(PrefKeyBackendSpecificType, normalized);
                }
            }
        }

        public string BackendSubcategory
        {
            get => _backendSubcategory;
            set
            {
                string normalized = value ?? "";
                if (_backendSubcategory != normalized)
                {
                    _backendSubcategory = normalized;
                    EditorPrefs.SetString(PrefKeyBackendSubcategory, normalized);
                }
            }
        }

        // Reference Images
        public List<string> ReferenceImages { get; set; } = new List<string>();

        // Quality Settings
        public JobQualitySettings Quality { get; set; } = new JobQualitySettings
        {
            Pivot = "Base",
            TopologyType = "Triangles",
            Polycount = QualityDefaults.Polycount,
            TextureResolution = "1024",
            AspectRatio = "1:1",
            GenerateLODs = false,
            GenerateCollider = false,
            GenerateAO = true,
            GenerateTransparent = false,
            GenerateNormalMap = true,
            GenerateMetallicSmoothnessMap = true
        };

        /// <summary>
        /// Constructor - loads persisted name and description from EditorPrefs
        /// </summary>
        public ModelGeneratorState()
        {
            // Load persisted values (without triggering setters to avoid redundant saves)
            _assetName = EditorPrefs.GetString(PrefKeyAssetName, "");
            _description = EditorPrefs.GetString(PrefKeyDescription, "");
            _backendCategory = NormalizeBackendCategory(
                EditorPrefs.GetString(PrefKeyBackendCategory, AssetCategories.Wearable));
            _backendSpecificType = EditorPrefs.GetString(PrefKeyBackendSpecificType, "");
            _backendSubcategory = EditorPrefs.GetString(PrefKeyBackendSubcategory, "");
        }

        /// <summary>
        /// Clone the current state (useful for undo/redo or comparison)
        /// </summary>
        public ModelGeneratorState Clone()
        {
            return new ModelGeneratorState
            {
                AssetName = AssetName,
                Description = Description,
                BackendCategory = BackendCategory,
                BackendSpecificType = BackendSpecificType,
                BackendSubcategory = BackendSubcategory,
                ReferenceImages = new List<string>(ReferenceImages),
                Quality = new JobQualitySettings
                {
                    Pivot = Quality.Pivot,
                    TopologyType = Quality.TopologyType,
                    Polycount = Quality.Polycount,
                    TextureResolution = Quality.TextureResolution,
                    AspectRatio = Quality.AspectRatio,
                    GenerateLODs = Quality.GenerateLODs,
                    GenerateCollider = Quality.GenerateCollider,
                    GenerateAO = Quality.GenerateAO,
                    GenerateTransparent = Quality.GenerateTransparent,
                    GenerateNormalMap = Quality.GenerateNormalMap,
                    GenerateMetallicSmoothnessMap = Quality.GenerateMetallicSmoothnessMap
                }
            };
        }

        /// <summary>
        /// Reset to default values
        /// </summary>
        public void Reset()
        {
            AssetName = "";
            Description = "";
            BackendCategory = AssetCategories.Wearable;
            BackendSpecificType = "";
            BackendSubcategory = "";
            ReferenceImages.Clear();
            Quality = new JobQualitySettings
            {
                Pivot = "Base",
                TopologyType = "Triangles",
                Polycount = QualityDefaults.Polycount,
                TextureResolution = "1024",
                AspectRatio = "1:1",
                GenerateLODs = false,
                GenerateCollider = false,
                GenerateAO = true,
                GenerateTransparent = false,
                GenerateNormalMap = true,
                GenerateMetallicSmoothnessMap = true
            };
        }

        private static string NormalizeBackendCategory(string category)
        {
            string[] availableCategories = AssetCategories.GetAvailableForUI();
            if (availableCategories == null || availableCategories.Length == 0)
            {
                return AssetCategories.Wearable;
            }

            foreach (string availableCategory in availableCategories)
            {
                if (availableCategory == category)
                {
                    return availableCategory;
                }
            }

            return availableCategories[0];
        }
    }
}
