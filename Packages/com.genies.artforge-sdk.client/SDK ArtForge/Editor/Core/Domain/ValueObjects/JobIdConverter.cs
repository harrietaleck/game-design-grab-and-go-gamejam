using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects
{
    /// <summary>
    /// JSON converter for JobId value object.
    /// Handles serialization/deserialization of JobId which uses a factory method.
    /// </summary>
    internal class JobIdConverter : JsonConverter<JobId>
    {
        public override void WriteJson(JsonWriter writer, JobId value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                writer.WriteValue(value.Value.ToString());
            }
        }

        public override JobId ReadJson(JsonReader reader, Type objectType, JobId existingValue, bool hasExistingValue, JsonSerializer serializer)
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

                // Use the Parse method to create JobId
                try
                {
                    return JobId.Parse(value);
                }
                catch (ArgumentException)
                {
                    // If parsing fails, return null
                    return null;
                }
            }

            // Handle case where JobId is stored as an object with Value property
            if (reader.TokenType == JsonToken.StartObject)
            {
                JObject obj = JObject.Load(reader);
                string value = obj["Value"]?.ToString();

                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }

                try
                {
                    return JobId.Parse(value);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }

            throw new JsonSerializationException($"Unexpected token type {reader.TokenType} when deserializing JobId");
        }
    }
}

