using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Genies.ArtForge.ContentAPI.Models;
using Genies.Convex.Exceptions;
using Genies.Convex.Values;

namespace Genies.ArtForge.ContentAPI
{
    internal sealed class DecorEndpoint : ContentEndpointBase
    {
        private const string DecorEndpointPath = "content/decor_gaf";
        internal DecorEndpoint(GeniesContentAPI api) : base(api) { }

        public async Task<DecorConfig> GetConfigAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureConnectedAsync(cancellationToken);
                var args = new { };
                var convexResult = await Client.QueryAsync<ConvexValue>($"{DecorEndpointPath}:getConfig", args, cancellationToken);

                return ConvertToDecorConfig(convexResult);
            }
            catch (ConvexError ex)
            {
                var errorCode = ExtractErrorCode(ex);
                throw new InvalidOperationException(FormatErrorMessage("Get decor config", ex.Message, errorCode), ex);
            }
            catch (ConvexExecutionException ex)
            {
                throw new InvalidOperationException(FormatErrorMessage("Get decor config", ex.Message), ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unexpected error getting decor config: {ex.Message}", ex);
            }
        }

        public Task<DecorResult> CreateAsync(DecorData data, CancellationToken cancellationToken = default)
        {
            return CreateAsync(data, null, null, cancellationToken);
        }

        public async Task<DecorResult> CreateAsync(
            DecorData data,
            int? polycount,
            string meshMode,
            CancellationToken cancellationToken = default)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            try
            {
                await EnsureConnectedAsync(cancellationToken);
                var args = new Dictionary<string, object>
                {
                    { "prompt", data.prompt },
                    { "referenceImageIds", data.referenceImageIds },
                    { "style", data.style },
                    { "ip", data.ip }
                };

                if (!string.IsNullOrWhiteSpace(data.type))
                {
                    args["type"] = data.type;
                }

                if (!string.IsNullOrWhiteSpace(data.subcategory))
                {
                    args["subcategory"] = data.subcategory;
                }

                if (polycount.HasValue)
                {
                    args["polycount"] = polycount.Value;
                }

                if (!string.IsNullOrWhiteSpace(meshMode))
                {
                    args["meshMode"] = meshMode;
                }

                if (!string.IsNullOrWhiteSpace(data.aspectRatio))
                {
                    args["aspectRatio"] = data.aspectRatio;
                }

                if (!string.IsNullOrWhiteSpace(data.imageSize))
                {
                    args["imageSize"] = data.imageSize;
                }

                var convexResult = await Client.MutationAsync<ConvexValue>($"{DecorEndpointPath}:create", args, cancellationToken);

                return ConvertToDecorResult(convexResult);
            }
            catch (ConvexError ex)
            {
                var errorCode = ExtractErrorCode(ex);
                throw new InvalidOperationException(FormatErrorMessage("Decor creation", ex.Message, errorCode), ex);
            }
            catch (ConvexExecutionException ex)
            {
                throw new InvalidOperationException(FormatErrorMessage("Decor creation", ex.Message), ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unexpected error creating decor: {ex.Message}", ex);
            }
        }

        private DecorConfig ConvertToDecorConfig(ConvexValue convexValue)
        {
            if (convexValue is ConvexValue.Object obj)
            {
                var config = new DecorConfig();
                config.supportedAspectRatios = GetSupportedAspectRatios(obj);

                var typesList = new List<DecorType>();
                var seenTypeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in GetDecorTypeCandidates(obj))
                {
                    if (!TryParseDecorType(item, out DecorType decorType))
                    {
                        continue;
                    }

                    string typeKey = decorType.type ?? decorType.name ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(typeKey) || !seenTypeKeys.Add(typeKey))
                    {
                        continue;
                    }

                    typesList.Add(decorType);
                }
                config.types = typesList.ToArray();

                Logger.Info(
                    $"[GeniesContentAPI] Decor config loaded: {config.types.Length} decor types, {config.supportedAspectRatios.Length} supported aspect ratios");

                return config;
            }

            throw new InvalidOperationException("Expected Convex object response for decor config");
        }

        private static IEnumerable<ConvexValue> GetDecorTypeCandidates(ConvexValue.Object obj)
        {
            var keys = new[] { "types", "decorTypes", "options", "assetTypes", "categories" };
            foreach (var key in keys)
            {
                if (obj.TryGetValue(key, out var value) && value is ConvexValue.Array array)
                {
                    return array.Value;
                }
            }

            return Array.Empty<ConvexValue>();
        }

        private static string[] GetSupportedAspectRatios(ConvexValue.Object obj)
        {
            var keys = new[] { "supportedAspectRatios", "aspectRatios", "supportedRatios" };
            var ratios = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string key in keys)
            {
                if (!obj.TryGetValue(key, out var value) || !(value is ConvexValue.Array array))
                {
                    continue;
                }

                foreach (ConvexValue item in array.Value)
                {
                    string ratio = item.AsString()?.Trim();
                    if (string.IsNullOrWhiteSpace(ratio) || !seen.Add(ratio))
                    {
                        continue;
                    }

                    ratios.Add(ratio);
                }
            }

            return ratios.ToArray();
        }

        private static bool TryParseDecorType(ConvexValue value, out DecorType type)
        {
            if (value is ConvexValue.String stringValue)
            {
                string raw = stringValue.Value?.Trim();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    type = null;
                    return false;
                }

                type = new DecorType
                {
                    type = raw,
                    name = raw,
                    subcategories = Array.Empty<DecorSubcategory>()
                };
                return true;
            }

            if (value is ConvexValue.Object obj)
            {
                string rawType = GetFirstString(obj, "type", "value", "id", "key", "slug");
                string rawName = GetFirstString(obj, "name", "label", "displayName", "title");

                if (string.IsNullOrWhiteSpace(rawType) && string.IsNullOrWhiteSpace(rawName))
                {
                    type = null;
                    return false;
                }

                string normalizedType = string.IsNullOrWhiteSpace(rawType) ? rawName.Trim() : rawType.Trim();
                string normalizedName = string.IsNullOrWhiteSpace(rawName) ? normalizedType : rawName.Trim();
                int? polycount = GetFirstInt(obj, "polycount", "polyCount", "targetPolycount");
                DecorSubcategory[] subcategories = GetSubcategories(obj);

                type = new DecorType
                {
                    type = normalizedType,
                    name = normalizedName,
                    polycount = polycount,
                    subcategories = subcategories
                };
                return true;
            }

            type = null;
            return false;
        }

        private static DecorSubcategory[] GetSubcategories(ConvexValue.Object obj)
        {
            var keys = new[] { "subcategories", "subCategories", "categories" };
            foreach (string key in keys)
            {
                if (!obj.TryGetValue(key, out var value) || !(value is ConvexValue.Array array))
                {
                    continue;
                }

                var subcategories = new List<DecorSubcategory>();
                var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ConvexValue item in array.Value)
                {
                    if (!TryParseDecorSubcategory(item, out DecorSubcategory subcategory))
                    {
                        continue;
                    }

                    string keyName = subcategory.name ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(keyName) || !seenNames.Add(keyName))
                    {
                        continue;
                    }

                    subcategories.Add(subcategory);
                }

                return subcategories.ToArray();
            }

            return Array.Empty<DecorSubcategory>();
        }

        private static bool TryParseDecorSubcategory(ConvexValue value, out DecorSubcategory subcategory)
        {
            if (value is ConvexValue.String stringValue)
            {
                string raw = stringValue.Value?.Trim();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    subcategory = null;
                    return false;
                }

                subcategory = new DecorSubcategory
                {
                    name = raw
                };
                return true;
            }

            if (value is ConvexValue.Object obj)
            {
                string rawName = GetFirstString(obj, "name", "label", "displayName", "title");
                if (string.IsNullOrWhiteSpace(rawName))
                {
                    subcategory = null;
                    return false;
                }

                subcategory = new DecorSubcategory
                {
                    name = rawName.Trim(),
                    polycount = GetFirstInt(obj, "polycount", "polyCount", "targetPolycount"),
                    defaultAspectRatio = GetFirstString(obj, "defaultAspectRatio", "aspectRatio", "defaultAR"),
                    transparentBackground = GetFirstBool(obj, "transparentBackground", "transparent", "hasTransparency")
                };
                return true;
            }

            subcategory = null;
            return false;
        }

        private static string GetFirstString(ConvexValue.Object obj, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!obj.TryGetValue(key, out var value))
                {
                    continue;
                }

                string asString = value.AsString();
                if (!string.IsNullOrWhiteSpace(asString))
                {
                    return asString;
                }
            }

            return null;
        }

        private static int? GetFirstInt(ConvexValue.Object obj, params string[] keys)
        {
            foreach (string key in keys)
            {
                if (!obj.TryGetValue(key, out var value))
                {
                    continue;
                }

                int? parsed = ParseInt(value);
                if (parsed.HasValue)
                {
                    return parsed.Value;
                }
            }

            return null;
        }

        private static bool? GetFirstBool(ConvexValue.Object obj, params string[] keys)
        {
            foreach (string key in keys)
            {
                if (!obj.TryGetValue(key, out var value))
                {
                    continue;
                }

                bool? parsed = ParseBool(value);
                if (parsed.HasValue)
                {
                    return parsed.Value;
                }
            }

            return null;
        }

        private static int? ParseInt(ConvexValue value)
        {
            long? int64Value = value.AsInt64();
            if (int64Value.HasValue)
            {
                return (int)int64Value.Value;
            }

            double? float64Value = value.AsFloat64();
            if (float64Value.HasValue)
            {
                return (int)float64Value.Value;
            }

            string asString = value.AsString();
            if (int.TryParse(asString, out int parsed))
            {
                return parsed;
            }

            return null;
        }

        private static bool? ParseBool(ConvexValue value)
        {
            bool? boolValue = value.AsBoolean();
            if (boolValue.HasValue)
            {
                return boolValue.Value;
            }

            long? int64Value = value.AsInt64();
            if (int64Value.HasValue)
            {
                return int64Value.Value != 0;
            }

            string asString = value.AsString();
            if (bool.TryParse(asString, out bool parsed))
            {
                return parsed;
            }

            if (string.Equals(asString, "1", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(asString, "0", StringComparison.Ordinal))
            {
                return false;
            }

            return null;
        }

        private DecorResult ConvertToDecorResult(ConvexValue convexValue)
        {
            if (convexValue is ConvexValue.Object obj)
            {
                var result = new DecorResult
                {
                    assetId = ConvexValueReader.GetStringValue(obj, "assetId"),
                    jobId = ConvexValueReader.GetStringValue(obj, "jobId")
                };

                Logger.Info($"[GeniesContentAPI] Decor creation result: assetId={result.assetId}, jobId={result.jobId}");

                return result;
            }

            throw new InvalidOperationException("Expected Convex object response for decor creation");
        }
    }
}
