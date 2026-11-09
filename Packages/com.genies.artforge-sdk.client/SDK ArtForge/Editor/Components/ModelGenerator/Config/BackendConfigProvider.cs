using System;
using System.Threading.Tasks;
using Genies.ArtForge.ContentAPI.Models;
using Genies.Sdk.ArtForge.Editor.Auth;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using Genies.Sdk.ArtForge.Editor.Core.Application.Services;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.API;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using UnityEditor;
using UnityEngine;

namespace Genies.Sdk.ArtForge.Editor.Components.ModelGenerator.Config
{
    /// <summary>
    /// Default implementation of IModelGeneratorConfigProvider.
    /// Handles authentication, caching, and loading of backend configuration.
    /// Uses GenerationService directly (backend-specific component).
    /// </summary>
    internal sealed class BackendConfigProvider : IModelGeneratorConfigProvider
    {
        private readonly GenerationService _backendService;
        private readonly ILogger _logger;
        private BackendConfigResult _cachedConfig;
        private ConfigLoadStatus _status = ConfigLoadStatus.NotStarted;
        private string _lastError;
        private Task<BackendConfigResult> _loadingTask;

        public ConfigLoadStatus Status
        {
            get => _status;
            private set
            {
                if (_status != value)
                {
                    _status = value;
                    OnStatusChanged?.Invoke(_status);
                }
            }
        }

        public string LastError => _lastError;
        public BackendConfigResult CachedConfig => _cachedConfig;

        public event Action<ConfigLoadStatus> OnStatusChanged;

        public BackendConfigProvider(GenerationService backendService, ILogger logger = null)
        {
            _backendService = backendService ?? throw new ArgumentNullException(nameof(backendService));
            _logger = logger ?? GeniesArtForgeService.Logger ?? new UnityLogger();
        }

        public async Task<BackendConfigResult> GetAsync()
        {
            // Return cached if available
            if (Status == ConfigLoadStatus.Loaded && _cachedConfig != null)
            {
                return _cachedConfig;
            }

            // If already loading, wait for existing task
            if (_loadingTask != null)
            {
                return await _loadingTask;
            }

            // Start new load
            return await ReloadAsync();
        }

        public async Task<BackendConfigResult> ReloadAsync()
        {
            // If already loading, wait for existing task
            if (_loadingTask != null)
            {
                return await _loadingTask;
            }

            _loadingTask = LoadInternalAsync();
            try
            {
                var result = await _loadingTask;
                return result;
            }
            finally
            {
                _loadingTask = null;
            }
        }

        public void ClearCache()
        {
            _cachedConfig = null;
            _lastError = null;
            Status = ConfigLoadStatus.NotStarted;
        }

        private async Task<BackendConfigResult> LoadInternalAsync()
        {
            Status = ConfigLoadStatus.Loading;
            _lastError = null;

            try
            {
                _logger.Info("[BackendConfigProvider] Loading backend configuration...");

                // Check if already authenticated, if not, try auto-authentication
                if (!_backendService.IsBackendAvailable)
                {
                    _logger.Info("[BackendConfigProvider] Backend not authenticated, attempting auto-authentication...");

                    bool authSuccess = await TryAutoAuthenticateAsync();

                    if (!authSuccess)
                    {
                        _lastError = "Backend not authenticated. Please authenticate in Settings first.";
                        Status = ConfigLoadStatus.Failed;
                        _logger.Warning($"[BackendConfigProvider] {_lastError}");
                        return BackendConfigResult.Failure(_lastError);
                    }

                    _logger.Info("[BackendConfigProvider] Auto-authentication successful!");
                }

                // Get backend client
                var backendClient = _backendService.GetBackendClient();
                if (backendClient == null)
                {
                    _lastError = "Backend client is null. Please authenticate in Settings.";
                    Status = ConfigLoadStatus.Failed;
                    return BackendConfigResult.Failure(_lastError);
                }

                // Load wearable config
                var wearableConfig = await backendClient.GetWearableConfigAsync();

                // Validate required config(s)
                if (wearableConfig == null || wearableConfig.types.Length == 0)
                {
                    _lastError = "Wearable configuration is empty or invalid";
                    Status = ConfigLoadStatus.Failed;
                    return BackendConfigResult.Failure(_lastError);
                }

                DecorConfig decorConfig = null;
                try
                {
                    decorConfig = await backendClient.GetDecorConfigAsync();
                    if (decorConfig == null)
                    {
                        decorConfig = new DecorConfig();
                    }
                }
                catch (Exception decorEx)
                {
                    // Keep wearable generation functional if decor config lookup fails.
                    _logger.Warning(
                        $"[BackendConfigProvider] Decor config unavailable, continuing with wearable config only: {decorEx.Message}");
                    decorConfig = new DecorConfig();
                }

                // Cache result
                _cachedConfig = BackendConfigResult.Success(wearableConfig, decorConfig);
                Status = ConfigLoadStatus.Loaded;

                _logger.Info(
                    $"[BackendConfigProvider] Config loaded: {wearableConfig.types.Length} wearable types, {decorConfig.types.Length} decor types");

                return _cachedConfig;
            }
            catch (Exception ex)
            {
                _lastError = $"Failed to load configuration: {ex.Message}";
                Status = ConfigLoadStatus.Failed;
                _logger.Error($"[BackendConfigProvider] {_lastError}\n{ex.StackTrace}");
                return BackendConfigResult.Failure(_lastError);
            }
        }

        /// <summary>
        /// Attempts to automatically authenticate using saved credentials from EditorPrefs.
        /// Returns true if authentication succeeds or was already authenticated.
        /// </summary>
        private async Task<bool> TryAutoAuthenticateAsync()
        {
            try
            {
                _logger.Info("[BackendConfigProvider] Starting auto-authentication...");

                // Get saved OAuth token from EditorPrefs
                // Note: Settings uses "GAF_Settings_" prefix for all keys
                string backendToken = await WorkOSAuthService.GetValidBackendAuthTokenAsync(_logger.Info, _logger.Warning);
                const string PrefixKey = "GAF_Settings_";
                string environment = EditorPrefs.GetString(
                    $"{PrefixKey}{WorkOSAuthConstants.PrefKeyEnvironment}",
                    WorkOSAuthConstants.EnvironmentProduction);

                if (string.IsNullOrWhiteSpace(backendToken))
                {
                    _logger.Warning("[BackendConfigProvider] No OAuth token found in EditorPrefs. Please login in Settings.");
                    _logger.Warning("[BackendConfigProvider] === AUTO-AUTHENTICATION FAILED (No Token) ===");
                    return false;
                }

                _logger.Info($"[BackendConfigProvider] Using stored OAuth token for {environment} environment");

                // Parse environment
                var deploymentEnvironment = ContentClient.ResolveEnvironment(environment);

                // Initialize backend with a backend-compatible JWT token (prefers id_token).
                bool success = await _backendService.InitializeBackendAsync(backendToken, deploymentEnvironment);

                if (success)
                {
                    _logger.Info("[BackendConfigProvider] ✓ Auto-authentication successful");
                }
                else
                {
                    _logger.Warning("[BackendConfigProvider] Backend initialization failed");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.Error($"[BackendConfigProvider] Auto-authentication error: {ex.Message}");
                return false;
            }
        }
    }
}
