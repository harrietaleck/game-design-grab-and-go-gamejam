using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Genies.ArtForge.ContentAPI.Models;
using Genies.Sdk.ArtForge.Editor.Auth;
using Genies.Sdk.ArtForge.Editor.Components.ModelGenerator.Config;
using Genies.Sdk.ArtForge.Editor.Components.ModelGenerator.UI;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.API;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Helpers;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using Genies.Sdk.ArtForge.Editor.Core.UI;
using Genies.Sdk.ArtForge.Editor.Core.UI.Helpers;
using Genies.Sdk.ArtForge.Editor.Hub.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Genies.Sdk.ArtForge.Editor.Components.ModelGenerator
{
    /// <summary>
    /// Model Generator view controller - Refactored Version.
    /// Clean, maintainable, event-driven architecture.
    /// Composition-only - delegates logic to presenter and panels.
    /// </summary>
    internal class ModelGeneratorView : IViewController
    {
        private const string Decor2DType = "2d_image";
        private const string DefaultAspectRatio = "1:1";

        // ------------------------------------------------------------
        // Telemetry signals (Observer subscribes to these)
        // ------------------------------------------------------------
        public static event Action<string, Dictionary<string, object>> ModelGenTelemetry;

        private VisualElement _root;
        private ILogger _logger;

        // State and Presenter
        private ModelGeneratorState _state;
        private ModelGeneratorPresenter _presenter;
        private IModelGeneratorConfigProvider _configProvider;

        // Panels
        private BasicInfoPanel _basicInfoPanel;
        private ITypeSelectionSection _typeSelectionSection;
        private ReferenceImagesSection _refImagesSection;
        private QualitySettingsPanel _qualitySettingsPanel;
        private string _lastDefaultsType;
        private string _lastDefaultsSubcategory;

        // Action bar
        private Button _generateButton;
        private Label _statusLabel;

        public void Load(VisualElement container)
        {
            _logger = GeniesArtForgeService.Logger ?? new UnityLogger();

            // Initialize state
            _state = new ModelGeneratorState();
            _lastDefaultsType = _state.BackendSpecificType ?? "";
            _lastDefaultsSubcategory = _state.BackendSubcategory ?? "";

            // Initialize config provider
            _configProvider = GeniesArtForgeService.BackendConfigProvider
                              ?? new BackendConfigProvider(GeniesArtForgeService.Generation, _logger);

            // Initialize presenter
            _presenter = new ModelGeneratorPresenter(_state, _configProvider, GeniesArtForgeService.Generation, _logger);
            _presenter.OnStatusMessageChanged += OnStatusMessageChanged;
            _presenter.OnStateChanged += OnStateChanged;

            // Build UI
            _root = BuildUI();
            container.Add(_root);

            // Bind panels to presenter
            _presenter.BindPanels(_basicInfoPanel, _typeSelectionSection, _refImagesSection);

            // Initialize (loads config if needed)
            _presenter.Initialize();
        }

        private VisualElement BuildUI()
        {
            var root = new VisualElement();
            root.style.flexGrow = 1;

            // Header
            var header = StyleHelpers.CreateHeader("Model Generator");
            root.Add(header);

            // Scroll view for content
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;

            var content = new VisualElement();
            content.style.paddingLeft = 20;
            content.style.paddingRight = 20;
            content.style.paddingTop = 10;
            content.style.paddingBottom = 20;

            // Build panels
            _basicInfoPanel = new BasicInfoPanel();
            content.Add(_basicInfoPanel.Root);

            content.Add(BuildTypeSelectionSection());

            // Only show reference images if feature is available (not in backend mode)
            if (FeatureAvailabilityService.AreReferenceImagesAvailable)
            {
                _refImagesSection = new ReferenceImagesSection(_logger);
                content.Add(_refImagesSection.Root);
            }

            _qualitySettingsPanel = new QualitySettingsPanel();
            _qualitySettingsPanel.Initialize(
                () => _state.Quality.Pivot ?? "Base",
                v =>
                {
                    if (_state.Quality.Pivot != v)
                    {
                        _state.Quality.Pivot = v;
                        EmitFieldChanged("quality_setting", v, valueSource: "dropdown", settingName: "pivot");
                    }
                },
                () => _state.Quality.TopologyType ?? "Triangles",
                v =>
                {
                    if (_state.Quality.TopologyType != v)
                    {
                        _state.Quality.TopologyType = v;
                        EmitFieldChanged("quality_setting", v, valueSource: "dropdown", settingName: "topology_type");
                    }
                },
                () => _state.Quality.Polycount,
                v =>
                {
                    if (_state.Quality.Polycount != v)
                    {
                        _state.Quality.Polycount = v;
                        EmitFieldChanged("quality_setting", v, valueSource: "typed", settingName: "polycount");
                    }
                },
                () => _state.Quality.GenerateLODs,
                v =>
                {
                    if (_state.Quality.GenerateLODs != v)
                    {
                        _state.Quality.GenerateLODs = v;
                        EmitFieldChanged("quality_setting", v, valueSource: "dropdown", settingName: "generate_lods");
                    }
                },
                () => _state.Quality.GenerateCollider,
                v =>
                {
                    if (_state.Quality.GenerateCollider != v)
                    {
                        _state.Quality.GenerateCollider = v;
                        EmitFieldChanged("quality_setting", v, valueSource: "dropdown", settingName: "generate_collider");
                    }
                },
                () => _state.Quality.TextureResolution ?? "1024",
                v =>
                {
                    if (_state.Quality.TextureResolution != v)
                    {
                        _state.Quality.TextureResolution = v;
                        EmitFieldChanged("quality_setting", v, valueSource: "dropdown", settingName: "texture_resolution");
                    }
                },
                () => _state.Quality.GenerateAO,
                v =>
                {
                    if (_state.Quality.GenerateAO != v)
                    {
                        _state.Quality.GenerateAO = v;
                        // Keep legacy fields in sync for older logic that still checks them.
                        _state.Quality.GenerateNormalMap = v;
                        _state.Quality.GenerateMetallicSmoothnessMap = v;

                        EmitFieldChanged("quality_setting", v, valueSource: "dropdown", settingName: "generate_ao");
                    }
                },
                getAspectRatio: () => _state.Quality.AspectRatio ?? "1:1",
                setAspectRatio: v =>
                {
                    if (_state.Quality.AspectRatio != v)
                    {
                        _state.Quality.AspectRatio = v;
                        EmitFieldChanged("quality_setting", v, valueSource: "dropdown", settingName: "aspect_ratio");
                    }
                },
                getAspectRatioChoices: GetDecorAspectRatioChoices,
                allowTopologySelection: () => _state.BackendCategory == AssetCategories.Decor,
                allowAspectRatioSelection: IsDecor2DSelected,
                showMeshSettings: () => !IsDecor2DSelected(),
                getTransparent: () => _state.Quality.GenerateTransparent,
                setTransparent: v =>
                {
                    if (_state.Quality.GenerateTransparent != v)
                    {
                        _state.Quality.GenerateTransparent = v;
                        EmitFieldChanged("quality_setting", v, valueSource: "dropdown", settingName: "transparent");
                    }
                },
                showTransparentSetting: IsTransparentSettingAvailableForSelectedSubcategory,
                prefsKeyPrefix: "GAF_QualitySettings_ModelGenerator"
            );
            content.Add(_qualitySettingsPanel.Root);

            scrollView.Add(content);
            root.Add(scrollView);

            // Action bar
            root.Add(BuildActionBar());

            return root;
        }

        private VisualElement BuildTypeSelectionSection()
        {
            var section = StyleHelpers.CreateSection("Asset Type");
            _typeSelectionSection = new BackendTypeSelectionSection();
            section.Add(_typeSelectionSection.Root);
            return section;
        }

        private VisualElement BuildActionBar()
        {
            var actionBar = new VisualElement();
            actionBar.style.flexDirection = FlexDirection.Row;
            actionBar.style.justifyContent = Justify.FlexStart;
            actionBar.style.alignItems = Align.FlexStart;
            actionBar.style.flexWrap = Wrap.Wrap;
            actionBar.style.paddingLeft = 20;
            actionBar.style.paddingRight = 20;
            actionBar.style.paddingTop = 15;
            actionBar.style.paddingBottom = 15;
            actionBar.style.borderTopWidth = 1;
            actionBar.style.borderTopColor = StyleHelpers.Colors.BorderDark;
            actionBar.style.backgroundColor = StyleHelpers.Colors.BackgroundDark;

            _statusLabel = new Label("");
            _statusLabel.style.fontSize = 12;
            _statusLabel.style.flexGrow = 1;
            _statusLabel.style.flexShrink = 1;
            _statusLabel.style.minWidth = 0;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _statusLabel.style.marginRight = 10;
            actionBar.Add(_statusLabel);

            _generateButton = new Button(OnGenerateClicked);
            UpdateGenerateButtonText();
            StyleHelpers.ApplyPrimaryButtonStyle(_generateButton);
            _generateButton.style.minWidth = 150;
            _generateButton.style.flexShrink = 0;
            _generateButton.style.alignSelf = Align.FlexEnd;
            actionBar.Add(_generateButton);

            return actionBar;
        }

        private async void OnGenerateClicked()
        {
            // Ensure state reflects latest UI BEFORE telemetry
            _basicInfoPanel?.UpdateState(_state);
            _typeSelectionSection?.UpdateState(_state);

            ModelGenTelemetry?.Invoke("model_gen_generate_clicked", new Dictionary<string, object>
            {
                { "asset_category", _state.BackendCategory ?? "" },
                { "wearable_type", _state.BackendCategory == Genies.Sdk.ArtForge.Editor.Core.Domain.Enums.AssetCategories.Wearable ? (_state.BackendSpecificType ?? "") : "" },
                { "description_length", _state.Description?.Length ?? 0 },
                { "name_length", _state.AssetName?.Length ?? 0 },
                { "image_references_count", _state.ReferenceImages?.Count ?? 0 },
            });

            _generateButton.SetEnabled(false);

            try
            {
                _statusLabel.text = "Checking backend connection...";
                _statusLabel.style.color = StyleHelpers.Colors.TextPrimary;

                bool connected = await EnsureBackendConnectedAsync();
                if (!connected)
                {
                    ModelGenTelemetry?.Invoke("model_gen_generate_blocked", new Dictionary<string, object>
                        {
                            { "block_reason", "not_logged_in" }
                        }
                    );
                    return;
                }

                if (!IsBackendConnectionActive())
                {
                    ModelGenTelemetry?.Invoke("model_gen_generate_blocked", new Dictionary<string, object>
                        {
                            { "block_reason", "not_logged_in" }
                        }
                    );

                    PromptRelogin("Real-time backend connection is not active. Please reconnect in Settings.");
                    return;
                }

                _statusLabel.text = "Adding to queue...";
                _statusLabel.style.color = StyleHelpers.Colors.TextPrimary;
                _presenter.GenerateJob();
            }
            finally
            {
                _generateButton.SetEnabled(true);
            }
        }

        private async Task<bool> EnsureBackendConnectedAsync()
        {
            var generationService = GeniesArtForgeService.Generation;
            if (generationService == null || !generationService.IsBackendAvailable)
            {
                _logger?.Warning($"[ModelGenerator] Backend connection check failed before generation: service unavailable or not authenticated. {BuildBackendStateSnapshot()}");
                bool recovered = await TryRecoverBackendSessionAsync("service unavailable or unauthenticated");
                if (!recovered)
                {
                    PromptRelogin("Backend not authenticated. Please login in Settings.");
                    return false;
                }
            }

            generationService = GeniesArtForgeService.Generation;
            var backendClient = generationService?.GetBackendClient();
            if (backendClient == null)
            {
                _logger?.Warning($"[ModelGenerator] Backend connection check failed before generation: backend client is null. {BuildBackendStateSnapshot()}");
                bool recovered = await TryRecoverBackendSessionAsync("backend client is null");
                if (!recovered)
                {
                    PromptRelogin("Backend client not initialized. Please login in Settings.");
                    return false;
                }

                generationService = GeniesArtForgeService.Generation;
                backendClient = generationService?.GetBackendClient();
                if (backendClient == null)
                {
                    PromptRelogin("Backend client not initialized. Please login in Settings.");
                    return false;
                }
            }

            if (backendClient.IsConnected)
            {
                bool healthyConnection = await backendClient.EnsureConnectedAsync();
                if (healthyConnection && backendClient.IsConnected)
                {
                    return true;
                }

                _logger?.Warning($"[ModelGenerator] Backend reported IsConnected=true but health check failed. healthyConnection={healthyConnection}. {BuildBackendStateSnapshot()}");
            }

            _statusLabel.text = "Reconnecting to backend...";
            _statusLabel.style.color = StyleHelpers.Colors.TextPrimary;

            bool reconnected = await backendClient.EnsureConnectedAsync();
            if (reconnected && backendClient.IsConnected)
            {
                _logger?.Info($"[ModelGenerator] Backend reconnect succeeded before generation. {BuildBackendStateSnapshot()}");
                return true;
            }

            _logger?.Warning($"[ModelGenerator] Backend reconnect failed before generation. reconnected={reconnected}. {BuildBackendStateSnapshot()}");
            bool recoveredAfterReconnectFailure = await TryRecoverBackendSessionAsync("reconnect attempt failed");
            if (recoveredAfterReconnectFailure)
            {
                generationService = GeniesArtForgeService.Generation;
                backendClient = generationService?.GetBackendClient();
                if (backendClient != null && backendClient.IsConnected)
                {
                    return true;
                }
            }

            PromptRelogin("Real-time backend connection lost. Please login again in Settings.");
            return false;
        }

        private bool IsBackendConnectionActive()
        {
            var generationService = GeniesArtForgeService.Generation;
            if (generationService == null || !generationService.IsBackendAvailable)
            {
                return false;
            }

            var backendClient = generationService.GetBackendClient();
            return backendClient != null && backendClient.IsConnected;
        }

        private void PromptRelogin(string message)
        {
            _logger?.Warning($"[ModelGenerator] Prompting relogin. reason='{message}'. {BuildBackendStateSnapshot()}");
            _statusLabel.text = message;
            _statusLabel.style.color = StyleHelpers.Colors.Error;
            BackendConnectionRecoveryHelper.PromptOpenSettings(message);
        }

        private static string BuildBackendStateSnapshot()
        {
            var generationService = GeniesArtForgeService.Generation;
            if (generationService == null)
            {
                return "state: generationService=null";
            }

            var backendClient = generationService.GetBackendClient();
            return $"state: isBackendAvailable={generationService.IsBackendAvailable}, backendClientNull={backendClient == null}, backendIsAuthenticated={backendClient?.IsAuthenticated ?? false}, backendIsConnected={backendClient?.IsConnected ?? false}";
        }

        private async Task<bool> TryRecoverBackendSessionAsync(string reason)
        {
            var generationService = GeniesArtForgeService.Generation;
            if (generationService == null)
            {
                _logger?.Warning($"[ModelGenerator] Silent backend recovery skipped: generation service is null. trigger='{reason}'.");
                return false;
            }

            _logger?.Info($"[ModelGenerator] Attempting silent backend recovery. trigger='{reason}'. {BuildBackendStateSnapshot()}");

            try
            {
                Action<string> info = _logger != null ? new Action<string>(_logger.Info) : null;
                Action<string> warn = _logger != null ? new Action<string>(_logger.Warning) : null;
                string backendToken = await WorkOSAuthService.GetValidBackendAuthTokenAsync(info, warn);
                if (string.IsNullOrWhiteSpace(backendToken))
                {
                    _logger?.Warning("[ModelGenerator] Silent backend recovery failed: no valid backend token available.");
                    return false;
                }

                string environmentName = WorkOSAuthConstants.GetEnvironment();
                var environment = ContentClient.ResolveEnvironment(environmentName);

                bool initialized = await generationService.InitializeBackendAsync(backendToken, environment);
                if (initialized)
                {
                    var client = generationService.GetBackendClient();
                    if (client != null)
                    {
                        bool connected = client.IsConnected || await client.EnsureConnectedAsync();
                        if (connected && client.IsConnected)
                        {
                            _logger?.Info($"[ModelGenerator] Silent backend recovery succeeded (in-place). {BuildBackendStateSnapshot()}");
                            return true;
                        }
                    }
                }

                _logger?.Warning("[ModelGenerator] In-place backend recovery did not establish connection. Recreating backend client...");
                await generationService.DisconnectBackendAsync();

                initialized = await generationService.InitializeBackendAsync(backendToken, environment);
                if (!initialized)
                {
                    _logger?.Warning("[ModelGenerator] Silent backend recovery failed: re-initialization failed after client recycle.");
                    return false;
                }

                var recycledClient = generationService.GetBackendClient();
                if (recycledClient == null)
                {
                    _logger?.Warning("[ModelGenerator] Silent backend recovery failed: backend client is null after recycle.");
                    return false;
                }

                bool reconnected = recycledClient.IsConnected || await recycledClient.EnsureConnectedAsync();
                if (reconnected && recycledClient.IsConnected)
                {
                    _logger?.Info($"[ModelGenerator] Silent backend recovery succeeded (after client recycle). {BuildBackendStateSnapshot()}");
                    return true;
                }

                _logger?.Warning($"[ModelGenerator] Silent backend recovery failed to establish real-time connection after client recycle. {BuildBackendStateSnapshot()}");
                return false;
            }
            catch (Exception ex)
            {
                _logger?.Warning($"[ModelGenerator] Silent backend recovery error: {ex.Message}");
                return false;
            }
        }

        private void OnStatusMessageChanged(string message, Color color)
        {
            _statusLabel.text = message;
            _statusLabel.style.color = color;
        }

        private void OnStateChanged()
        {
            ApplyDecorSubcategoryDefaults(force: false);
            _qualitySettingsPanel?.RefreshValues();
            UpdateGenerateButtonText();
        }

        public void Unload()
        {
            _basicInfoPanel?.UpdateState(_state);
            _typeSelectionSection?.UpdateState(_state);

            // Dispose presenter (unregisters all callbacks)
            _presenter?.Dispose();

            // Clear config provider
            _configProvider?.ClearCache();

            // Remove from hierarchy
            _root?.RemoveFromHierarchy();
            _root = null;
        }

        // ------------------------------------------------------------
        // Telemetry helpers (event-only)
        // ------------------------------------------------------------

        private static void EmitFieldChanged(string field, object value, string valueSource, string settingName = null)
        {
            int valueLength = 0;
            if (value != null)
            {
                string s = value as string ?? value.ToString();
                valueLength = string.IsNullOrEmpty(s) ? 0 : s.Length;
            }

            var props = new Dictionary<string, object>
            {
                { "field", field },
                { "value_length", valueLength },
                { "value_source", valueSource ?? "" },
            };

            if (!string.IsNullOrEmpty(settingName))
            {
                // extra context (non-breaking)
                props["setting_name"] = settingName;
            }

            ModelGenTelemetry?.Invoke("model_gen_field_changed", props);
        }

        private bool IsDecor2DSelected()
        {
            if (!string.Equals(_state.BackendCategory, AssetCategories.Decor, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return IsDecor2DType(_state.BackendSpecificType);
        }

        private IReadOnlyList<string> GetCurrentDecorSubcategoryChoices()
        {
            var decorType = GetSelectedDecorType();
            if (decorType?.subcategories == null || decorType.subcategories.Length == 0)
            {
                return Array.Empty<string>();
            }

            var choices = new List<string>();
            foreach (var subcategory in decorType.subcategories)
            {
                string name = subcategory?.name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                bool alreadyAdded = choices.Any(existing =>
                    string.Equals(existing, name, StringComparison.OrdinalIgnoreCase));
                if (!alreadyAdded)
                {
                    choices.Add(name);
                }
            }

            return choices;
        }

        private IReadOnlyList<string> GetDecorAspectRatioChoices()
        {
            var decorConfig = _configProvider?.CachedConfig?.DecorConfig;
            if (decorConfig?.supportedAspectRatios == null || decorConfig.supportedAspectRatios.Length == 0)
            {
                return Array.Empty<string>();
            }

            var choices = new List<string>();
            foreach (string ratio in decorConfig.supportedAspectRatios)
            {
                if (string.IsNullOrWhiteSpace(ratio))
                {
                    continue;
                }

                bool alreadyAdded = choices.Any(existing =>
                    string.Equals(existing, ratio, StringComparison.OrdinalIgnoreCase));
                if (!alreadyAdded)
                {
                    choices.Add(ratio);
                }
            }

            return choices;
        }

        private void ApplyDecorSubcategoryDefaults(bool force)
        {
            if (!string.Equals(_state.BackendCategory, AssetCategories.Decor, StringComparison.OrdinalIgnoreCase))
            {
                _lastDefaultsType = null;
                _lastDefaultsSubcategory = null;
                return;
            }

            var decorType = GetSelectedDecorType();
            if (decorType == null)
            {
                // Decor config/type may not be available yet during initial load.
                // Keep last-tracked values so we do not treat tab/view refresh as a selection change.
                return;
            }

            var subcategoryChoices = GetCurrentDecorSubcategoryChoices();
            if (subcategoryChoices.Count == 0)
            {
                _state.BackendSubcategory = "";
                _lastDefaultsType = _state.BackendSpecificType ?? "";
                _lastDefaultsSubcategory = "";
                return;
            }

            string selectedSubcategoryName = _state.BackendSubcategory ?? "";
            if (!subcategoryChoices.Any(choice =>
                string.Equals(choice, selectedSubcategoryName, StringComparison.OrdinalIgnoreCase)))
            {
                selectedSubcategoryName = subcategoryChoices[0];
                _state.BackendSubcategory = selectedSubcategoryName;
            }

            bool changed = !string.Equals(_lastDefaultsType, _state.BackendSpecificType, StringComparison.OrdinalIgnoreCase) ||
                           !string.Equals(_lastDefaultsSubcategory, selectedSubcategoryName, StringComparison.OrdinalIgnoreCase);
            if (!force && !changed)
            {
                return;
            }

            var selectedSubcategory = decorType.subcategories.FirstOrDefault(subcategory =>
                subcategory != null &&
                string.Equals(subcategory.name, selectedSubcategoryName, StringComparison.OrdinalIgnoreCase));

            bool isDecor2D = IsDecor2DType(_state.BackendSpecificType);
            if (isDecor2D)
            {
                string defaultAspectRatio = selectedSubcategory?.defaultAspectRatio;
                if (string.IsNullOrWhiteSpace(defaultAspectRatio))
                {
                    if (string.IsNullOrWhiteSpace(_state.Quality.AspectRatio))
                    {
                        _state.Quality.AspectRatio = DefaultAspectRatio;
                    }
                }
                else
                {
                    _state.Quality.AspectRatio = defaultAspectRatio;
                }

                if (selectedSubcategory?.transparentBackground.HasValue == true)
                {
                    _state.Quality.GenerateTransparent = selectedSubcategory.transparentBackground.Value;
                }
            }
            else
            {
                int? defaultPolycount = selectedSubcategory?.polycount ?? decorType.polycount;
                if (defaultPolycount.HasValue && defaultPolycount.Value > 0)
                {
                    _state.Quality.Polycount = defaultPolycount.Value;
                }
            }

            _lastDefaultsType = _state.BackendSpecificType ?? "";
            _lastDefaultsSubcategory = selectedSubcategoryName;
        }

        private DecorType GetSelectedDecorType()
        {
            var decorConfig = _configProvider?.CachedConfig?.DecorConfig;
            if (decorConfig?.types == null || decorConfig.types.Length == 0)
            {
                return null;
            }

            return decorConfig.types.FirstOrDefault(type =>
                type != null &&
                (!string.IsNullOrWhiteSpace(type.type) &&
                 string.Equals(type.type, _state.BackendSpecificType, StringComparison.OrdinalIgnoreCase) ||
                 !string.IsNullOrWhiteSpace(type.name) &&
                 string.Equals(type.name, _state.BackendSpecificType, StringComparison.OrdinalIgnoreCase)));
        }

        private bool IsTransparentSettingAvailableForSelectedSubcategory()
        {
            if (!IsDecor2DSelected())
            {
                return false;
            }

            var decorType = GetSelectedDecorType();
            if (decorType?.subcategories == null || decorType.subcategories.Length == 0)
            {
                return true;
            }

            string selectedSubcategory = _state.BackendSubcategory ?? string.Empty;
            if (string.IsNullOrWhiteSpace(selectedSubcategory))
            {
                return true;
            }

            var configuredSubcategory = decorType.subcategories.FirstOrDefault(subcategory =>
                subcategory != null &&
                !string.IsNullOrWhiteSpace(subcategory.name) &&
                string.Equals(subcategory.name, selectedSubcategory, StringComparison.OrdinalIgnoreCase));
            if (configuredSubcategory == null)
            {
                return true;
            }

            // Explicit false means transparency is disabled for this subcategory.
            if (configuredSubcategory.transparentBackground.HasValue &&
                !configuredSubcategory.transparentBackground.Value &&
                configuredSubcategory.name.ToLower() != "default")
            {
                return false;
            }

            return true;
        }

        private void UpdateGenerateButtonText()
        {
            if (_generateButton == null)
            {
                return;
            }

            _generateButton.text = IsDecor2DSelected() ? "Generate Image" : "Generate Model";
        }

        private static bool IsDecor2DType(string specificType)
        {
            if (string.IsNullOrWhiteSpace(specificType))
            {
                return false;
            }

            string normalized = specificType.Trim()
                .Replace("_", "")
                .Replace("-", "")
                .Replace(" ", "")
                .ToLowerInvariant();

            if (normalized == "2d" || normalized == "2dimage")
            {
                return true;
            }

            return string.Equals(specificType.Trim(), Decor2DType, StringComparison.OrdinalIgnoreCase);
        }
    }
}
