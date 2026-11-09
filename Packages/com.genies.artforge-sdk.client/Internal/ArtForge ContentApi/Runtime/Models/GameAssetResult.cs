using System;

namespace Genies.ArtForge.ContentAPI.Models
{
    /// <summary>
    /// Represents the result of creating a game asset for inventory.
    /// </summary>
    [Serializable]
    internal class GameAssetResult
    {
        /// <summary>
        /// Job ID of the game asset creation job.
        /// </summary>
        public string jobId;

        /// <summary>
        /// The model ID created for the game asset.
        /// </summary>
        public string modelId;

        /// <summary>
        /// Initializes a new instance of the GameAssetResult class.
        /// </summary>
        public GameAssetResult()
        {
            jobId = null;
            modelId = null;
        }

        /// <summary>
        /// Initializes a new instance of the GameAssetResult class with parameters.
        /// </summary>
        /// <param name="jobId">The job ID if created.</param>
        /// <param name="modelId">The model ID created.</param>
        public GameAssetResult(string jobId = null, string modelId = null)
        {
            this.jobId = jobId;
            this.modelId = modelId;
        }
    }
}
