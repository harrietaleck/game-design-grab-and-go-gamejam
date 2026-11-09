using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Helpers;
using Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Helpers;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using Genies.Sdk.ArtForge.Editor.Core.UI.Helpers;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Genies.Sdk.ArtForge.Editor.Hub.UI
{
    /// <summary>
    /// Controller for the queue panel UI.
    /// Manages job display, progress tracking, and user interactions.
    /// </summary>
    internal class QueuePanelController
    {
        // ------------------------------------------------------------
        // Telemetry signals (Observer subscribes to these)
        // ------------------------------------------------------------
        public static event Action<string, Dictionary<string, object>> QueueCancelJob;
        public static event Action<string, Dictionary<string, object>> QueueOpen2DPreview;
        public static event Action<string, Dictionary<string, object>> QueueOpenFolder;
        public static event Action<string, Dictionary<string, object>> QueueJobRatedUp;
        public static event Action<string, Dictionary<string, object>> QueueJobRatedDown;

        // 3D Gen (UI-driven)
        public static event Action<string, Dictionary<string, object>> Model3DRetryClicked;
        public static event Action<string, Dictionary<string, object>> Model3DCancelClicked;

        // Inventory (UI + pipeline around CreateGameAssetAsync)
        public static event Action<string, Dictionary<string, object>> InventorySendClicked;
        public static event Action<string, Dictionary<string, object>> InventorySendStarted;
        public static event Action<string, Dictionary<string, object>> InventorySendSucceeded;
        public static event Action<string, Dictionary<string, object>> InventorySendFailed;
        public static event Action<string, Dictionary<string, object>> InventoryResendClicked;

        // Errors (UI dialogs + API failures surfaced here)
        public static event Action<string, Dictionary<string, object>> UiErrorShown;
        public static event Action<string, Dictionary<string, object>> ApiRequestFailed;

        private readonly ILogger _logger;
        private readonly VisualElement _queuePanel;
        private readonly ScrollView _queueItemsContainer;
        private readonly ProgressBar _overallProgressBar;
        private readonly Label _progressLabel;
        private readonly Label _emptyQueueLabel;
        private readonly Button _clearCompletedButton;

        private bool _isInitialized;

        // Smooth progress tracking: JobId -> (displayedProgress, lastUpdateTime)
        private readonly System.Collections.Generic.Dictionary<JobId, (float displayedProgress, double lastUpdateTime)> _smoothProgressCache
            = new System.Collections.Generic.Dictionary<JobId, (float, double)>();

        private const float FakeProgressSpeed = 0.01f; // 1% per second when no updates
        private const float AnimationSpeed = 2.0f; // How fast to catch up to real progress
        private const float ProgressPercentScale = 100f;
        private const float FakeProgressCap = 0.95f;
        private const float CompletionProgress = 1f;
        private const float JobPreviewSize = 40f;
        private const float JobPreviewInset = 8f;
        private const float JobPreviewHeaderGap = 4f;

        public QueuePanelController(VisualElement root, ILogger logger = null)
        {
            _logger = logger ?? GeniesArtForgeService.Logger ?? new UnityLogger();
            if (root == null)
            {
                _logger.Error("[QueuePanelController] Root element is null");
                return;
            }

            // Get queue panel references
            _queuePanel = root.Q<VisualElement>("queue-panel");
            _queueItemsContainer = root.Q<ScrollView>("queue-items");
            _overallProgressBar = root.Q<ProgressBar>("overall-progress");
            _progressLabel = root.Q<Label>("progress-label");
            _emptyQueueLabel = root.Q<Label>("empty-queue-label");
            _clearCompletedButton = root.Q<Button>("clear-completed-btn");

            if (_queuePanel == null)
            {
                _logger.Warning("[QueuePanelController] Queue panel not found in UI");
                return;
            }

            Initialize();
        }

        private void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            // Subscribe to queue events
            var queue = GeniesArtForgeService.Queue;
            if (queue != null)
            {
                queue.OnJobQueued += OnJobQueued;
                queue.OnJobStarted += OnJobStarted;
                queue.OnJobCompleted += OnJobCompleted;
                queue.OnJobFailed += OnJobFailed;
                queue.OnJobCancelled += OnJobCancelled;
                queue.OnQueueChanged += RefreshQueueUI;
            }

            // Setup button handlers
            _clearCompletedButton?.RegisterCallback<ClickEvent>(evt => OnClearCompletedClicked());

            _isInitialized = true;

            // Initial refresh
            RefreshQueueUI();
        }

        /// <summary>
        /// Call this from the hub's Update() to refresh processing jobs
        /// </summary>
        public void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            UpdateApprovalPreviewButtons();

            // Only update progress bars, don't recreate the entire UI every frame
            var queue = GeniesArtForgeService.Queue;
            var stats = queue?.GetStats();
            bool hasGapSubmission = queue?.GetAllJobs().Any(GapSubmissionTracker.IsInProgress) ?? false;
            if (stats != null && (stats.Value.processing > 0 || hasGapSubmission))
            {
                UpdateJobProgress();
            }
        }

        /// <summary>
        /// Update progress for existing job elements without recreating them
        /// </summary>
        private void UpdateJobProgress()
        {
            if (_queueItemsContainer == null)
            {
                return;
            }

            var queue = GeniesArtForgeService.Queue;
            if (queue == null)
            {
                return;
            }

            var allJobs = queue.GetAllJobs();
            double currentTime = UnityEngine.Time.realtimeSinceStartupAsDouble;

            // Update progress bars for each existing job element
            foreach (var jobElement in _queueItemsContainer.Children().ToList())
            {
                var jobId = jobElement.userData as JobId;
                if (jobId == null)
                {
                    continue;
                }

                var job = allJobs.FirstOrDefault(j => j.Id.Equals(jobId));
                if (job == null)
                {
                    continue;
                }

                if (job.State != JobState.Processing && !GapSubmissionTracker.IsInProgress(job))
                {
                    continue;
                }

                // Get or initialize smooth progress for this job
                if (!_smoothProgressCache.TryGetValue(jobId, out var cached))
                {
                    cached = (job.Progress, currentTime);
                    _smoothProgressCache[jobId] = cached;
                }

                float targetProgress = job.Progress;
                float displayedProgress = cached.displayedProgress;
                double lastUpdateTime = cached.lastUpdateTime;
                double deltaTime = currentTime - lastUpdateTime;

                // Avoid jarring drops for GAP progress when step mappings change.
                if (GapSubmissionTracker.IsInProgress(job) && targetProgress < displayedProgress)
                {
                    targetProgress = displayedProgress;
                }

                // Reset progress if GAP submission starts after completion (avoid sticking at 100%)
                if (GapSubmissionTracker.IsInProgress(job) && displayedProgress > targetProgress)
                {
                    displayedProgress = targetProgress;
                }

                // Smooth progress animation
                if (displayedProgress < targetProgress)
                {
                    // Real update came in - rush forward to catch up
                    displayedProgress = Mathf.Lerp(displayedProgress, targetProgress, (float)deltaTime * AnimationSpeed);
                    displayedProgress = Mathf.Min(displayedProgress, targetProgress); // Don't overshoot
                }
                else if (displayedProgress < FakeProgressCap) // Cap fake progress at 95%
                {
                    // No update - apply fake slow progress
                    displayedProgress += FakeProgressSpeed * (float)deltaTime;
                    displayedProgress = Mathf.Min(displayedProgress, FakeProgressCap);
                }

                // Update cache
                _smoothProgressCache[jobId] = (displayedProgress, currentTime);

                // Update progress bar
                var progressBar = jobElement.Q<ProgressBar>("job-progress");
                if (progressBar != null)
                {
                    progressBar.value = displayedProgress * ProgressPercentScale;
                }

                // Update two-line status text
                var statusLine = jobElement.Q<Label>("job-status-line");
                if (statusLine != null)
                {
                    statusLine.text = GetJobStatusLineText(job);
                }

            }

            // Clean up cache for completed/removed jobs
            var activeJobIds = allJobs
                .Where(j => j.State == JobState.Processing || GapSubmissionTracker.IsInProgress(j))
                .Select(j => j.Id)
                .ToList();
            var keysToRemove = _smoothProgressCache.Keys.Where(k => !activeJobIds.Any(id => id.Equals(k))).ToList();
            foreach (var key in keysToRemove)
            {
                _smoothProgressCache.Remove(key);
            }
        }

        /// <summary>
        /// Cleanup and unsubscribe from events
        /// </summary>
        public void Dispose()
        {
            if (!_isInitialized)
            {
                return;
            }

            // Unsubscribe from queue events
            var queue = GeniesArtForgeService.Queue;
            if (queue != null)
            {
                queue.OnJobQueued -= OnJobQueued;
                queue.OnJobStarted -= OnJobStarted;
                queue.OnJobCompleted -= OnJobCompleted;
                queue.OnJobFailed -= OnJobFailed;
                queue.OnJobCancelled -= OnJobCancelled;
                queue.OnQueueChanged -= RefreshQueueUI;
            }

            _isInitialized = false;
        }

        private void OnJobQueued(Job job) => RefreshQueueUI();
        private void OnJobStarted(Job job) => RefreshQueueUI();
        private void OnJobCompleted(Job job) => RefreshQueueUI();
        private void OnJobFailed(Job job) => RefreshQueueUI();
        private void OnJobCancelled(Job job) => RefreshQueueUI();

        /// <summary>
        /// Refresh the entire queue UI
        /// </summary>
        private void RefreshQueueUI()
        {
            if (_queueItemsContainer == null)
            {
                return;
            }

            var queue = GeniesArtForgeService.Queue;
            if (queue == null)
            {
                return;
            }

            var visibleJobs = queue.GetVisibleJobs(); // Only show jobs not hidden by "Clear Done"
            var stats = queue.GetStats();

            // Update progress bar
            int totalProcessed = stats.completed + stats.failed;
            int grandTotal = stats.queued + stats.processing + totalProcessed;

            if (_overallProgressBar != null && _progressLabel != null)
            {
                float progress = grandTotal > 0 ? (float)totalProcessed / grandTotal : 0f;
                _overallProgressBar.value = progress * ProgressPercentScale;
                _progressLabel.text = $"{totalProcessed}/{grandTotal} jobs";
            }

            // Show/hide empty label
            if (_emptyQueueLabel != null)
            {
                _emptyQueueLabel.style.display = visibleJobs.Any() ? DisplayStyle.None : DisplayStyle.Flex;
            }

            // Clear and rebuild job list
            _queueItemsContainer.Clear();

            foreach (var job in visibleJobs.OrderBy(j => j.QueuedAt))
            {
                var jobElement = CreateJobElement(job);
                _queueItemsContainer.Add(jobElement);
            }

            // Sync preview button visibility with dialog state
            UpdateApprovalPreviewButtons();
        }

        /// <summary>
        /// Create a visual element for a single job
        /// </summary>
        private VisualElement CreateJobElement(Job job)
        {
            var container = new VisualElement();
            container.AddToClassList("queue-job-item");
            container.style.position = Position.Relative;

            // Store job ID for later updates
            container.userData = job.Id;

            // Colored accent bar based on state
            var accentBar = new VisualElement();
            accentBar.style.position = Position.Absolute;
            accentBar.style.left = 0;
            accentBar.style.top = 0;
            accentBar.style.bottom = 0;
            accentBar.style.width = 3;
            accentBar.style.backgroundColor = GapSubmissionTracker.IsInProgress(job)
                ? new Color(0.2f, 0.6f, 0.2f)
                : GetJobStateColor(job.State);
            container.Add(accentBar);

            bool hasPreview = JobPreviewThumbnailUI.TryCreatePreviewElement(job, JobPreviewSize, out VisualElement previewElement);
            if (hasPreview)
            {
                previewElement.style.position = Position.Absolute;
                previewElement.style.left = JobPreviewInset;
                previewElement.style.top = JobPreviewInset;
                container.Add(previewElement);
            }

            var headerSection = new VisualElement();
            if (hasPreview)
            {
                headerSection.style.marginLeft = JobPreviewInset + JobPreviewSize + JobPreviewHeaderGap;
                headerSection.style.minHeight = JobPreviewSize;
            }
            container.Add(headerSection);

            // Job header (name + status)
            var header = new VisualElement();
            header.AddToClassList("queue-job-header");
            header.style.alignItems = Align.Center;
            headerSection.Add(header);

            var nameLabel = new Label(job.DisplayName);
            nameLabel.AddToClassList("queue-job-name");
            nameLabel.style.color = job.State == JobState.Failed
                ? StyleHelpers.Colors.Error
                : StyleHelpers.Colors.TextPrimary;
            nameLabel.style.flexGrow = 1;
            nameLabel.style.flexShrink = 1;
            nameLabel.style.minWidth = 0;
            nameLabel.style.whiteSpace = WhiteSpace.Normal;
            nameLabel.style.marginRight = 6;
            header.Add(nameLabel);

            var headerActions = new VisualElement();
            headerActions.style.flexDirection = FlexDirection.Row;
            headerActions.style.alignItems = Align.Center;
            headerActions.style.flexShrink = 0;
            header.Add(headerActions);

            var statusBadge = new Label(GetQueueStatusBadgeText(job));
            statusBadge.style.fontSize = 11;
            statusBadge.style.paddingTop = 3;
            statusBadge.style.paddingBottom = 3;
            statusBadge.style.paddingLeft = 8;
            statusBadge.style.paddingRight = 8;
            statusBadge.style.borderTopLeftRadius = 3;
            statusBadge.style.borderTopRightRadius = 3;
            statusBadge.style.borderBottomLeftRadius = 3;
            statusBadge.style.borderBottomRightRadius = 3;
            var (statusBackground, statusForeground) = GetQueueStatusBadgeColors(job);
            statusBadge.style.backgroundColor = statusBackground;
            statusBadge.style.color = statusForeground;
            statusBadge.style.marginRight = 6;
            statusBadge.style.flexShrink = 0;
            headerActions.Add(statusBadge);

            if (job.State == JobState.Completed)
            {
                var feedbackButton = JobFeedbackUI.CreateInlineMenuButton(
                    job,
                    feedbackType =>
                    {
                        GeniesArtForgeService.Queue?.SaveJobState(job);
                        EmitJobFeedbackTelemetry(job, feedbackType);
                    },
                    useButtonBackground: true,
                    buttonWidth: 22f,
                    buttonHeight: 22f,
                    iconFontSize: 20);
                headerActions.Add(feedbackButton);
            }

            // Add tooltip with job description if available
            if (!string.IsNullOrEmpty(job.Description))
            {
                nameLabel.tooltip = $"Description: {job.Description}";
            }

            // Progress bar with two-line text (only for processing jobs)
            if (job.State == JobState.Processing || job.State == JobState.Queued || GapSubmissionTracker.IsInProgress(job))
            {
                var progressContainer = new VisualElement();
                progressContainer.style.marginTop = 4;
                progressContainer.style.marginBottom = 4;
                progressContainer.style.marginLeft = 0;

                // Progress bar
                var progressBar = new ProgressBar();
                progressBar.name = "job-progress";
                progressBar.AddToClassList("queue-job-progress");
                progressBar.value = job.Progress * ProgressPercentScale;
                progressBar.style.height = 20;
                progressBar.style.marginLeft = 0;
                progressContainer.Add(progressBar);

                // Two-line status text below progress bar
                var statusLine = new Label(GetJobStatusLineText(job));
                statusLine.name = "job-status-line";
                statusLine.style.fontSize = 11;
                statusLine.style.color = StyleHelpers.Colors.TextSecondary;
                statusLine.style.marginTop = 2;
                progressContainer.Add(statusLine);

                container.Add(progressContainer);
            }
            else if (job.State == JobState.Failed)
            {
                // Failed job - show error with expandable details
                var errorContainer = new VisualElement();
                errorContainer.style.marginTop = 4;
                errorContainer.style.marginBottom = 4;
                errorContainer.style.backgroundColor = new Color(0.3f, 0.1f, 0.1f, 0.3f); // Dark red tint
                errorContainer.style.paddingLeft = 8;
                errorContainer.style.paddingRight = 8;
                errorContainer.style.paddingTop = 6;
                errorContainer.style.paddingBottom = 6;

                // Error header with expand/collapse button
                var errorHeader = new VisualElement();
                errorHeader.style.flexDirection = FlexDirection.Row;
                errorHeader.style.alignItems = Align.Center;
                errorHeader.style.justifyContent = Justify.SpaceBetween;

                var errorTitle = new Label("Error");
                errorTitle.style.color = StyleHelpers.Colors.Error;
                errorTitle.style.fontSize = 12;
                errorTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                errorHeader.Add(errorTitle);

                // Expand/collapse button (only if error message exists)
                Button expandButton = null;
                VisualElement errorDetails = null;

                if (!string.IsNullOrEmpty(job.ErrorMessage))
                {
                    expandButton = new Button();
                    expandButton.name = "error-expand-btn";
                    expandButton.text = "▼ Show Details";
                    expandButton.style.fontSize = 10;
                    expandButton.style.height = 20;
                    expandButton.style.paddingLeft = 8;
                    expandButton.style.paddingRight = 8;
                    errorHeader.Add(expandButton);
                }

                errorContainer.Add(errorHeader);

                // Error details (expandable)
                if (!string.IsNullOrEmpty(job.ErrorMessage))
                {
                    errorDetails = new VisualElement();
                    errorDetails.name = "error-details";
                    errorDetails.style.marginTop = 6;
                    errorDetails.style.display = DisplayStyle.None; // Hidden by default

                    var errorLabel = new Label(job.ErrorMessage);
                    errorLabel.style.color = new Color(1f, 0.8f, 0.8f); // Light red text
                    errorLabel.style.fontSize = 11;
                    errorLabel.style.whiteSpace = WhiteSpace.Normal; // Allow text wrapping
                    errorDetails.Add(errorLabel);

                    errorContainer.Add(errorDetails);

                    // Toggle expand/collapse
                    bool isExpanded = false;
                    expandButton.clicked += () =>
                    {
                        isExpanded = !isExpanded;
                        errorDetails.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                        expandButton.text = isExpanded ? "▲ Hide Details" : "▼ Show Details";
                    };
                }

                container.Add(errorContainer);
            }
            // Action buttons
            if (!job.IsTerminal())
            {
                var actionsContainer = new VisualElement();
                actionsContainer.AddToClassList("queue-job-actions");
                actionsContainer.style.marginLeft = 0;
                container.Add(actionsContainer);

                if (job.RequiresApproval)
                {
                    var openPreviewButton = new Button(() =>
                    {
                        QueueOpen2DPreview?.Invoke(
                            "queue_open_2d_preview",
                            new Dictionary<string, object>
                            {
                                { "job_id", job.Id?.ToString() ?? "" }
                            }
                        );

                        ApprovalReviewDialog.ShowForJob(job);
                    })
                    {
                        text = "Open Preview"
                    };
                    openPreviewButton.name = "open-preview-btn";
                    openPreviewButton.AddToClassList("queue-job-action-btn");
                    openPreviewButton.style.backgroundColor = new Color(0.2f, 0.4f, 0.6f);
                    openPreviewButton.style.display = JobHasPreviewReady(job) ? DisplayStyle.Flex : DisplayStyle.None;
                    actionsContainer.Add(openPreviewButton);
                }

                var cancelButton = new Button(() =>
                {
                    QueueCancelJob?.Invoke(
                        "queue_cancel_job",
                        new Dictionary<string, object>
                        {
                            { "job_id", job.Id?.ToString() ?? "" }
                        }
                    );

                    // Emit 3D cancel only when it's actually a 3D pipeline job / failure surface.
                    // Otherwise we'd count "cancel approval" as 3D cancel.
                    if (job.IsFailed3DGeneration || job.State == JobState.Processing && job.ImageConfirmed)
                    {
                        int attempt = Math.Max(0, job.RetryCount);

                        Model3DCancelClicked?.Invoke(
                            "model3d_cancel_clicked",
                            new Dictionary<string, object>
                            {
                                { "job_id", job.Id?.ToString() ?? "" },
                                { "attempt", attempt }
                            }
                        );
                    }

                    GeniesArtForgeService.Queue.CancelJob(job.Id);
                })
                {
                    text = "Cancel"
                };
                cancelButton.AddToClassList("queue-job-action-btn");
                actionsContainer.Add(cancelButton);
            }
            else if (job.State == JobState.Completed)
            {
                var actionsContainer = new VisualElement();
                actionsContainer.AddToClassList("queue-job-actions");
                actionsContainer.style.marginLeft = 0;
                container.Add(actionsContainer);

                var openFolderButton = new Button(() =>
                {
                    QueueOpenFolder?.Invoke(
                        "queue_open_folder",
                        new Dictionary<string, object>
                        {
                            { "job_id", job.Id?.ToString() ?? "" }
                        }
                    );

                    SelectAssetInProject(job);
                })
                {
                    text = "Open"
                };
                openFolderButton.AddToClassList("queue-job-action-btn");
                openFolderButton.style.backgroundColor = new Color(0.2f, 0.3f, 0.2f);
                actionsContainer.Add(openFolderButton);

                if (!string.IsNullOrEmpty(job.BackendAssetId) && !string.IsNullOrEmpty(job.BackendModelId))
                {
                    bool isDecorAsset = job.IsDecorAsset;
                    if (!isDecorAsset)
                    {
                        bool isSubmittingToGAP = GapSubmissionTracker.IsInProgress(job);
                        bool canRetryGap = job.GAPStatus == GapStatus.Failed && job.CanRetryGAP();
                        bool gapIncompatible = job.GAPStatus == GapStatus.Failed && !job.CanRetryGAP();

                        var sendToGAPButton = new Button(() =>
                        {
                            string destination = "inventory";
                            int attempt = Math.Max(0, job.GAPRetryCount);

                            InventorySendClicked?.Invoke(
                                "inventory_send_clicked",
                                new Dictionary<string, object>
                                {
                                    { "job_id", job.Id?.ToString() ?? "" },
                                    { "destination", destination }
                                }
                            );

                            if (canRetryGap)
                            {
                                string reason = "failed_before";
                                InventoryResendClicked?.Invoke(
                                    "inventory_resend_clicked",
                                    new Dictionary<string, object>
                                    {
                                        { "job_id", job.Id?.ToString() ?? "" },
                                        { "attempt", attempt },
                                        { "reason", reason }
                                    }
                                );
                            }

                            SendToGAPAsync(job);
                        })
                        {
                            text = isSubmittingToGAP
                                ? "Submitting to Inventory..."
                                : (gapIncompatible
                                    ? "Inventory Incompatible"
                                    : (canRetryGap
                                        ? $"Retry Inventory ({job.GAPRetryCount + 1}/{job.GAPMaxRetries})"
                                        : (job.IsSubmittedToGAP() ? "Available In Inventory" : "Submit to Inventory")))
                        };
                        sendToGAPButton.AddToClassList("queue-job-action-btn");
                        sendToGAPButton.style.backgroundColor = isSubmittingToGAP
                            ? new Color(0.2f, 0.6f, 0.2f)
                            : (job.IsSubmittedToGAP()
                                ? new Color(0.2f, 0.6f, 0.2f)
                                : new Color(0.2f, 0.4f, 0.6f));
                        if (gapIncompatible)
                        {
                            sendToGAPButton.style.backgroundColor = new Color(0.4f, 0.2f, 0.2f);
                        }
                        sendToGAPButton.SetEnabled(!job.IsSubmittedToGAP() && !isSubmittingToGAP && !gapIncompatible);
                        actionsContainer.Add(sendToGAPButton);
                    }
                }
            }
            else if (job.IsFailed3DGeneration)
            {
                var queueService = GeniesArtForgeService.Queue;
                int maxRetries = queueService?.MaxRetryAttempts ?? 2;
                bool canRetry = queueService != null && job.CanRetry(maxRetries);
                bool canCheckServerStatus = !string.IsNullOrEmpty(job.BackendModelGenerationJobId);

                if (canRetry || canCheckServerStatus)
                {
                    var actionsContainer = new VisualElement();
                    actionsContainer.AddToClassList("queue-job-actions");
                    actionsContainer.style.marginLeft = 0;
                    container.Add(actionsContainer);

                    if (canRetry)
                    {
                        var retry3DButton = new Button(() => RetryFailed3DJobAsync(job))
                        {
                            text = $"Retry 3D ({job.RetryCount + 1}/{maxRetries})"
                        };
                        retry3DButton.AddToClassList("queue-job-action-btn");
                        retry3DButton.style.backgroundColor = new Color(0.35f, 0.5f, 0.25f);
                        actionsContainer.Add(retry3DButton);
                    }

                    if (canCheckServerStatus)
                    {
                        var checkStatusButton = new Button(() => CheckServerStatusAsync(job))
                        {
                            text = "Check Server Status"
                        };
                        checkStatusButton.AddToClassList("queue-job-action-btn");
                        checkStatusButton.style.backgroundColor = new Color(0.25f, 0.35f, 0.55f);
                        actionsContainer.Add(checkStatusButton);
                    }
                }
            }

            return container;
        }

        private static void EmitJobFeedbackTelemetry(Job job, JobFeedbackType feedbackType)
        {
            if (job == null)
            {
                return;
            }

            var properties = new Dictionary<string, object>
            {
                { "job_id", job.Id?.ToString() ?? "" },
                { "asset_type", job.AssetTypeId ?? "" },
                { "stage", "3d" },
                { "context", "queue" },
            };

            switch (feedbackType)
            {
                case JobFeedbackType.ThumbsUp:
                    QueueJobRatedUp?.Invoke("result_thumbs_up", properties);
                    break;
                case JobFeedbackType.ThumbsDown:
                    QueueJobRatedDown?.Invoke("result_thumbs_down", properties);
                    break;
            }
        }

        private Color GetJobStateColor(JobState state)
        {
            return state switch
            {
                JobState.Queued => new Color(0.6f, 0.6f, 0.6f),
                JobState.Processing => StyleHelpers.Colors.Accent,
                JobState.Completed => StyleHelpers.Colors.Success,
                JobState.Failed => StyleHelpers.Colors.Error,
                JobState.Cancelled => new Color(0.5f, 0.5f, 0.5f),
                _ => new Color(0.6f, 0.6f, 0.6f)
            };
        }

        private static string GetQueueStatusBadgeText(Job job)
        {
            if (job == null)
            {
                return "Unknown";
            }

            if (GapSubmissionTracker.IsInProgress(job))
            {
                return "⟳ Submitting";
            }

            return job.State switch
            {
                JobState.Queued => "Queued",
                JobState.Processing => "Processing",
                JobState.AwaitingImageConfirm => "Awaiting",
                JobState.Completed => "Completed",
                JobState.Failed => "Failed",
                JobState.Cancelled => "Cancelled",
                _ => "Pending"
            };
        }

        private static (Color background, Color foreground) GetQueueStatusBadgeColors(Job job)
        {
            if (job == null)
            {
                return (new Color(0.3f, 0.3f, 0.3f, 0.35f), new Color(0.8f, 0.8f, 0.8f));
            }

            if (GapSubmissionTracker.IsInProgress(job))
            {
                return (new Color(0.2f, 0.6f, 0.2f, 0.3f), new Color(0.6f, 1f, 0.6f));
            }

            return job.State switch
            {
                JobState.Completed => (new Color(0.2f, 0.6f, 0.2f, 0.3f), new Color(0.5f, 1f, 0.5f)),
                JobState.Failed => (new Color(0.6f, 0.2f, 0.2f, 0.3f), new Color(1f, 0.5f, 0.5f)),
                JobState.Cancelled => (new Color(0.4f, 0.4f, 0.4f, 0.3f), new Color(0.85f, 0.85f, 0.85f)),
                JobState.AwaitingImageConfirm => (new Color(0.2f, 0.4f, 0.6f, 0.3f), new Color(0.7f, 0.9f, 1f)),
                JobState.Processing => (new Color(0.2f, 0.4f, 0.6f, 0.3f), new Color(0.7f, 0.9f, 1f)),
                JobState.Queued => (new Color(0.35f, 0.35f, 0.35f, 0.3f), new Color(0.85f, 0.85f, 0.85f)),
                _ => (new Color(0.3f, 0.3f, 0.3f, 0.35f), new Color(0.8f, 0.8f, 0.8f))
            };
        }

        private static string GetJobStatusLineText(Job job)
        {
            if (job == null)
            {
                return "Processing";
            }

            if (GapSubmissionTracker.IsInProgress(job))
            {
                return GapSubmissionTracker.GetStatusText(job);
            }

            if (job.State == JobState.Queued)
            {
                if (job.QueueSubstate == JobQueueSubstate.AwaitingLogin)
                {
                    return "Login to continue";
                }

                if (job.QueueSubstate == JobQueueSubstate.ReconnectingBackend)
                {
                    return "Reconnecting backend...";
                }

                if (!string.IsNullOrEmpty(job.ProgressMessage))
                {
                    return job.ProgressMessage;
                }
            }

            if (job.State == JobState.Processing || job.State == JobState.AwaitingImageConfirm)
            {
                if (!string.IsNullOrEmpty(job.CurrentStep))
                {
                    return JobStepFormatter.GetStageName(job.CurrentStep);
                }

                if (!string.IsNullOrEmpty(job.ProgressMessage))
                {
                    return job.ProgressMessage;
                }
            }

            return JobStepFormatter.GetStageName(job.CurrentStep);
        }

        private void SelectAssetInProject(Job job)
        {
            if (job == null || job.AssetName == null)
            {
                return;
            }

            string assetName = job.AssetName.Value;
            if (job.IsDecor2DAsset)
            {
                string referenceImagePath = BrandingConstants.AssetPaths.GetReferenceImagePath(assetName);
                var referenceAsset = AssetDatabase.LoadMainAssetAtPath(referenceImagePath);
                if (referenceAsset != null)
                {
                    EditorUtility.FocusProjectWindow();
                    Selection.activeObject = referenceAsset;
                    EditorGUIUtility.PingObject(referenceAsset);
                    _logger.Info($"[QueuePanelController] Selected reference image in Project window: {referenceImagePath}");
                    return;
                }

                string refImagesFolderPath = BrandingConstants.AssetPaths.GetRefImagesFolder(assetName).TrimEnd('/');
                if (AssetDatabase.IsValidFolder(refImagesFolderPath))
                {
                    var refImagesFolderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(refImagesFolderPath);
                    if (refImagesFolderAsset != null)
                    {
                        EditorUtility.FocusProjectWindow();
                        Selection.activeObject = refImagesFolderAsset;
                        EditorGUIUtility.PingObject(refImagesFolderAsset);
                        _logger.Info($"[QueuePanelController] Selected RefImages folder in Project window: {refImagesFolderPath}");
                        return;
                    }
                }

                _logger.Warning($"[QueuePanelController] Decor 2D reference image not found: {referenceImagePath}");
                return;
            }

            string prefabPath = BrandingConstants.AssetPaths.GetPrefabPath(assetName);
            var prefabAsset = AssetDatabase.LoadMainAssetAtPath(prefabPath);
            if (prefabAsset != null)
            {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = prefabAsset;
                EditorGUIUtility.PingObject(prefabAsset);
                _logger.Info($"[QueuePanelController] Selected prefab in Project window: {prefabPath}");
                return;
            }

            string assetFolderPath = BrandingConstants.AssetPaths.GetPrefabsFolder(assetName);
            assetFolderPath = assetFolderPath.TrimEnd('/');

            if (!AssetDatabase.IsValidFolder(assetFolderPath))
            {
                _logger.Warning($"[QueuePanelController] Prefab not found: {prefabPath}");
                return;
            }

            var folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(assetFolderPath);
            if (folderAsset == null)
            {
                _logger.Warning($"[QueuePanelController] Unable to load prefab: {prefabPath}");
                return;
            }

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = folderAsset;
            EditorGUIUtility.PingObject(folderAsset);
            _logger.Info($"[QueuePanelController] Selected folder in Project window: {assetFolderPath}");
        }

        private void OnClearCompletedClicked()
        {
            var queue = GeniesArtForgeService.Queue;
            if (queue == null)
            {
                return;
            }

            queue.ClearCompleted();
        }

        private async void RetryFailed3DJobAsync(Job job)
        {
            var queue = GeniesArtForgeService.Queue;
            if (queue == null || job == null)
            {
                return;
            }

            if (!job.CanRetry(queue.MaxRetryAttempts))
            {
                UiErrorShown?.Invoke(
                    "ui_error_shown",
                    new Dictionary<string, object>
                    {
                        { "error_code", "retry_limit_reached" },
                        { "surface", "dialog" },
                        { "actionable", true }
                    }
                );

                EditorUtility.DisplayDialog("Retry Limit Reached",
                    $"'{job.DisplayName}' has reached the retry limit ({queue.MaxRetryAttempts}).",
                    "OK");
                return;
            }

            int attempt = Math.Max(0, job.RetryCount);

            Model3DRetryClicked?.Invoke(
                "model3d_retry_clicked",
                new Dictionary<string, object>
                {
                    { "job_id", job.Id?.ToString() ?? "" },
                    { "attempt", attempt },
                    { "retry_source", "dialog" }
                }
            );

            string serverStatusSummary = await GetServerStatusSummaryAsync(job);
            if (!string.IsNullOrEmpty(serverStatusSummary))
            {
                _logger.Info($"[QueuePanelController] Retrying '{job.DisplayName}' after server status check: {serverStatusSummary}");
            }

            queue.RetryJob(job.Id);
            RefreshQueueUI();
        }

        private async void CheckServerStatusAsync(Job job)
        {
            string summary = await GetServerStatusSummaryAsync(job);
            if (string.IsNullOrEmpty(summary))
            {
                summary = "Server status unavailable.";
            }

            EditorUtility.DisplayDialog("Server Job Status", summary, "OK");
        }

        private async Task<string> GetServerStatusSummaryAsync(Job job)
        {
            if (job == null)
            {
                return "Job not found.";
            }

            if (string.IsNullOrEmpty(job.BackendModelGenerationJobId))
            {
                return "No tracked backend 3D job ID for this job yet.";
            }

            var contentClient = GeniesArtForgeService.ContentClient;
            if (contentClient == null || !contentClient.IsAuthenticated)
            {
                return "Backend is not authenticated. Please login in Settings.";
            }

            double startedAt = EditorApplication.timeSinceStartup;

            try
            {
                var status = await contentClient.GetJobStatusAsync(job.BackendModelGenerationJobId);
                if (status == null)
                {
                    return $"No status returned for backend job {job.BackendModelGenerationJobId}.";
                }

                string step = string.IsNullOrEmpty(status.step) ? "n/a" : status.step;
                return $"Job ID: {job.BackendModelGenerationJobId}\nStatus: {status.status}\nStep: {step}\nProgress: {status.progress * 100f:F0}%";
            }
            catch (System.Exception ex)
            {
                long latencyMs = (long)Math.Max(0d, (EditorApplication.timeSinceStartup - startedAt) * 1000d);

                ApiRequestFailed?.Invoke(
                    "api_request_failed",
                    new Dictionary<string, object>
                    {
                        { "endpoint_name", "GetJobStatusAsync" },
                        { "http_status", "unknown" },
                        { "error_code", ex.GetType().Name },
                        { "latency_ms", latencyMs }
                    }
                );

                _logger.Warning($"[QueuePanelController] Failed to fetch server status for {job.BackendModelGenerationJobId}: {ex.Message}");
                return $"Failed to fetch server status for {job.BackendModelGenerationJobId}:\n{ex.Message}";
            }
        }

        private void UpdateApprovalPreviewButtons()
        {
            if (_queueItemsContainer == null)
            {
                return;
            }

            var queue = GeniesArtForgeService.Queue;
            if (queue == null)
            {
                return;
            }

            var jobsById = queue.GetAllJobs().ToDictionary(j => j.Id, j => j);

            foreach (var jobElement in _queueItemsContainer.Children())
            {
                var jobId = jobElement.userData as JobId;
                if (jobId == null)
                {
                    continue;
                }

                if (!jobsById.TryGetValue(jobId, out var job))
                {
                    continue;
                }

                var openPreviewButton = jobElement.Q<Button>("open-preview-btn");
                if (openPreviewButton == null)
                {
                    continue;
                }

                openPreviewButton.style.display = JobHasPreviewReady(job) ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private bool JobHasPreviewReady(Job job)
        {
            if (job == null)
            {
                return false;
            }

            if (job.State != JobState.AwaitingImageConfirm)
            {
                return false;
            }

            string previewImagePath = job.GetSelectedImagePath();
            if (string.IsNullOrEmpty(previewImagePath))
            {
                return false;
            }

            string fullImagePath = Path.Combine(UnityEngine.Application.dataPath, "..", previewImagePath);
            fullImagePath = Path.GetFullPath(fullImagePath);
            return File.Exists(fullImagePath);
        }

        private async void SendToGAPAsync(Job job)
        {
            if (job == null)
            {
                return;
            }

            string assetId = job.BackendAssetId;
            string modelId = job.BackendModelId;
            string assetName = job.AssetName.Value;
            bool shouldConsumeGapRetry = job.GAPStatus == GapStatus.Failed && job.CanRetryGAP();
            string previousGapStatus = job.GAPStatus;
            string previousGapJobId = job.GAPJobId;
            DateTime? previousGapSubmittedAt = job.GAPSubmittedAt;

            int attempt = Math.Max(0, job.GAPRetryCount);

            double startedAt = EditorApplication.timeSinceStartup;

            try
            {
                _logger.Info($"[QueuePanelController] Sending to GAP - Asset: {assetId}, Model: {modelId}, Name: {assetName}");

                bool backendReady = await BackendConnectionRecoveryHelper.EnsureConnectedOrPromptAsync(
                    _logger,
                    "Real-time backend connection is unavailable. Please reconnect in Settings before submitting to Inventory.");
                if (!backendReady)
                {
                    return;
                }

                var contentClient = GeniesArtForgeService.ContentClient;
                if (contentClient == null || !contentClient.IsAuthenticated)
                {
                    return;
                }

                InventorySendStarted?.Invoke(
                    "inventory_send_started",
                    new Dictionary<string, object>
                    {
                        { "job_id", job.Id?.ToString() ?? "" },
                        { "attempt", attempt }
                    }
                );

                var queue = GeniesArtForgeService.Queue;
                if (queue != null)
                {
                    GapSubmissionTracker.MarkSubmitting(job);
                    queue.SaveJobState(job);
                    _smoothProgressCache.Remove(job.Id);
                }

                var result = await contentClient.CreateGameAssetAsync(assetId, modelId);
                if (shouldConsumeGapRetry)
                {
                    job.PrepareGapRetry();
                }

                long latencyMs = (long)Math.Max(0d, (EditorApplication.timeSinceStartup - startedAt) * 1000d);

                if (result != null && !string.IsNullOrEmpty(result.jobId))
                {
                    _logger.Info($"[QueuePanelController] ✓ GAP job created - Job ID: {result.jobId}");

                    if (queue != null)
                    {
                        GapSubmissionTracker.MarkProcessing(job, result.jobId);
                        queue.SaveJobState(job);
                        _logger.Info("[QueuePanelController] Job marked as submitted to GAP, tracking status");

                        await GapSubmissionTracker.TrackAsync(
                            job,
                            contentClient,
                            queue.SaveJobState,
                            RefreshQueueUI,
                            message => _logger.Error($"[QueuePanelController] Failed to track GAP job status: {message}"));

                        if (job.GAPStatus == GapStatus.Completed)
                        {
                            InventorySendSucceeded?.Invoke(
                                "inventory_send_succeeded",
                                new Dictionary<string, object>
                                {
                                    { "job_id", job.Id?.ToString() ?? "" },
                                    { "attempt", attempt },
                                    { "latency_ms", latencyMs }
                                }
                            );

                            EditorUtility.DisplayDialog("Success",
                                $"Asset '{assetName}' submitted to Inventory!\n\nInventory Job ID: {result.jobId}",
                                "OK");
                        }
                        else if (job.GAPStatus == GapStatus.Failed)
                        {
                            InventorySendFailed?.Invoke(
                                "inventory_send_failed",
                                new Dictionary<string, object>
                                {
                                    { "job_id", job.Id?.ToString() ?? "" },
                                    { "attempt", attempt },
                                    { "error_code", "gap_job_failed" },
                                    { "latency_ms", latencyMs },
                                    { "is_retryable", job.CanRetryGAP() }
                                }
                            );

                            UiErrorShown?.Invoke(
                                "ui_error_shown",
                                new Dictionary<string, object>
                                {
                                    { "error_code", "inventory_send_failed" },
                                    { "surface", "dialog" },
                                    { "actionable", job.CanRetryGAP() }
                                }
                            );

                            EditorUtility.DisplayDialog("Submit to Inventory Failed",
                                $"Failed to submit asset to Inventory.\n\nInventory Job ID: {result.jobId}",
                                "OK");
                        }
                    }

                    RefreshQueueUI();
                }
                else
                {
                    _logger.Warning("[QueuePanelController] GAP job created but no job ID returned");

                    InventorySendSucceeded?.Invoke(
                        "inventory_send_succeeded",
                        new Dictionary<string, object>
                        {
                            { "job_id", job.Id?.ToString() ?? "" },
                            { "attempt", attempt },
                            { "latency_ms", latencyMs }
                        }
                    );

                    if (queue != null)
                    {
                        job.GAPStatus = GapStatus.NotSubmitted;
                        job.UpdateProgress(CompletionProgress, "Completed");
                        queue.SaveJobState(job);
                    }

                    EditorSfx.PlayGapSubmissionCompleted(job.Id?.ToString() ?? string.Empty);

                    EditorUtility.DisplayDialog("Partial Success",
                        $"Asset '{assetName}' was submitted to Inventory, but no job ID was returned.",
                        "OK");
                }
            }
            catch (System.Exception ex)
            {
                long latencyMs = (long)Math.Max(0d, (EditorApplication.timeSinceStartup - startedAt) * 1000d);

                ApiRequestFailed?.Invoke(
                    "api_request_failed",
                    new Dictionary<string, object>
                    {
                        { "endpoint_name", "CreateGameAssetAsync" },
                        { "http_status", "unknown" },
                        { "error_code", ex.GetType().Name },
                        { "latency_ms", latencyMs }
                    }
                );

                if (BackendConnectionRecoveryHelper.IsRecoverableConnectionError(ex))
                {
                    RestoreGapSubmissionState(job, previousGapStatus, previousGapJobId, previousGapSubmittedAt);

                    var queueService = GeniesArtForgeService.Queue;
                    queueService?.SaveJobState(job);
                    RefreshQueueUI();

                    await BackendConnectionRecoveryHelper.EnsureConnectedOrPromptAsync(
                        _logger,
                        "Real-time backend connection is reconnecting. Please reconnect in Settings before retrying Inventory submission.");
                    return;
                }

                InventorySendFailed?.Invoke(
                    "inventory_send_failed",
                    new Dictionary<string, object>
                    {
                        { "job_id", job.Id?.ToString() ?? "" },
                        { "attempt", attempt },
                        { "error_code", ex.GetType().Name },
                        { "latency_ms", latencyMs },
                        { "is_retryable", job.CanRetryGAP() }
                    }
                );

                UiErrorShown?.Invoke(
                    "ui_error_shown",
                    new Dictionary<string, object>
                    {
                        { "error_code", "inventory_send_failed" },
                        { "surface", "dialog" },
                        { "actionable", job.CanRetryGAP() }
                    }
                );

                _logger.Error($"[QueuePanelController] Failed to send to GAP: {ex.Message}");
                var queue = GeniesArtForgeService.Queue;
                if (queue != null)
                {
                    job.GAPStatus = GapStatus.Failed;
                    job.UpdateProgress(CompletionProgress, "Completed");
                    queue.SaveJobState(job);
                }
                EditorUtility.DisplayDialog("Submit to Inventory Failed", $"Failed to submit asset to Inventory:\n\n{ex.Message}", "OK");
            }
        }

        private static void RestoreGapSubmissionState(
            Job job,
            string previousGapStatus,
            string previousGapJobId,
            DateTime? previousGapSubmittedAt)
        {
            if (job == null)
            {
                return;
            }

            job.GAPStatus = previousGapStatus;
            job.GAPJobId = previousGapJobId;
            job.GAPSubmittedAt = previousGapSubmittedAt;
            job.UpdateProgress(CompletionProgress, previousGapStatus == GapStatus.Failed ? "Failed" : "Completed");
        }
    }
}
