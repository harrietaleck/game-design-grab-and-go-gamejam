using System;
using System.Collections.Generic;
using System.IO;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Genies.Sdk.ArtForge.Editor.Core.UI.Helpers
{
    /// <summary>
    /// Shared thumbnail helper for rendering compact job preview images in queue/history cards.
    /// </summary>
    internal static class JobPreviewThumbnailUI
    {
        private sealed class CachedExternalTexture
        {
            public long LastWriteTimeUtcTicks;
            public Texture2D Texture;
        }

        private static readonly Dictionary<string, CachedExternalTexture> ExternalTextureCache =
            new Dictionary<string, CachedExternalTexture>(StringComparer.OrdinalIgnoreCase);

        static JobPreviewThumbnailUI()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ClearExternalTextureCache;
            EditorApplication.quitting += ClearExternalTextureCache;
        }

        public static bool TryCreatePreviewElement(Job job, float size, out VisualElement previewElement)
        {
            previewElement = null;
            Texture2D texture = GetPreviewTexture(job);
            if (texture == null)
            {
                return false;
            }

            var image = new Image
            {
                image = texture,
                scaleMode = ScaleMode.ScaleAndCrop
            };
            image.pickingMode = PickingMode.Ignore;
            image.style.width = size;
            image.style.height = size;
            image.style.minWidth = size;
            image.style.minHeight = size;
            image.style.borderTopLeftRadius = 3;
            image.style.borderTopRightRadius = 3;
            image.style.borderBottomLeftRadius = 3;
            image.style.borderBottomRightRadius = 3;
            image.style.backgroundColor = new Color(0.14f, 0.14f, 0.14f);
            previewElement = image;
            return true;
        }

        private static Texture2D GetPreviewTexture(Job job)
        {
            string previewPath = GetPreviewImagePath(job);
            if (string.IsNullOrEmpty(previewPath))
            {
                return null;
            }

            string normalizedPath = previewPath.Replace('\\', '/');
            if (normalizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                // Prefer imported assets to avoid duplicate runtime textures.
                Texture2D importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalizedPath);
                if (importedTexture != null)
                {
                    return importedTexture;
                }
            }

            string fullPath = ResolveFullPath(previewPath);
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                return null;
            }

            return LoadExternalTexture(fullPath);
        }

        private static string GetPreviewImagePath(Job job)
        {
            if (job == null)
            {
                return null;
            }

            string selectedImagePath = job.GetSelectedImagePath();
            if (!string.IsNullOrEmpty(selectedImagePath))
            {
                return selectedImagePath;
            }

            if (!string.IsNullOrEmpty(job.ReferenceImagePath))
            {
                return job.ReferenceImagePath;
            }

            if (job.HasImageVersions)
            {
                var latestVersion = job.ImageVersions[job.ImageVersions.Count - 1];
                if (!string.IsNullOrEmpty(latestVersion?.ImagePath))
                {
                    return latestVersion.ImagePath;
                }
            }

            return null;
        }

        private static string ResolveFullPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (Path.IsPathRooted(path))
            {
                return Path.GetFullPath(path);
            }

            string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        private static Texture2D LoadExternalTexture(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                return null;
            }

            DateTime lastWriteUtc = File.GetLastWriteTimeUtc(fullPath);
            if (ExternalTextureCache.TryGetValue(fullPath, out CachedExternalTexture cached) &&
                cached.Texture != null &&
                cached.LastWriteTimeUtcTicks == lastWriteUtc.Ticks)
            {
                return cached.Texture;
            }

            Texture2D loadedTexture = null;
            try
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                loadedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                loadedTexture.hideFlags = HideFlags.HideAndDontSave;
                if (!loadedTexture.LoadImage(bytes, true))
                {
                    UnityEngine.Object.DestroyImmediate(loadedTexture);
                    return null;
                }
            }
            catch
            {
                if (loadedTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(loadedTexture);
                }
                return null;
            }

            if (cached?.Texture != null && cached.Texture != loadedTexture)
            {
                UnityEngine.Object.DestroyImmediate(cached.Texture);
            }

            ExternalTextureCache[fullPath] = new CachedExternalTexture
            {
                LastWriteTimeUtcTicks = lastWriteUtc.Ticks,
                Texture = loadedTexture
            };

            return loadedTexture;
        }

        private static void ClearExternalTextureCache()
        {
            foreach (var entry in ExternalTextureCache.Values)
            {
                if (entry?.Texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(entry.Texture);
                }
            }

            ExternalTextureCache.Clear();
        }
    }
}
