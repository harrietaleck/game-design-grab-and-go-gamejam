using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects
{
    /// <summary>
    /// JSON converter for AssetName value object.
    /// Handles serialization/deserialization of AssetName which uses a factory method.
    /// </summary>
    internal class AssetNameConverter : JsonConverter<AssetName>
    {
        private const int MaxAssetNameLength = 30;

        public override void WriteJson(JsonWriter writer, AssetName value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                writer.WriteValue(value.Value);
            }
        }

        public override AssetName ReadJson(JsonReader reader, Type objectType, AssetName existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonToken.String)
            {
                string value = reader.Value?.ToString();
                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }

                // Use the factory method to create AssetName
                var result = AssetName.Create(value);
                if (result.IsSuccess)
                {
                    return result.Value;
                }
                else
                {
                    // If validation fails during deserialization, create a sanitized version
                    // This handles edge cases where persisted data might have slightly invalid names
                    // that were valid when originally created but fail stricter validation now
                    var sanitizedResult = AssetName.Create(value.Trim().Replace(" ", "_"));
                    if (sanitizedResult.IsSuccess)
                    {
                        return sanitizedResult.Value;
                    }

                    // Last resort: create with minimal validation (use internal constructor)
                    string sanitized = value.Trim();
                    if (sanitized.Length > MaxAssetNameLength)
                    {
                        sanitized = sanitized.Substring(0, MaxAssetNameLength);
                    }

                    return new AssetName(sanitized);
                }
            }

            // Handle case where AssetName is stored as an object with Value property
            if (reader.TokenType == JsonToken.StartObject)
            {
                JObject obj = JObject.Load(reader);
                string value = obj["Value"]?.ToString();

                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }

                var result = AssetName.Create(value);
                return result.IsSuccess ? result.Value : null;
            }

            throw new JsonSerializationException($"Unexpected token type {reader.TokenType} when deserializing AssetName");
        }
    }
}
