using System;
using System.Collections.Generic;

namespace Genies.ArtForge.ContentAPI.Models
{
    /// <summary>
    /// Input data for creating a decor item.
    /// </summary>
    [Serializable]
    internal class DecorData
    {
        /// <summary>
        /// The user prompt describing the decor to create (required).
        /// Example: "a modern gaming chair with RGB lighting"
        /// </summary>
        public string prompt;

        /// <summary>
        /// Optional decor type selected by the user.
        /// Must match a value from DecorConfig.types when provided.
        /// </summary>
        public string type;

        /// <summary>
        /// Optional decor subcategory selected by the user.
        /// Must match a value from the selected DecorType.subcategories when provided.
        /// </summary>
        public string subcategory;

        /// <summary>
        /// Optional storage IDs of previously uploaded reference images.
        /// Uses the same storage/upload flow as wearable reference images.
        /// </summary>
        public string[] referenceImageIds;

        /// <summary>
        /// Optional style parameter for the decor.
        /// Example: "modern", "vintage", "minimalist"
        /// </summary>
        public string style;

        /// <summary>
        /// Optional brand/intellectual property identifier.
        /// Example: "ikea", "herman-miller"
        /// </summary>
        public string ip;

        /// <summary>
        /// Optional aspect ratio for 2D decor generation.
        /// Example: "16:9"
        /// </summary>
        public string aspectRatio;

        /// <summary>
        /// Optional image size for 2D decor generation.
        /// Format: "WIDTHxHEIGHT", e.g. "1024x1024"
        /// </summary>
        public string imageSize;

        /// <summary>
        /// Initializes a new instance of the DecorData class.
        /// </summary>
        public DecorData()
        {
        }

        /// <summary>
        /// Initializes a new instance of the DecorData class with a prompt.
        /// </summary>
        /// <param name="prompt">The user prompt describing the decor.</param>
        public DecorData(string prompt)
        {
            this.prompt = prompt;
        }

        /// <summary>
        /// Initializes a new instance of the DecorData class with full parameters.
        /// </summary>
        /// <param name="prompt">The user prompt describing the decor.</param>
        /// <param name="type">Optional decor type (must be from DecorConfig).</param>
        /// <param name="subcategory">Optional decor subcategory (must be from selected decor type).</param>
        /// <param name="referenceImageIds">Optional storage IDs of uploaded reference images.</param>
        /// <param name="style">Optional style parameter.</param>
        /// <param name="ip">Optional brand/IP identifier.</param>
        /// <param name="aspectRatio">Optional aspect ratio for 2D decor generation.</param>
        /// <param name="imageSize">Optional image size for 2D decor generation in "WIDTHxHEIGHT" format.</param>
        public DecorData(
            string prompt,
            string type = null,
            string subcategory = null,
            string[] referenceImageIds = null,
            string style = null,
            string ip = null,
            string aspectRatio = null,
            string imageSize = null)
        {
            this.prompt = prompt;
            this.type = type;
            this.subcategory = subcategory;
            this.referenceImageIds = referenceImageIds;
            this.style = style;
            this.ip = ip;
            this.aspectRatio = aspectRatio;
            this.imageSize = imageSize;
        }
    }

    /// <summary>
    /// Result returned after creating a decor item.
    /// </summary>
    [Serializable]
    internal class DecorResult
    {
        /// <summary>
        /// The asset ID for tracking the decor creation progress and retrieving the asset.
        /// </summary>
        public string assetId;

        /// <summary>
        /// The job ID for tracking the decor creation progress.
        /// </summary>
        public string jobId;
    }
}
