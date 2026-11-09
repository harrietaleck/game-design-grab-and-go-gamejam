using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Genies.ArtForge.ContentAPI.Logging;
using Genies.ArtForge.ContentAPI.Models;
using Genies.Convex.Core;
using Genies.Convex.Exceptions;
using Genies.Convex.Transport;
using Genies.Convex.Values;

namespace Genies.ArtForge.ContentAPI
{
    /// <summary>
    /// Main API facade for Genies Content creation using Genies.Convex backend.
    /// Provides a simplified interface for creating wearables, decor, and tracking job progress.
    /// </summary>
    /// <remarks>
    /// This wrapper hides all Convex implementation details and provides a clean API
    /// with simple data transfer objects using modern async/await patterns.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Initialize the API
    /// var api = new GeniesContentAPI();
    /// api.Initialize("https://your-deployment.convex.cloud");
    ///
    /// // Set authentication token
    /// await api.SetAuthTokenAsync("your-jwt-token");
    ///
    /// // Create a wearable
    /// var wearableData = new WearableData("Cool Hat", "headwear");
    /// var result = await api.CreateWearableAsync(wearableData);
    ///
    /// // Track job progress
    /// var status = await api.GetJobStatusAsync(result.jobId);
    /// </code>
    /// </example>
    internal class GeniesContentAPI : IDisposable
    {
        private const int DefaultEnsureConnectedTimeoutMs = 15000;
        private const int EnsureConnectedPollDelayMs = 100;

        private ConvexClient _convexClient;
        private bool _disposed;
        private IContentApiLogger _logger;
        private readonly object _lock = new object();
        private readonly SemaphoreSlim _connectLock = new SemaphoreSlim(1, 1);
        private readonly GeniesContentApiEndpointOptions _endpointOptions;
        private readonly Dictionary<Type, ContentEndpointBase> _endpointCache = new Dictionary<Type, ContentEndpointBase>();
        private readonly Dictionary<Type, Func<GeniesContentAPI, ContentEndpointBase>> _endpointFactories =
            new Dictionary<Type, Func<GeniesContentAPI, ContentEndpointBase>>();
        private readonly object _endpointLock = new object();

        /// <summary>
        /// Creates a Genies Content API with all endpoints enabled.
        /// </summary>
        public GeniesContentAPI() : this(null)
        {
        }

        /// <summary>
        /// Creates a Genies Content API with configurable endpoint initialization.
        /// </summary>
        /// <param name="endpointOptions">Options controlling which endpoints are created.</param>
        public GeniesContentAPI(GeniesContentApiEndpointOptions endpointOptions)
        {
            _endpointOptions = endpointOptions ?? new GeniesContentApiEndpointOptions();
            _logger = new UnityContentApiLogger();
            RegisterDefaultEndpoints();
        }

        /// <summary>
        /// Gets a value indicating whether the API has been initialized.
        /// </summary>
        public bool IsInitialized => _convexClient != null;

        /// <summary>
        /// Gets the current connection state of the underlying client.
        /// </summary>
        public bool IsConnected => _convexClient != null && _convexClient.State == ConnectionState.Connected;

        internal bool UseLegacyAssetOwnerQueries => _endpointOptions.UseLegacyAssetOwnerQueries;

        internal IContentApiLogger Logger => _logger;

        public void SetLogger(IContentApiLogger logger)
        {
            _logger = logger ?? new UnityContentApiLogger();
        }

        public void SetLoggingEnabled(bool enabled)
        {
            _logger.IsEnabled = enabled;
            SetConvexDebugLogging(enabled);
        }

        /// <summary>
        /// Access wearable-related endpoints.
        /// </summary>
        public WearablesEndpoint Wearables => GetEndpoint<WearablesEndpoint>();


        /// <summary>
        /// Access decor-related endpoints.
        /// </summary>
        public DecorEndpoint Decor => GetEndpoint<DecorEndpoint>();

        /// <summary>
        /// Access job-related endpoints.
        /// </summary>
        public JobsEndpoint Jobs => GetEndpoint<JobsEndpoint>();

        /// <summary>
        /// Access asset-related endpoints.
        /// </summary>
        public AssetsEndpoint Assets => GetEndpoint<AssetsEndpoint>();

        #region Initialization

        /// <summary>
        /// Initializes the Genies Content API with the specified deployment URL.
        /// </summary>
        /// <param name="deploymentUrl">The Convex deployment URL (e.g., "https://example.convex.cloud").</param>
        /// <exception cref="ArgumentException">Thrown when deploymentUrl is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when API is already initialized.</exception>
        /// <example>
        /// <code>
        /// var api = new GeniesContentAPI();
        /// api.Initialize("https://your-deployment.convex.cloud");
        /// </code>
        /// </example>
        public void Initialize(string deploymentUrl)
        {
            if (string.IsNullOrWhiteSpace(deploymentUrl))
            {
                throw new ArgumentException("Deployment URL cannot be null or empty.", nameof(deploymentUrl));
            }

            lock (_lock)
            {
                if (_convexClient != null)
                {
                    throw new InvalidOperationException("API is already initialized. Call Dispose() before reinitializing.");
                }

                _convexClient = new ConvexClient(deploymentUrl);
            }
        }

        /// <summary>
        /// Initializes the Genies Content API with custom Convex client options.
        /// </summary>
        /// <param name="options">The Convex client configuration options.</param>
        /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when API is already initialized.</exception>
        /// <example>
        /// <code>
        /// var options = new ConvexClientOptions
        /// {
        ///     DeploymentUrl = "https://your-deployment.convex.cloud",
        ///     TimeoutMs = 30000,
        ///     AutoReconnect = true
        /// };
        /// var api = new GeniesContentAPI();
        /// api.Initialize(options);
        /// </code>
        /// </example>
        public void Initialize(ConvexClientOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            lock (_lock)
            {
                if (_convexClient != null)
                {
                    throw new InvalidOperationException("API is already initialized. Call Dispose() before reinitializing.");
                }

                _convexClient = new ConvexClient(options);
            }
        }

        /// <summary>
        /// Enables or disables Convex debug logging.
        /// </summary>
        /// <param name="enabled">True to enable logging, false to disable.</param>
        private void SetConvexDebugLogging(bool enabled)
        {
            if (_convexClient == null)
            {
                return;
            }

            _convexClient.SetDebug(enabled);
        }

        #endregion

        #region Authentication

        /// <summary>
        /// Sets the authentication token for API calls (async version).
        /// </summary>
        /// <param name="token">The JWT authentication token.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <exception cref="InvalidOperationException">Thrown when API is not initialized.</exception>
        /// <example>
        /// <code>
        /// await api.SetAuthTokenAsync("your-jwt-token");
        /// </code>
        /// </example>
        public async Task SetAuthTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            try
            {
                await _convexClient.SetAuthAsync(token, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to set authentication token: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Clears the current authentication token.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <exception cref="InvalidOperationException">Thrown when API is not initialized.</exception>
        public async Task ClearAuthTokenAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            try
            {
                await _convexClient.ClearAuthAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to clear authentication token: {ex.Message}", ex);
            }
        }

        #endregion

        #region Connection

        /// <summary>
        /// Ensures the underlying Convex connection is established.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>True if connected after attempting to connect.</returns>
        public async Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            await EnsureConnectedInternalAsync(cancellationToken);
            return IsConnected;
        }

        #endregion

        #region Wearable Operations

        /// <summary>
        /// Gets the wearable configuration including available wearable types.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The wearable configuration with available types.</returns>
        /// <exception cref="InvalidOperationException">Thrown when API is not initialized or query fails.</exception>
        public Task<WearableConfig> GetWearableConfigAsync(CancellationToken cancellationToken = default)
        {
            return Wearables.GetConfigAsync(cancellationToken);
        }

        /// <summary>
        /// Creates a new wearable item (async version).
        /// </summary>
        /// <param name="data">The wearable data to create.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The result containing wearable ID and job ID.</returns>
        /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when API is not initialized or creation fails.</exception>
        public Task<WearableResult> CreateWearableAsync(WearableData data, CancellationToken cancellationToken = default)
        {
            return Wearables.CreateAsync(data, cancellationToken);
        }

        #endregion


        #region Decor Operations

        /// <summary>
        /// Gets the decor configuration including available decor types.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The decor configuration with available types.</returns>
        /// <exception cref="InvalidOperationException">Thrown when API is not initialized or query fails.</exception>
        public Task<DecorConfig> GetDecorConfigAsync(CancellationToken cancellationToken = default)
        {
            return Decor.GetConfigAsync(cancellationToken);
        }

        /// <summary>
        /// Creates a new decor item (async version).
        /// </summary>
        /// <param name="data">The decor data to create.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The result containing decor ID and job ID.</returns>
        /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when API is not initialized or creation fails.</exception>
        public Task<DecorResult> CreateDecorAsync(DecorData data, CancellationToken cancellationToken = default)
        {
            return Decor.CreateAsync(data, cancellationToken);
        }

        /// <summary>
        /// Creates a new decor item (async version) with optional model quality settings.
        /// </summary>
        /// <param name="data">The decor data to create.</param>
        /// <param name="polycount">Optional target polycount.</param>
        /// <param name="meshMode">Optional mesh mode ("Quad" or "Raw").</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The result containing decor ID and job ID.</returns>
        /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when API is not initialized or creation fails.</exception>
        public Task<DecorResult> CreateDecorAsync(
            DecorData data,
            int? polycount,
            string meshMode,
            CancellationToken cancellationToken = default)
        {
            return Decor.CreateAsync(data, polycount, meshMode, cancellationToken);
        }

        #endregion

        #region Job Status

        /// <summary>
        /// Gets the current status of a job (async version).
        /// </summary>
        /// <param name="jobId">The job ID to query status for.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The current job status.</returns>
        /// <exception cref="ArgumentException">Thrown when assetId is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when API is not initialized or query fails.</exception>
        public Task<JobStatus> GetJobStatusAsync(string jobId, CancellationToken cancellationToken = default)
        {
            return Jobs.GetStatusAsync(jobId, cancellationToken);
        }

        /// <summary>
        /// Subscribes to real-time status updates for an asset.
        /// </summary>
        /// <param name="jobId">The job ID to watch.</param>
        /// <param name="onUpdate">Callback invoked when status updates (status string and progress %).</param>
        /// <param name="onError">Callback invoked when an error occurs.</param>
        /// <returns>A subscription that can be disposed to stop watching.</returns>
        /// <exception cref="ArgumentException">Thrown when assetId is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when onUpdate or onError is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when API is not initialized.</exception>
        public IQuerySubscription SubscribeToJobStatus(string jobId, Action<JobStatus> onUpdate, Action<Exception> onError)
        {
            return Jobs.SubscribeToStatus(jobId, onUpdate, onError);
        }

        #endregion

        #region Asset Images

        /// <summary>
        /// Gets the images generated for a completed asset.
        /// </summary>
        /// <param name="assetId">The asset ID to retrieve images for.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A list of images generated for the asset.</returns>
        /// <exception cref="ArgumentException">Thrown when assetId is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when API is not initialized or query fails.</exception>
        public Task<List<AssetImage>> GetAssetImagesAsync(string assetId, CancellationToken cancellationToken = default)
        {
            return Assets.GetImagesAsync(assetId, cancellationToken);
        }

        /// <summary>
        /// Gets the 3D model generated for a completed asset.
        /// </summary>
        /// <param name="assetId">The asset ID to retrieve the model for.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 3D model generated for the asset.</returns>
        /// <exception cref="ArgumentException">Thrown when assetId is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when API is not initialized or query fails.</exception>
        public Task<AssetModel> GetAssetModelAsync(string assetId, CancellationToken cancellationToken = default)
        {
            return Assets.GetModelAsync(assetId, cancellationToken);
        }

        #endregion

        #region Wearable Model Operations

        /// <summary>
        /// Creates a wearable model from a selected image.
        /// </summary>
        /// <param name="assetId">The asset ID for the wearable.</param>
        /// <param name="imageId">The selected image ID to create the model from.</param>
        /// <param name="polycount">Optional target polycount.</param>
        /// <param name="meshMode">Optional mesh mode ("Quad" or "Raw").</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The result of the model creation operation.</returns>
        /// <exception cref="ArgumentException">Thrown when assetId or imageId is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when API is not initialized.</exception>
        public Task<WearableModelResult> CreateWearableModelAsync(
            string assetId,
            string imageId,
            int? polycount = null,
            string meshMode = null,
            CancellationToken cancellationToken = default)
        {
            return Wearables.CreateModelAsync(assetId, imageId, polycount, meshMode, cancellationToken);
        }

        /// <summary>
        /// Creates a wearable game asset from a completed model.
        /// </summary>
        /// <param name="assetId">The asset ID for the wearable.</param>
        /// <param name="modelId">The model ID to process into a game asset.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The result of the game asset creation operation.</returns>
        /// <exception cref="ArgumentException">Thrown when assetId or modelId is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when API is not initialized.</exception>
        public Task<WearableModelResult> CreateWearableGameAssetAsync(
            string assetId,
            string modelId,
            CancellationToken cancellationToken = default)
        {
            return Wearables.CreateGameAssetAsync(assetId, modelId, cancellationToken);
        }

        /// <summary>
        /// Creates a game asset for inventory using the GAP API.
        /// This simplified method only requires assetId and modelId parameters.
        /// </summary>
        /// <param name="assetId">The asset ID to create a game asset from.</param>
        /// <param name="modelId">The model ID to use for game asset creation.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A GameAssetResult containing the job ID and model ID for tracking.</returns>
        /// <exception cref="ArgumentException">Thrown when assetId or modelId is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the API is not initialized or the operation fails.</exception>
        public Task<GameAssetResult> CreateGameAssetAsync(
            string assetId,
            string modelId,
            CancellationToken cancellationToken = default)
        {
            return Wearables.CreateInventoryGameAssetAsync(assetId, modelId, cancellationToken);
        }

        #endregion

        #region Internal Helpers

        internal ConvexClient Client
        {
            get
            {
                EnsureInitialized();
                return _convexClient;
            }
        }

        internal async Task EnsureConnectedInternalAsync(CancellationToken cancellationToken)
        {
            EnsureInitialized();

            if (_convexClient.State == ConnectionState.Connected)
            {
                return;
            }

            // Wait for the lock using only the caller-supplied token so that a concurrent caller
            // that is already polling does not consume this caller's connection-work deadline
            // while it is merely queued behind the mutex.
            await _connectLock.WaitAsync(cancellationToken);
            try
            {
                if (_convexClient.State == ConnectionState.Connected)
                {
                    return;
                }

                // Apply the connection-work timeout only after the lock is held, so every caller
                // gets a fresh budget for the actual connect/poll work regardless of how long it
                // waited for the mutex.
                CancellationTokenSource timeoutCts = null;
                CancellationTokenSource deadlineCts = null;
                CancellationToken effectiveToken = cancellationToken;
                if (!effectiveToken.CanBeCanceled)
                {
                    timeoutCts = new CancellationTokenSource(DefaultEnsureConnectedTimeoutMs);
                    effectiveToken = timeoutCts.Token;
                }
                else
                {
                    // Link caller token with a fresh deadline so the work itself is still bounded.
                    // Both CTSes must be disposed; deadlineCts is the inner source, timeoutCts the linked one.
                    deadlineCts = new CancellationTokenSource(DefaultEnsureConnectedTimeoutMs);
                    timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineCts.Token);
                    effectiveToken = timeoutCts.Token;
                }

                try
                {
                    if (_convexClient.State == ConnectionState.Disconnected ||
                        _convexClient.State == ConnectionState.Closed)
                    {
                        _logger.Info("[GeniesContentAPI] Establishing lazy connection to Convex...");
                        await _convexClient.ConnectAsync(effectiveToken);
                    }
                    else
                    {
                        _logger.Info($"[GeniesContentAPI] Convex is {_convexClient.State}; waiting for connected state...");
                    }

                    // Convex transport can remain in Connecting/Reconnecting after ConnectAsync no-ops.
                    // Wait for a real Connected state so callers don't immediately fail with
                    // "Cannot send request. WebSocket state is Reconnecting".
                    while (_convexClient.State != ConnectionState.Connected)
                    {
                        effectiveToken.ThrowIfCancellationRequested();

                        if (_convexClient.State == ConnectionState.Disconnected ||
                            _convexClient.State == ConnectionState.Closed)
                        {
                            await _convexClient.ConnectAsync(effectiveToken);
                        }

                        await Task.Delay(EnsureConnectedPollDelayMs, effectiveToken);
                    }

                    _logger.Info("[GeniesContentAPI] Convex connection established");
                }
                catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"Timed out connecting to Convex after {DefaultEnsureConnectedTimeoutMs}ms.", ex);
                }
                finally
                {
                    timeoutCts?.Dispose();
                    deadlineCts?.Dispose();
                }
            }
            finally
            {
                _connectLock.Release();
            }
        }

        internal string ExtractErrorCode(ConvexError error)
        {
            if (error?.ErrorData == null)
            {
                return null;
            }

            if (error.ErrorData is ConvexValue.Object errorObj)
            {
                return ConvexValueReader.GetStringValue(errorObj, "code") ?? ConvexValueReader.GetStringValue(errorObj, "errorCode");
            }

            return null;
        }

        internal string FormatErrorMessage(string operation, string message, string errorCode = null)
        {
            if (!string.IsNullOrEmpty(errorCode))
            {
                return $"{operation} failed [{errorCode}]: {message}";
            }
            return $"{operation} failed: {message}";
        }

        public void RegisterEndpoint<TEndpoint>(Func<GeniesContentAPI, TEndpoint> factory)
            where TEndpoint : ContentEndpointBase
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            var type = typeof(TEndpoint);
            lock (_endpointLock)
            {
                if (_endpointFactories.ContainsKey(type))
                {
                    throw new InvalidOperationException($"Endpoint already registered: {type.Name}");
                }

                _endpointFactories[type] = api => factory(api);
            }
        }

        public bool TryGetEndpoint<TEndpoint>(out TEndpoint endpoint)
            where TEndpoint : ContentEndpointBase
        {
            lock (_endpointLock)
            {
                if (_endpointCache.TryGetValue(typeof(TEndpoint), out var cached))
                {
                    endpoint = (TEndpoint)cached;
                    return true;
                }

                if (_endpointFactories.TryGetValue(typeof(TEndpoint), out var factory))
                {
                    var created = factory(this);
                    _endpointCache[typeof(TEndpoint)] = created;
                    endpoint = (TEndpoint)created;
                    return true;
                }
            }

            endpoint = null;
            return false;
        }

        public TEndpoint GetEndpoint<TEndpoint>()
            where TEndpoint : ContentEndpointBase
        {
            if (TryGetEndpoint(out TEndpoint endpoint))
            {
                return endpoint;
            }

            throw new InvalidOperationException($"Endpoint is not registered: {typeof(TEndpoint).Name}");
        }

        private void RegisterDefaultEndpoints()
        {
            if (_endpointOptions.EnableWearables)
            {
                RegisterEndpoint(api => new WearablesEndpoint(api));
            }

            if (_endpointOptions.EnableDecor)
            {
                RegisterEndpoint(api => new DecorEndpoint(api));
            }

            if (_endpointOptions.EnableJobs)
            {
                RegisterEndpoint(api => new JobsEndpoint(api));
            }

            if (_endpointOptions.EnableAssets)
            {
                RegisterEndpoint(api => new AssetsEndpoint(api));
            }
        }

        private void EnsureInitialized()
        {
            if (_convexClient == null)
            {
                throw new InvalidOperationException("API is not initialized. Call Initialize() first.");
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GeniesContentAPI));
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Releases all resources used by the GeniesContentAPI.
        /// </summary>
        /// <remarks>
        /// Disposes the underlying Convex client and releases all managed resources.
        /// After disposal, the API cannot be reused. Create a new instance if needed.
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                try
                {
                    _convexClient?.Dispose();
                    _convexClient = null;
                }
                catch (Exception ex)
                {
                    _logger.Error($"[GeniesContentAPI] Error during disposal: {ex.Message}");
                }
            }

            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
