using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Import
{
    /// <summary>
    /// Reads post-process import settings from asset metadata.json.
    /// </summary>
    internal static class AssetMetadataSettingsReader
    {
        private const string MetadataFileName = "metadata.json";

        internal class ImportSettings
        {
            public int? TextureResolution { get; set; }
            public bool? GenerateLODs { get; set; }
            public bool? GenerateCollider { get; set; }
            public bool? GenerateAO { get; set; }
            public string BackendAssetTypeId { get; set; }
        }

        public static ImportSettings Read(string unityAssetPath, ILogger logger = null)
        {
            try
            {
                string metadataPath = ResolveMetadataPath(unityAssetPath);
                if (string.IsNullOrEmpty(metadataPath) || !File.Exists(metadataPath))
                {
                    return null;
                }

                var root = JObject.Parse(File.ReadAllText(metadataPath));
                var settingsToken = root["PostProcessSettings"] ?? root["postProcessSettings"];
                string backendAssetTypeId = ReadString(root, "BackendAssetTypeId", "backendAssetTypeId");
                var settingsObject = settingsToken as JObject;

                int? textureResolution = settingsObject != null
                    ? ReadInt(settingsObject, "TextureResolution", "textureResolution")
                    : null;
                bool? generateLODs = settingsObject != null
                    ? ReadBool(settingsObject, "GenerateLODs", "generateLODs")
                    : null;
                bool? generateCollider = settingsObject != null
                    ? ReadBool(settingsObject, "GenerateCollider", "generateCollider")
                    : null;
                bool? generateAO = settingsObject != null
                    ? ReadBool(settingsObject, "GenerateAO", "generateAO")
                    : null;

                bool hasAnySetting = textureResolution != null ||
                                     generateLODs != null ||
                                     generateCollider != null ||
                                     generateAO != null ||
                                     !string.IsNullOrWhiteSpace(backendAssetTypeId);
                if (!hasAnySetting)
                {
                    return null;
                }

                return new ImportSettings
                {
                    TextureResolution = textureResolution,
                    GenerateLODs = generateLODs,
                    GenerateCollider = generateCollider,
                    GenerateAO = generateAO,
                    BackendAssetTypeId = backendAssetTypeId
                };
            }
            catch (Exception ex)
            {
                logger?.Warning($"[AssetMetadataSettingsReader] Failed to read metadata settings for {unityAssetPath}: {ex.Message}");
                return null;
            }
        }

        private static string ResolveMetadataPath(string unityAssetPath)
        {
            if (string.IsNullOrWhiteSpace(unityAssetPath))
            {
                return null;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            string fullAssetPath = Path.GetFullPath(Path.Combine(projectRoot, unityAssetPath));
            string currentDirectory = Path.GetDirectoryName(fullAssetPath);

            while (!string.IsNullOrEmpty(currentDirectory) &&
                   currentDirectory.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                string candidate = Path.Combine(currentDirectory, MetadataFileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                string parent = Directory.GetParent(currentDirectory)?.FullName;
                if (string.Equals(parent, currentDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                currentDirectory = parent;
            }

            return null;
        }

        private static int? ReadInt(JObject obj, params string[] keys)
        {
            foreach (string key in keys)
            {
                JToken token = TryGetToken(obj, key);
                if (token == null)
                {
                    continue;
                }

                if (token.Type == JTokenType.Integer)
                {
                    return token.Value<int>();
                }

                if (token.Type == JTokenType.String && int.TryParse(token.Value<string>(), out int value))
                {
                    return value;
                }
            }

            return null;
        }

        private static bool? ReadBool(JObject obj, params string[] keys)
        {
            foreach (string key in keys)
            {
                JToken token = TryGetToken(obj, key);
                if (token == null)
                {
                    continue;
                }

                if (token.Type == JTokenType.Boolean)
                {
                    return token.Value<bool>();
                }

                if (token.Type == JTokenType.String && bool.TryParse(token.Value<string>(), out bool value))
                {
                    return value;
                }
            }

            return null;
        }

        private static string ReadString(JObject obj, params string[] keys)
        {
            foreach (string key in keys)
            {
                JToken token = TryGetToken(obj, key);
                if (token == null)
                {
                    continue;
                }

                if (token.Type == JTokenType.String)
                {
                    string value = token.Value<string>();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }
            }

            return null;
        }

        private static JToken TryGetToken(JObject obj, string key)
        {
            if (obj == null || string.IsNullOrEmpty(key))
            {
                return null;
            }

            foreach (var property in obj.Properties())
            {
                if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value;
                }
            }

            return null;
        }
    }
}
