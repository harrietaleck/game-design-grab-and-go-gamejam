using System;

namespace Genies.ArtForge.ContentAPI.Models
{
    /// <summary>
    /// Represents the result of creating a wearable model from a selected image.
    /// </summary>
    [Serializable]
    internal class WearableModelResult
    {
        /// <summary>
        /// The asset ID being processed for model generation.
        /// </summary>
        public string assetId;

        /// <summary>
        /// Job ID of the modelling job.
        /// </summary>
        public string jobId;

        /// <summary>
        /// Initializes a new instance of the WearableModelResult class.
        /// </summary>
        public WearableModelResult()
        {
            assetId = null;
            jobId = null;
        }

        /// <summary>
        /// Initializes a new instance of the WearableModelResult class with parameters.
        /// </summary>
        /// <param name="assetId">The asset ID being processed.</param>
        /// <param name="jobId">The job ID if created.</param>
        public WearableModelResult(string assetId = null, string jobId = null)
        {
            this.assetId = assetId;
            this.jobId = jobId;
        }
    }
}
