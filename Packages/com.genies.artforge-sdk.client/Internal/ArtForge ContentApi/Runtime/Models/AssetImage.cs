using System;

namespace Genies.ArtForge.ContentAPI.Models
{
    /// <summary>
    /// Represents an image generated for an asset.
    /// </summary>
    [Serializable]
    internal class AssetImage
    {
        /// <summary>
        /// The unique identifier for this image.
        /// </summary>
        public string imageId;

        /// <summary>
        /// The URL where the image can be accessed.
        /// </summary>
        public string imageUrl;

        /// <summary>
        /// The width of the image in pixels.
        /// </summary>
        public int width;

        /// <summary>
        /// The height of the image in pixels.
        /// </summary>
        public int height;

        /// <summary>
        /// Initializes a new instance of the AssetImage class.
        /// </summary>
        public AssetImage()
        {
            imageId = "";
            imageUrl = "";
            width = 0;
            height = 0;
        }

        /// <summary>
        /// Initializes a new instance of the AssetImage class with all parameters.
        /// </summary>
        /// <param name="imageId">The unique identifier for this image.</param>
        /// <param name="imageUrl">The URL where the image can be accessed.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        public AssetImage(string imageId, string imageUrl, int width, int height)
        {
            this.imageId = imageId;
            this.imageUrl = imageUrl;
            this.width = width;
            this.height = height;
        }
    }
}
