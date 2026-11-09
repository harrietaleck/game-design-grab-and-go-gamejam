using System;

namespace Genies.ArtForge.ContentAPI.Models
{
    /// <summary>
    /// Represents an individual step within a job's processing pipeline.
    /// </summary>
    [Serializable]
    internal class JobStep
    {
        /// <summary>
        /// The name of this processing step.
        /// Examples: "model_generation", "model_post_processing"
        /// </summary>
        public string name;

        /// <summary>
        /// The current status of this step.
        /// Possible values: "pending", "running", "completed", "failed"
        /// </summary>
        public string status;

        /// <summary>
        /// The expected duration of this step in milliseconds (optional).
        /// Based on rolling EMA statistics. Null if estimates are not available.
        /// </summary>
        public long? expectedMs;

        /// <summary>
        /// The progress of this specific step as a value between 0.0 and 1.0 (optional).
        /// Only present for steps that can report live progress.
        /// </summary>
        public float? progress;
    }

    /// <summary>
    /// Represents the status of a content creation job.
    /// </summary>
    [Serializable]
    internal class JobStatus
    {
        /// <summary>
        /// The job identifier.
        /// </summary>
        public string id;

        /// <summary>
        /// Human-readable job name.
        /// </summary>
        public string name;

        /// <summary>
        /// The asset ID for this job.
        /// </summary>
        public string assetId;

        /// <summary>
        /// The current status of the job.
        /// Possible values: "pending", "running", "completed", "failed", "aborted", "timed_out"
        /// </summary>
        public string status;

        /// <summary>
        /// The current or last step name in the job processing pipeline.
        /// Examples: "prompt_moderation", "image_generation", "model_creation"
        /// </summary>
        public string step;

        /// <summary>
        /// The overall progress of the job as a value between 0.0 and 1.0.
        /// 0.0 = not started, 1.0 = completed
        /// </summary>
        public float progress;

        /// <summary>
        /// The workflow identifier for this job.
        /// Examples: "wearable.createImages", "decor.create", "mammal.createModel"
        /// </summary>
        public string workflow;

        /// <summary>
        /// Error message if the job failed (optional).
        /// </summary>
        public string error;

        /// <summary>
        /// Error code if the job failed (optional).
        /// Examples: "AUTHENTICATION_ERROR", "NOT_FOUND", "VALIDATION_ERROR"
        /// </summary>
        public string errorCode;

        /// <summary>
        /// The current step index (0-based) in the job processing pipeline.
        /// </summary>
        public int? stepIndex;

        /// <summary>
        /// The total number of steps in the job processing pipeline.
        /// </summary>
        public int totalSteps;

        /// <summary>
        /// The total estimated duration of the entire job in milliseconds (optional).
        /// Sum of all step expected durations based on rolling EMA statistics.
        /// Only present for jobs/steps that require fake progress simulation.
        /// Null when live progress updates are available.
        /// </summary>
        public long? totalEstimatedMs;

        /// <summary>
        /// Unix timestamp (milliseconds) when the job started running.
        /// Use this with GetElapsedMs() to calculate elapsed time for progress estimation.
        /// </summary>
        public long startedAt;

        /// <summary>
        /// The individual steps of the job with their status and expected durations.
        /// </summary>
        public JobStep[] steps;

        /// <summary>
        /// Initializes a new instance of the JobStatus class.
        /// </summary>
        public JobStatus()
        {
            status = "pending";
            progress = 0.0f;
            step = "";
        }

        /// <summary>
        /// Checks if the job is still in progress (pending or running).
        /// </summary>
        /// <returns>True if the job is pending or running, false otherwise.</returns>
        public bool IsInProgress()
        {
            return status == "pending" || status == "running";
        }

        /// <summary>
        /// Checks if the job has completed successfully.
        /// </summary>
        /// <returns>True if the job status is "completed", false otherwise.</returns>
        public bool IsCompleted()
        {
            return status == "completed";
        }

        /// <summary>
        /// Checks if the job has failed.
        /// </summary>
        /// <returns>True if the job status is "failed", false otherwise.</returns>
        public bool IsFailed()
        {
            return status == "failed";
        }

        /// <summary>
        /// Checks if the job was aborted.
        /// </summary>
        /// <returns>True if the job status is "aborted", false otherwise.</returns>
        public bool IsAborted()
        {
            return status == "aborted";
        }

        /// <summary>
        /// Checks if the job timed out.
        /// </summary>
        /// <returns>True if the job status is "timed_out", false otherwise.</returns>
        public bool IsTimedOut()
        {
            return status == "timed_out";
        }

        /// <summary>
        /// Checks if estimated duration data is available for fake progress calculation.
        /// When true, use GetEstimatedProgress() for fake progress bars.
        /// When false, use the live progress field directly.
        /// </summary>
        /// <returns>True if totalEstimatedMs has a value greater than 0.</returns>
        public bool HasEstimates => totalEstimatedMs.HasValue && totalEstimatedMs.Value > 0;

        /// <summary>
        /// Gets the elapsed time since the job started, in milliseconds.
        /// Uses the server-provided startedAt timestamp for accurate calculation.
        /// </summary>
        /// <returns>Elapsed milliseconds since job started, or 0 if startedAt is not set.</returns>
        public long GetElapsedMs()
        {
            if (startedAt <= 0)
            {
                return 0;
            }

            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return Math.Max(0, nowMs - startedAt);
        }

        /// <summary>
        /// Calculates an estimated progress value based on elapsed time and total estimated duration.
        /// Use this for "fake" progress bars when live progress updates are not available.
        /// </summary>
        /// <returns>
        /// A progress value between 0.0 and 0.99, calculated from server startedAt timestamp.
        /// If estimates are not available, returns the real progress value.
        /// </returns>
        /// <example>
        /// <code>
        /// // Simple usage - automatically calculates elapsed time from server timestamp
        /// var displayProgress = status.HasEstimates
        ///     ? status.GetEstimatedProgress()
        ///     : status.progress;
        /// </code>
        /// </example>
        public float GetEstimatedProgress()
        {
            if (!HasEstimates)
            {
                return progress;
            }

            var elapsedMs = GetElapsedMs();
            return Math.Min(0.99f, (float)elapsedMs / totalEstimatedMs.Value);
        }

        /// <summary>
        /// Calculates an estimated progress value based on provided elapsed time.
        /// Use this overload if you need to calculate progress for a specific point in time.
        /// </summary>
        /// <param name="elapsedMs">The elapsed time since the job started, in milliseconds.</param>
        /// <returns>
        /// A progress value between 0.0 and 0.99.
        /// If estimates are not available, returns the real progress value.
        /// </returns>
        public float GetEstimatedProgress(long elapsedMs)
        {
            if (!HasEstimates)
            {
                return progress;
            }

            return Math.Min(0.99f, (float)elapsedMs / totalEstimatedMs.Value);
        }
    }
}
