using System;
using System.Threading;
using System.Threading.Tasks;
using Genies.ArtForge.ContentAPI.Models;
using Genies.ArtForge.ContentAPI.Logging;

namespace Genies.ArtForge.ContentAPI
{
    /// <summary>
    /// Extension methods for GeniesContentAPI to add connection management and retry logic
    /// </summary>
    internal static class GeniesContentAPIExtensions
    {
        private static bool IsConnectionError(Exception exception)
        {
            if (exception == null)
            {
                return false;
            }

            var message = exception.Message;
            return message.Contains("WebSocket") ||
                   message.Contains("not open") ||
                   message.Contains("not connected") ||
                   message.Contains("Connection");
        }

        private static async Task<T> ExecuteWithRetryAsync<T>(
            GeniesContentAPI api,
            Func<Task<T>> operation,
            int maxRetries,
            int retryDelayMs,
            string finalFailureMessage,
            Func<int, string> attemptLogMessage,
            Func<int, string> successLogMessage,
            Func<int, Exception, string> retryLogMessage,
            Func<int, Exception, string> failureLogMessage,
            CancellationToken cancellationToken)
        {
            var logger = api.Logger;
            int attempt = 0;
            Exception lastException = null;

            while (attempt < maxRetries)
            {
                attempt++;

                try
                {
                    if (attemptLogMessage != null)
                    {
                        logger.Info(attemptLogMessage(attempt));
                    }

                    var result = await operation();

                    if (successLogMessage != null)
                    {
                        logger.Info(successLogMessage(attempt));
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    bool isConnectionError = IsConnectionError(ex);

                    if (!isConnectionError || attempt >= maxRetries)
                    {
                        if (failureLogMessage != null)
                        {
                            logger.Error(failureLogMessage(attempt, ex));
                        }

                        throw;
                    }

                    if (retryLogMessage != null)
                    {
                        logger.Warning(retryLogMessage(attempt, ex));
                    }

                    await Task.Delay(retryDelayMs * attempt, cancellationToken);
                }
            }

            throw new InvalidOperationException(finalFailureMessage, lastException);
        }

        /// <summary>
        /// Creates a wearable with automatic retry on connection failures
        /// </summary>
        public static async Task<WearableResult> CreateWearableWithRetryAsync(
            this GeniesContentAPI api,
            WearableData data,
            int maxRetries = 3,
            int retryDelayMs = 1000,
            CancellationToken cancellationToken = default)
        {
            if (api == null)
            {
                throw new ArgumentNullException(nameof(api));
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return await ExecuteWithRetryAsync(
                api,
                () => api.CreateWearableAsync(data, cancellationToken),
                maxRetries,
                retryDelayMs,
                $"Failed to create wearable after {maxRetries} attempts",
                attempt => $"[GeniesAPI] Attempt {attempt}/{maxRetries} to create wearable",
                attempt => $"[GeniesAPI] ✓ Successfully created wearable on attempt {attempt}",
                (attempt, ex) => $"[GeniesAPI] Connection error on attempt {attempt}, retrying in {retryDelayMs}ms: {ex.Message}",
                (attempt, ex) => $"[GeniesAPI] Failed on attempt {attempt}: {ex.Message}",
                cancellationToken);
        }

        /// <summary>
        /// Creates a decor item with automatic retry on connection failures
        /// </summary>
        public static async Task<DecorResult> CreateDecorWithRetryAsync(
            this GeniesContentAPI api,
            DecorData data,
            int maxRetries = 3,
            int retryDelayMs = 1000,
            CancellationToken cancellationToken = default)
        {
            if (api == null)
            {
                throw new ArgumentNullException(nameof(api));
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return await ExecuteWithRetryAsync(
                api,
                () => api.CreateDecorAsync(data, cancellationToken),
                maxRetries,
                retryDelayMs,
                $"Failed to create decor after {maxRetries} attempts",
                attempt => $"[GeniesAPI] Attempt {attempt}/{maxRetries} to create decor",
                attempt => $"[GeniesAPI] ✓ Successfully created decor on attempt {attempt}",
                (attempt, ex) => $"[GeniesAPI] Connection error on attempt {attempt}, retrying in {retryDelayMs}ms: {ex.Message}",
                (attempt, ex) => $"[GeniesAPI] Failed on attempt {attempt}: {ex.Message}",
                cancellationToken);
        }

        /// <summary>
        /// Creates a decor item with automatic retry on connection failures and optional model quality settings.
        /// </summary>
        public static async Task<DecorResult> CreateDecorWithRetryAsync(
            this GeniesContentAPI api,
            DecorData data,
            int? polycount,
            string meshMode,
            int maxRetries = 3,
            int retryDelayMs = 1000,
            CancellationToken cancellationToken = default)
        {
            if (api == null)
            {
                throw new ArgumentNullException(nameof(api));
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return await ExecuteWithRetryAsync(
                api,
                () => api.CreateDecorAsync(data, polycount, meshMode, cancellationToken),
                maxRetries,
                retryDelayMs,
                $"Failed to create decor after {maxRetries} attempts",
                attempt => $"[GeniesAPI] Attempt {attempt}/{maxRetries} to create decor",
                attempt => $"[GeniesAPI] ✓ Successfully created decor on attempt {attempt}",
                (attempt, ex) => $"[GeniesAPI] Connection error on attempt {attempt}, retrying in {retryDelayMs}ms: {ex.Message}",
                (attempt, ex) => $"[GeniesAPI] Failed on attempt {attempt}: {ex.Message}",
                cancellationToken);
        }

        /// <summary>
        /// Gets job status with automatic retry on connection failures
        /// </summary>
        public static async Task<JobStatus> GetJobStatusWithRetryAsync(
            this GeniesContentAPI api,
            string assetId,
            int maxRetries = 3,
            int retryDelayMs = 500,
            CancellationToken cancellationToken = default)
        {
            if (api == null)
            {
                throw new ArgumentNullException(nameof(api));
            }

            if (string.IsNullOrWhiteSpace(assetId))
            {
                throw new ArgumentException("Asset ID cannot be null or empty", nameof(assetId));
            }

            return await ExecuteWithRetryAsync(
                api,
                () => api.GetJobStatusAsync(assetId, cancellationToken),
                maxRetries,
                retryDelayMs,
                $"Failed to get job status after {maxRetries} attempts",
                null,
                null,
                (attempt, ex) => $"[GeniesAPI] Status check error on attempt {attempt}, retrying: {ex.Message}",
                null,
                cancellationToken);
        }

        /// <summary>
        /// Ensures the API is connected and ready for operations
        /// </summary>
        public static async Task<bool> EnsureReadyAsync(
            this GeniesContentAPI api,
            int timeoutMs = 5000,
            int checkConnectionEveryMs = 100,
            CancellationToken cancellationToken = default)
        {
            if (api == null)
            {
                throw new ArgumentNullException(nameof(api));
            }

            if (!api.IsInitialized)
            {
                api.Logger.Error("[GeniesAPI] API is not initialized");
                return false;
            }

            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromMilliseconds(timeoutMs);

            // Wait for connection with timeout
            while (!api.IsConnected && (DateTime.UtcNow - startTime) < timeout)
            {
                await Task.Delay(checkConnectionEveryMs, cancellationToken);
            }

            if (api.IsConnected)
            {
                api.Logger.Info("[GeniesAPI] Connection ready");
                return true;
            }

            api.Logger.Warning($"[GeniesAPI] Connection not established within {timeoutMs}ms");
            return false;
        }
    }
}
