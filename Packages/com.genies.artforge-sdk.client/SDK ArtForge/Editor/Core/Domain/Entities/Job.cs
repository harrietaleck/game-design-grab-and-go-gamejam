using System;
using System.Collections.Generic;
using System.Linq;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Common;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Helpers;
using Newtonsoft.Json;

namespace Genies.Sdk.ArtForge.Editor.Core.Domain.Entities
{
    /// <summary>
    /// Represents a version of a generated reference image
    /// </summary>
    internal class ImageVersion
    {
        public int Iteration { get; set; }
        public string ImagePath { get; set; }
        public string Prompt { get; set; }
        public DateTime GeneratedAt { get; set; }

        // Backend-specific metadata
        public string BackendImageId { get; set; } // ID of the image in the backend system

        public string GetDisplayText() => $"v{Iteration} - {GeneratedAt.ToLocalTime():HH:mm:ss}";
    }

    /// <summary>
    /// Quality settings for asset generation jobs
    /// </summary>
    internal class JobQualitySettings
    {
        public string Pivot { get; set; }
        public string TopologyType { get; set; }
        public int Polycount { get; set; }
        public string TextureResolution { get; set; }
        public string AspectRatio { get; set; }
        public bool GenerateLODs { get; set; }
        public bool GenerateCollider { get; set; }
        public bool GenerateAO { get; set; }
        public bool GenerateTransparent { get; set; }
        public bool GenerateNormalMap { get; set; }
        public bool GenerateMetallicSmoothnessMap { get; set; }
    }


    /// <summary>
    /// Represents a generation or edit job in the processing queue.
    /// Contains all information needed to execute and track the job.
    /// </summary>
    internal class Job : Entity<JobId>
    {
        private const string DecorType = "decor";
        private const string DecorationType = "decoration";
        private const string PropType = "prop";
        private const string Decor2DType = "2d_image";
        private const float ProgressMin = 0f;
        private const float ProgressMax = 1f;
        private const int InitialRetryCount = 0;
        private const int InitialSelectedVersionIndex = 0;
        private const int InitialIteration = -1;
        private const int InitialGapRetryCount = 0;
        private const int DefaultGapMaxRetries = 3;
        private const JobFeedbackType DefaultFeedbackType = JobFeedbackType.None;
        private const string QueuedMessage = "Queued";
        private string _displayName;

        public AssetName AssetName { get; private set; }
        public string DisplayName
        {
            get => string.IsNullOrWhiteSpace(_displayName) ? AssetName?.Value ?? string.Empty : _displayName;
            private set => _displayName = value;
        }
        public JobType Type { get; private set; }
        public JobState State { get; private set; }

        // Job metadata
        public string Description { get; private set; }
        public string Prompt { get; set; } // Public setter for ApprovalReviewDialog retry
        public string AssetTypeId { get; private set; }
        [JsonIgnore]
        public bool IsDecorAsset => IsDecorAssetTypeId(AssetTypeId);
        [JsonIgnore]
        public bool IsDecor2DAsset => IsDecor2DAssetTypeId(AssetTypeId);
        [JsonIgnore]
        public bool IsFailed3DGeneration => State == JobState.Failed &&
                                            ImageConfirmed &&
                                            !string.IsNullOrEmpty(BackendAssetId) &&
                                            !string.IsNullOrEmpty(BackendImageId);
        [JsonIgnore]
        public bool CanRecoverFailed3DGeneration => IsFailed3DGeneration &&
                                                    !string.IsNullOrEmpty(BackendModelGenerationJobId);
        [JsonIgnore]
        public bool HasImageVersions => ImageVersions != null && ImageVersions.Count > 0;
        [JsonIgnore]
        public bool RequiresApprovalConfirmation => RequiresApproval && !ImageConfirmed;
        [JsonIgnore]
        public bool ShouldRestoreAsAwaitingApproval => RequiresApprovalConfirmation && HasImageVersions;
        [JsonIgnore]
        public bool IsApprovalApplied => ImageConfirmed &&
                                         (State == JobState.Queued || State == JobState.Completed);
        public List<string> ReferenceImagePaths { get; private set; }
        public string ReferenceImagePath => ReferenceImagePaths?.Count > 0 ? ReferenceImagePaths[0] : null;

        // Quality settings
        public JobQualitySettings QualitySettings { get; private set; }
        [JsonIgnore]
        public string Pivot => QualitySettings?.Pivot;
        [JsonIgnore]
        public string TopologyType => QualitySettings?.TopologyType;
        [JsonIgnore]
        public int PolycountTarget => QualitySettings?.Polycount ?? 0;
        [JsonIgnore]
        public string TextureResolution => QualitySettings?.TextureResolution;
        [JsonIgnore]
        public string AspectRatio => QualitySettings?.AspectRatio;
        [JsonIgnore]
        public bool GenerateLODs => QualitySettings?.GenerateLODs ?? false;
        [JsonIgnore]
        public bool GenerateCollider => QualitySettings?.GenerateCollider ?? false;
        [JsonIgnore]
        public bool GenerateAO => QualitySettings?.GenerateAO ?? (QualitySettings?.GenerateNormalMap ?? false);
        [JsonIgnore]
        public bool GenerateTransparent => QualitySettings?.GenerateTransparent ?? false;

        // Generation results
        public string GeneratedAssetPath { get; private set; }

        // Progress tracking
        public float Progress { get; private set; }
        public string ProgressMessage { get; private set; }
        public string CurrentStep { get; private set; } // Current backend job step (e.g., "prompt_moderation", "image_generation", "model_creation")
        public JobQueueSubstate QueueSubstate { get; private set; }

        // Timestamps
        public DateTime QueuedAt { get; private set; }
        public DateTime? StartedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        // Image approval workflow
        public bool RequiresApproval { get; private set; }
        public bool ImageConfirmed { get; set; } // Public setter for ApprovalReviewDialog
        public DateTime? AwaitingApprovalSince { get; private set; }

        // Image versioning (for retry/regenerate workflow)
        public List<ImageVersion> ImageVersions { get; private set; }
        public int SelectedVersionIndex { get; set; } // Public setter for ApprovalReviewDialog
        public int CurrentIteration { get; private set; }

        // Error handling
        public string ErrorMessage { get; private set; }
        public int RetryCount { get; private set; }

        // Asset reference (for edit jobs)
        public AssetId TargetAssetId { get; private set; }

        // Backend API metadata (for backend generation workflow)
        public string BackendAssetId { get; set; } // Public setter for BackendAssetOrchestrator
        public string BackendImageId { get; set; } // Public setter for ApprovalReviewDialog/BackendAssetOrchestrator
        public string BackendSubcategory { get; private set; } // Decor subcategory selected when the job was queued
        public string BackendModelId { get; set; } // Public setter for BackendAssetOrchestrator - used for GAP integration
        public string BackendModelGenerationJobId { get; set; } // Tracks model generation job for retry/reconciliation

        // GAP (Game Asset Pipeline) tracking
        public string GAPJobId { get; set; } // GAP job ID from CreateGameAssetAsync
        public DateTime? GAPSubmittedAt { get; set; } // When the asset was submitted to GAP
        public string GAPStatus { get; set; } // GAP job status (e.g., "pending", "processing", "completed", "failed")
        public int GAPRetryCount { get; set; } // Number of GAP retries attempted
        public int GAPMaxRetries { get; set; } // Max GAP retries allowed

        // UI visibility (for "Clear Done" functionality - hides from queue but keeps in history)
        public bool IsVisibleInQueue { get; set; } = true; // Public setter for QueueService

        // User feedback (thumbs up/down)
        public JobFeedbackType UserFeedback { get; private set; }
        public DateTime? UserFeedbackAt { get; private set; }

        private Job() { } // For serialization

        public Job(
            JobId id,
            AssetName assetName,
            JobType type,
            string description,
            string prompt,
            string assetTypeId,
            List<string> referenceImagePaths,
            JobQualitySettings qualitySettings,
            AssetId targetAssetId = null,
            string displayName = null,
            string backendSubcategory = null)
        {
            Id = id;
            AssetName = assetName;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? assetName?.Value : displayName.Trim();
            Type = type;
            Description = description;
            Prompt = prompt;
            AssetTypeId = assetTypeId;
            ReferenceImagePaths = referenceImagePaths ?? new List<string>();
            QualitySettings = qualitySettings ?? new JobQualitySettings();
            TargetAssetId = targetAssetId;
            BackendSubcategory = backendSubcategory?.Trim() ?? string.Empty;

            State = JobState.Queued;
            Progress = ProgressMin;
            ProgressMessage = QueuedMessage;
            QueueSubstate = JobQueueSubstate.None;
            QueuedAt = DateTime.UtcNow;
            RetryCount = InitialRetryCount;

            // Model generation jobs require approval by default
            RequiresApproval = (type == JobType.ModelGeneration);
            ImageConfirmed = false;

            // Initialize image versioning
            ImageVersions = new List<ImageVersion>();
            SelectedVersionIndex = InitialSelectedVersionIndex;
            CurrentIteration = InitialIteration; // Start at -1 so first version becomes 0

            // Initialize GAP tracking
            GAPStatus = GapStatus.NotSubmitted;
            GAPRetryCount = InitialGapRetryCount;
            GAPMaxRetries = DefaultGapMaxRetries;

            // Initialize user feedback
            UserFeedback = DefaultFeedbackType;
            UserFeedbackAt = null;
        }

        /// <summary>
        /// Add a new image version (e.g., after regeneration)
        /// Initially stores temp path, which should be updated after import
        /// </summary>
        public void AddImageVersion(string tempImagePath, string prompt, int iterationNumber)
        {
            var version = new ImageVersion
            {
                Iteration = iterationNumber,
                ImagePath = tempImagePath, // Temp path initially, updated after import
                Prompt = prompt,
                GeneratedAt = DateTime.UtcNow
            };

            ImageVersions.Add(version);
            CurrentIteration = iterationNumber; // Track the latest iteration
            SelectedVersionIndex = ImageVersions.Count - 1; // Auto-select newest version
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Update the image path for a specific version after import
        /// </summary>
        public void UpdateImageVersionPath(int iterationNumber, string historyPath)
        {
            var version = ImageVersions.FirstOrDefault(v => v.Iteration == iterationNumber);
            if (version != null)
            {
                version.ImagePath = historyPath;
                UpdatedAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Get the next iteration number for image generation
        /// </summary>
        public int GetNextIterationNumber()
        {
            return CurrentIteration + 1;
        }

        /// <summary>
        /// Get the image path for the currently selected version
        /// Returns the path from History folder
        /// </summary>
        public string GetSelectedImagePath()
        {
            var selectedVersion = GetSelectedVersion();
            return selectedVersion?.ImagePath;
        }

        /// <summary>
        /// Get the currently selected image version
        /// </summary>
        public ImageVersion GetSelectedVersion()
        {
            if (ImageVersions.Count == 0)
            {
                return null;
            }

            if (SelectedVersionIndex < 0 || SelectedVersionIndex >= ImageVersions.Count)
            {
                return ImageVersions[ImageVersions.Count - 1];
            }

            return ImageVersions[SelectedVersionIndex];
        }

        /// <summary>
        /// Update the job state
        /// </summary>
        public void SetState(JobState newState)
        {
            State = newState;

            switch (newState)
            {
                case JobState.Queued:
                    QueueSubstate = JobQueueSubstate.None;
                    Progress = ProgressMin;
                    CurrentStep = null;
                    ProgressMessage = QueuedMessage;
                    break;

                case JobState.Processing:
                    QueueSubstate = JobQueueSubstate.None;
                    StartedAt = DateTime.UtcNow;
                    Progress = ProgressMin;
                    CurrentStep = null;
                    ProgressMessage = "Processing...";
                    break;

                case JobState.AwaitingImageConfirm:
                    QueueSubstate = JobQueueSubstate.None;
                    AwaitingApprovalSince = DateTime.UtcNow;
                    CurrentStep = null;
                    ProgressMessage = "Awaiting approval";
                    break;

                case JobState.Completed:
                    QueueSubstate = JobQueueSubstate.None;
                    CompletedAt = DateTime.UtcNow;
                    CurrentStep = null;
                    Progress = ProgressMax;
                    ProgressMessage = "Completed";
                    break;

                case JobState.Failed:
                    QueueSubstate = JobQueueSubstate.None;
                    CompletedAt = DateTime.UtcNow;
                    CurrentStep = null;
                    ProgressMessage = "Failed";
                    break;

                case JobState.Cancelled:
                    QueueSubstate = JobQueueSubstate.None;
                    CompletedAt = DateTime.UtcNow;
                    CurrentStep = null;
                    ProgressMessage = "Cancelled";
                    break;
            }

            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Update job progress (0.0 to 1.0)
        /// </summary>
        public void UpdateProgress(float progress, string message, string step = null)
        {
            Progress = Math.Clamp(progress, ProgressMin, ProgressMax);
            ProgressMessage = message;
            if (!string.IsNullOrEmpty(step))
            {
                CurrentStep = step;
            }
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Sets queue-only substate while the job remains in queued state.
        /// </summary>
        public void SetQueueSubstate(JobQueueSubstate substate, string message = null)
        {
            QueueSubstate = substate;
            if (!string.IsNullOrWhiteSpace(message))
            {
                ProgressMessage = message;
            }
            else if (substate == JobQueueSubstate.None)
            {
                ProgressMessage = QueuedMessage;
            }

            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Set error message and mark as failed
        /// </summary>
        public void SetError(string errorMessage)
        {
            ErrorMessage = errorMessage;
            SetState(JobState.Failed);
        }

        /// <summary>
        /// Increment retry count
        /// </summary>
        public void IncrementRetry()
        {
            RetryCount++;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Reset for retry
        /// </summary>
        public void ResetForRetry()
        {
            State = JobState.Queued;
            QueueSubstate = JobQueueSubstate.None;
            Progress = ProgressMin;
            ProgressMessage = "Queued for retry";
            CurrentStep = null;
            ErrorMessage = null;
            StartedAt = null;
            CompletedAt = null;
            IncrementRetry();
        }

        /// <summary>
        /// Reset for backend recovery without consuming a retry attempt.
        /// Used when client state was stale but backend job is still valid.
        /// </summary>
        public void ResetForRecovery()
        {
            State = JobState.Queued;
            QueueSubstate = JobQueueSubstate.None;
            Progress = ProgressMin;
            ProgressMessage = "Queued for recovery";
            CurrentStep = null;
            ErrorMessage = null;
            StartedAt = null;
            CompletedAt = null;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Check if job can be retried
        /// </summary>
        public bool CanRetry(int maxRetries)
        {
            return State == JobState.Failed && RetryCount < maxRetries;
        }

        /// <summary>
        /// Get duration of job execution
        /// </summary>
        public TimeSpan? GetDuration()
        {
            if (StartedAt.HasValue && CompletedAt.HasValue)
            {
                return CompletedAt.Value - StartedAt.Value;
            }

            if (StartedAt.HasValue && State == JobState.Processing)
            {
                return DateTime.UtcNow - StartedAt.Value;
            }

            return null;
        }

        /// <summary>
        /// Check if job is in a terminal state
        /// </summary>
        public bool IsTerminal()
        {
            return State == JobState.Completed ||
                   State == JobState.Failed ||
                   State == JobState.Cancelled;
        }

        /// <summary>
        /// Check if job is awaiting user action (approval)
        /// </summary>
        public bool IsAwaitingUserAction()
        {
            return State == JobState.AwaitingImageConfirm;
        }

        /// <summary>
        /// Set the path to the generated asset
        /// </summary>
        public void SetGeneratedAssetPath(string assetPath)
        {
            GeneratedAssetPath = assetPath;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Mark job as submitted to GAP
        /// </summary>
        public void MarkAsSubmittedToGAP(string gapJobId)
        {
            GAPJobId = gapJobId;
            GAPSubmittedAt = DateTime.UtcNow;
            GAPStatus = GapStatus.Submitted;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Check if job has been submitted to GAP
        /// </summary>
        public bool IsSubmittedToGAP()
        {
            return !string.IsNullOrEmpty(GAPJobId) && GAPStatus == GapStatus.Completed;
        }

        public bool CanRetryGAP()
        {
            return GAPRetryCount < GAPMaxRetries;
        }

        public void PrepareGapRetry()
        {
            GAPRetryCount++;
            GAPJobId = null;
            GAPSubmittedAt = null;
            GAPStatus = GapStatus.NotSubmitted;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Sets the current user feedback for this job.
        /// </summary>
        public void SetUserFeedback(JobFeedbackType feedbackType)
        {
            UserFeedback = feedbackType;
            UserFeedbackAt = feedbackType == JobFeedbackType.None ? null : DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public static bool IsDecorAssetTypeId(string assetTypeId)
        {
            if (string.IsNullOrWhiteSpace(assetTypeId))
            {
                return false;
            }

            if (AssetCategories.IsDecorAssetTypeId(assetTypeId))
            {
                return true;
            }

            string typeLower = assetTypeId.Trim().ToLowerInvariant();
            return typeLower == DecorType ||
                   typeLower == DecorationType ||
                   typeLower == PropType;
        }

        public static bool IsDecor2DAssetTypeId(string assetTypeId)
        {
            if (!IsDecorAssetTypeId(assetTypeId))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(assetTypeId))
            {
                return false;
            }

            string[] parts = assetTypeId.Split(new[] { AssetCategories.Delimiter }, StringSplitOptions.None);
            string specificType = parts.Length > 1 ? parts[1] : string.Empty;
            if (string.IsNullOrWhiteSpace(specificType))
            {
                return false;
            }

            string normalized = specificType.Trim()
                .Replace("_", "")
                .Replace("-", "")
                .Replace(" ", "")
                .ToLowerInvariant();

            return normalized == "2d" ||
                   normalized == "2dimage" ||
                   string.Equals(specificType.Trim(), Decor2DType, StringComparison.OrdinalIgnoreCase);
        }
    }
}
