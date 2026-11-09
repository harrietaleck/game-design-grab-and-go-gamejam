using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Genies.ArtForge.Telemetry
{
    [Serializable]
    internal class GeniesArtForgeTelemetryEvent
    {
        [JsonProperty("name")]
        public string Name;
        
        [JsonProperty("timestamp")]
        public string Timestamp;
        
        [JsonProperty("id")]
        public string Id;
        
        [JsonProperty("properties")]
        public Dictionary<string, object> Properties;

        internal static GeniesArtForgeTelemetryEvent Create(
            string name,
            Dictionary<string, object> properties = null,
            string id = null,
            DateTime? utcTimestamp = null)
        {
            return new GeniesArtForgeTelemetryEvent
            {
                Name = name,
                Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString() : id,
                Timestamp = (utcTimestamp ?? DateTime.UtcNow).ToString("o"),
                Properties = properties ?? new Dictionary<string, object>()
            };
        }
    }

// Container we serialize to PlayerPrefs as JSON
    [Serializable]
    internal class PendingTelemetryEnvelope
    {
        public List<GeniesArtForgeTelemetryEvent> events = new List<GeniesArtForgeTelemetryEvent>();
    }
}