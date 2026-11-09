using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using Genies.Sdk.ArtForge.Editor.Core.Application.Services;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Helpers;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Helpers;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Processing;
using Genies.Sdk.ArtForge.Editor.Core.UI.Helpers;

namespace Genies.Sdk.ArtForge.Editor.Hub.UI
{
    /// <summary>
    /// Approval dialog for reviewing generated reference images before continuing with 3D model generation.
    /// </summary>
    internal class ApprovalReviewDialog : EditorWindow
    {
        // ------------------------------------------------------------
        // Telemetry signals (Observer subscribes to these)
        // ------------------------------------------------------------
        public static event Action<string, Dictionary<string, object>> Preview2DJobStarted;
        public static event Action<string, Dictionary<string, object>> Preview2DJobSucceeded;
        public static event Action<string, Dictionary<string, object>> Preview2DJobFailed;
        public static event Action<string, Dictionary<string, object>> Preview2DImageViewed;
        public static event Action<string, Dictionary<string, object>> Preview2DApproved;
        public static event Action<string, Dictionary<string, object>> Preview2DCancelled;
        public static event Action<string, Dictionary<string, object>> Preview2DEditDescription;
        public static event Action<string, Dictionary<string, object>> Preview2DRetryClicked;
        public static event Action<string, Dictionary<string, object>> Preview2DJobRatedUp;
        public static event Action<string, Dictionary<string, object>> Preview2DJobRatedDown;

        private const float ProgressPercentScale = 100f;
        private const int RetryProgressRefreshIntervalMs = 100;
        private const float RetryFakeProgressSpeed = 0.01f;
        private const float RetryAnimationSpeed = 2.0f;
        private const float RetryFakeProgressCap = 0.95f;
        private const int DialogPadding = 10;

        private static List<Job> _pendingJobs;
        private static int _currentIndex = 0;
        private static ApprovalReviewDialog _instance;
        private static ILogger _logger;
        private QueueService _queueEventSource;

        public static bool IsOpen => _instance != null;

        private VisualElement _root;
        private VisualElement _statusRow;
        private VisualElement _imageContainer;
        private VisualElement _statusFeedbackContainer;
        private TextField _promptField;
        private Label _statusLabel;
        private Label _versionLabel;
        private VisualElement _promptHeaderRow;
        private Toggle _showTransparentToggle;
        private VisualElement _retryProgressContainer;
        private ProgressBar _retryProgressBar;
        private Label _retryProgressLabel;
        private Button _prevVersionButton;
        private Button _nextVersionButton;
        private Button _approveButton;
        private Button _retryButton;
        private Button _cancelButton;
        private Button _makeTransparentButton;
        private IVisualElementScheduledItem _retryProgressRefreshItem;
        private float _displayedRetryProgress;
        private double _lastRetryProgressUpdateTime;
        private string _promptInitializedJobId;
        private string _regeneratingJobId;

        private Texture2D _currentTexture;
        private bool _isRegenerating = false;
        private bool _isCancellingFromDialog = false;
        private bool _isApplyingTransparency = false;

        private string _lastImageViewMethod = "click";
        private string _lastPromptValue = string.Empty;

        // Dedupe for preview2d_image_viewed (avoid spamming on RefreshUI)
        private string _lastViewedJobId;
        private int _lastViewedIndex = -1;
        private string _lastPreviewModeJobId;
        private int _lastPreviewModeVersionIndex = -1;

        /// <summary>
        /// Show the approval dialog for jobs awaiting confirmation
        /// </summary>
        public static void Show(List<Job> pendingJobs, ILogger logger, Job startJob = null)
        {
            if (pendingJobs == null || pendingJobs.Count == 0)
            {
                EditorUtility.DisplayDialog("No Approvals", "No jobs pending approval.", "OK");
                return;
            }

            _logger = logger;
            _pendingJobs = pendingJobs.Where(j => j.State == JobState.AwaitingImageConfirm).ToList();

            if (_pendingJobs.Count == 0)
            {
                EditorUtility.DisplayDialog("No Approvals", "No jobs awaiting approval.", "OK");
                return;
            }

            // Find starting index
            _currentIndex = 0;
            if (startJob != null)
            {
                int startIndex = _pendingJobs.FindIndex(j => j.Id == startJob.Id);
                if (startIndex >= 0)
                {
                    _currentIndex = startIndex;
                }
            }

            // Create or focus window
            if (_instance == null)
            {
                _instance = GetWindow<ApprovalReviewDialog>("Approve Reference Image");
                _instance.minSize = new Vector2(800, 600);
            }
            else
            {
                _instance.Focus();
            }

            if (_instance._makeTransparentButton == null || _instance._showTransparentToggle == null)
            {
                _instance.BuildUI();
            }
            _instance.SubscribeToQueueEvents();
            _instance.RefreshUI();
        }

        public static bool TryFocusOpenWindow()
        {
            if (_instance == null)
            {
                return false;
            }

            _instance.Show();
            _instance.Focus();
            return true;
        }

        public static void ShowForJob(Job job)
        {
            if (job == null)
            {
                EditorUtility.DisplayDialog("Approval Preview", "Job not found.", "OK");
                return;
            }

            var queue = GeniesArtForgeService.Queue;
            var logger = GeniesArtForgeService.Logger;
            if (queue == null)
            {
                EditorUtility.DisplayDialog("Approval Preview", "Queue service not available.", "OK");
                return;
            }

            var awaitingJobs = queue.GetJobsByState(JobState.AwaitingImageConfirm);
            if (awaitingJobs.Count == 0)
            {
                EditorUtility.DisplayDialog("No Approvals", "No jobs awaiting approval.", "OK");
                return;
            }

            // Ensure the requested job is part of the dialog list.
            if (!awaitingJobs.Any(j => j.Id == job.Id))
            {
                awaitingJobs.Add(job);
            }

            Show(awaitingJobs, logger, job);
        }

        private void CreateGUI()
        {
            _root = rootVisualElement;
            _root.style.paddingTop = DialogPadding;
            _root.style.paddingBottom = DialogPadding;
            _root.style.paddingLeft = DialogPadding;
            _root.style.paddingRight = DialogPadding;
            _root.style.position = Position.Relative;

            BuildUI();
            RefreshUI();
        }

        private void BuildUI()
        {
            _root.Clear();

            // Header
            var header = new Label("Approve Reference Image");
            header.style.fontSize = 18;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 10;
            _root.Add(header);

            // Status row (job X of Y)
            _statusRow = new VisualElement();
            _statusRow.style.flexDirection = FlexDirection.Row;
            _statusRow.style.justifyContent = Justify.FlexStart;
            _statusRow.style.alignItems = Align.Center;
            _statusRow.style.marginBottom = 10;
            _root.Add(_statusRow);

            _statusLabel = new Label();
            _statusRow.Add(_statusLabel);

            _statusFeedbackContainer = new VisualElement();
            _statusFeedbackContainer.style.position = Position.Absolute;
            _statusFeedbackContainer.style.top = DialogPadding;
            _statusFeedbackContainer.style.right = DialogPadding;
            _root.Add(_statusFeedbackContainer);

            // Asset name
            var assetNameLabel = new Label();
            assetNameLabel.name = "asset-name-label";
            assetNameLabel.style.fontSize = 14;
            assetNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            assetNameLabel.style.marginBottom = 10;
            _root.Add(assetNameLabel);

            // Version navigation row
            var versionRow = new VisualElement();
            versionRow.style.flexDirection = FlexDirection.Row;
            versionRow.style.justifyContent = Justify.Center;
            versionRow.style.marginBottom = 5;
            _root.Add(versionRow);

            _prevVersionButton = new Button(() => NavigateVersion(-1));
            _prevVersionButton.text = "◄";
            _prevVersionButton.style.width = 30;
            versionRow.Add(_prevVersionButton);

            _versionLabel = new Label();
            _versionLabel.style.marginLeft = 10;
            _versionLabel.style.marginRight = 10;
            _versionLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _versionLabel.style.minWidth = 100;
            versionRow.Add(_versionLabel);

            _nextVersionButton = new Button(() => NavigateVersion(1));
            _nextVersionButton.text = "►";
            _nextVersionButton.style.width = 30;
            versionRow.Add(_nextVersionButton);

            // Image container
            _imageContainer = new VisualElement();
            _imageContainer.style.flexGrow = 1;
            _imageContainer.style.alignItems = Align.Center;
            _imageContainer.style.justifyContent = Justify.Center;
            _imageContainer.style.marginBottom = 10;
            _imageContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            _root.Add(_imageContainer);

            _promptHeaderRow = new VisualElement();
            _promptHeaderRow.style.flexDirection = FlexDirection.Row;
            _promptHeaderRow.style.justifyContent = Justify.SpaceBetween;
            _promptHeaderRow.style.alignItems = Align.Center;
            _promptHeaderRow.style.marginBottom = 5;
            _root.Add(_promptHeaderRow);

            // Prompt field (controlled by FeatureAvailabilityService)
            var promptLabel = new Label("Refine:");
            promptLabel.name = "prompt-label"; // Add name for easy reference
            _promptHeaderRow.Add(promptLabel);

            _showTransparentToggle = new Toggle("Show Transparent");
            _showTransparentToggle.value = true;
            _showTransparentToggle.style.display = DisplayStyle.None;
            _showTransparentToggle.RegisterValueChangedCallback(_ =>
            {
                var job = GetCurrentJob();
                if (job != null)
                {
                    LoadAndDisplayImage(job);
                }
            });
            _promptHeaderRow.Add(_showTransparentToggle);

            _promptField = new TextField();
            _promptField.multiline = true;
            _promptField.style.whiteSpace = WhiteSpace.Normal;
            _promptField.style.minHeight = 60;
            _promptField.style.marginBottom = 10;
            _promptField.name = "prompt-field"; // Add name for easy reference
            _promptField.RegisterValueChangedCallback(evt =>
            {
                var job = GetCurrentJob();
                if (job == null)
                {
                    _lastPromptValue = evt.newValue ?? "";
                    return;
                }

                _lastPromptValue = evt.newValue ?? "";

                Preview2DEditDescription?.Invoke(
                    "preview2d_edit_description",
                    new Dictionary<string, object>
                    {
                        { "job_id", job.Id?.ToString() ?? "" },
                        { "new_description_length", (_lastPromptValue ?? "").Length },
                        { "edit_source", "field" },
                    }
                );
            });
            _root.Add(_promptField);

            // Retry progress (shown only while regenerating)
            _retryProgressContainer = new VisualElement();
            _retryProgressContainer.style.display = DisplayStyle.None;
            _retryProgressContainer.style.marginBottom = 10;

            _retryProgressBar = new ProgressBar();
            _retryProgressBar.style.height = 18;
            _retryProgressContainer.Add(_retryProgressBar);

            _retryProgressLabel = new Label("Regenerating reference image...");
            _retryProgressLabel.style.fontSize = 11;
            _retryProgressLabel.style.marginTop = 3;
            _retryProgressContainer.Add(_retryProgressLabel);

            _root.Add(_retryProgressContainer);

            // Action buttons
            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            _root.Add(buttonRow);

            var actionButtons = new VisualElement();
            actionButtons.style.flexDirection = FlexDirection.Row;
            buttonRow.Add(actionButtons);

            _makeTransparentButton = new Button(OnMakeTransparentClicked);
            _makeTransparentButton.text = "Make Transparent";
            _makeTransparentButton.style.width = 140;
            actionButtons.Add(_makeTransparentButton);

            _cancelButton = new Button(OnCancelClicked);
            _cancelButton.text = "Cancel Job";
            _cancelButton.style.width = 100;
            _cancelButton.style.marginLeft = 5;
            actionButtons.Add(_cancelButton);

            _retryButton = new Button(OnRetryClicked);
            _retryButton.text = "Retry";
            _retryButton.style.width = 100;
            _retryButton.style.marginLeft = 5;
            _retryButton.name = "retry-button"; // Add name for easy reference (BACKEND MODE: will be hidden)
            actionButtons.Add(_retryButton);

            _approveButton = new Button(OnApproveClicked);
            _approveButton.text = "✓ Approve";
            _approveButton.style.width = 120;
            _approveButton.style.marginLeft = 5;
            _approveButton.style.backgroundColor = new Color(0.2f, 0.7f, 0.2f);
            actionButtons.Add(_approveButton);
        }

        private void RefreshUI()
        {
            if (_root == null || _makeTransparentButton == null || _showTransparentToggle == null)
            {
                BuildUI();
            }

            if (_pendingJobs == null || _pendingJobs.Count == 0)
            {
                Close();
                return;
            }

            var currentJob = GetCurrentJob();
            if (currentJob == null)
            {
                Close();
                return;
            }

            // Show/hide prompt/retry based on FeatureAvailabilityService
            bool showPromptField = FeatureAvailabilityService.IsApprovalDescriptionFieldAvailable;
            bool showRetryButton = FeatureAvailabilityService.IsApprovalRetryButtonAvailable;

            if (!showPromptField)
            {
                var promptLabel = _root.Q<Label>("prompt-label");
                var promptField = _root.Q<TextField>("prompt-field");

                if (promptLabel != null)
                {
                    promptLabel.style.display = DisplayStyle.None;
                }

                if (promptField != null)
                {
                    promptField.style.display = DisplayStyle.None;
                }

                _logger?.Info("[ApprovalDialog] FeatureAvailabilityService: Hiding description field");
            }
            else
            {
                var promptLabel = _root.Q<Label>("prompt-label");
                var promptField = _root.Q<TextField>("prompt-field");

                if (promptLabel != null)
                {
                    promptLabel.style.display = DisplayStyle.Flex;
                }

                if (promptField != null)
                {
                    promptField.style.display = DisplayStyle.Flex;
                }
            }

            if (!showRetryButton)
            {
                var retryButton = _root.Q<Button>("retry-button");
                if (retryButton != null)
                {
                    retryButton.style.display = DisplayStyle.None;
                }

                _logger?.Info("[ApprovalDialog] FeatureAvailabilityService: Hiding retry button");
            }
            else
            {
                var retryButton = _root.Q<Button>("retry-button");
                if (retryButton != null)
                {
                    retryButton.style.display = DisplayStyle.Flex;
                }
            }

            // Update status
            _statusLabel.text = $"Job {_currentIndex + 1} of {_pendingJobs.Count}";

            // Update asset name
            var assetNameLabel = _root.Q<Label>("asset-name-label");
            if (assetNameLabel != null)
            {
                assetNameLabel.text = $"Asset: {currentJob.DisplayName}";
            }

            // Update version info
            int versionCount = currentJob.ImageVersions.Count;
            int selectedVersion = currentJob.SelectedVersionIndex;
            bool isCurrentJobRegenerating = IsRegeneratingJob(currentJob);
            if (_isRegenerating && !isCurrentJobRegenerating)
            {
                SetRegeneratingState(false, null);
            }

            if (isCurrentJobRegenerating)
            {
                _versionLabel.text = "Regenerating...";
                _prevVersionButton.SetEnabled(false);
                _nextVersionButton.SetEnabled(false);
            }
            else if (versionCount > 0)
            {
                _versionLabel.text = $"Version {selectedVersion + 1} of {versionCount}";
                _prevVersionButton.SetEnabled(selectedVersion > 0);
                _nextVersionButton.SetEnabled(selectedVersion < versionCount - 1);
            }
            else
            {
                _versionLabel.text = "No versions";
                _prevVersionButton.SetEnabled(false);
                _nextVersionButton.SetEnabled(false);
            }

            RefreshFeedbackControls(currentJob, isCurrentJobRegenerating);

            // Update prompt (show prompt from selected version unless regenerating)
            string currentJobId = currentJob.Id?.ToString();
            bool isNewJobForPromptField = !string.Equals(_promptInitializedJobId, currentJobId, StringComparison.Ordinal);
            if (isCurrentJobRegenerating)
            {
                _promptField.SetValueWithoutNotify(currentJob.Prompt ?? "");
                _lastPromptValue = currentJob.Prompt ?? "";
            }
            else if (isNewJobForPromptField)
            {
                var selectedVersionData = currentJob.GetSelectedVersion();
                var value = selectedVersionData?.Prompt ?? currentJob.Prompt ?? "";
                _promptField.SetValueWithoutNotify(value);
                _lastPromptValue = value;
                _promptInitializedJobId = currentJobId;
            }

            // Reset view dedupe when switching jobs
            if (!string.Equals(_lastViewedJobId, currentJobId, StringComparison.Ordinal))
            {
                _lastViewedJobId = currentJobId;
                _lastViewedIndex = -1;
            }

            bool showTransparencyPreviewToggle = ShouldShowTransparencyPreviewToggle(currentJob);
            bool isPreviewContextChanged = !string.Equals(_lastPreviewModeJobId, currentJobId, StringComparison.Ordinal)
                                           || _lastPreviewModeVersionIndex != currentJob.SelectedVersionIndex;

            if (_showTransparentToggle != null)
            {
                if (!showTransparencyPreviewToggle)
                {
                    _showTransparentToggle.SetValueWithoutNotify(true);
                    _lastPreviewModeJobId = null;
                    _lastPreviewModeVersionIndex = -1;
                }
                else if (isPreviewContextChanged)
                {
                    bool defaultTransparent = ShouldDefaultToTransparentPreview(currentJob);
                    _showTransparentToggle.SetValueWithoutNotify(defaultTransparent);
                    _lastPreviewModeJobId = currentJobId;
                    _lastPreviewModeVersionIndex = currentJob.SelectedVersionIndex;
                }

                _showTransparentToggle.style.display = showTransparencyPreviewToggle ? DisplayStyle.Flex : DisplayStyle.None;
                _showTransparentToggle.SetEnabled(showTransparencyPreviewToggle && !isCurrentJobRegenerating && !_isApplyingTransparency);
            }

            if (_promptHeaderRow != null)
            {
                bool showPromptHeader = showPromptField || showTransparencyPreviewToggle;
                _promptHeaderRow.style.display = showPromptHeader ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // Disable editing/actions if regenerating
            bool actionsEnabled = !isCurrentJobRegenerating && !_isApplyingTransparency;
            _promptField.SetEnabled(actionsEnabled);
            _retryButton.SetEnabled(actionsEnabled);
            _approveButton.SetEnabled(actionsEnabled);
            _cancelButton.SetEnabled(actionsEnabled);
            if (_makeTransparentButton != null)
            {
                bool shouldShowMakeTransparent = ShouldShowMakeTransparentButton(currentJob);
                _makeTransparentButton.style.display = shouldShowMakeTransparent ? DisplayStyle.Flex : DisplayStyle.None;
                _makeTransparentButton.SetEnabled(shouldShowMakeTransparent && actionsEnabled);
            }
            if (_retryProgressContainer != null)
            {
                _retryProgressContainer.style.display = isCurrentJobRegenerating ? DisplayStyle.Flex : DisplayStyle.None;
                if (isCurrentJobRegenerating)
                {
                    UpdateRetryProgressUI();
                }
            }

            // Load and display image
            LoadAndDisplayImage(currentJob);
        }

        private void RefreshFeedbackControls(Job job, bool isCurrentJobRegenerating)
        {
            if (_statusFeedbackContainer == null)
            {
                return;
            }

            _statusFeedbackContainer.Clear();
            var feedbackButton = JobFeedbackUI.CreateInlineMenuButton(
                job,
                feedbackType =>
                {
                    GeniesArtForgeService.Queue?.SaveJobState(job);
                    EmitJobFeedbackTelemetry(job, feedbackType);
                },
                useButtonBackground: true,
                buttonWidth: 34f,
                buttonHeight: 34f,
                iconFontSize: 28);
            feedbackButton.SetEnabled(!isCurrentJobRegenerating && job != null);
            _statusFeedbackContainer.Add(feedbackButton);
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
                { "stage", "2d" },
                { "context", "approval" },
            };

            switch (feedbackType)
            {
                case JobFeedbackType.ThumbsUp:
                    Preview2DJobRatedUp?.Invoke("result_thumbs_up", properties);
                    break;
                case JobFeedbackType.ThumbsDown:
                    Preview2DJobRatedDown?.Invoke("result_thumbs_down", properties);
                    break;
            }
        }

        private void NavigateVersion(int direction)
        {
            var job = GetCurrentJob();
            if (job == null || job.ImageVersions.Count == 0)
            {
                return;
            }

            // Update selected version index
            int newIndex = job.SelectedVersionIndex + direction;
            newIndex = Mathf.Clamp(newIndex, 0, job.ImageVersions.Count - 1);

            if (newIndex != job.SelectedVersionIndex)
            {
                job.SelectedVersionIndex = newIndex;
                _logger?.Info($"Switched to version {newIndex + 1} for: {job.DisplayName}");

                // Update BackendImageId if this version has one (for backend-generated images)
                var selectedVersion = job.GetSelectedVersion();
                if (selectedVersion != null && !string.IsNullOrEmpty(selectedVersion.BackendImageId))
                {
                    job.BackendImageId = selectedVersion.BackendImageId;
                    _logger?.Info($"Updated BackendImageId to: {job.BackendImageId} for selected version");
                }

                // Copy the selected version from History to current ref location
                if (selectedVersion != null)
                {
                    string currentRefPath = BrandingConstants.AssetPaths.GetReferenceImagePath(job.AssetName.Value);
                    string fullCurrentPath = Path.Combine(UnityEngine.Application.dataPath, "..", currentRefPath);

                    string sourcePath = Path.Combine(UnityEngine.Application.dataPath, "..", selectedVersion.ImagePath);
                    _logger?.Info($"Using reference image path: {sourcePath}");

                    if (File.Exists(sourcePath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(fullCurrentPath));
                        File.Copy(sourcePath, fullCurrentPath, true);
                        _logger?.Info($"✓ Copied version {newIndex + 1} to current reference: {currentRefPath}");
                    }
                    else
                    {
                        _logger?.Warning($"✗ Source image not found: {sourcePath}");
                    }
                }

                _lastImageViewMethod = "arrows";
                RefreshUI();
            }
        }

        private void LoadAndDisplayImage(Job job)
        {
            _imageContainer.Clear();

            // Unload previous texture
            if (_currentTexture != null)
            {
                DestroyImmediate(_currentTexture);
                _currentTexture = null;
            }

            string imagePath = ResolvePreviewImagePath(job, out bool fellBackToTransparent);
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                var errorLabel = new Label("Image not found");
                errorLabel.style.color = Color.red;
                _imageContainer.Add(errorLabel);
                return;
            }

            string fullImagePath = Path.Combine(UnityEngine.Application.dataPath, "..", imagePath);

            if (!File.Exists(fullImagePath))
            {
                var errorLabel = new Label($"Image not found: {imagePath}");
                errorLabel.style.color = Color.red;
                _imageContainer.Add(errorLabel);
                return;
            }

            if (fellBackToTransparent)
            {
                var warningLabel = new Label("Other image is unavailable for this version. Showing default preview.");
                warningLabel.style.color = new Color(1f, 0.8f, 0.3f);
                warningLabel.style.marginBottom = 6;
                _imageContainer.Add(warningLabel);
            }

            // Load texture
            byte[] imageData = File.ReadAllBytes(fullImagePath);
            _currentTexture = new Texture2D(2, 2);
            _currentTexture.LoadImage(imageData);

            // Emit view event only when (job_id, index) changes.
            string jobIdStr = job.Id?.ToString() ?? "";
            int index = job.SelectedVersionIndex;

            if (!string.IsNullOrEmpty(jobIdStr) && index != _lastViewedIndex)
            {
                Preview2DImageViewed?.Invoke(
                    "preview2d_image_viewed",
                    new Dictionary<string, object>
                    {
                        { "job_id", jobIdStr },
                        { "index", index },
                        { "view_method", _lastImageViewMethod },
                    }
                );

                _lastViewedIndex = index;
            }

            _lastImageViewMethod = "click";

            // Calculate display size (fit within container, maintain aspect ratio)
            float maxWidth = 700;
            float maxHeight = 400;
            float aspectRatio = (float)_currentTexture.width / _currentTexture.height;

            float displayWidth = maxWidth;
            float displayHeight = maxWidth / aspectRatio;

            if (displayHeight > maxHeight)
            {
                displayHeight = maxHeight;
                displayWidth = maxHeight * aspectRatio;
            }

            // Create image element
            var imageElement = new Image();
            imageElement.image = _currentTexture;
            imageElement.scaleMode = ScaleMode.ScaleToFit;
            imageElement.style.width = displayWidth;
            imageElement.style.height = displayHeight;
            imageElement.style.maxWidth = displayWidth;
            imageElement.style.maxHeight = displayHeight;

            _imageContainer.Add(imageElement);
        }

        private bool IsTransparencyPreviewEligibleJob(Job job)
        {
            if (job == null)
            {
                return false;
            }

            // Transparency controls are only supported for 2D decor jobs.
            return job.IsDecor2DAsset;
        }

        private bool ShouldShowTransparencyPreviewToggle(Job job)
        {
            if (!IsTransparencyPreviewEligibleJob(job) || !job.GenerateTransparent)
            {
                return false;
            }

            return TryGetExistingOriginalImagePath(job, out _);
        }

        private bool ShouldShowMakeTransparentButton(Job job)
        {
            if (!IsTransparencyPreviewEligibleJob(job))
            {
                return false;
            }

            if (!IsTransparencySupportedForSelectedSubcategory(job))
            {
                return false;
            }

            // Primary intent: show for non-transparent jobs.
            // Recovery path: also show when transparency is enabled but originals are missing.
            if (!job.GenerateTransparent)
            {
                return true;
            }

            return !TryGetExistingOriginalImagePath(job, out _);
        }

        private bool IsTransparencySupportedForSelectedSubcategory(Job job)
        {
            if (job == null)
            {
                return false;
            }

            string selectedSubcategory = job.BackendSubcategory?.Trim();
            if (string.IsNullOrWhiteSpace(selectedSubcategory))
            {
                // Older jobs may not have subcategory persisted yet.
                return true;
            }

            var decorConfig = GeniesArtForgeService.BackendConfigProvider?.CachedConfig?.DecorConfig;
            if (decorConfig?.types == null || decorConfig.types.Length == 0)
            {
                // If config is unavailable, do not hide action unexpectedly.
                return true;
            }

            string specificType = GetSpecificTypeFromAssetTypeId(job.AssetTypeId);
            if (string.IsNullOrWhiteSpace(specificType))
            {
                return true;
            }

            var decorType = decorConfig.types.FirstOrDefault(type =>
                type != null &&
                ((!string.IsNullOrWhiteSpace(type.type) &&
                  string.Equals(type.type, specificType, StringComparison.OrdinalIgnoreCase)) ||
                 (!string.IsNullOrWhiteSpace(type.name) &&
                  string.Equals(type.name, specificType, StringComparison.OrdinalIgnoreCase))));
            if (decorType?.subcategories == null || decorType.subcategories.Length == 0)
            {
                return true;
            }

            var selectedConfigSubcategory = decorType.subcategories.FirstOrDefault(subcategory =>
                subcategory != null &&
                !string.IsNullOrWhiteSpace(subcategory.name) &&
                string.Equals(subcategory.name, selectedSubcategory, StringComparison.OrdinalIgnoreCase));
            if (selectedConfigSubcategory == null)
            {
                return true;
            }

            // Explicit false means the selected subcategory does not support transparency.
            if (selectedConfigSubcategory.transparentBackground.HasValue &&
                selectedConfigSubcategory.name.ToLower() != "default")
            {
                return selectedConfigSubcategory.transparentBackground.Value;
            }

            return true;
        }

        private static string GetSpecificTypeFromAssetTypeId(string assetTypeId)
        {
            if (string.IsNullOrWhiteSpace(assetTypeId))
            {
                return string.Empty;
            }

            string[] parts = assetTypeId.Split(new[] { AssetCategories.Delimiter }, StringSplitOptions.None);
            if (parts.Length > 1)
            {
                return parts[1]?.Trim() ?? string.Empty;
            }

            return string.Empty;
        }

        private bool ShouldDefaultToTransparentPreview(Job job)
        {
            if (job == null)
            {
                return true;
            }

            if (!TryGetExistingOriginalImagePath(job, out string originalImagePath))
            {
                return true;
            }

            return HasMagentaCornerBackground(originalImagePath);
        }

        private string ResolvePreviewImagePath(Job job, out bool fellBackToTransparent)
        {
            fellBackToTransparent = false;

            string transparentPath = BrandingConstants.AssetPaths.GetReferenceImagePath(job.AssetName.Value);
            bool shouldShowTransparent = _showTransparentToggle == null || _showTransparentToggle.value;
            if (!ShouldShowTransparencyPreviewToggle(job) || shouldShowTransparent)
            {
                return transparentPath;
            }

            string selectedVersionPath = job.GetSelectedImagePath();
            string selectedOriginalPath = GetOriginalImagePath(selectedVersionPath);
            if (AssetPathExists(selectedOriginalPath))
            {
                return selectedOriginalPath;
            }

            string currentOriginalPath = GetOriginalImagePath(transparentPath);
            if (AssetPathExists(currentOriginalPath))
            {
                return currentOriginalPath;
            }

            fellBackToTransparent = true;
            return transparentPath;
        }

        private bool TryGetExistingOriginalImagePath(Job job, out string originalImagePath)
        {
            originalImagePath = null;
            if (job == null)
            {
                return false;
            }

            string selectedOriginalPath = GetOriginalImagePath(job.GetSelectedImagePath());
            if (AssetPathExists(selectedOriginalPath))
            {
                originalImagePath = selectedOriginalPath;
                return true;
            }

            string currentTransparentPath = BrandingConstants.AssetPaths.GetReferenceImagePath(job.AssetName.Value);
            string currentOriginalPath = GetOriginalImagePath(currentTransparentPath);
            if (AssetPathExists(currentOriginalPath))
            {
                originalImagePath = currentOriginalPath;
                return true;
            }

            return false;
        }

        private static bool HasMagentaCornerBackground(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return true;
            }

            string fullPath = Path.Combine(UnityEngine.Application.dataPath, "..", assetPath);
            if (!File.Exists(fullPath))
            {
                return true;
            }

            Texture2D texture = null;
            try
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes, false))
                {
                    return true;
                }

                int sampleSize = Mathf.Clamp(4, 1, Mathf.Min(texture.width, texture.height));
                Color bottomLeft = SampleCornerAverage(texture, 0, 0, sampleSize, sampleSize);
                Color bottomRight = SampleCornerAverage(texture, texture.width - sampleSize, 0, sampleSize, sampleSize);
                Color topLeft = SampleCornerAverage(texture, 0, texture.height - sampleSize, sampleSize, sampleSize);
                Color topRight = SampleCornerAverage(texture, texture.width - sampleSize, texture.height - sampleSize, sampleSize, sampleSize);

                const float magentaDistanceThreshold = 0.28f;
                int magentaCorners = 0;
                if (RgbDistance(bottomLeft, Color.magenta) <= magentaDistanceThreshold)
                {
                    magentaCorners++;
                }

                if (RgbDistance(bottomRight, Color.magenta) <= magentaDistanceThreshold)
                {
                    magentaCorners++;
                }

                if (RgbDistance(topLeft, Color.magenta) <= magentaDistanceThreshold)
                {
                    magentaCorners++;
                }

                if (RgbDistance(topRight, Color.magenta) <= magentaDistanceThreshold)
                {
                    magentaCorners++;
                }

                return magentaCorners >= 3;
            }
            catch
            {
                return true;
            }
            finally
            {
                if (texture != null)
                {
                    DestroyImmediate(texture);
                }
            }
        }

        private static Color SampleCornerAverage(Texture2D texture, int startX, int startY, int width, int height)
        {
            Color total = Color.black;
            int count = 0;
            int endX = Mathf.Min(texture.width, startX + width);
            int endY = Mathf.Min(texture.height, startY + height);

            for (int y = Mathf.Max(0, startY); y < endY; y++)
            {
                for (int x = Mathf.Max(0, startX); x < endX; x++)
                {
                    total += texture.GetPixel(x, y);
                    count++;
                }
            }

            return count > 0 ? total / count : Color.black;
        }

        private static float RgbDistance(Color a, Color b)
        {
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }

        private static bool AssetPathExists(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            string fullPath = Path.Combine(UnityEngine.Application.dataPath, "..", assetPath);
            return File.Exists(fullPath);
        }

        private static string GetOriginalImagePath(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return null;
            }

            string directory = Path.GetDirectoryName(imagePath);
            string fileName = Path.GetFileNameWithoutExtension(imagePath);
            string extension = Path.GetExtension(imagePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            if (!fileName.EndsWith("_original", StringComparison.OrdinalIgnoreCase))
            {
                fileName += "_original";
            }

            string combined = string.IsNullOrWhiteSpace(directory)
                ? $"{fileName}{extension}"
                : Path.Combine(directory, $"{fileName}{extension}");

            return combined.Replace('\\', '/');
        }

        private void OnMakeTransparentClicked()
        {
            var job = GetCurrentJob();
            if (!ShouldShowMakeTransparentButton(job))
            {
                return;
            }

            _isApplyingTransparency = true;
            try
            {
                if (job.QualitySettings == null)
                {
                    EditorUtility.DisplayDialog(
                        "Make Transparent",
                        "Quality settings are missing on this job, cannot enable transparency.",
                        "OK");
                    return;
                }

                var imagePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string currentRefPath = BrandingConstants.AssetPaths.GetReferenceImagePath(job.AssetName.Value);
                if (!string.IsNullOrWhiteSpace(currentRefPath))
                {
                    imagePaths.Add(currentRefPath);
                }

                if (job.ImageVersions != null)
                {
                    foreach (var version in job.ImageVersions)
                    {
                        if (!string.IsNullOrWhiteSpace(version?.ImagePath))
                        {
                            imagePaths.Add(version.ImagePath);
                        }
                    }
                }

                int convertedCount = 0;
                foreach (string imagePath in imagePaths)
                {
                    if (ConvertImageToTransparent(imagePath))
                    {
                        convertedCount++;
                    }
                }

                if (convertedCount == 0)
                {
                    EditorUtility.DisplayDialog(
                        "Make Transparent",
                        "No images were converted. Check that source image files exist.",
                        "OK");
                    return;
                }

                job.QualitySettings.GenerateTransparent = true;
                GeniesArtForgeService.Queue?.SaveJobState(job);
                AssetDatabase.Refresh();

                _logger?.Info($"[ApprovalDialog] Converted {convertedCount} image(s) to transparent for job {job.DisplayName}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"[ApprovalDialog] Make Transparent failed: {ex.Message}", ex);
                EditorUtility.DisplayDialog(
                    "Make Transparent Failed",
                    $"Failed to convert images:\n{ex.Message}",
                    "OK");
            }
            finally
            {
                _isApplyingTransparency = false;
                RefreshUI();
            }
        }

        private bool ConvertImageToTransparent(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return false;
            }

            string fullImagePath = Path.Combine(UnityEngine.Application.dataPath, "..", imagePath);
            if (!File.Exists(fullImagePath))
            {
                return false;
            }

            string originalImagePath = GetOriginalImagePath(imagePath);
            if (string.IsNullOrWhiteSpace(originalImagePath))
            {
                return false;
            }

            string fullOriginalImagePath = Path.Combine(UnityEngine.Application.dataPath, "..", originalImagePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOriginalImagePath) ?? string.Empty);

            if (!File.Exists(fullOriginalImagePath))
            {
                File.Move(fullImagePath, fullOriginalImagePath);
            }

            Texture2D sourceTexture = null;
            Texture2D processedTexture = null;
            try
            {
                byte[] imageData = File.ReadAllBytes(fullOriginalImagePath);
                sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!sourceTexture.LoadImage(imageData, false))
                {
                    return false;
                }

                var settings = new TransparencyProcessor.Settings
                {
                    manualBackgroundColor = Color.magenta
                };

                var result = TransparencyProcessor.Process(sourceTexture, settings);
                processedTexture = result?.texture;
                if (processedTexture == null)
                {
                    return false;
                }

                byte[] processedBytes = processedTexture.EncodeToPNG();
                if (processedBytes == null || processedBytes.Length == 0)
                {
                    return false;
                }

                File.WriteAllBytes(fullImagePath, processedBytes);
                return true;
            }
            finally
            {
                if (sourceTexture != null)
                {
                    DestroyImmediate(sourceTexture);
                }

                if (processedTexture != null)
                {
                    DestroyImmediate(processedTexture);
                }
            }
        }

        private void OnApproveClicked()
        {
            var job = GetCurrentJob();
            if (job == null)
            {
                return;
            }

            if (job.State != JobState.AwaitingImageConfirm)
            {
                _logger?.Warning($"Cannot approve job from dialog - expected AwaitingImageConfirm but was {job.State}: {job.DisplayName}");
                EditorUtility.DisplayDialog(
                    "Approval Unavailable",
                    "This job is no longer awaiting approval. Reopen preview from the queue once it is ready.",
                    "OK");
                RefreshUI();
                return;
            }

            Preview2DApproved?.Invoke(
                "preview2d_approved",
                new Dictionary<string, object>
                {
                    { "job_id", job.Id?.ToString() ?? "" },
                    { "selected_index", job.SelectedVersionIndex },
                }
            );

            _logger?.Info($"User approved reference image for: {job.DisplayName}");

            // Copy the currently displayed image (transparent or original) to current ref location.
            string currentRefPath = BrandingConstants.AssetPaths.GetReferenceImagePath(job.AssetName.Value);
            string displayImagePath = ResolvePreviewImagePath(job, out _);
            if (string.IsNullOrWhiteSpace(displayImagePath))
            {
                // Fallback to selected version for robustness.
                displayImagePath = job.GetSelectedVersion()?.ImagePath;
            }

            if (!string.IsNullOrWhiteSpace(displayImagePath))
            {
                string fullSourcePath = Path.Combine(UnityEngine.Application.dataPath, "..", displayImagePath);
                string fullCurrentPath = Path.Combine(UnityEngine.Application.dataPath, "..", currentRefPath);

                if (File.Exists(fullSourcePath))
                {
                    if (!string.Equals(
                            Path.GetFullPath(fullSourcePath),
                            Path.GetFullPath(fullCurrentPath),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(fullSourcePath, fullCurrentPath, true);
                    }

                    _logger?.Info($"Copied approved displayed image to current ref: {currentRefPath} (source: {displayImagePath})");
                }
                else
                {
                    _logger?.Warning($"Displayed source image not found during approval copy: {displayImagePath}");
                }
            }

            if (job.IsDecor2DAsset)
            {
                ApplyDecor2DApprovalTextureImportSettings(currentRefPath);
            }

            // Approve the job using QueueService
            GeniesArtForgeService.Queue?.ApproveJob(job.Id);

            bool approvalApplied = job.IsApprovalApplied;
            if (!approvalApplied)
            {
                _logger?.Warning($"QueueService rejected approval for {job.DisplayName} (state: {job.State}, imageConfirmed: {job.ImageConfirmed})");
                EditorUtility.DisplayDialog(
                    "Approval Not Applied",
                    "Unable to approve this preview right now. Please try again.",
                    "OK");
                RefreshUI();
                return;
            }

            // Remove from pending list
            _pendingJobs.RemoveAt(_currentIndex);

            // Move to next job or close
            if (_pendingJobs.Count > 0)
            {
                _currentIndex = Mathf.Clamp(_currentIndex, 0, _pendingJobs.Count - 1);
                RefreshUI();
            }
            else
            {
                Close();
            }
        }

        private void ApplyDecor2DApprovalTextureImportSettings(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            try
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var textureImporter = UnityEditor.AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (textureImporter == null)
                {
                    _logger?.Warning($"[ApprovalDialog] Could not resolve TextureImporter for approved ref image: {assetPath}");
                    return;
                }

                if (textureImporter.npotScale != TextureImporterNPOTScale.ToLarger)
                {
                    textureImporter.npotScale = TextureImporterNPOTScale.ToLarger;
                    textureImporter.SaveAndReimport();
                    _logger?.Info($"[ApprovalDialog] Set NPOT Scale=ToLarger for approved 2D decor image: {assetPath}");
                }
            }
            catch (Exception ex)
            {
                _logger?.Warning($"[ApprovalDialog] Failed to apply NPOT import settings for approved 2D decor image: {ex.Message}");
            }
        }

        private async void OnRetryClicked()
        {
            var job = GetCurrentJob();
            if (job == null)
            {
                return;
            }

            string newPrompt = _promptField.value;

            if (string.IsNullOrWhiteSpace(newPrompt))
            {
                EditorUtility.DisplayDialog("Invalid Prompt", "Please enter a prompt for regeneration.", "OK");
                return;
            }

            var selectedVersionForReason = job.GetSelectedVersion();
            string previousPromptForReason = selectedVersionForReason?.Prompt ?? job.Prompt ?? "";
            string reason = string.Equals((previousPromptForReason ?? "").Trim(), (newPrompt ?? "").Trim(), StringComparison.Ordinal)
                ? "not_happy"
                : "edit_description";

            int attempt = job.CurrentIteration;

            Preview2DRetryClicked?.Invoke(
                "preview2d_retry_clicked",
                new Dictionary<string, object>
                {
                    { "job_id", job.Id?.ToString() ?? "" },
                    { "attempt", attempt },
                    { "reason", reason },
                }
            );

            _logger?.Info($"Inline retry for: {job.DisplayName} with prompt: {newPrompt}");

            bool backendReady = await BackendConnectionRecoveryHelper.EnsureConnectedOrPromptAsync(
                _logger,
                "Real-time backend connection is unavailable. Please reconnect in Settings before retrying image generation.",
                disconnectOnFailure: false);
            if (!backendReady)
            {
                return;
            }

            // Update prompt
            job.Prompt = newPrompt;

            var selectedVersion = job.GetSelectedVersion();
            var previousImageVersions = new List<ImageVersion>(job.ImageVersions);
            var previousSelectedIndex = job.SelectedVersionIndex;
            var previousBackendAssetId = job.BackendAssetId;
            var previousBackendImageId = job.BackendImageId;
            var previousBackendModelId = job.BackendModelId;
            var previousBackendModelGenerationJobId = job.BackendModelGenerationJobId;
            var previousReferencePaths = new List<string>(job.ReferenceImagePaths ?? new List<string>());
            var previousState = job.State;
            var previousImageConfirmed = job.ImageConfirmed;
            bool didOverrideReferencePaths = false;

            // Set regenerating state
            SetRegeneratingState(true, job);
            RefreshUI();

            double startedAt = EditorApplication.timeSinceStartup;

            Preview2DJobStarted?.Invoke(
                "preview2d_job_started",
                new Dictionary<string, object>
                {
                    { "job_id", job.Id?.ToString() ?? "" },
                    { "attempt", attempt },
                }
            );

            try
            {
                // Generate new image using GenerationService
                var generationService = GeniesArtForgeService.Generation;
                if (generationService == null)
                {
                    throw new Exception("GenerationService not available");
                }

                // Temporarily set job to processing state
                job.SetState(JobState.Processing);

                if (FeatureAvailabilityService.IsApprovalRetryButtonAvailable)
                {
                    if (selectedVersion == null || string.IsNullOrEmpty(selectedVersion.ImagePath))
                    {
                        throw new Exception("Selected reference image not available for retry.");
                    }

                    string selectedFullPath = Path.Combine(UnityEngine.Application.dataPath, "..", selectedVersion.ImagePath);
                    if (!File.Exists(selectedFullPath))
                    {
                        throw new Exception($"Selected reference image not found: {selectedVersion.ImagePath}");
                    }

                    if (job.ReferenceImagePaths != null)
                    {
                        job.ReferenceImagePaths.Clear();
                        job.ReferenceImagePaths.Add(selectedFullPath);
                        didOverrideReferencePaths = true;
                    }
                    job.BackendAssetId = null;
                    job.BackendImageId = null;
                    job.BackendModelId = null;
                    job.BackendModelGenerationJobId = null;
                    job.ImageVersions.Clear();
                    job.SelectedVersionIndex = 0;
                }

                // Ensure queue row transitions to Processing and shows per-job progress UI.
                GeniesArtForgeService.Queue?.SaveJobState(job);

                // Generate the image (this will add a new version to the job)
                var result = await generationService.GenerateAssetAsync(job);

                if (result.Success && result.RequiresApproval)
                {
                    var newSelectedVersion = job.GetSelectedVersion();
                    if (newSelectedVersion != null && File.Exists(Path.Combine(UnityEngine.Application.dataPath, "..", newSelectedVersion.ImagePath)))
                    {
                        string currentRefPath = BrandingConstants.AssetPaths.GetReferenceImagePath(job.AssetName.Value);
                        string fullCurrentRefPath = Path.Combine(UnityEngine.Application.dataPath, "..", currentRefPath);
                        string fullSelectedPath = Path.Combine(UnityEngine.Application.dataPath, "..", newSelectedVersion.ImagePath);

                        Directory.CreateDirectory(Path.GetDirectoryName(fullCurrentRefPath));
                        File.Copy(fullSelectedPath, fullCurrentRefPath, true);
                        _logger?.Info($"Copied selected backend image to current reference: {currentRefPath}");
                    }

                    // Restore awaiting state
                    job.SetState(JobState.AwaitingImageConfirm);

                    if (didOverrideReferencePaths && job.ReferenceImagePaths != null)
                    {
                        job.ReferenceImagePaths.Clear();
                        job.ReferenceImagePaths.AddRange(previousReferencePaths);
                    }

                    // Save job state
                    GeniesArtForgeService.Queue?.SaveJobState(job);

                    _logger?.Info($"Regeneration complete - version {job.ImageVersions.Count} added (iteration {job.CurrentIteration})");

                    long latencyMs = (long)Math.Max(0d, (EditorApplication.timeSinceStartup - startedAt) * 1000d);

                    Preview2DJobSucceeded?.Invoke(
                        "preview2d_job_succeeded",
                        new Dictionary<string, object>
                        {
                            { "job_id", job.Id?.ToString() ?? "" },
                            { "attempt", attempt },
                            { "latency_ms", latencyMs },
                            { "image_count", job.ImageVersions?.Count ?? 0 },
                        }
                    );

                    EditorSfx.PlayPreviewReady(job.Id?.ToString() ?? string.Empty);

                    // Refresh to show new version
                    SetRegeneratingState(false, job);
                    RefreshUI();
                }
                else
                {
                    throw new Exception(result.ErrorMessage ?? "Generation failed");
                }
            }
            catch (Exception ex)
            {
                long latencyMs = (long)Math.Max(0d, (EditorApplication.timeSinceStartup - startedAt) * 1000d);

                Preview2DJobFailed?.Invoke(
                    "preview2d_job_failed",
                    new Dictionary<string, object>
                    {
                        { "job_id", job.Id?.ToString() ?? "" },
                        { "attempt", attempt },
                        { "error_code", ex.GetType().Name },
                        { "latency_ms", latencyMs },
                    }
                );

                job.ImageVersions.Clear();
                job.ImageVersions.AddRange(previousImageVersions);
                job.SelectedVersionIndex = previousSelectedIndex;
                job.BackendAssetId = previousBackendAssetId;
                job.BackendImageId = previousBackendImageId;
                job.BackendModelId = previousBackendModelId;
                job.BackendModelGenerationJobId = previousBackendModelGenerationJobId;
                if (job.ReferenceImagePaths != null)
                {
                    job.ReferenceImagePaths.Clear();
                    job.ReferenceImagePaths.AddRange(previousReferencePaths);
                }
                job.ImageConfirmed = previousImageConfirmed;
                job.SetState(previousState);
                GeniesArtForgeService.Queue?.SaveJobState(job);

                SetRegeneratingState(false, job);
                RefreshUI();

                if (BackendConnectionRecoveryHelper.IsRecoverableConnectionError(ex))
                {
                    await BackendConnectionRecoveryHelper.EnsureConnectedOrPromptAsync(
                        _logger,
                        "Real-time backend connection is reconnecting. Please reconnect in Settings before retrying image generation.",
                        disconnectOnFailure: false);
                    return;
                }

                _logger?.Error($"Inline retry failed: {ex.Message}", ex);
                EditorUtility.DisplayDialog("Regeneration Failed", $"Failed to regenerate image:\n{ex.Message}", "OK");
            }
        }

        private void SetRegeneratingState(bool isRegenerating, Job job)
        {
            _isRegenerating = isRegenerating;
            if (_isRegenerating)
            {
                _regeneratingJobId = job?.Id?.ToString();
                job?.UpdateProgress(0f, "Regenerating reference image...");
                _displayedRetryProgress = Mathf.Clamp01(job?.Progress ?? 0f);
                _lastRetryProgressUpdateTime = Time.realtimeSinceStartupAsDouble;
                StartRetryProgressUpdates();
                UpdateRetryProgressUI();
            }
            else
            {
                _regeneratingJobId = null;
                StopRetryProgressUpdates();
            }
        }

        private void StartRetryProgressUpdates()
        {
            if (_root == null || _retryProgressRefreshItem != null)
            {
                return;
            }

            _retryProgressRefreshItem = _root.schedule
                .Execute(UpdateRetryProgressUI)
                .Every(RetryProgressRefreshIntervalMs);
        }

        private void StopRetryProgressUpdates()
        {
            _retryProgressRefreshItem?.Pause();
            _retryProgressRefreshItem = null;
        }

        private void UpdateRetryProgressUI()
        {
            if (!_isRegenerating || _retryProgressBar == null || _retryProgressLabel == null)
            {
                return;
            }

            var job = GetCurrentJob();
            if (job == null)
            {
                return;
            }

            if (!IsRegeneratingJob(job))
            {
                SetRegeneratingState(false, null);
                return;
            }

            float targetProgress = Mathf.Clamp01(job.Progress);
            double now = Time.realtimeSinceStartupAsDouble;
            double deltaTime = Math.Max(0d, now - _lastRetryProgressUpdateTime);

            if (_displayedRetryProgress < targetProgress)
            {
                _displayedRetryProgress = Mathf.Lerp(
                    _displayedRetryProgress,
                    targetProgress,
                    (float)deltaTime * RetryAnimationSpeed);
                _displayedRetryProgress = Mathf.Min(_displayedRetryProgress, targetProgress);
            }
            else if (_displayedRetryProgress < RetryFakeProgressCap)
            {
                _displayedRetryProgress += RetryFakeProgressSpeed * (float)deltaTime;
                _displayedRetryProgress = Mathf.Min(_displayedRetryProgress, RetryFakeProgressCap);
            }

            _lastRetryProgressUpdateTime = now;

            _retryProgressBar.value = _displayedRetryProgress * ProgressPercentScale;
            _retryProgressLabel.text = GetRetryProgressText(job);
        }

        private static string GetRetryProgressText(Job job)
        {
            if (job == null)
            {
                return "Regenerating reference image...";
            }

            if (!string.IsNullOrWhiteSpace(job.CurrentStep))
            {
                return JobStepFormatter.GetStageName(job.CurrentStep);
            }

            if (!string.IsNullOrWhiteSpace(job.ProgressMessage))
            {
                return job.ProgressMessage;
            }

            return "Regenerating reference image...";
        }

        private void OnCancelClicked()
        {
            var job = GetCurrentJob();
            if (job == null)
            {
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Cancel Job?",
                $"Are you sure you want to cancel '{job.DisplayName}'?",
                "Yes, Cancel",
                "No"
            );

            if (!confirmed)
            {
                return;
            }

            Preview2DCancelled?.Invoke(
                "preview2d_cancelled",
                new Dictionary<string, object>
                {
                    { "job_id", job.Id?.ToString() ?? "" },
                    { "reason", "edit_request" },
                }
            );

            _logger?.Info($"User cancelled job: {job.DisplayName}");

            // Cancel the job
            if (GeniesArtForgeService.Queue != null)
            {
                _isCancellingFromDialog = true;
                try
                {
                    GeniesArtForgeService.Queue.CancelJob(job.Id);
                }
                finally
                {
                    _isCancellingFromDialog = false;
                }
            }

            // Remove from pending list
            _pendingJobs.RemoveAt(_currentIndex);

            // Move to next job or close
            if (_pendingJobs.Count > 0)
            {
                _currentIndex = Mathf.Clamp(_currentIndex, 0, _pendingJobs.Count - 1);
                RefreshUI();
            }
            else
            {
                Close();
            }
        }

        private void OnEnable()
        {
            SubscribeToQueueEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromQueueEvents();
        }

        private void SubscribeToQueueEvents()
        {
            var queue = GeniesArtForgeService.Queue;
            if (queue == null)
            {
                return;
            }

            if (ReferenceEquals(_queueEventSource, queue))
            {
                return;
            }

            UnsubscribeFromQueueEvents();
            _queueEventSource = queue;
            _queueEventSource.OnJobCancelled += OnExternalJobCancelled;
        }

        private void UnsubscribeFromQueueEvents()
        {
            if (_queueEventSource == null)
            {
                return;
            }

            _queueEventSource.OnJobCancelled -= OnExternalJobCancelled;
            _queueEventSource = null;
        }

        private void OnExternalJobCancelled(Job job)
        {
            if (job == null || _pendingJobs == null || _instance != this)
            {
                return;
            }

            if (_isCancellingFromDialog)
            {
                return;
            }

            bool cancelledJobWasInDialog = _pendingJobs.Any(j => j.Id == job.Id);
            if (!cancelledJobWasInDialog)
            {
                return;
            }

            var currentJob = GetCurrentJob();
            bool isActiveJobCancelled = currentJob != null && currentJob.Id == job.Id;
            if (isActiveJobCancelled)
            {
                _logger?.Info($"Closing approval dialog because active job was cancelled from queue: {job.DisplayName}");
                Close();
                return;
            }

            // Keep dialog open for non-active job cancels, but remove stale entries.
            int cancelledIndex = _pendingJobs.FindIndex(j => j.Id == job.Id);
            if (cancelledIndex < 0)
            {
                return;
            }

            _pendingJobs.RemoveAt(cancelledIndex);
            if (_pendingJobs.Count == 0)
            {
                Close();
                return;
            }

            if (cancelledIndex < _currentIndex)
            {
                _currentIndex--;
            }

            _currentIndex = Mathf.Clamp(_currentIndex, 0, _pendingJobs.Count - 1);
            RefreshUI();
        }

        private Job GetCurrentJob()
        {
            if (_pendingJobs == null || _currentIndex < 0 || _currentIndex >= _pendingJobs.Count)
            {
                return null;
            }

            return _pendingJobs[_currentIndex];
        }

        private bool IsRegeneratingJob(Job job)
        {
            if (!_isRegenerating || job == null)
            {
                return false;
            }

            string jobId = job.Id?.ToString();
            return !string.IsNullOrEmpty(jobId)
                   && !string.IsNullOrEmpty(_regeneratingJobId)
                   && string.Equals(_regeneratingJobId, jobId, StringComparison.Ordinal);
        }

        private void OnDestroy()
        {
            StopRetryProgressUpdates();
            UnsubscribeFromQueueEvents();

            if (_currentTexture != null)
            {
                DestroyImmediate(_currentTexture);
                _currentTexture = null;
            }

            _promptInitializedJobId = null;
            _lastViewedJobId = null;
            _lastViewedIndex = -1;
            _regeneratingJobId = null;

            _instance = null;
        }
    }
}
