using System;

namespace Genies.ArtForge.ContentAPI.Models
{
    /// <summary>
    /// Represents a 3D model generated for an asset.
    /// </summary>
    [Serializable]
    internal class AssetModel
    {
        /// <summary>
        /// The unique identifier for this model.
        /// </summary>
        public string modelId;

        /// <summary>
        /// The URL where the model can be accessed.
        /// </summary>
        public string modelUrl;

        /// <summary>
        /// The format of the model file (e.g., "glb", "gltf", "fbx").
        /// </summary>
        public string format;

        /// <summary>
        /// Initializes a new instance of the AssetModel class.
        /// </summary>
        public AssetModel()
        {
            modelId = "";
            modelUrl = "";
            format = "";
        }

        /// <summary>
        /// Initializes a new instance of the AssetModel class with all parameters.
        /// </summary>
        /// <param name="modelId">The unique identifier for this model.</param>
        /// <param name="modelUrl">The URL where the model can be accessed.</param>
        /// <param name="format">The format of the model file.</param>
        public AssetModel(string modelId, string modelUrl, string format)
        {
            this.modelId = modelId;
            this.modelUrl = modelUrl;
            this.format = format;
        }
    }
}
