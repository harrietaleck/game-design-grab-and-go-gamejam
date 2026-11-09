using System.IO;
using UnityEditor;
using UnityEngine;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Processing
{
    /// <summary>
    /// Centralized utilities for asset path manipulation and file I/O.
    /// Single source of truth for path handling to avoid platform and edge case bugs.
    /// </summary>
    internal static class AssetPathHelpers
    {
        /// <summary>
        /// Converts a Unity asset path (relative to project) to full filesystem path.
        /// </summary>
        public static string ToFullPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(assetPath))
            {
                return Path.GetFullPath(assetPath);
            }

            string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        /// <summary>
        /// Ensures the parent directory for an asset path exists.
        /// </summary>
        public static void EnsureParentDir(string assetPath)
        {
            string fullPath = ToFullPath(assetPath);
            string directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// Loads an asset from the AssetDatabase, optionally forcing import if it doesn't exist yet.
        /// </summary>
        public static T LoadOrImport<T>(string assetPath, bool forceImport = false) where T : Object
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            // Try to load first
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

            if (asset == null && forceImport)
            {
                // Check if file exists on disk
                string fullPath = ToFullPath(assetPath);
                if (File.Exists(fullPath))
                {
                    // Force synchronous import
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                }
            }

            return asset;
        }

        /// <summary>
        /// Checks if a file exists on disk (full path or asset path).
        /// </summary>
        public static bool FileExists(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            string fullPath = Path.IsPathRooted(path) ? path : ToFullPath(path);
            return File.Exists(fullPath);
        }

        /// <summary>
        /// Verifies a file is readable and has content.
        /// </summary>
        public static bool IsFileValid(string path, ILogger logger = null)
        {
            if (!FileExists(path))
            {
                return false;
            }

            string fullPath = ToFullPath(path);
            FileInfo fileInfo = new FileInfo(fullPath);

            if (fileInfo.Length == 0)
            {
                logger?.Warning($"[AssetPathHelpers] File is empty: {path}");
                return false;
            }

            try
            {
                using (FileStream fs = File.OpenRead(fullPath))
                {
                    return fs.Length > 0;
                }
            }
            catch (System.Exception ex)
            {
                logger?.Error($"[AssetPathHelpers] File is not readable: {path} - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Normalizes path separators to forward slashes (Unity standard).
        /// </summary>
        public static string NormalizePath(string path)
        {
            return path?.Replace('\\', '/');
        }
    }
}

