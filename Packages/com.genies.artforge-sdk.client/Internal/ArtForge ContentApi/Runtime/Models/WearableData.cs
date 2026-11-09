using System;
using System.Collections.Generic;

namespace Genies.ArtForge.ContentAPI.Models
{
    /// <summary>
    /// Input data for creating a wearable item.
    /// </summary>
    [Serializable]
    internal class WearableData
    {
        /// <summary>
        /// The user prompt describing the wearable to create (required).
        /// Example: "a cool baseball cap with neon lights"
        /// </summary>
        public string prompt;

        /// <summary>
        /// Optional display name for the asset provided by the user.
        /// Example: "Neon Cap"
        /// </summary>
        public string assetDisplayName;

        /// <summary>
        /// The type of wearable (required).
        /// Example: "HOODIE", "SHIRT", "PANTS"
        /// Must match a value from WearableConfig.types.
        /// </summary>
        public string type;

        /// <summary>
        /// Optional storage IDs of previously uploaded reference images.
        /// Use WearablesEndpoint.UploadWearableReferenceAsync to generate these IDs.
        /// </summary>
        public string[] referenceImageIds;

        /// <summary>
        /// Initializes a new instance of the WearableData class.
        /// </summary>
        public WearableData()
        {
        }

        /// <summary>
        /// Initializes a new instance of the WearableData class with full parameters.
        /// </summary>
        /// <param name="prompt">The user prompt describing the wearable.</param>
        /// <param name="type">The wearable type (must be from WearableConfig).</param>
        /// <param name="referenceImageIds">Optional storage IDs of uploaded reference images.</param>
        /// <param name="assetDisplayName">Optional display name provided by the user.</param>
        public WearableData(
            string prompt,
            string type,
            string[] referenceImageIds = null,
            string assetDisplayName = null)
        {
            this.prompt = prompt;
            this.type = type;
            this.referenceImageIds = referenceImageIds;
            this.assetDisplayName = assetDisplayName;
        }
    }

    /// <summary>
    /// Result returned after creating a wearable item.
    /// </summary>
    [Serializable]
    internal class WearableResult
    {
        /// <summary>
        /// The asset ID for tracking the wearable creation progress and retrieving the asset.
        /// </summary>
        public string assetId;

        /// <summary>
        /// The job ID for tracking the wearable creation progress.
        /// </summary>
        public string jobId;
    }
}
