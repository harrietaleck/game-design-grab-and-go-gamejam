namespace Genies.Sdk.ArtForge.Editor.Core.Domain.Enums
{
    /// <summary>
    /// Represents the state of a generation or edit job in the queue.
    /// </summary>
    internal enum JobState
    {
        /// <summary>
        /// Job is waiting in the queue to be processed
        /// </summary>
        Queued,

        /// <summary>
        /// Job is currently being processed
        /// </summary>
        Processing,

        /// <summary>
        /// Job is awaiting user approval of the generated reference image
        /// </summary>
        AwaitingImageConfirm,

        /// <summary>
        /// Job completed successfully
        /// </summary>
        Completed,

        /// <summary>
        /// Job failed with an error
        /// </summary>
        Failed,

        /// <summary>
        /// Job was cancelled by the user
        /// </summary>
        Cancelled
    }
}


