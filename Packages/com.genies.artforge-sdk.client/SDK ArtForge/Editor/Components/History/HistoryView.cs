using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using Genies.Sdk.ArtForge.Editor.Core.UI;
using Genies.Sdk.ArtForge.Editor.Core.UI.Helpers;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Helpers;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Helpers;

namespace Genies.Sdk.ArtForge.Editor.Components.History
{
    /// <summary>
    /// View controller for job history.
    /// Displays all completed jobs with GAP submission tracking.
    /// </summary>
    internal class HistoryView : IViewController
    {
        public static event Action<string, Dictionary<string, object>> HistoryJobRatedUp;
        public static event Action<string, Dictionary<string, object>> HistoryJobRatedDown;

        private const float ProgressPercentScale = 100f;
        private const float CompletionProgress = 1f;
        private const float JobPreviewSize = 48f;
        private const float JobPreviewTopInset = 12f;
        private const float JobPreviewLeftInset = 15f;
        private const float JobPreviewHeaderGap = -5f;
        private ILogger _logger;
        private VisualElement _container;
        private ScrollView _jobsContainer;
        private Label _emptyLabel;
        private Button _removeAllJobsButton;
        private double _lastGapRefreshTime;

        private const double GapRefreshIntervalSeconds = 0.5;

        public void Load(VisualElement container)
        {
            _logger = GeniesArtForgeService.Logger ?? new UnityLogger();
            _container = container;
            _container.Clear();

            // Main container
            var mainContainer = new ScrollView();
            mainContainer.style.flexGrow = 1;
            _container.Add(mainContainer);

            // Header row
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 20;
            mainContainer.Add(headerRow);

            // Header
            var header = new Label("History");
            header.style.fontSize = 24;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = StyleHelpers.Colors.TextPrimary;
            headerRow.Add(header);

            _removeAllJobsButton = new Button(RemoveAllJobs)
            {
                text = "Remove All"
            };
            _removeAllJobsButton.style.height = 24;
            _removeAllJobsButton.style.paddingLeft = 10;
            _removeAllJobsButton.style.paddingRight = 10;
            _removeAllJobsButton.style.fontSize = 10;
            _removeAllJobsButton.style.backgroundColor = new Color(0.4f, 0.2f, 0.2f);
            _removeAllJobsButton.style.borderTopLeftRadius = 3;
            _removeAllJobsButton.style.borderTopRightRadius = 3;
            _removeAllJobsButton.style.borderBottomLeftRadius = 3;
            _removeAllJobsButton.style.borderBottomRightRadius = 3;
            _removeAllJobsButton.SetEnabled(false);
            headerRow.Add(_removeAllJobsButton);

            // Description
            var description = new Label("View all completed generation jobs and manage Inventory submissions.");
            description.style.fontSize = 12;
            description.style.marginBottom = 30;
            description.style.color = StyleHelpers.Colors.TextSecondary;
            mainContainer.Add(description);

            // Jobs list container
            _jobsContainer = new ScrollView();
            _jobsContainer.style.flexGrow = 1;
            _jobsContainer.style.marginBottom = 20;
            mainContainer.Add(_jobsContainer);

            // Empty state label
            _emptyLabel = new Label("No completed jobs yet");
            _emptyLabel.style.fontSize = 14;
            _emptyLabel.style.color = StyleHelpers.Colors.TextHint;
            _emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _emptyLabel.style.marginTop = 40;
            _emptyLabel.style.display = DisplayStyle.None;
            _jobsContainer.Add(_emptyLabel);

            // Initial load
            RefreshJobsList();
        }

        public void Unload()
        {
            _container?.Clear();
        }

        public void Update()
        {
            // Refresh periodically to catch any updates
            // This is lightweight as it only rebuilds UI, no API calls
            var queue = GeniesArtForgeService.Queue;
            if (queue == null)
            {
                return;
            }

            if (queue.GetAllJobs().Any(GapSubmissionTracker.IsInProgress))
            {
                double now = EditorApplication.timeSinceStartup;
                if (now - _lastGapRefreshTime >= GapRefreshIntervalSeconds)
                {
                    _lastGapRefreshTime = now;
                    RefreshJobsList();
                }
            }
        }

        private void RefreshJobsList()
        {
            if (_jobsContainer == null)
            {
                return;
            }

            // Clear existing jobs
            _jobsContainer.Clear();
            _jobsContainer.Add(_emptyLabel);

            // Get all completed jobs from queue
            var queue = GeniesArtForgeService.Queue;
            if (queue == null)
            {
                return;
            }

            var allJobs = queue.GetAllJobs();
            var completedJobs = allJobs
                .Where(j => j.State == JobState.Completed || j.State == JobState.Failed)
                .OrderByDescending(j => j.CompletedAt ?? j.UpdatedAt)
                .ToList();

            // Show/hide empty label
            _emptyLabel.style.display = completedJobs.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _removeAllJobsButton?.SetEnabled(completedJobs.Count > 0);

            // Create job elements
            foreach (var job in completedJobs)
            {
                var jobElement = CreateJobElement(job);
                _jobsContainer.Add(jobElement);
            }
        }

        private VisualElement CreateJobElement(Job job)
        {
            var container = new VisualElement();
            container.style.position = Position.Relative;
            container.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            container.style.borderTopWidth = 1;
            container.style.borderBottomWidth = 1;
            container.style.borderLeftWidth = 1;
            container.style.borderRightWidth = 1;
            container.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
            container.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
            container.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f);
            container.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            container.style.marginBottom = 10;
            container.style.paddingTop = 12;
            container.style.paddingBottom = 12;
            container.style.paddingLeft = 15;
            container.style.paddingRight = 15;

            bool hasPreview = JobPreviewThumbnailUI.TryCreatePreviewElement(job, JobPreviewSize, out VisualElement previewElement);
            if (hasPreview)
            {
                previewElement.style.position = Position.Absolute;
                previewElement.style.left = JobPreviewLeftInset;
                previewElement.style.top = JobPreviewTopInset;
                container.Add(previewElement);
            }

            var headerSection = new VisualElement();
            if (hasPreview)
            {
                headerSection.style.marginLeft = JobPreviewLeftInset + JobPreviewSize + JobPreviewHeaderGap;
                headerSection.style.minHeight = JobPreviewSize;
            }
            container.Add(headerSection);

            // Header row (name + status badge)
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 7;
            headerSection.Add(headerRow);

            // Asset name
            var nameLabel = new Label(job.DisplayName);
            nameLabel.style.fontSize = 14;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = job.State == JobState.Failed
                ? StyleHelpers.Colors.Error
                : StyleHelpers.Colors.TextPrimary;
            headerRow.Add(nameLabel);

            // Status badge
            bool isSubmittingToGAP = GapSubmissionTracker.IsInProgress(job);
            bool isDecorAsset = job.IsDecorAsset;
            bool hasInventorySubmissionTarget =
                !string.IsNullOrEmpty(job.BackendAssetId) &&
                !string.IsNullOrEmpty(job.BackendModelId);
            var statusBadge = new Label(isSubmittingToGAP
                ? "⟳ Submitting to Inventory"
                : (job.State == JobState.Completed ? "✓ Completed" : "✗ Failed"));
            statusBadge.style.fontSize = 11;
            statusBadge.style.paddingTop = 3;
            statusBadge.style.paddingBottom = 3;
            statusBadge.style.paddingLeft = 3;
            statusBadge.style.paddingRight = 8;
            statusBadge.style.borderTopLeftRadius = 3;
            statusBadge.style.borderTopRightRadius = 3;
            statusBadge.style.borderBottomLeftRadius = 3;
            statusBadge.style.borderBottomRightRadius = 3;
            statusBadge.style.backgroundColor = isSubmittingToGAP
                ? new Color(0.2f, 0.6f, 0.2f, 0.25f)
                : (job.State == JobState.Completed
                    ? new Color(0.2f, 0.6f, 0.2f, 0.3f)
                    : new Color(0.6f, 0.2f, 0.2f, 0.3f));
            statusBadge.style.color = isSubmittingToGAP
                ? new Color(0.6f, 1f, 0.6f)
                : (job.State == JobState.Completed
                    ? new Color(0.5f, 1f, 0.5f)
                    : new Color(1f, 0.5f, 0.5f));
            var headerActions = new VisualElement();
            headerActions.style.flexDirection = FlexDirection.Row;
            headerActions.style.alignItems = Align.Center;
            headerRow.Add(headerActions);

            statusBadge.style.marginRight = 6;
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
                    buttonWidth: 20f,
                    buttonHeight: 20f,
                    iconFontSize: 18);
                feedbackButton.style.marginRight = 6;
                headerActions.Add(feedbackButton);
            }

            var removeJobButton = new Button(() => RemoveJob(job))
            {
                text = "x"
            };
            removeJobButton.tooltip = "Remove from history";
            removeJobButton.style.width = 20;
            removeJobButton.style.height = 20;
            removeJobButton.style.paddingLeft = 0;
            removeJobButton.style.paddingRight = 0;
            removeJobButton.style.paddingTop = 0;
            removeJobButton.style.paddingBottom = 0;
            removeJobButton.style.fontSize = 10;
            removeJobButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            removeJobButton.style.backgroundColor = new Color(0.4f, 0.2f, 0.2f);
            removeJobButton.style.borderTopLeftRadius = 3;
            removeJobButton.style.borderTopRightRadius = 3;
            removeJobButton.style.borderBottomLeftRadius = 3;
            removeJobButton.style.borderBottomRightRadius = 3;
            headerActions.Add(removeJobButton);

            // Metadata row (type + date)
            var metadataRow = new VisualElement();
            metadataRow.style.flexDirection = FlexDirection.Row;
            metadataRow.style.marginTop = 0;
            metadataRow.style.marginBottom = 8;
            headerSection.Add(metadataRow);

            var typeLabel = new Label($"Type: {job.AssetTypeId}");
            typeLabel.style.fontSize = 11;
            typeLabel.style.color = StyleHelpers.Colors.TextHint;
            typeLabel.style.marginRight = 15;
            metadataRow.Add(typeLabel);

            var completionDate = (job.CompletedAt ?? job.UpdatedAt).ToLocalTime();
            var dateLabel = new Label($"Completed: {completionDate:yyyy-MM-dd HH:mm}");
            dateLabel.style.fontSize = 11;
            dateLabel.style.color = StyleHelpers.Colors.TextHint;
            metadataRow.Add(dateLabel);

            // Description (if available)
            if (!string.IsNullOrEmpty(job.Description))
            {
                var defaultDescriptionButtonColor = new Color(0.2f, 0.2f, 0.2f);
                var hoverDescriptionButtonColor = new Color(0.24f, 0.24f, 0.24f);
                var pressedDescriptionButtonColor = new Color(0.12f, 0.12f, 0.12f);

                var descriptionButton = new Button(() => CopyDescriptionToClipboard(job.Description));
                descriptionButton.tooltip = "Click to copy description";
                descriptionButton.style.alignSelf = Align.Stretch;
                descriptionButton.style.width = Length.Percent(100);
                descriptionButton.style.flexGrow = 1;
                descriptionButton.style.marginLeft = 0;
                descriptionButton.style.marginRight = 0;
                descriptionButton.style.marginBottom = 6;
                descriptionButton.style.paddingTop = 6;
                descriptionButton.style.paddingBottom = 6;
                descriptionButton.style.paddingLeft = 10;
                descriptionButton.style.paddingRight = 10;
                descriptionButton.style.backgroundColor = defaultDescriptionButtonColor;
                descriptionButton.style.borderTopWidth = 0;
                descriptionButton.style.borderBottomWidth = 0;
                descriptionButton.style.borderLeftWidth = 0;
                descriptionButton.style.borderRightWidth = 0;
                descriptionButton.style.borderTopLeftRadius = 3;
                descriptionButton.style.borderTopRightRadius = 3;
                descriptionButton.style.borderBottomLeftRadius = 3;
                descriptionButton.style.borderBottomRightRadius = 3;
                descriptionButton.style.unityTextAlign = TextAnchor.UpperLeft;

                void ApplyDescriptionButtonState(Color backgroundColor)
                {
                    descriptionButton.style.backgroundColor = backgroundColor;
                }

                descriptionButton.RegisterCallback<PointerEnterEvent>(_ =>
                    ApplyDescriptionButtonState(hoverDescriptionButtonColor));
                descriptionButton.RegisterCallback<PointerLeaveEvent>(_ =>
                    ApplyDescriptionButtonState(defaultDescriptionButtonColor));
                descriptionButton.RegisterCallback<PointerDownEvent>(_ =>
                    ApplyDescriptionButtonState(pressedDescriptionButtonColor), TrickleDown.TrickleDown);
                descriptionButton.RegisterCallback<PointerUpEvent>(_ =>
                    ApplyDescriptionButtonState(hoverDescriptionButtonColor), TrickleDown.TrickleDown);

                var descLabel = new Label(job.Description);
                descLabel.style.fontSize = 11;
                descLabel.style.color = StyleHelpers.Colors.TextSecondary;
                descLabel.style.whiteSpace = WhiteSpace.Normal;
                descLabel.style.unityTextAlign = TextAnchor.UpperLeft;
                descLabel.pickingMode = PickingMode.Ignore;
                descriptionButton.Add(descLabel);

                container.Add(descriptionButton);
            }

            // GAP submission progress (if in progress)
            if (isSubmittingToGAP)
            {
                var progressContainer = new VisualElement();
                progressContainer.style.marginBottom = 12;

                var progressBar = new ProgressBar();
                progressBar.style.height = 20;
                progressBar.value = job.Progress * ProgressPercentScale;
                progressBar.style.backgroundColor = new Color(0.3f, 0.2f, 0.1f);
                progressContainer.Add(progressBar);

                var statusLine = new Label(JobStepFormatter.GetStageName(job.CurrentStep));
                statusLine.style.fontSize = 11;
                statusLine.style.color = StyleHelpers.Colors.TextSecondary;
                statusLine.style.marginTop = 4;
                progressContainer.Add(statusLine);

                container.Add(progressContainer);
            }

            // GAP status row (if backend job)
            if (hasInventorySubmissionTarget && !isDecorAsset)
            {
                var gapRow = new VisualElement();
                gapRow.style.flexDirection = FlexDirection.Row;
                gapRow.style.alignItems = Align.Center;
                gapRow.style.marginBottom = 7;
                gapRow.style.paddingTop = 8;
                gapRow.style.paddingBottom = 8;
                gapRow.style.paddingLeft = 10;
                gapRow.style.paddingRight = 10;
                gapRow.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
                gapRow.style.borderTopLeftRadius = 3;
                gapRow.style.borderTopRightRadius = 3;
                gapRow.style.borderBottomLeftRadius = 3;
                gapRow.style.borderBottomRightRadius = 3;
                container.Add(gapRow);

                var gapLabel = new Label("Inventory Status:");
                gapLabel.style.fontSize = 11;
                gapLabel.style.color = StyleHelpers.Colors.TextHint;
                gapLabel.style.marginRight = 8;
                gapRow.Add(gapLabel);

                var gapStatusLabel = new Label(isSubmittingToGAP
                    ? GapSubmissionTracker.GetStatusText(job)
                    : (job.IsSubmittedToGAP() ? $"Available In Inventory ({job.GAPJobId})" : "Not In Inventory"));
                gapStatusLabel.style.fontSize = 11;
                gapStatusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                gapStatusLabel.style.color = isSubmittingToGAP
                    ? new Color(0.6f, 1f, 0.6f)
                    : (job.IsSubmittedToGAP()
                        ? new Color(0.5f, 1f, 0.5f)
                        : StyleHelpers.Colors.TextSecondary);
                gapRow.Add(gapStatusLabel);
            }

            // Action buttons row
            var actionsRow = new VisualElement();
            actionsRow.style.flexDirection = FlexDirection.Row;
            actionsRow.style.marginLeft = 0;
            actionsRow.style.paddingLeft = 0;
            container.Add(actionsRow);

            // Open button
            var openFolderBtn = new Button(() => OpenAssetFolder(job))
            {
                text = "Open"
            };
            openFolderBtn.style.height = 28;
            openFolderBtn.style.marginLeft = 0;
            openFolderBtn.style.paddingLeft = 5;
            openFolderBtn.style.paddingRight = 12;
            openFolderBtn.style.marginRight = 8;
            openFolderBtn.style.backgroundColor = new Color(0.2f, 0.3f, 0.2f);
            openFolderBtn.style.borderTopLeftRadius = 3;
            openFolderBtn.style.borderTopRightRadius = 3;
            openFolderBtn.style.borderBottomLeftRadius = 3;
            openFolderBtn.style.borderBottomRightRadius = 3;
            actionsRow.Add(openFolderBtn);

            // Retry/check controls for failed 3D generation jobs
            if (job.IsFailed3DGeneration && !hasInventorySubmissionTarget)
            {
                var queue = GeniesArtForgeService.Queue;
                int maxRetries = queue?.MaxRetryAttempts ?? 2;
                bool canRetry = queue != null && job.CanRetry(maxRetries);
                bool canCheckServerStatus = !string.IsNullOrEmpty(job.BackendModelGenerationJobId);

                if (canRetry)
                {
                    var retry3DBtn = new Button(() => RetryFailed3DJobAsync(job))
                    {
                        text = $"Retry 3D ({job.RetryCount + 1}/{maxRetries})"
                    };
                    retry3DBtn.style.height = 28;
                    retry3DBtn.style.marginLeft = 0;
                    retry3DBtn.style.paddingLeft = 12;
                    retry3DBtn.style.paddingRight = 12;
                    retry3DBtn.style.marginRight = 8;
                    retry3DBtn.style.backgroundColor = new Color(0.35f, 0.5f, 0.25f);
                    retry3DBtn.style.borderTopLeftRadius = 3;
                    retry3DBtn.style.borderTopRightRadius = 3;
                    retry3DBtn.style.borderBottomLeftRadius = 3;
                    retry3DBtn.style.borderBottomRightRadius = 3;
                    actionsRow.Add(retry3DBtn);
                }

                if (canCheckServerStatus)
                {
                    var checkStatusBtn = new Button(() => CheckServerStatusAsync(job))
                    {
                        text = "Check Server Status"
                    };
                    checkStatusBtn.style.height = 28;
                    checkStatusBtn.style.marginLeft = 0;
                    checkStatusBtn.style.paddingLeft = 12;
                    checkStatusBtn.style.paddingRight = 12;
                    checkStatusBtn.style.marginRight = 8;
                    checkStatusBtn.style.backgroundColor = new Color(0.2f, 0.4f, 0.6f);
                    checkStatusBtn.style.borderTopLeftRadius = 3;
                    checkStatusBtn.style.borderTopRightRadius = 3;
                    checkStatusBtn.style.borderBottomLeftRadius = 3;
                    checkStatusBtn.style.borderBottomRightRadius = 3;
                    actionsRow.Add(checkStatusBtn);
                }
            }

            // Send to GAP button (if backend job and not yet submitted)
            if (hasInventorySubmissionTarget && !isDecorAsset)
            {
                bool canRetryGap = job.GAPStatus == GapStatus.Failed && job.CanRetryGAP();
                bool gapIncompatible = job.GAPStatus == GapStatus.Failed && !job.CanRetryGAP();
                var gapBtn = new Button(() => SendToGAP(job))
                {
                    text = isSubmittingToGAP
                        ? "Submitting to Inventory..."
                        : (gapIncompatible
                            ? "Inventory Incompatible"
                            : (canRetryGap
                                ? $"Retry Inventory ({job.GAPRetryCount + 1}/{job.GAPMaxRetries})"
                                : (job.IsSubmittedToGAP() ? "Available In Inventory" : "Submit to Inventory")))
                };
                gapBtn.style.height = 28;
                gapBtn.style.marginLeft = 0;
                gapBtn.style.paddingLeft = 12;
                gapBtn.style.paddingRight = 12;
                gapBtn.style.backgroundColor = isSubmittingToGAP
                    ? new Color(0.2f, 0.6f, 0.2f)
                    : (job.IsSubmittedToGAP()
                        ? new Color(0.2f, 0.6f, 0.2f)
                        : new Color(0.2f, 0.4f, 0.6f));
                if (gapIncompatible)
                {
                    gapBtn.style.backgroundColor = new Color(0.4f, 0.2f, 0.2f);
                }
                gapBtn.style.borderTopLeftRadius = 3;
                gapBtn.style.borderTopRightRadius = 3;
                gapBtn.style.borderBottomLeftRadius = 3;
                gapBtn.style.borderBottomRightRadius = 3;
                gapBtn.SetEnabled(!job.IsSubmittedToGAP() && !isSubmittingToGAP && !gapIncompatible);
                actionsRow.Add(gapBtn);
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
                { "stage", "inventory" },
                { "context", "history" },
            };

            switch (feedbackType)
            {
                case JobFeedbackType.ThumbsUp:
                    HistoryJobRatedUp?.Invoke("result_thumbs_up", properties);
                    break;
                case JobFeedbackType.ThumbsDown:
                    HistoryJobRatedDown?.Invoke("result_thumbs_down", properties);
                    break;
            }
        }

        private void RemoveJob(Job job)
        {
            if (job == null)
            {
                return;
            }

            var queue = GeniesArtForgeService.Queue;
            if (queue == null)
            {
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Remove Job",
                $"Remove '{job.DisplayName}' from history?",
                "Remove",
                "Cancel");
            if (!confirmed)
            {
                return;
            }

            if (!queue.RemoveCompletedJob(job.Id))
            {
                EditorUtility.DisplayDialog(
                    "Remove Job",
                    "Failed to remove the selected job from history.",
                    "OK");
            }

            RefreshJobsList();
        }

        private void RemoveAllJobs()
        {
            var queue = GeniesArtForgeService.Queue;
            if (queue == null)
            {
                return;
            }

            int totalHistoryJobs = queue.GetAllJobs()
                .Count(j => j.State == JobState.Completed || j.State == JobState.Failed);
            if (totalHistoryJobs == 0)
            {
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Remove All Jobs",
                $"Remove all {totalHistoryJobs} jobs from history?",
                "Remove All",
                "Cancel");
            if (!confirmed)
            {
                return;
            }

            queue.RemoveAllCompletedJobs();
            RefreshJobsList();
        }

        private void OpenAssetFolder(Job job)
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
                        return;
                    }
                }

                EditorUtility.DisplayDialog("Reference Image Not Found",
                    $"Reference image not found:\n{referenceImagePath}",
                    "OK");
                return;
            }

            string prefabPath = BrandingConstants.AssetPaths.GetPrefabPath(assetName);
            var prefabAsset = AssetDatabase.LoadMainAssetAtPath(prefabPath);
            if (prefabAsset != null)
            {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = prefabAsset;
                EditorGUIUtility.PingObject(prefabAsset);
                return;
            }

            string assetFolderPath = BrandingConstants.AssetPaths.GetPrefabsFolder(assetName);
            assetFolderPath = assetFolderPath.TrimEnd('/');

            if (!AssetDatabase.IsValidFolder(assetFolderPath))
            {
                EditorUtility.DisplayDialog("Prefab Not Found",
                    $"Prefab not found:\n{prefabPath}",
                    "OK");
                return;
            }

            var folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(assetFolderPath);
            if (folderAsset == null)
            {
                EditorUtility.DisplayDialog("Prefab Not Found",
                    $"Unable to load prefab:\n{prefabPath}",
                    "OK");
                return;
            }

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = folderAsset;
            EditorGUIUtility.PingObject(folderAsset);
        }

        private void CopyDescriptionToClipboard(string description)
        {
            if (string.IsNullOrEmpty(description))
            {
                return;
            }

            EditorGUIUtility.systemCopyBuffer = description;
            _logger?.Info("[HistoryView] Copied job description to clipboard.");
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
                EditorUtility.DisplayDialog("Retry Limit Reached",
                    $"'{job.DisplayName}' has reached the retry limit ({queue.MaxRetryAttempts}).",
                    "OK");
                return;
            }

            string serverStatusSummary = await GetServerStatusSummaryAsync(job);
            if (!string.IsNullOrEmpty(serverStatusSummary))
            {
                _logger.Info($"[HistoryView] Retrying '{job.DisplayName}' after server status check: {serverStatusSummary}");
            }

            queue.RetryJob(job.Id);
            RefreshJobsList();
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
            catch (Exception ex)
            {
                _logger.Warning($"[HistoryView] Failed to fetch server status for {job.BackendModelGenerationJobId}: {ex.Message}");
                return $"Failed to fetch server status for {job.BackendModelGenerationJobId}:\n{ex.Message}";
            }
        }

        private async void SendToGAP(Job job)
        {
            if (job == null)
            {
                return;
            }

            bool shouldConsumeGapRetry = job.GAPStatus == GapStatus.Failed &&
                                         job.CanRetryGAP();
            string previousGapStatus = job.GAPStatus;
            string previousGapJobId = job.GAPJobId;
            DateTime? previousGapSubmittedAt = job.GAPSubmittedAt;

            try
            {
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

                GapSubmissionTracker.MarkSubmitting(job);
                GeniesArtForgeService.Queue.SaveJobState(job);
                RefreshJobsList();

                var result = await contentClient.CreateGameAssetAsync(job.BackendAssetId, job.BackendModelId);
                if (shouldConsumeGapRetry)
                {
                    job.PrepareGapRetry();
                }

                if (result != null && !string.IsNullOrEmpty(result.jobId))
                {
                    GapSubmissionTracker.MarkProcessing(job, result.jobId);
                    GeniesArtForgeService.Queue.SaveJobState(job);

                    RefreshJobsList();

                    await GapSubmissionTracker.TrackAsync(
                        job,
                        contentClient,
                        GeniesArtForgeService.Queue.SaveJobState,
                        RefreshJobsList,
                        message => _logger.Error($"[HistoryView] Failed to track GAP job status: {message}"));

                    if (job.GAPStatus == GapStatus.Completed)
                    {
                        EditorUtility.DisplayDialog("Success",
                            $"Asset '{job.DisplayName}' submitted to Inventory!\n\nInventory Job ID: {result.jobId}",
                            "OK");
                    }
                    else if (job.GAPStatus == GapStatus.Failed)
                    {
                        EditorUtility.DisplayDialog("Submit to Inventory Failed",
                            $"Failed to submit asset to Inventory.\n\nInventory Job ID: {result.jobId}",
                            "OK");
                    }
                }
                else
                {
                    job.GAPStatus = GapStatus.NotSubmitted;
                    job.UpdateProgress(CompletionProgress, "Completed");
                    GeniesArtForgeService.Queue.SaveJobState(job);
                    EditorSfx.PlayGapSubmissionCompleted(job.Id?.ToString() ?? string.Empty);
                    EditorUtility.DisplayDialog("Partial Success",
                        "Asset was submitted to Inventory, but no job ID was returned.",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                if (BackendConnectionRecoveryHelper.IsRecoverableConnectionError(ex))
                {
                    RestoreGapSubmissionState(job, previousGapStatus, previousGapJobId, previousGapSubmittedAt);
                    GeniesArtForgeService.Queue.SaveJobState(job);
                    RefreshJobsList();

                    await BackendConnectionRecoveryHelper.EnsureConnectedOrPromptAsync(
                        _logger,
                        "Real-time backend connection is reconnecting. Please reconnect in Settings before retrying Inventory submission.");
                    return;
                }

                job.GAPStatus = GapStatus.Failed;
                job.UpdateProgress(CompletionProgress, "Completed");
                GeniesArtForgeService.Queue.SaveJobState(job);
                EditorUtility.DisplayDialog("Submit to Inventory Failed",
                    $"Failed to submit asset to Inventory:\n\n{ex.Message}",
                    "OK");
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
