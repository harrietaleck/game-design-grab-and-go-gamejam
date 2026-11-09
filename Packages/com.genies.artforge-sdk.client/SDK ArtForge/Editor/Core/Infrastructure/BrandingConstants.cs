using System;
using System.IO;
using UnityEngine;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using Newtonsoft.Json.Linq;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure
{
    /// <summary>
    /// Centralized branding constants for GeniesArtForge.
    /// </summary>
    internal static class BrandingConstants
    {
        private static readonly ILogger Logger = new UnityLogger();
        /// <summary>
        /// Full product name displayed in UI
        /// </summary>
        public const string ProductName = "GeniesArtForge";

        /// <summary>
        /// Short product name displayed in UI
        /// </summary>
        public const string ShortProductName = "ArtForge";

        /// <summary>
        /// Short acronym used in logs and references
        /// </summary>
        public const string Acronym = "GAF";

        /// <summary>
        /// Unity menu path for opening the Hub window
        /// </summary>
        public const string MenuPath = "Tools/Genies/Art Forge Hub";

        /// <summary>
        /// Standardized log prefix for console messages
        /// </summary>
        public static string LogPrefix => $"[{Acronym}]";

#if GENIES_SDK_INTERNAL
        /// <summary>
        /// Package identifier without version (e.g., "com.genies.sdk.artforge")
        /// </summary>
        public const string PackageId = "com.genies.sdk.artforge";
#else
        /// <summary>
        /// Package identifier without version (e.g., "com.genies.artforge-sdk.client")
        /// </summary>
        public const string PackageId = "com.genies.artforge-sdk.client";
#endif

        private static string _cachedPackageVersion;

        /// <summary>
        /// Package version string read from package.json (e.g., "1.0.0")
        /// </summary>
        public static string PackageVersion
        {
            get
            {
                if (_cachedPackageVersion == null)
                {
                    _cachedPackageVersion = ReadPackageVersion();
                }
                return _cachedPackageVersion;
            }
        }

        /// <summary>
        /// Full package identifier with version (e.g., "com.genies.sdk.artforge@1.0.0")
        /// </summary>
        public static string PackageFullId => $"{PackageId}@{PackageVersion}";

        /// <summary>
        /// Gets the package path dynamically from Unity's PackageManager.
        /// Uses PackageInfo.FindForAssetPath to discover the actual package location.
        /// </summary>
        /// <returns>Package path (e.g., "Packages/com.genies.sdk.artforge@1.0.0")</returns>
        public static string GetPackagePath()
        {
#if UNITY_EDITOR
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath($"Packages/{PackageId}");
            if (packageInfo != null)
            {
                return packageInfo.assetPath;
            }
#endif
            // Fallback to versioned path
            return $"Packages/{PackageFullId}";
        }

        /// <summary>
        /// Reads the package version from package.json file
        /// </summary>
        /// <returns>Version string from package.json, or "1.0.0" as fallback</returns>
        private static string ReadPackageVersion()
        {
#if UNITY_EDITOR
            try
            {
                // First try to get version from PackageInfo (most reliable)
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath($"Packages/{PackageId}");
                if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.version))
                {
                    return packageInfo.version;
                }

                // Fallback: Read from package.json file directly
                string packagePath = GetPackagePath();
                string packageJsonPath = Path.Combine(UnityEngine.Application.dataPath.Replace("/Assets", ""), packagePath, "package.json");

                if (File.Exists(packageJsonPath))
                {
                    string jsonContent = File.ReadAllText(packageJsonPath);
                    JObject packageJson = JObject.Parse(jsonContent);
                    JToken versionToken = packageJson["version"];
                    if (versionToken != null)
                    {
                        return versionToken.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[BrandingConstants] Failed to read package version: {ex.Message}");
            }
#endif
            // Last resort fallback
            return "1.0.0";
        }

        /// <summary>
        /// Window title with version info
        /// </summary>
        /// <param name="version">Optional version string (e.g., "1.0.0")</param>
        /// <returns>Formatted window title</returns>
        public static string GetWindowTitle(string version = null)
        {
            return string.IsNullOrEmpty(version)
                ? ProductName
                : $"{ProductName} v{version}";
        }

        /// <summary>
        /// Asset folder paths for generated content
        /// Structure: Assets/GeniesArtForge/{AssetType}/{AssetName}/Models, Textures, Materials, Prefabs, RefImages
        /// </summary>
        public static class AssetPaths
        {
            /// <summary>
            /// Default asset type for 3D model generation
            /// </summary>
            public const string DefaultAssetType = "Models";

            /// <summary>
            /// Root folder for all generated assets (e.g., "Assets/GeniesArtForge/")
            /// </summary>
            public static string Root => $"Assets/Genies/{ShortProductName}/";

            /// <summary>
            /// Style references folder (e.g., "Assets/GeniesArtForge/StyleReferences/")
            /// </summary>
            public static string StyleReferences => $"{Root}StyleReferences/";

            /// <summary>
            /// User references storage folder (e.g., "Assets/GeniesArtForge/_Settings/UserReferences")
            /// </summary>
            public static string UserReferences => $"{Root}_Settings/UserReferences";

            /// <summary>
            /// Temporary cache folder (absolute path)
            /// </summary>
            public static string TempCache => Path.Combine(UnityEngine.Application.temporaryCachePath, ProductName, "Temp");

            /// <summary>
            /// Generation working directory (absolute path) - per-job temporary folder
            /// </summary>
            public static string GetGenerationWorkDir(string jobId) => Path.Combine(UnityEngine.Application.temporaryCachePath, ProductName, "Generation", jobId);

            /// <summary>
            /// Queue persistence file path (absolute path in project Library folder)
            /// </summary>
            public static string GetQueuePersistencePath()
            {
                string dataPath = UnityEngine.Application.dataPath;
                string projectPath = Path.GetDirectoryName(dataPath);
                return Path.Combine(projectPath, "Library", "Genies", ShortProductName, "queue_persistence.json");
            }

            /// <summary>
            /// Gets the root folder for a specific asset
            /// </summary>
            /// <param name="assetName">Name of the asset</param>
            /// <param name="assetType">Type of asset (e.g., "Models", "Characters", "Environments"). Defaults to "Models".</param>
            /// <returns>Path like "Assets/GeniesArtForge/Models/MyAsset/"</returns>
            public static string GetAssetRoot(string assetName, string assetType = DefaultAssetType) => $"{Root}{assetType}/{assetName}/";

            /// <summary>
            /// Gets the models folder for a specific asset
            /// </summary>
            /// <param name="assetName">Name of the asset</param>
            /// <param name="assetType">Type of asset. Defaults to "Models".</param>
            /// <returns>Path like "Assets/GeniesArtForge/Models/MyAsset/Models/"</returns>
            public static string GetModelsFolder(string assetName, string assetType = DefaultAssetType) => $"{GetAssetRoot(assetName, assetType)}Models/";

            /// <summary>
            /// Gets the textures folder for a specific asset
            /// </summary>
            /// <param name="assetName">Name of the asset</param>
            /// <param name="assetType">Type of asset. Defaults to "Models".</param>
            /// <returns>Path like "Assets/GeniesArtForge/Models/MyAsset/Textures/"</returns>
            public static string GetTexturesFolder(string assetName, string assetType = DefaultAssetType) => $"{GetAssetRoot(assetName, assetType)}Textures/";

            /// <summary>
            /// Gets the materials folder for a specific asset
            /// </summary>
            /// <param name="assetName">Name of the asset</param>
            /// <param name="assetType">Type of asset. Defaults to "Models".</param>
            /// <returns>Path like "Assets/GeniesArtForge/Models/MyAsset/Materials/"</returns>
            public static string GetMaterialsFolder(string assetName, string assetType = DefaultAssetType) => $"{GetAssetRoot(assetName, assetType)}Materials/";

            /// <summary>
            /// Gets the prefabs folder for a specific asset
            /// </summary>
            /// <param name="assetName">Name of the asset</param>
            /// <param name="assetType">Type of asset. Defaults to "Models".</param>
            /// <returns>Path like "Assets/GeniesArtForge/Models/MyAsset/Prefabs/"</returns>
            public static string GetPrefabsFolder(string assetName, string assetType = DefaultAssetType) => $"{GetAssetRoot(assetName, assetType)}Prefabs/";

            /// <summary>
            /// Gets the reference images folder for a specific asset
            /// </summary>
            /// <param name="assetName">Name of the asset</param>
            /// <param name="assetType">Type of asset. Defaults to "Models".</param>
            /// <returns>Path like "Assets/GeniesArtForge/Models/MyAsset/RefImages/"</returns>
            public static string GetRefImagesFolder(string assetName, string assetType = DefaultAssetType) => $"{GetAssetRoot(assetName, assetType)}RefImages/";

            /// <summary>
            /// Gets the source folder for a specific asset (for GLB source files from backend)
            /// </summary>
            /// <param name="assetName">Name of the asset</param>
            /// <param name="assetType">Type of asset. Defaults to "Models".</param>
            /// <returns>Path like "Assets/GeniesArtForge/Models/MyAsset/Source/"</returns>
            public static string GetSourceFolder(string assetName, string assetType = DefaultAssetType) => $"{GetAssetRoot(assetName, assetType)}Source/";

            /// <summary>
            /// Gets the full path to the GLB source file (stored in Source folder)
            /// </summary>
            /// <param name="assetName">Name of the asset</param>
            /// <param name="assetType">Type of asset. Defaults to "Models".</param>
            /// <returns>Path like "Assets/GeniesArtForge/Models/MyAsset/Source/MyAsset.glb"</returns>
            public static string GetSourceGlbPath(string assetName, string assetType = DefaultAssetType) => $"{GetSourceFolder(assetName, assetType)}{assetName}.glb";

            /// <summary>
            /// Gets the full path to the FBX model file
            /// </summary>
            /// <param name="assetName">Name of the asset</param>
            /// <param name="assetType">Type of asset. Defaults to "Models".</param>
            /// <returns>Path like "Assets/GeniesArtForge/Models/MyAsset/Models/MyAsset.fbx"</returns>
            public static string GetFbxPath(string assetName, string assetType = DefaultAssetType) => $"{GetModelsFolder(assetName, assetType)}{assetName}.fbx";

            /// <summary>
            /// Gets the full path to the GLB model file
            /// </summary>
            /// <param name="assetName">Name of the asset</param>
            /// <param name="assetType">Type of asset. Defaults to "Models".</param>
            /// <returns>Path like "Assets/GeniesArtForge/Models/MyAsset/Models/MyAsset.glb"</returns>
            public static string GetGlbPath(string assetName, string assetType = DefaultAssetType) => $"{GetModelsFolder(assetName, assetType)}{assetName}.glb";

            /// <summary>
            /// Gets the full path to the model file, checking for both FBX and GLB formats
            /// Returns the path of the file that exists, or FBX path if neither exists (for compatibility)
            /// </summary>
            /// <param name="assetName">Name of the asset</param>
            /// <param name="assetType">Type of asset. Defaults to "Models".</param>
            /// <returns>Path to existing model file (FBX or GLB), or FBX path if neither exists</returns>
            public static string GetModelPath(string assetName, string assetType = DefaultAssetType)
            {
                string glbPath = GetGlbPath(assetName, assetType);
                string fbxPath = GetFbxPath(assetName, assetType);

                // Check if GLB exists
                string fullGlbPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", glbPath);
                if (System.IO.File.Exists(fullGlbPath))
                {
                    return glbPath;
                }

                // Check if FBX exists
                string fullFbxPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", fbxPath);
                if (System.IO.File.Exists(fullFbxPath))
                {
                    return fbxPath;
                }

                // Default to FBX if neither exists (for backward compatibility)
                return fbxPath;
            }

            /// <summary>
            /// Gets the full path to the material file
            /// </summary>
            /// <param name="assetName">Name of the asset</param>
            /// <param name="assetType">Type of asset. Defaults to "Models".</param>
            /// <returns>Path like "Assets/GeniesArtForge/Models/MyAsset/Materials/MAT_MyAsset.mat"</returns>
            public static string GetMaterialPath(string assetName, string assetType = DefaultAssetType) => $"{GetMaterialsFolder(assetName, assetType)}MAT_{assetName}.mat";

            /// <summary>
            /// Gets the full path to the prefab file
            /// </summary>
            /// <param name="assetName">Name of the asset</param>
            /// <param name="assetType">Type of asset. Defaults to "Models".</param>
            /// <returns>Path like "Assets/GeniesArtForge/Models/MyAsset/Prefabs/MyAsset.prefab"</returns>
            public static string GetPrefabPath(string assetName, string assetType = DefaultAssetType) => $"{GetPrefabsFolder(assetName, assetType)}{assetName}.prefab";

            /// <summary>
            /// Gets the full path to the current reference image file (PNG)
            /// This is always the selected/active version
            /// </summary>
            /// <param name="assetName">Name of the asset</param>
            /// <param name="assetType">Type of asset. Defaults to "Models".</param>
            /// <returns>Path like "Assets/GeniesArtForge/Models/MyAsset/RefImages/MyAsset_ref.png"</returns>
            public static string GetReferenceImagePath(string assetName, string assetType = DefaultAssetType)
                => $"{GetRefImagesFolder(assetName, assetType)}{assetName}_ref.png";

            /// <summary>
            /// Gets the History folder path for reference image versions
            /// </summary>
            /// <param name="assetName">Name of the asset</param>
            /// <param name="assetType">Type of asset. Defaults to "Models".</param>
            /// <returns>Path like "Assets/GeniesArtForge/Models/MyAsset/RefImages/History/"</returns>
            public static string GetRefImagesHistoryFolder(string assetName, string assetType = DefaultAssetType)
                => $"{GetRefImagesFolder(assetName, assetType)}History/";

            /// <summary>
            /// Gets the full path to a versioned reference image in the History folder
            /// </summary>
            /// <param name="assetName">Name of the asset</param>
            /// <param name="versionNumber">Version number (0-based: 00, 01, 02, etc.)</param>
            /// <param name="assetType">Type of asset. Defaults to "Models".</param>
            /// <returns>Path like "Assets/GeniesArtForge/Models/MyAsset/RefImages/History/MyAsset_ref_00.png"</returns>
            public static string GetVersionedReferenceImagePath(string assetName, int versionNumber, string assetType = DefaultAssetType)
                => $"{GetRefImagesHistoryFolder(assetName, assetType)}{assetName}_ref_{versionNumber:D2}.png";

            /// <summary>
            /// Gets the History folder path for texture versions
            /// </summary>
            /// <param name="assetName">Name of the asset</param>
            /// <param name="assetType">Type of asset. Defaults to "Models".</param>
            /// <returns>Path like "Assets/GeniesArtForge/Models/MyAsset/Textures/History/"</returns>
            public static string GetTexturesHistoryFolder(string assetName, string assetType = DefaultAssetType)
                => $"{GetTexturesFolder(assetName, assetType)}History/";

            /// <summary>
            /// Gets the full path to a versioned texture in the History folder
            /// </summary>
            /// <param name="assetName">Name of the asset</param>
            /// <param name="textureFileName">Name of the texture file (e.g., "MyAsset_Albedo.png")</param>
            /// <param name="versionNumber">Version number (0-based: 00, 01, 02, etc.)</param>
            /// <param name="assetType">Type of asset. Defaults to "Models".</param>
            /// <returns>Path like "Assets/GeniesArtForge/Models/MyAsset/Textures/History/MyAsset_Albedo_00.png"</returns>
            public static string GetVersionedTexturePath(string assetName, string textureFileName, int versionNumber, string assetType = DefaultAssetType)
            {
                // Extract base name and extension from textureFileName
                string baseName = System.IO.Path.GetFileNameWithoutExtension(textureFileName);
                string extension = System.IO.Path.GetExtension(textureFileName);
                string versionedFileName = $"{baseName}_{versionNumber:D2}{extension}";
                return $"{GetTexturesHistoryFolder(assetName, assetType)}{versionedFileName}";
            }
        }
    }
}
