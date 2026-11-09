using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Genies.Convex.Core;
using Genies.Convex.Unity;
using Genies.ArtForge.ContentAPI;
using Genies.ArtForge.ContentAPI.Logging;
using Genies.Sdk.ArtForge.Editor.Auth;
using Newtonsoft.Json.Linq;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.API
{
    /// <summary>
    /// Manages the Convex WebSocket connection lifecycle, authentication state, and
    /// error-recovery wrapping for all backend operations.
    /// </summary>
    internal sealed class ConvexConnectionManager : IDisposable
    {
        private const int ConnectionInitialDelayMs = 500;
        private const int ConnectionPollMaxAttempts = 10;
        private const int ConnectionPollDelayMs = 200;
        private const int DefaultEnsureConnectedTimeoutMs = 15000;
        internal const int DefaultMaxRetries = 3;
        internal const int DefaultRetryDelayMs = 1000;
        private const int ConvexRequestTimeoutMs = 30000;
        private const int ConvexHeartbeatTimeoutMs = 120000;
        private const int ConvexMaxReconnectAttempts = 10;

        private static readonly string[] AuthErrorFragments =
        {
            "InvalidAuthHeader", "No auth provider found",
            "token identity expired", "identity expired", "Token expired",
            "HTTP 401", "Unauthorized"
        };

        private static readonly string[] HardInvalidateFragments =
        {
            "InvalidAuthHeader", "No auth provider found",
            "HTTP 401", "Unauthorized"
        };

        private static readonly string[] CancellationMessageFragments =
        {
            "task was cancel",
            "operation was cancel"
        };

        private readonly ILogger _logger;
        private readonly SemaphoreSlim _softResetLock = new SemaphoreSlim(1, 1);
        private readonly string _deploymentUrl;
        private readonly bool _useLegacyAssetOwnerQueries;

        private GeniesContentAPI _api;
        private bool _isAuthenticated;
        private bool _isDisposed;
        private bool _loggingEnabled;
        private string _lastBackendJwtToken;

        internal bool IsAuthenticated => _isAuthenticated;
        internal bool IsConnected => _api?.IsConnected ?? false;
        internal GeniesContentAPI Api => _api;

        internal ConvexConnectionManager(
            ILogger logger,
            string deploymentUrl,
            bool useLegacyAssetOwnerQueries,
            bool loggingEnabled)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _deploymentUrl = deploymentUrl ?? throw new ArgumentNullException(nameof(deploymentUrl));
            _useLegacyAssetOwnerQueries = useLegacyAssetOwnerQueries;
            _loggingEnabled = loggingEnabled;
            _api = CreateApi();
        }

        internal async Task<bool> AuthenticateAsync(string jwtToken)
        {
            return await RunAsync("[BackendClient] Authenticate", async () =>
            {
                _logger.Info("[BackendClient] Setting authentication token...");
                LogAuthTokenDiagnostics("[BackendClient][AuthDiag]", jwtToken);
                await _api.SetAuthTokenAsync(jwtToken);

                _isAuthenticated = true;
                _lastBackendJwtToken = jwtToken;

                await Task.Delay(ConnectionInitialDelayMs);

                for (int i = 0; i < ConnectionPollMaxAttempts && !_api.IsConnected; i++)
                {
                    await Task.Delay(ConnectionPollDelayMs);
                }

                if (_api.IsConnected)
                {
                    _logger.Info("[BackendClient] ✓ Authentication successful, WebSocket connected");
                }
                else
                {
                    _logger.Info("[BackendClient] ✓ Authentication successful (WebSocket will connect when needed)");
                }

                return true;
            }, ex =>
            {
                _logger.Error($"[BackendClient] Authentication failed: {ex.Message}");
            });
        }

        internal void SetLoggingEnabled(bool enabled)
        {
            _loggingEnabled = enabled;

            if (_api == null)
            {
                return;
            }

            try
            {
                _api.SetLoggingEnabled(enabled);
            }
            catch (Exception ex)
            {
                _logger.Warning($"[BackendClient] Failed to update Convex logging: {ex.Message}");
            }
        }

        internal async Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed || !_isAuthenticated || _api == null)
            {
                return false;
            }

            try
            {
                bool connected = await _api.EnsureConnectedAsync(cancellationToken);
                if (connected)
                {
                    return true;
                }

                return await TrySoftResetConnectionAsync(
                    "EnsureConnectedAsync returned disconnected state",
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                if (IsCallerCancellation(ex, cancellationToken))
                {
                    throw;
                }

                if (ex is OperationCanceledException)
                {
                    ex = new TimeoutException(
                        $"Timed out connecting to Convex after {DefaultEnsureConnectedTimeoutMs}ms.", ex);
                }

                _logger.Warning($"[BackendClient] Failed to reconnect WebSocket: {ex.Message}");

                string chain = BuildErrorChain(ex);
                bool isAuthError = IsAuthenticationError(chain);
                bool isTransitionTimeout = IsTransitionResponseTimeout(chain);
                _logger.Info(
                    $"[BackendClient][AuthRecoveryDiag] EnsureConnectedAsync failure classified: " +
                    $"isAuthError={isAuthError}, isConnectionError={IsConnectionError(chain)}, " +
                    $"isTransitionTimeout={isTransitionTimeout}, error_chain='{chain}'");

                if (!isAuthError && !isTransitionTimeout)
                {
                    return await TrySoftResetConnectionAsync(
                        $"reconnect failed: {ex.Message}", CancellationToken.None);
                }

                string reason = isAuthError ? "auth-related failure" : "transition-timeout fallback";
                _logger.Info(
                    $"[BackendClient] Reconnect failure detected ({reason}). Attempting token refresh (non-destructive mode)...");

                bool refreshed = await TryRefreshAuthAsync();
                if (!refreshed)
                {
                    _logger.Warning("[BackendClient] Token refresh failed during reconnect.");
                    return await TrySoftResetConnectionAsync(
                        "token refresh failed during reconnect", CancellationToken.None);
                }

                try
                {
                    bool connected = await _api.EnsureConnectedAsync(cancellationToken);
                    if (connected)
                    {
                        _logger.Info("[BackendClient] Reconnect succeeded after auth refresh.");
                    }

                    return connected;
                }
                catch (Exception retryEx)
                {
                    _logger.Warning(
                        $"[BackendClient] Reconnect still failed after auth refresh: {retryEx.Message}");
                    return await TrySoftResetConnectionAsync(
                        $"reconnect failed after auth refresh: {retryEx.Message}",
                        CancellationToken.None);
                }
            }
        }

        // Void overload delegates to the generic version to avoid duplicating the retry/recovery logic.
        internal Task RunAsync(
            string opName,
            Func<Task> action,
            Action<Exception> errorLog = null,
            CancellationToken cancellationToken = default)
            => RunAsync<bool>(opName, async () => { await action(); return true; }, errorLog, cancellationToken);

        internal async Task<T> RunAsync<T>(
            string opName,
            Func<Task<T>> action,
            Action<Exception> errorLog = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureConnectionIfNeededAsync(opName, cancellationToken);

            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                var c = ClassifyException(ex, cancellationToken);

                if (c.IsTransportCancellation)
                {
                    _logger.Warning(
                        $"[BackendClient] {opName} was canceled by transport while caller token remained active. Treating as connection failure.");
                }

                _logger.Info(
                    $"[BackendClient][AuthRecoveryDiag] {opName} failure classified: " +
                    $"isAuthError={c.IsAuthError}, isConnectionError={c.IsConnectionError}, " +
                    $"isTransitionTimeout={c.IsTransitionTimeout}, error_chain='{c.ErrorChain}'");

                if (c.ShouldAttemptRefresh && await TryRefreshAuthAsync())
                {
                    try
                    {
                        return await action();
                    }
                    catch (Exception retryEx)
                    {
                        ex = retryEx;
                        c = ClassifyException(ex, cancellationToken);
                        if (c.IsTransportCancellation)
                        {
                            _logger.Warning(
                                $"[BackendClient] {opName} retry-after-refresh was canceled by transport while caller token remained active.");
                        }

                        _logger.Info(
                            $"[BackendClient][AuthRecoveryDiag] {opName} retry-after-refresh failed: " +
                            $"isAuthError={c.IsAuthError}, isConnectionError={c.IsConnectionError}, " +
                            $"isTransitionTimeout={c.IsTransitionTimeout}, error_chain='{c.ErrorChain}'");
                    }
                }
                else if (c.IsConnectionError && !c.IsCallerCancellation)
                {
                    bool reconnected = await EnsureConnectedAsync();
                    if (reconnected && IsConnected)
                    {
                        _logger.Info($"[BackendClient] {opName} recovered connection, retrying operation.");
                        try
                        {
                            return await action();
                        }
                        catch (Exception retryEx)
                        {
                            ex = retryEx;
                            c = ClassifyException(ex, cancellationToken);
                            if (c.IsTransportCancellation)
                            {
                                _logger.Warning(
                                    $"[BackendClient] {opName} retry-after-reconnect was canceled by transport while caller token remained active.");
                            }

                            _logger.Info(
                                $"[BackendClient][AuthRecoveryDiag] {opName} retry-after-reconnect failed: " +
                                $"isAuthError={c.IsAuthError}, isConnectionError={c.IsConnectionError}, " +
                                $"isTransitionTimeout={c.IsTransitionTimeout}, error_chain='{c.ErrorChain}'");
                        }
                    }

                    bool softResetRecovered = await TrySoftResetConnectionAsync(
                        $"{opName} retry-after-reconnect fallback", CancellationToken.None);
                    if (softResetRecovered && IsConnected)
                    {
                        _logger.Info(
                            $"[BackendClient] {opName} recovered via soft backend reset, retrying operation.");
                        try
                        {
                            return await action();
                        }
                        catch (Exception softResetRetryEx)
                        {
                            ex = softResetRetryEx;
                            c = ClassifyException(ex, cancellationToken);
                            _logger.Info(
                                $"[BackendClient][AuthRecoveryDiag] {opName} retry-after-soft-reset failed: " +
                                $"isAuthError={c.IsAuthError}, isConnectionError={c.IsConnectionError}, " +
                                $"isTransitionTimeout={c.IsTransitionTimeout}, error_chain='{c.ErrorChain}'");
                        }
                    }
                }

                if (c.IsAuthError && ShouldHardInvalidateAuthentication(c.ErrorChain))
                {
                    HandleAuthenticationError(ex);
                }

                if (errorLog != null)
                {
                    errorLog(ex);
                }
                else
                {
                    _logger.Error($"{opName} failed: {ex.Message}");
                }

                throw;
            }
        }

        internal async Task<T> RetryAsync<T>(
            string operation,
            Func<Task<T>> action,
            int maxRetries,
            int retryDelayMs,
            CancellationToken cancellationToken = default)
        {
            int attempt = 0;
            Exception lastException = null;

            while (attempt < maxRetries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                attempt++;

                try
                {
                    return await action();
                }
                catch (Exception ex)
                {
                    if (IsCallerCancellation(ex, cancellationToken))
                    {
                        throw;
                    }

                    string chain = BuildErrorChain(ex);
                    bool isTransportCancellation = IsTransportCancellation(ex, cancellationToken, chain);
                    bool isConnectionError = IsConnectionError(chain) || isTransportCancellation;
                    lastException = ex;

                    if (!isConnectionError || attempt >= maxRetries)
                    {
                        _logger.Error(
                            $"[BackendClient] Failed to {operation} on attempt {attempt}: {ex.Message}");

                        if (isConnectionError)
                        {
                            // Do NOT attach the connection error as InnerException: BuildErrorChain
                            // traverses the chain, and RunAsync would re-classify this as a
                            // connection error and retry the entire operation (causing duplicate
                            // non-idempotent calls). The error detail was already logged above.
                            throw new InvalidOperationException(
                                $"Operation '{operation}' exhausted all {maxRetries} retries.");
                        }

                        throw;
                    }

                    if (isTransportCancellation)
                    {
                        _logger.Warning(
                            $"[BackendClient] Transport canceled {operation} on attempt {attempt}; treating as transient connection failure.");
                    }

                    bool reconnected = await EnsureConnectedAsync(cancellationToken);
                    if (reconnected && IsConnected)
                    {
                        _logger.Info(
                            $"[BackendClient] Reconnected WebSocket before retrying {operation} (attempt {attempt + 1}).");
                    }

                    _logger.Warning(
                        $"[BackendClient] Connection error on attempt {attempt}, retrying in {retryDelayMs}ms: {ex.Message}");
                    await Task.Delay(retryDelayMs * attempt, cancellationToken);
                }
            }

            throw new InvalidOperationException(
                $"Failed to {operation} after {maxRetries} attempts", lastException);
        }

        internal async Task DisconnectAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                if (_api != null)
                {
                    await _api.ClearAuthTokenAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"[BackendClient] Failed to clear auth token: {ex.Message}");
            }

            Dispose();
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                _api?.Dispose();
                _api = null;
            }
            catch (Exception ex)
            {
                _logger.Warning($"[BackendClient] Error during disposal: {ex.Message}");
            }

            _softResetLock.Dispose();
            _isDisposed = true;
            _isAuthenticated = false;
            _lastBackendJwtToken = null;
        }

        internal void LogAuthTokenDiagnostics(string prefix, string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.Warning($"{prefix} Token is empty");
                return;
            }

            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                _logger.Warning($"{prefix} Non-JWT token (length={token.Length})");
                return;
            }

            if (!TryDecodeJwtJson(parts[0], out var header) || !TryDecodeJwtJson(parts[1], out var payload))
            {
                _logger.Warning($"{prefix} Failed to decode JWT header/payload");
                return;
            }

            var issuer = payload["iss"]?.Value<string>() ?? "<missing>";
            var audienceToken = payload["aud"];
            string audience;
            if (audienceToken == null)
            {
                audience = "<missing>";
            }
            else if (audienceToken.Type == JTokenType.Array)
            {
                audience = string.Join(", ", audienceToken.Values<string>());
            }
            else
            {
                audience = audienceToken.Value<string>() ?? audienceToken.ToString();
            }

            var algorithm = header["alg"]?.Value<string>() ?? "<missing>";
            var keyId = header["kid"]?.Value<string>() ?? "<missing>";

            _logger.Info($"{prefix} iss={issuer}");
            _logger.Info($"{prefix} aud={audience}");
            _logger.Info($"{prefix} alg={algorithm}, kid={keyId}");
        }

        private GeniesContentAPI CreateApi()
        {
            var endpointOptions = new GeniesContentApiEndpointOptions
            {
                UseLegacyAssetOwnerQueries = _useLegacyAssetOwnerQueries
            };

            var api = new GeniesContentAPI(endpointOptions);
            api.Initialize(new ConvexClientOptions
            {
                DeploymentUrl = _deploymentUrl,
                Debug = _loggingEnabled,
                TimeoutMs = ConvexRequestTimeoutMs,
                WebSocketHeartbeatTimeoutMs = ConvexHeartbeatTimeoutMs,
                MaxReconnectAttempts = ConvexMaxReconnectAttempts
            });
            api.SetLogger(new UnityContentApiLogger());
            api.SetLoggingEnabled(_loggingEnabled);
            return api;
        }

        private async Task<bool> TrySoftResetConnectionAsync(string reason, CancellationToken cancellationToken)
        {
            if (_isDisposed)
            {
                return false;
            }

            await _softResetLock.WaitAsync(cancellationToken);
            try
            {
                if (_isDisposed)
                {
                    return false;
                }

                if (_api != null && _api.IsConnected)
                {
                    return true;
                }

                _logger.Warning($"[BackendClient] Attempting soft backend client reset ({reason})...");

                GeniesContentAPI previousApi = _api;
                GeniesContentAPI replacementApi = null;

                try
                {
                    replacementApi = CreateApi();

                    if (_isAuthenticated)
                    {
                        string token = await ResolveBackendAuthTokenForRecoveryAsync();
                        if (string.IsNullOrWhiteSpace(token))
                        {
                            _logger.Warning("[BackendClient] Soft reset aborted: no backend auth token available.");
                            replacementApi.Dispose();
                            return false;
                        }

                        LogAuthTokenDiagnostics("[BackendClient][AuthDiag]", token);
                        await replacementApi.SetAuthTokenAsync(token, cancellationToken);
                        _lastBackendJwtToken = token;
                    }

                    bool connected = await replacementApi.EnsureConnectedAsync(cancellationToken);
                    if (!connected)
                    {
                        _logger.Warning("[BackendClient] Soft reset could not establish Convex connection.");
                        replacementApi.Dispose();
                        return false;
                    }

                    _api = replacementApi;
                    replacementApi = null;

                    try
                    {
                        previousApi?.Dispose();
                    }
                    catch (Exception disposeEx)
                    {
                        _logger.Warning(
                            $"[BackendClient] Failed to dispose previous backend API after soft reset: {disposeEx.Message}");
                    }

                    _logger.Info("[BackendClient] Soft backend client reset succeeded.");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.Warning($"[BackendClient] Soft backend client reset failed: {ex.Message}");
                    try
                    {
                        replacementApi?.Dispose();
                    }
                    catch (Exception disposeEx)
                    {
                        _logger.Warning(
                            $"[BackendClient] Failed disposing replacement backend API after reset failure: {disposeEx.Message}");
                    }

                    return false;
                }
            }
            finally
            {
                _softResetLock.Release();
            }
        }

        private async Task<bool> TryRefreshAuthAsync()
        {
            var refreshedToken = await WorkOSAuthService.RefreshBackendAuthTokenAsync(
                _logger.Info, _logger.Warning);
            if (string.IsNullOrEmpty(refreshedToken))
            {
                _logger.Warning("[BackendClient] Refresh did not produce a backend-compatible JWT token.");
                return false;
            }

            try
            {
                _logger.Info("[BackendClient] Applying refreshed backend token to Convex auth...");
                LogAuthTokenDiagnostics("[BackendClient][AuthDiag]", refreshedToken);
                await _api.SetAuthTokenAsync(refreshedToken);
                _isAuthenticated = true;
                _lastBackendJwtToken = refreshedToken;
                _logger.Info("[BackendClient] Authentication refreshed and applied to Convex auth provider.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Warning($"[BackendClient] Failed to set refreshed token: {ex.Message}");
                return false;
            }
        }

        private void HandleAuthenticationError(Exception ex)
        {
            if (!_isAuthenticated)
            {
                return;
            }

            _isAuthenticated = false;
            _lastBackendJwtToken = null;
            _logger.Warning($"[BackendClient] Authentication invalidated: {ex.Message}");

            WorkOSAuthService.ClearIdToken();
            WorkOSAuthService.ClearAccessToken();
        }

        private async Task<string> ResolveBackendAuthTokenForRecoveryAsync()
        {
            string token = await WorkOSAuthService.GetValidBackendAuthTokenAsync(
                _logger.Info, _logger.Warning);
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }

            if (!string.IsNullOrWhiteSpace(_lastBackendJwtToken))
            {
                _logger.Warning("[BackendClient] Falling back to last known backend JWT token for soft reset.");
                return _lastBackendJwtToken;
            }

            return null;
        }

        private async Task EnsureConnectionIfNeededAsync(
            string operation,
            CancellationToken cancellationToken = default)
        {
            if (_isDisposed || !_isAuthenticated || _api == null || IsConnected)
            {
                return;
            }

            _logger.Info(
                $"[BackendClient] {operation} requested while disconnected. Attempting automatic reconnect...");

            bool reconnected = await EnsureConnectedAsync(cancellationToken);
            if (reconnected && IsConnected)
            {
                _logger.Info($"[BackendClient] {operation} reconnect succeeded.");
                return;
            }

            _logger.Warning(
                $"[BackendClient] {operation} reconnect attempt did not establish an active WebSocket connection.");
        }

        private readonly struct ExceptionClassification
        {
            public readonly bool IsCallerCancellation;
            public readonly bool IsTransportCancellation;
            public readonly bool IsAuthError;
            public readonly bool IsConnectionError;
            public readonly bool IsTransitionTimeout;
            public readonly string ErrorChain;

            public bool ShouldAttemptRefresh => !IsCallerCancellation && (IsAuthError || IsTransitionTimeout);

            public ExceptionClassification(
                bool isCallerCancellation,
                bool isTransportCancellation,
                bool isAuthError,
                bool isConnectionError,
                bool isTransitionTimeout,
                string errorChain)
            {
                IsCallerCancellation = isCallerCancellation;
                IsTransportCancellation = isTransportCancellation;
                IsAuthError = isAuthError;
                IsConnectionError = isConnectionError;
                IsTransitionTimeout = isTransitionTimeout;
                ErrorChain = errorChain;
            }
        }

        private ExceptionClassification ClassifyException(Exception ex, CancellationToken cancellationToken)
        {
            string chain = BuildErrorChain(ex);
            bool isCallerCancellation = IsCallerCancellation(ex, cancellationToken);
            bool isTransportCancellation = IsTransportCancellation(ex, cancellationToken, chain);
            bool isAuthError = IsAuthenticationError(chain);
            bool isTransitionTimeout = IsTransitionResponseTimeout(chain);
            bool isConnectionError = IsConnectionError(chain) || isTransportCancellation;
            return new ExceptionClassification(
                isCallerCancellation, isTransportCancellation,
                isAuthError, isConnectionError, isTransitionTimeout, chain);
        }

        private static bool IsCallerCancellation(Exception ex, CancellationToken cancellationToken)
            => ex is OperationCanceledException &&
               cancellationToken.CanBeCanceled &&
               cancellationToken.IsCancellationRequested;

        private static bool IsTransportCancellation(
            Exception ex,
            CancellationToken cancellationToken,
            string errorChain = null)
        {
            if (IsCallerCancellation(ex, cancellationToken))
            {
                return false;
            }

            if (ex is OperationCanceledException)
            {
                return true;
            }

            return ContainsCancellationMessage(errorChain ?? BuildErrorChain(ex));
        }

        private static bool ContainsCancellationMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            foreach (string fragment in CancellationMessageFragments)
            {
                if (message.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsConnectionError(string errorChain)
            => errorChain.IndexOf("WebSocket", StringComparison.OrdinalIgnoreCase) >= 0 ||
               errorChain.IndexOf("not open", StringComparison.OrdinalIgnoreCase) >= 0 ||
               errorChain.IndexOf("not connected", StringComparison.OrdinalIgnoreCase) >= 0 ||
               errorChain.IndexOf("timed out connecting to convex", StringComparison.OrdinalIgnoreCase) >= 0 ||
               errorChain.IndexOf("Connection timeout waiting for Transition response", StringComparison.OrdinalIgnoreCase) >= 0 ||
               ContainsCancellationMessage(errorChain) ||
               errorChain.IndexOf("Connection", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsTransitionResponseTimeout(string errorChain)
            => errorChain.IndexOf(
                "Connection timeout waiting for Transition response",
                StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsAuthenticationError(string errorChain)
        {
            foreach (string fragment in AuthErrorFragments)
            {
                if (errorChain.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldHardInvalidateAuthentication(string errorChain)
        {
            foreach (string fragment in HardInvalidateFragments)
            {
                if (errorChain.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildErrorChain(Exception ex)
        {
            if (ex == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            int depth = 0;
            Exception current = ex;
            while (current != null && depth < 6)
            {
                if (!string.IsNullOrWhiteSpace(current.Message))
                {
                    parts.Add(current.Message.Trim());
                }

                current = current.InnerException;
                depth++;
            }

            return string.Join(" | ", parts);
        }

        private static bool TryDecodeJwtJson(string base64Url, out JObject json)
        {
            json = null;
            if (string.IsNullOrWhiteSpace(base64Url))
            {
                return false;
            }

            string normalized = base64Url.Replace('-', '+').Replace('_', '/');
            switch (normalized.Length % 4)
            {
                case 2:
                    normalized += "==";
                    break;
                case 3:
                    normalized += "=";
                    break;
                case 0:
                    break;
                default:
                    return false;
            }

            try
            {
                var bytes = Convert.FromBase64String(normalized);
                var rawJson = System.Text.Encoding.UTF8.GetString(bytes);
                json = JObject.Parse(rawJson);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
