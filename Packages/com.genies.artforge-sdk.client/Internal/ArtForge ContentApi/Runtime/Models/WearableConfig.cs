using System;

namespace Genies.ArtForge.ContentAPI.Models
{
    /// <summary>
    /// Configuration data for wearable creation, including available wearable types.
    /// Retrieved from the backend via GetWearableConfigAsync() before creating a wearable.
    /// </summary>
    [Serializable]
    internal class WearableConfig
    {
        /// <summary>
        /// Array of available wearable types.
        /// Example: [{ type: "HOODIE", name: "Hoodie" }, { type: "SHIRT", name: "Shirt" }]
        /// </summary>
        public WearableType[] types;

        /// <summary>
        /// Initializes a new instance of the WearableConfig class.
        /// </summary>
        public WearableConfig()
        {
            types = new WearableType[0];
        }

        /// <summary>
        /// Initializes a new instance of the WearableConfig class with types.
        /// </summary>
        /// <param name="types">Array of available wearable types.</param>
        public WearableConfig(WearableType[] types)
        {
            this.types = types ?? new WearableType[0];
        }
    }

    /// <summary>
    /// Represents a single wearable type option.
    /// </summary>
    [Serializable]
    internal class WearableType
    {
        /// <summary>
        /// The type identifier (e.g., "HOODIE", "SHIRT", "PANTS").
        /// </summary>
        public string type;

        /// <summary>
        /// The display name for the type (e.g., "Hoodie", "Shirt", "Pants").
        /// </summary>
        public string name;

        /// <summary>
        /// Initializes a new instance of the WearableType class.
        /// </summary>
        public WearableType()
        {
        }

        /// <summary>
        /// Initializes a new instance of the WearableType class with parameters.
        /// </summary>
        /// <param name="type">The type identifier.</param>
        /// <param name="name">The display name.</param>
        public WearableType(string type, string name)
        {
            this.type = type;
            this.name = name;
        }
    }
}
