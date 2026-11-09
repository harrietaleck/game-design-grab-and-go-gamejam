using System;
using System.Collections.Generic;
using System.Linq;
using Genies.Sdk.ArtForge.Editor.Components.ModelGenerator.Config;
using Genies.Sdk.ArtForge.Editor.Components.ModelGenerator.Job;
using Genies.Sdk.ArtForge.Editor.Components.ModelGenerator.UI;
using Genies.Sdk.ArtForge.Editor.Core.Application.Services;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using Genies.Sdk.ArtForge.Editor.Core.UI.Helpers;
using UnityEditor;
using UnityEngine;

namespace Genies.Sdk.ArtForge.Editor.Components.ModelGenerator
{
    /// <summary>
    /// Presenter for Model Generator.
    /// Binds UI callbacks, mutates state, and triggers re-rendering.
    /// Event-driven architecture - no per-frame polling.
    /// </summary>
    internal sealed class ModelGeneratorPresenter : IDisposable
    {
        // ------------------------------------------------------------
        // Telemetry signals (Observer subscribes to these)
        // ------------------------------------------------------------
        public static event Action<string, Dictionary<string, object>> ModelGenTelemetry;

        private readonly ModelGeneratorState _state;
        private readonly IModelGeneratorConfigProvider _configProvider;
        private readonly GenerationService _generationService;
        private readonly JobFactory _jobFactory;
        private readonly JobRequestValidator _validator;
        private readonly ILogger _logger;

        private BasicInfoPanel _basicInfoPanel;
        private ITypeSelectionSection _typeSelectionSection;
        private ReferenceImagesSection _refImagesSection;

        // Track the last enqueued job to show its completion/failure status
        private JobId _lastEnqueuedJobId;

        public event Action<string, Color> OnStatusMessageChanged;
        public event Action OnStateChanged;

        // ------------------------------------------------------------
        // Telemetry state (minimal)
        // ------------------------------------------------------------
        private string _lastAssetName = null;
        private string _lastDescription = null;
        private string _lastBackendCategory = null;
        private string _lastBackendSpecificType = null;

        private double _nextTypedFieldTelemetryAt = 0d;
        private const double TypedFieldTelemetryCooldownSeconds = 1.5d;

        private HashSet<string> _lastReferenceImages = new HashSet<string>();

        public ModelGeneratorPresenter(
            ModelGeneratorState state,
            IModelGeneratorConfigProvider configProvider,
            GenerationService generationService,
            ILogger logger = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
            _generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));
            _logger = logger ?? GeniesArtForgeService.Logger ?? new UnityLogger();

            _jobFactory = new JobFactory();
            _validator = new JobRequestValidator();

            // Subscribe to config provider events
            _configProvider.OnStatusChanged += OnConfigStatusChanged;

            // Subscribe to queue events to track job completion/failure
            var queue = GeniesArtForgeService.Queue;
            if (queue != null)
            {
                queue.OnJobCompleted += OnJobCompleted;
                queue.OnJobFailed += OnJobFailed;
            }

            // Seed telemetry “last” values from initial state
            _lastAssetName = _state.AssetName;
            _lastDescription = _state.Description;
            _lastBackendCategory = _state.BackendCategory;
            _lastBackendSpecificType = _state.BackendSpecificType;

            if (_state.ReferenceImages != null)
            {
                _lastReferenceImages = new HashSet<string>(_state.ReferenceImages);
            }
        }

        /// <summary>
        /// Bind to UI panels
        /// </summary>
        public void BindPanels(
            BasicInfoPanel basicInfoPanel,
            ITypeSelectionSection typeSelectionSection,
            ReferenceImagesSection refImagesSection)
        {
            _basicInfoPanel = basicInfoPanel;
            _typeSelectionSection = typeSelectionSection;
            _refImagesSection = refImagesSection;

            // Register UI callbacks
            if (_basicInfoPanel != null)
            {
                _basicInfoPanel.OnChanged += OnBasicInfoChanged;
            }

            if (_typeSelectionSection != null)
            {
                _typeSelectionSection.OnSelectionChanged += OnTypeSelectionChanged;

                // If it's backend UI, handle retry
                if (_typeSelectionSection is BackendTypeSelectionSection backendUI)
                {
                    backendUI.OnRetryRequested += OnConfigRetryRequested;
                }
            }

            // Only subscribe to reference images if panel exists (not in backend mode)
            if (_refImagesSection != null)
            {
                _refImagesSection.OnChanged += OnReferenceImagesChanged;
                _lastReferenceImages = new HashSet<string>(_refImagesSection.ImagePaths ?? new List<string>());
            }

            // Initial render
            Render();
        }

        /// <summary>
        /// Initialize (load backend config)
        /// </summary>
        public async void Initialize()
        {
            // Load backend config (backend-only mode)
            await LoadBackendConfigAsync();
        }

        /// <summary>
        /// Render UI from current state
        /// </summary>
        private void Render()
        {
            // Bind basic info
            _basicInfoPanel?.Bind(_state);

            // Show/hide description based on feature availability
            bool showDescription = FeatureAvailabilityService.IsDescriptionFieldAvailable(_state.BackendCategory);
            _basicInfoPanel?.SetDescriptionVisible(showDescription);

            // Bind type selection
            var config = _configProvider.CachedConfig;
            _typeSelectionSection?.Bind(_state, config);

            // Bind reference images (only if panel exists - not available in backend mode)
            if (_refImagesSection != null)
            {
                _refImagesSection.SetImages(_state.ReferenceImages);
            }

            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// Load backend configuration
        /// </summary>
        private async System.Threading.Tasks.Task LoadBackendConfigAsync()
        {
            _typeSelectionSection?.SetLoading(true, "Loading backend configuration...");
            _typeSelectionSection?.ClearError();

            try
            {
                var result = await _configProvider.GetAsync();

                if (result.IsSuccess)
                {
                    Render();
                }
                else
                {
                    _typeSelectionSection?.SetError(result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[ModelGeneratorPresenter] Failed to load config: {ex}");
                _typeSelectionSection?.SetError($"Failed to load configuration: {ex.Message}");
            }
            finally
            {
                _typeSelectionSection?.SetLoading(false);
            }
        }

        /// <summary>
        /// Handle config status changes
        /// </summary>
        private void OnConfigStatusChanged(ConfigLoadStatus status)
        {
            switch (status)
            {
                case ConfigLoadStatus.Loading:
                    _typeSelectionSection?.SetLoading(true, "Loading backend configuration...");
                    break;

                case ConfigLoadStatus.Loaded:
                    _typeSelectionSection?.SetLoading(false);
                    _typeSelectionSection?.ClearError();
                    Render();
                    break;

                case ConfigLoadStatus.Failed:
                    _typeSelectionSection?.SetLoading(false);
                    _typeSelectionSection?.SetError(_configProvider.LastError);
                    break;
            }
        }

        /// <summary>
        /// Handle config retry request
        /// </summary>
        private async void OnConfigRetryRequested()
        {
            await LoadBackendConfigAsync();
        }

        /// <summary>
        /// Handle basic info changes
        /// </summary>
        private void OnBasicInfoChanged()
        {
            _basicInfoPanel?.UpdateState(_state);

            RecordTypedFieldChange();

            OnStateChanged?.Invoke();
        }

        private void RecordTypedFieldChange()
        {
            // Only emit at most once per cooldown window (covers rapid typing)
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextTypedFieldTelemetryAt)
            {
                return;
            }

            bool emitted = false;

            if (_state.AssetName != _lastAssetName)
            {
                _lastAssetName = _state.AssetName;
                EmitFieldChanged("name", _state.AssetName, valueSource: "typed");
                emitted = true;
            }

            if (_state.Description != _lastDescription)
            {
                _lastDescription = _state.Description;
                EmitFieldChanged("type_description", _state.Description, valueSource: "typed");
                emitted = true;
            }

            if (emitted)
            {
                _nextTypedFieldTelemetryAt = now + TypedFieldTelemetryCooldownSeconds;
            }
        }

        /// <summary>
        /// Handle type selection changes
        /// </summary>
        private void OnTypeSelectionChanged()
        {
            _typeSelectionSection?.UpdateState(_state);

            if (_state.BackendCategory != _lastBackendCategory)
            {
                _lastBackendCategory = _state.BackendCategory;
                EmitFieldChanged("asset_category", _state.BackendCategory, valueSource: "dropdown");
            }

            if (_state.BackendSpecificType != _lastBackendSpecificType)
            {
                _lastBackendSpecificType = _state.BackendSpecificType;
                EmitFieldChanged("wearable_type", _state.BackendSpecificType, valueSource: "dropdown");
            }

            // If category changed, update description visibility
            bool showDescription = FeatureAvailabilityService.IsDescriptionFieldAvailable(_state.BackendCategory);
            _basicInfoPanel?.SetDescriptionVisible(showDescription);

            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// Handle reference images changes
        /// </summary>
        private void OnReferenceImagesChanged()
        {
            if (_refImagesSection == null)
            {
                return;
            }

            var newSet = new HashSet<string>(_refImagesSection.ImagePaths ?? new List<string>());

            // Added
            foreach (var added in newSet)
            {
                if (!_lastReferenceImages.Contains(added))
                {
                    ModelGenTelemetry?.Invoke("model_gen_attach_image", new Dictionary<string, object> { });
                }
            }

            // Removed
            foreach (var removed in _lastReferenceImages)
            {
                if (!newSet.Contains(removed))
                {
                    ModelGenTelemetry?.Invoke("model_gen_detach_image", new Dictionary<string, object> { });
                }
            }

            _lastReferenceImages = newSet;

            // Update state from UI
            _state.ReferenceImages.Clear();
            _state.ReferenceImages.AddRange(_refImagesSection.ImagePaths);

            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// Generate job (validate, create, enqueue)
        /// </summary>
        public void GenerateJob()
        {
            // Update state from UI
            _basicInfoPanel?.UpdateState(_state);
            _typeSelectionSection?.UpdateState(_state);

            // Validate
            var validationResult = _validator.Validate(_state);
            if (!validationResult.IsSuccess)
            {
                var missing = GetMissingFieldsForTelemetry(_state, validationResult.Error);
                var invalid = GetInvalidFieldsForTelemetry(_state, validationResult.Error);
                
                ModelGenTelemetry?.Invoke("model_gen_validation_failed", new Dictionary<string, object>
                {
                    { "missing_fields", missing.ToArray() },
                    { "invalid_fields", invalid.ToArray() },
                    { "error_code", GetErrorCodeForTelemetry(_state, validationResult.Error) }
                });

                OnStatusMessageChanged?.Invoke(validationResult.Error, StyleHelpers.Colors.Error);
                return;
            }

            // Create job
            var jobResult = _jobFactory.Create(_state);
            if (!jobResult.IsSuccess)
            {
                OnStatusMessageChanged?.Invoke(jobResult.Error, StyleHelpers.Colors.Error);
                return;
            }

            try
            {
                // Enqueue job
                var job = jobResult.Value;
                GeniesArtForgeService.Queue.EnqueueJob(job);

                // Track this job to show completion/failure status
                _lastEnqueuedJobId = job.Id;

                OnStatusMessageChanged?.Invoke($"Job '{job.DisplayName}' added to queue!", StyleHelpers.Colors.Success);
            }
            catch (Exception ex)
            {
                OnStatusMessageChanged?.Invoke($"Error: {ex.Message}", StyleHelpers.Colors.Error);
                _logger.Error($"[ModelGeneratorPresenter] Failed to enqueue job: {ex}");
            }
        }

        /// <summary>
        /// Clear form and reset state
        /// </summary>
        public void ClearForm()
        {
            _state.Reset();
            _basicInfoPanel?.Clear();

            if (_refImagesSection != null)
            {
                _refImagesSection.Clear();
                _lastReferenceImages.Clear();
            }

            Render();
        }

        private void OnJobCompleted(Core.Domain.Entities.Job job)
        {
            if (_lastEnqueuedJobId != null && job.Id.Equals(_lastEnqueuedJobId))
            {
                OnStatusMessageChanged?.Invoke(
                    $"✓ '{job.DisplayName}' completed successfully!",
                    StyleHelpers.Colors.Success);
                _lastEnqueuedJobId = null;
            }
        }

        private void OnJobFailed(Core.Domain.Entities.Job job)
        {
            if (_lastEnqueuedJobId != null && job.Id.Equals(_lastEnqueuedJobId))
            {
                string errorMsg = string.IsNullOrEmpty(job.ErrorMessage)
                    ? "Job failed"
                    : job.ErrorMessage;

                OnStatusMessageChanged?.Invoke(
                    $"✗ '{job.DisplayName}' failed: {errorMsg}",
                    StyleHelpers.Colors.Error);
                _lastEnqueuedJobId = null;
            }
        }

        public void Dispose()
        {
            var queue = GeniesArtForgeService.Queue;
            if (queue != null)
            {
                queue.OnJobCompleted -= OnJobCompleted;
                queue.OnJobFailed -= OnJobFailed;
            }

            if (_configProvider != null)
            {
                _configProvider.OnStatusChanged -= OnConfigStatusChanged;
            }

            if (_basicInfoPanel != null)
            {
                _basicInfoPanel.OnChanged -= OnBasicInfoChanged;
            }

            if (_typeSelectionSection != null)
            {
                _typeSelectionSection.OnSelectionChanged -= OnTypeSelectionChanged;

                if (_typeSelectionSection is BackendTypeSelectionSection backendUI)
                {
                    backendUI.OnRetryRequested -= OnConfigRetryRequested;
                }
            }

            if (_refImagesSection != null)
            {
                _refImagesSection.OnChanged -= OnReferenceImagesChanged;
                _refImagesSection.Dispose();
            }
        }

        // ------------------------------------------------------------
        // Telemetry helpers (event-only)
        // ------------------------------------------------------------

        private static void EmitFieldChanged(string field, object value, string valueSource)
        {
            int valueLength = 0;
            if (value != null)
            {
                string s = value as string ?? value.ToString();
                valueLength = string.IsNullOrEmpty(s) ? 0 : s.Length;
            }
            
            ModelGenTelemetry?.Invoke("model_gen_field_changed",  new Dictionary<string, object>
            {
                { "field", field },
                { "value_length", valueLength },
                { "value_source", valueSource ?? "" },
            });
        }
        
        // ------------------------------------------------------------
        // Validation -> telemetry mapping
        // (Uses error strings from Result since the return type is Result-only.)
        // Minimal + deterministic, no file paths or PII.
        // ------------------------------------------------------------

        private static List<string> GetMissingFieldsForTelemetry(ModelGeneratorState state, string error)
        {
            var missing = new List<string>();

            if (string.IsNullOrWhiteSpace(error))
            {
                return missing;
            }

            // direct "required" errors
            if (error.IndexOf("Description is required", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                missing.Add("type_description");
            }

            if (error.IndexOf("Backend category must be selected", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                missing.Add("asset_category");
            }

            if (error.IndexOf("Wearable type must be selected", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                missing.Add("wearable_type");
            }

            // AssetName.Create failures are domain-driven; treat blank as missing, else invalid.
            if (error.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0 &&
                string.IsNullOrWhiteSpace(state.AssetName))
            {
                missing.Add("name");
            }

            return missing;
        }

        private static List<string> GetInvalidFieldsForTelemetry(ModelGeneratorState state, string error)
        {
            var invalid = new List<string>();

            if (string.IsNullOrWhiteSpace(error))
            {
                return invalid;
            }

            // Asset name invalid (but not missing)
            if (error.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0 &&
                !string.IsNullOrWhiteSpace(state.AssetName))
            {
                invalid.Add("name");
            }

            // Description invalid length
            if (error.IndexOf("Description must be at least", StringComparison.OrdinalIgnoreCase) >= 0 ||
                error.IndexOf("Description must be less than", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                invalid.Add("type_description");
            }

            // Reference images errors - treat as invalid, but don't capture file paths
            if (error.IndexOf("reference image", StringComparison.OrdinalIgnoreCase) >= 0 ||
                error.IndexOf("reference images", StringComparison.OrdinalIgnoreCase) >= 0 ||
                error.IndexOf("Invalid image format", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                invalid.Add("reference_images");
            }

            // Duplicate asset name is an "invalid name" situation
            if (error.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0 ||
                error.IndexOf("already in the queue", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (!invalid.Contains("name"))
                {
                    invalid.Add("name");
                }
            }

            return invalid;
        }

        private static string GetErrorCodeForTelemetry(ModelGeneratorState state, string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return "validation_failed";
            }

            if (error.IndexOf("State cannot be null", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "state_null";
            }

            if (error.IndexOf("already in the queue", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "duplicate_name_in_queue";
            }

            if (error.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0 &&
                error.IndexOf("folder", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "duplicate_name_folder_exists";
            }

            if (error.IndexOf("Description is required", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "description_missing";
            }

            if (error.IndexOf("Description must be at least", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "description_too_short";
            }

            if (error.IndexOf("Description must be less than", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "description_too_long";
            }

            if (error.IndexOf("Maximum", StringComparison.OrdinalIgnoreCase) >= 0 &&
                error.IndexOf("reference images", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "too_many_reference_images";
            }

            if (error.IndexOf("Reference image not found", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "reference_image_missing";
            }

            if (error.IndexOf("Reference image too large", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "reference_image_too_large";
            }

            if (error.IndexOf("Invalid image format", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "reference_image_invalid_format";
            }

            if (error.IndexOf("Backend category must be selected", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "backend_category_missing";
            }

            if (error.IndexOf("Wearable type must be selected", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "wearable_type_missing";
            }

            // Domain AssetName.Create errors (unknown specifics)
            if (error.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return string.IsNullOrWhiteSpace(state.AssetName) ? "name_missing" : "name_invalid";
            }

            return "validation_failed";
        }
    }
}
