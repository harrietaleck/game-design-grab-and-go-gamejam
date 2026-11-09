using System;
using System.Linq;
using System.Threading.Tasks;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Helpers;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.API;
using UnityEngine;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Helpers
{
    internal static class GapSubmissionTracker
    {
        private const float SubmissionProgress = 0.05f;
        private const float ProcessingProgress = 0.1f;
        private const float CompletionProgress = 1f;
        private const float ProgressMin = 0f;
        private const float ProgressMax = 1f;
        private const float ProcessingBaseOffset = 0.25f;
        private const float ProcessingScale = 0.75f;
        private const float AutorigBaseOffset = 0.40f;
        private const float AutorigScale = 0.60f;
        private const float UniversalBuildBaseOffset = 0.75f;
        private const float UniversalBuildScale = 0.25f;

        private const string GapStepSubmission = "gap_submission";
        private const string GapStepProcessing = "gap_processing";
        private const string GapStepAutorig = "autorig";
        private const string GapStepUniversalBuild = "universalBuild";

        public static bool IsInProgress(Job job)
        {
            if (job == null || job.State != JobState.Completed)
            {
                return false;
            }
            return job.GAPStatus == GapStatus.Submitting
                || job.GAPStatus == GapStatus.Processing
                || job.GAPStatus == GapStatus.Pending
                || job.GAPStatus == GapStatus.Running
                || job.GAPStatus == GapStatus.TimedOut;
        }

        public static string GetStatusText(Job job)
        {
            if (job == null)
            {
                return "Processing...";
            }

            string step = JobStepFormatter.GetStageName(job.CurrentStep);
            if (string.IsNullOrEmpty(step))
            {
                return "Processing...";
            }

            return step.StartsWith("Processing", StringComparison.OrdinalIgnoreCase)
                ? step
                : $"Processing: {step}";
        }

        public static void MarkSubmitting(Job job)
        {
            if (job == null)
            {
                return;
            }
            job.GAPStatus = GapStatus.Submitting;
            job.UpdateProgress(SubmissionProgress, "Submitting to Inventory...", GapStepSubmission);
        }

        public static void MarkProcessing(Job job, string jobId)
        {
            if (job == null)
            {
                return;
            }
            job.GAPJobId = jobId;
            job.GAPSubmittedAt = DateTime.UtcNow;
            job.GAPStatus = GapStatus.Processing;
            job.UpdateProgress(ProcessingProgress, "Processing in Inventory...", GapStepProcessing);
        }

        public static async Task TrackAsync(
            Job job,
            ContentClient contentClient,
            Action<Job> saveJobState,
            Action refreshUi,
            Action<string> logError)
        {
            if (job == null || contentClient == null)
            {
                return;
            }

            try
            {
                var finalStatus = await contentClient.SubscribeAndWaitForJobAsync(
                    job.GAPJobId,
                    status =>
                    {
                        job.GAPStatus = status.status;
                        float displayProgress = GetDisplayProgress(status);
                        string stepMessage = JobStepFormatter.GetProgressMessage(status.step, displayProgress);
                        job.UpdateProgress(displayProgress, stepMessage, status.step);
                        saveJobState?.Invoke(job);
                        refreshUi?.Invoke();
                    });

                if (finalStatus.IsFailed())
                {
                    job.GAPStatus = GapStatus.Failed;
                    job.UpdateProgress(CompletionProgress, "Failed");
                }
                else
                {
                    job.GAPStatus = GapStatus.Completed;
                    job.UpdateProgress(CompletionProgress, "Completed");
                    EditorSfx.PlayGapSubmissionCompleted(job.Id?.ToString() ?? string.Empty);
                }

                saveJobState?.Invoke(job);
                refreshUi?.Invoke();
            }
            catch (Exception ex)
            {
                job.GAPStatus = GapStatus.Failed;
                job.UpdateProgress(CompletionProgress, "Failed");
                saveJobState?.Invoke(job);
                refreshUi?.Invoke();
                logError?.Invoke(ex.Message);
            }
        }

        private static float GetDisplayProgress(Genies.ArtForge.ContentAPI.Models.JobStatus status)
        {
            if (status == null)
            {
                return 0f;
            }

            // Use backend-reported overall progress directly when it's in range.
            if (!float.IsNaN(status.progress) && status.progress >= ProgressMin && status.progress <= ProgressMax)
            {
                float adjusted = AdjustGapProgressByStep(status.step, status.progress);
                return Mathf.Clamp01(adjusted);
            }

            // Fallback to step-based estimate only if overall progress is invalid.
            if (status.steps != null && status.steps.Length > 0 && status.totalSteps > 0)
            {
                int stepIndex = status.stepIndex.HasValue ? status.stepIndex.Value : -1;
                float stepProgress = 0f;

                var runningStep = status.steps.FirstOrDefault(s => s != null && s.status == GapStatus.Running);
                if (runningStep != null)
                {
                    stepProgress = runningStep.progress ?? 0f;
                }
                else
                {
                    var anyStep = status.steps.FirstOrDefault(s => s != null && s.progress.HasValue);
                    if (anyStep != null)
                    {
                        stepProgress = anyStep.progress ?? 0f;
                    }
                }

                if (stepIndex >= 0)
                {
                    return Mathf.Clamp01((stepIndex + stepProgress) / status.totalSteps);
                }
            }

            return 0f;
        }

        private static float AdjustGapProgressByStep(string step, float serverProgress)
        {
            if (string.IsNullOrEmpty(step))
            {
                return serverProgress;
            }

            // Stage offsets requested by user for GAP processing phases.
            switch (step)
            {
                case GapStepProcessing:
                    return ProcessingBaseOffset + ProcessingScale * serverProgress;
                case GapStepAutorig:
                    return AutorigBaseOffset + AutorigScale * serverProgress;
                case GapStepUniversalBuild:
                    return UniversalBuildBaseOffset + UniversalBuildScale * serverProgress;
                default:
                    return serverProgress;
            }
        }
    }
}
