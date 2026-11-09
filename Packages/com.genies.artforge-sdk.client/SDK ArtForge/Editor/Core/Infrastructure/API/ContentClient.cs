using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Genies.ArtForge.ContentAPI;
using Genies.ArtForge.ContentAPI.Models;
using Genies.Sdk.ArtForge.Editor.Auth;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using UnityEditor;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.API
{
    /// <summary>
    /// Editor-native client for Content API.
    /// Provides high-level workflows for asset generation using the Genies Content API.
    /// </summary>
    internal class ContentClient : IDisposable
    {
        private const int LogsEnabledDefault = 1;

        // These static methods are kept on ContentClient for backward compatibility.
        // The implementation lives in DeploymentEnvironment.
        public static IReadOnlyList<DeploymentEnvironment> GetAvailableEnvironments()
            => DeploymentEnvironment.GetAvailableEnvironments();

        public static DeploymentEnvironment ResolveEnvironment(string environmentName)
            => DeploymentEnvironment.ResolveEnvironment(environmentName);

        public static string GetDeploymentUrl(DeploymentEnvironment environment)
            => DeploymentEnvironment.GetDeploymentUrl(environment);

        private readonly ILogger _logger;
        private readonly ConvexConnectionManager _connection;
        private readonly JobWatcher _jobWatcher;
        private readonly PaymentsClient _paymentsClient;
        private bool _isDisposed;

        /// <summary>
        /// Gets whether the client is authenticated with the backend.
        /// Authentication can succeed even if WebSocket isn't connected yet.
        /// </summary>
        public bool IsAuthenticated => _connection.IsAuthenticated;

        /// <summary>
        /// Gets whether the WebSocket connection is established.
        /// </summary>
        public bool IsConnected => _connection.IsConnected;

        /// <summary>
        /// Creates a new ContentClient instance using the production backend.
        /// </summary>
        public ContentClient(ILogger logger)
            : this(logger, DeploymentEnvironment.Production)
        {
        }

        /// <summary>
        /// Creates a new ContentClient instance.
        /// </summary>
        public ContentClient(ILogger logger, DeploymentEnvironment environment)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            environment ??= DeploymentEnvironment.Production;

            string deploymentUrl = DeploymentEnvironment.GetDeploymentUrl(environment);
            string paymentsBaseUrl = DeploymentEnvironment.GetPaymentsBaseUrl(environment);
            bool loggingEnabled =
                EditorPrefs.GetInt(UnityLogger.LogsEnabledPrefKey, LogsEnabledDefault) == LogsEnabledDefault;

            _logger.Info($"[BackendClient] Initializing with environment: {environment.Name}");
            _logger.Info($"[BackendClient] Deployment URL: {deploymentUrl}");
            _logger.Info($"[BackendClient] Payments HTTP URL: {paymentsBaseUrl}");
            _logger.Info($"[BackendClient] Legacy asset owner queries: {environment.UseLegacyAssetOwnerQueries}");

            try
            {
                _connection = new ConvexConnectionManager(
                    logger, deploymentUrl, environment.UseLegacyAssetOwnerQueries, loggingEnabled);
                _logger.Info("[BackendClient] GeniesContentAPI initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.Error($"[BackendClient] Failed to initialize GeniesContentAPI: {ex.Message}");
                throw;
            }

            _jobWatcher = new JobWatcher(_connection, logger);
            _paymentsClient = new PaymentsClient(
                paymentsBaseUrl, logger, _connection.LogAuthTokenDiagnostics);
        }

        /// <summary>
        /// Updates Convex WebSocket logging behavior at runtime.
        /// </summary>
        public void SetConvexLoggingEnabled(bool enabled)
            => _connection.SetLoggingEnabled(enabled);

        /// <summary>
        /// Attempts to ensure the WebSocket connection is established.
        /// </summary>
        public Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken = default)
            => _connection.EnsureConnectedAsync(cancellationToken);

        /// <summary>
        /// Authenticates with the backend using a JWT token.
        /// </summary>
        public async Task<bool> AuthenticateAsync(string jwtToken)
        {
            if (string.IsNullOrEmpty(jwtToken))
            {
                throw new ArgumentException("JWT token cannot be null or empty", nameof(jwtToken));
            }

            bool isValidJwt = WorkOSAuthService.IsValidJwtFormat(jwtToken);
            TimeSpan expiredBy = TimeSpan.Zero;
            bool isExpired = isValidJwt &&
                WorkOSAuthService.IsTokenExpired(jwtToken, out expiredBy, _logger.Warning);

            if (!isValidJwt || isExpired)
            {
                if (!isValidJwt)
                {
                    _logger.Warning("[BackendClient] Invalid JWT format detected.");
                }
                else
                {
                    _logger.Warning(
                        $"[BackendClient] JWT token expired {expiredBy.TotalSeconds:F0} seconds ago");
                }

                _logger.Info("[BackendClient] Attempting to use stored OAuth token as fallback...");

                try
                {
                    jwtToken = await WorkOSAuthService.GetValidBackendAuthTokenAsync(
                        _logger.Info, _logger.Warning);
                    if (string.IsNullOrEmpty(jwtToken))
                    {
                        throw new InvalidOperationException("No valid OAuth token available.");
                    }

                    _logger.Info("[BackendClient] Using stored OAuth token");
                }
                catch (Exception ex)
                {
                    _logger.Error($"[BackendClient] Failed to get stored OAuth token: {ex.Message}");
                    throw new InvalidOperationException(
                        $"Invalid JWT format and no valid stored OAuth token found. " +
                        $"Please login via OAuth in Settings: {ex.Message}", ex);
                }
            }

            return await _connection.AuthenticateAsync(jwtToken);
        }

        /// <summary>
        /// Creates a wearable asset on the backend.
        /// </summary>
        public async Task<(string assetId, string jobId)> CreateWearableAsync(
            string prompt,
            string wearableType,
            string[] referenceImageIds = null,
            string assetDisplayName = null,
            CancellationToken cancellationToken = default)
        {
            EnsureAuthenticated();

            return await _connection.RunAsync("[BackendClient] CreateAsset", async () =>
            {
                _logger.Info($"[BackendClient] Creating wearable: {prompt} (Type: {wearableType})");

                var wearableData = new WearableData(
                    prompt: prompt,
                    type: wearableType,
                    referenceImageIds: referenceImageIds,
                    assetDisplayName: assetDisplayName);

                var wearables = _connection.Api.GetEndpoint<WearablesEndpoint>();
                var result = await _connection.RetryAsync(
                    "create wearable",
                    () => wearables.CreateAsync(wearableData, cancellationToken),
                    ConvexConnectionManager.DefaultMaxRetries,
                    ConvexConnectionManager.DefaultRetryDelayMs,
                    cancellationToken);

                _logger.Info(
                    $"[BackendClient] Wearable created - Asset ID: {result.assetId}, Job ID: {result.jobId}");
                return (result.assetId, result.jobId);
            }, ex =>
            {
                _logger.Error($"[BackendClient] Failed to create wearable: {ex.Message}");
            }, cancellationToken);
        }

        /// <summary>
        /// Uploads wearable reference images and returns storage IDs.
        /// </summary>
        public async Task<string[]> UploadWearableReferenceImagesAsync(
            IReadOnlyList<string> imagePaths,
            CancellationToken cancellationToken = default)
        {
            EnsureAuthenticated();

            if (imagePaths == null || imagePaths.Count == 0)
            {
                return Array.Empty<string>();
            }

            return await _connection.RunAsync("[BackendClient] UploadWearableReferenceImages", async () =>
            {
                var wearables = _connection.Api.GetEndpoint<WearablesEndpoint>();
                var storageIds = new List<string>();

                foreach (var path in imagePaths)
                {
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }

                    if (!File.Exists(path))
                    {
                        throw new FileNotFoundException($"Reference image not found: {path}", path);
                    }

                    var bytes = File.ReadAllBytes(path);
                    var contentType = GetContentType(path);
                    var fileName = Path.GetFileName(path);

                    _logger.Info($"[BackendClient] Uploading reference image: {fileName}");
                    var storageId = await wearables.UploadWearableReferenceAsync(
                        bytes, contentType, fileName, cancellationToken);
                    storageIds.Add(storageId);
                }

                _logger.Info($"[BackendClient] Uploaded {storageIds.Count} reference image(s)");
                return storageIds.ToArray();
            }, ex =>
            {
                _logger.Error($"[BackendClient] Failed to upload reference images: {ex.Message}");
            }, cancellationToken);
        }

        /// <summary>
        /// Creates a decor asset on the backend.
        /// </summary>
        public async Task<(string assetId, string jobId)> CreateDecorAsync(
            string prompt,
            string[] referenceImageIds = null,
            CancellationToken cancellationToken = default)
        {
            return await CreateDecorAsync(prompt, referenceImageIds, null, null, null, null, null, cancellationToken);
        }

        /// <summary>
        /// Creates a decor asset on the backend with an optional selected decor type.
        /// </summary>
        public async Task<(string assetId, string jobId)> CreateDecorAsync(
            string prompt,
            string decorType,
            string[] referenceImageIds = null,
            CancellationToken cancellationToken = default)
        {
            return await CreateDecorAsync(prompt, referenceImageIds, null, null, decorType, null, null, cancellationToken);
        }

        /// <summary>
        /// Creates a decor asset on the backend with optional model quality settings.
        /// </summary>
        public async Task<(string assetId, string jobId)> CreateDecorAsync(
            string prompt,
            string[] referenceImageIds,
            int? polycount,
            string meshMode,
            CancellationToken cancellationToken = default)
        {
            return await CreateDecorAsync(prompt, referenceImageIds, polycount, meshMode, null, null, null, cancellationToken);
        }

        /// <summary>
        /// Creates a decor asset on the backend with optional model quality settings and decor type.
        /// </summary>
        public async Task<(string assetId, string jobId)> CreateDecorAsync(
            string prompt,
            string[] referenceImageIds,
            int? polycount,
            string meshMode,
            string decorType,
            string aspectRatio,
            string imageSize,
            CancellationToken cancellationToken = default,
            string subcategory = null)
        {
            EnsureAuthenticated();

            return await _connection.RunAsync("[BackendClient] CreateAsset", async () =>
            {
                _logger.Info(
                    $"[BackendClient] Creating decor: {prompt} (Type: {(string.IsNullOrWhiteSpace(decorType) ? "unspecified" : decorType)}, Subcategory: {(string.IsNullOrWhiteSpace(subcategory) ? "unspecified" : subcategory)})");

                var decorData = new DecorData(
                    prompt: prompt,
                    type: decorType,
                    subcategory: subcategory,
                    referenceImageIds: referenceImageIds,
                    aspectRatio: aspectRatio,
                    imageSize: imageSize);
                var decor = _connection.Api.GetEndpoint<DecorEndpoint>();
                var result = await _connection.RetryAsync(
                    "create decor",
                    () => decor.CreateAsync(decorData, polycount, meshMode, cancellationToken),
                    ConvexConnectionManager.DefaultMaxRetries,
                    ConvexConnectionManager.DefaultRetryDelayMs,
                    cancellationToken);

                _logger.Info(
                    $"[BackendClient] Decor created - Asset ID: {result.assetId}, Job ID: {result.jobId}");
                return (result.assetId, result.jobId);
            }, ex =>
            {
                _logger.Error($"[BackendClient] Failed to create decor: {ex.Message}");
            }, cancellationToken);
        }

        /// <summary>
        /// Gets current backend status for a job.
        /// </summary>
        public async Task<JobStatus> GetJobStatusAsync(
            string jobId,
            CancellationToken cancellationToken = default)
        {
            EnsureAuthenticated();

            if (string.IsNullOrEmpty(jobId))
            {
                throw new ArgumentException("Job ID cannot be null or empty", nameof(jobId));
            }

            return await _connection.RunAsync("[BackendClient] GetJobStatus", async () =>
            {
                _logger.Info($"[BackendClient] Fetching job status: {jobId}");
                var jobs = _connection.Api.GetEndpoint<JobsEndpoint>();
                return await jobs.GetStatusAsync(jobId, cancellationToken);
            }, ex =>
            {
                _logger.Error($"[BackendClient] Failed to get job status: {ex.Message}");
            }, cancellationToken);
        }

        /// <summary>
        /// Subscribes to job status updates and waits for completion.
        /// </summary>
        public Task<JobStatus> SubscribeAndWaitForJobAsync(
            string jobId,
            Action<JobStatus> onProgress = null,
            CancellationToken cancellationToken = default)
        {
            EnsureAuthenticated();

            if (string.IsNullOrEmpty(jobId))
            {
                throw new ArgumentException("Job ID cannot be null or empty", nameof(jobId));
            }

            return _jobWatcher.SubscribeAndWaitForJobAsync(jobId, onProgress, cancellationToken);
        }

        /// <summary>
        /// Gets generated images for an asset.
        /// </summary>
        public async Task<List<AssetImage>> GetAssetImagesAsync(string assetId)
        {
            EnsureAuthenticated();

            return await _connection.RunAsync("[BackendClient] GetAssetImages", async () =>
            {
                _logger.Info($"[BackendClient] Fetching images for asset: {assetId}");
                var assets = _connection.Api.GetEndpoint<AssetsEndpoint>();
                var images = await assets.GetImagesAsync(assetId);
                _logger.Info($"[BackendClient] Retrieved {images.Count} images for asset {assetId}");
                return images;
            }, ex =>
            {
                _logger.Error($"[BackendClient] Failed to get asset images: {ex.Message}");
            });
        }

        /// <summary>
        /// Creates a 3D model from a selected image.
        /// </summary>
        public async Task<string> CreateWearableModelAsync(
            string assetId,
            string imageId,
            int? polycount = null,
            string meshMode = null)
        {
            EnsureAuthenticated();

            return await _connection.RunAsync("[BackendClient] CreateWearableModel", async () =>
            {
                _logger.Info(
                    $"[BackendClient] Creating wearable model for asset {assetId}, image {imageId}");
                var wearables = _connection.Api.GetEndpoint<WearablesEndpoint>();
                var result = await wearables.CreateModelAsync(assetId, imageId, polycount, meshMode);
                _logger.Info(
                    $"[BackendClient] Wearable model creation started - Job ID: {result.jobId}");
                return result.jobId;
            }, ex =>
            {
                _logger.Error($"[BackendClient] Failed to create wearable model: {ex.Message}");
            });
        }

        /// <summary>
        /// Gets wearable configuration from the backend (available types, etc.).
        /// </summary>
        public async Task<WearableConfig> GetWearableConfigAsync()
        {
            EnsureAuthenticated();

            return await _connection.RunAsync("[BackendClient] GetWearableConfig", async () =>
            {
                _logger.Info("[BackendClient] Fetching wearable configuration...");
                var wearables = _connection.Api.GetEndpoint<WearablesEndpoint>();
                var config = await wearables.GetConfigAsync();
                _logger.Info($"[BackendClient] Wearable config loaded: {config.types.Length} types");
                return config;
            }, ex =>
            {
                _logger.Error($"[BackendClient] Failed to get wearable config: {ex.Message}");
            });
        }

        /// <summary>
        /// Gets decor configuration from the backend (available types, etc.).
        /// </summary>
        public async Task<DecorConfig> GetDecorConfigAsync()
        {
            EnsureAuthenticated();

            return await _connection.RunAsync("[BackendClient] GetDecorConfig", async () =>
            {
                _logger.Info("[BackendClient] Fetching decor configuration...");
                var decor = _connection.Api.GetEndpoint<DecorEndpoint>();
                var config = await decor.GetConfigAsync();
                _logger.Info($"[BackendClient] Decor config loaded: {config.types.Length} types");
                return config;
            }, ex =>
            {
                _logger.Error($"[BackendClient] Failed to get decor config: {ex.Message}");
            });
        }

        /// <summary>
        /// Gets the generated 3D model for an asset.
        /// </summary>
        public async Task<AssetModel> GetAssetModelAsync(string assetId)
        {
            EnsureAuthenticated();

            return await _connection.RunAsync("[BackendClient] GetAssetModel", async () =>
            {
                _logger.Info($"[BackendClient] Fetching model for asset: {assetId}");
                var assets = _connection.Api.GetEndpoint<AssetsEndpoint>();
                var model = await assets.GetModelAsync(assetId);
                if (model == null || string.IsNullOrWhiteSpace(model.modelUrl))
                {
                    throw new InvalidOperationException(
                        $"Model not available yet for asset: {assetId}");
                }

                _logger.Info($"[BackendClient] Model retrieved - URL: {model.modelUrl}");
                return model;
            }, ex =>
            {
                _logger.Error($"[BackendClient] Failed to get asset model: {ex.Message}");
            });
        }

        /// <summary>
        /// Gets the current payments balance for the authenticated user.
        /// </summary>
        public Task<PaymentsBalanceSnapshot> GetPaymentsBalanceAsync(
            CancellationToken cancellationToken = default)
        {
            return _connection.RunAsync(
                "[BackendClient] GetPaymentsBalance",
                () => _paymentsClient.GetBalanceAsync(cancellationToken),
                ex =>
                {
                    _logger.Warning(
                        $"[BackendClient] Failed to fetch payments balance: {ex.Message}");
                },
                cancellationToken);
        }

        /// <summary>
        /// Creates a game asset for GAP (Game Asset Pipeline) integration.
        /// </summary>
        public async Task<GameAssetResult> CreateGameAssetAsync(string assetId, string modelId)
        {
            EnsureAuthenticated();

            return await _connection.RunAsync("[BackendClient] CreateGameAsset", async () =>
            {
                _logger.Info(
                    $"[BackendClient] Creating game asset for GAP - Asset: {assetId}, Model: {modelId}");
                var wearables = _connection.Api.GetEndpoint<WearablesEndpoint>();
                var result = await wearables.CreateInventoryGameAssetAsync(assetId, modelId);
                _logger.Info($"[BackendClient] ✓ Game asset created - Job ID: {result.jobId}");
                return result;
            }, ex =>
            {
                _logger.Error($"[BackendClient] Failed to create game asset: {ex.Message}");
            });
        }

        /// <summary>
        /// Clears auth and disposes of resources.
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            await _connection.DisconnectAsync();
        }

        /// <summary>
        /// Disposes of resources.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _logger.Info("[BackendClient] Disposing resources...");
            _connection.Dispose();
            _isDisposed = true;
            _logger.Info("[BackendClient] Disposal complete");
        }

        private void EnsureAuthenticated()
        {
            if (!IsAuthenticated)
            {
                throw new InvalidOperationException(
                    "Not authenticated with backend. Please authenticate first.");
            }
        }

        private static string GetContentType(string filePath)
        {
            var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
            switch (ext)
            {
                case ".png":
                    return "image/png";
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                default:
                    throw new InvalidOperationException($"Unsupported image type: {ext}");
            }
        }
    }
}
