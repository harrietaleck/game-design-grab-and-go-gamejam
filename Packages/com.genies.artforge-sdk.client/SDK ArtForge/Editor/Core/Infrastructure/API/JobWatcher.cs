using System;
using System.Threading;
using System.Threading.Tasks;
using Genies.Convex.Core;
using Genies.Convex.Unity;
using Genies.ArtForge.ContentAPI;
using Genies.ArtForge.ContentAPI.Models;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.API
{
    /// <summary>
    /// Subscribes to Convex job status updates and manages polling fallback during realtime stalls.
    /// </summary>
    internal sealed class JobWatcher
    {
        private const int WatchdogTickMs = 3000;
        private const int StallTimeoutMs = 20000;
        private const float ProgressPercentScale = 100f;

        private readonly ConvexConnectionManager _connection;
        private readonly ILogger _logger;

        internal JobWatcher(ConvexConnectionManager connection, ILogger logger)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        internal async Task<JobStatus> SubscribeAndWaitForJobAsync(
            string jobId,
            Action<JobStatus> onProgress = null,
            CancellationToken cancellationToken = default)
        {
            return await _connection.RunAsync("[BackendClient] SubscribeAndWaitForJob", async () =>
            {
                _logger.Info($"[BackendClient] Subscribing to job status: {jobId}");

                JobStatus finalStatus = null;
                var taskCompletionSource = new TaskCompletionSource<JobStatus>();
                DateTime lastStatusUpdateUtc = DateTime.UtcNow;
                IQuerySubscription subscription = null;

                if (cancellationToken != default)
                {
                    cancellationToken.Register(() =>
                    {
                        _logger.Warning($"[BackendClient] Job {jobId} subscription cancelled by user");
                        subscription?.Dispose();
                        taskCompletionSource.TrySetCanceled();
                    });
                }

                var jobs = _connection.Api.GetEndpoint<JobsEndpoint>();
                subscription = jobs.SubscribeToStatus(
                    jobId,
                    status =>
                    {
                        // WebSocket callbacks run on background thread — dispatch to main thread
                        UnityMainThreadDispatcher.Enqueue(() =>
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                subscription?.Dispose();
                                return;
                            }

                            string stepInfo = !string.IsNullOrEmpty(status.step)
                                ? $" - Step: {status.step}"
                                : "";
                            lastStatusUpdateUtc = DateTime.UtcNow;

                            if (status.IsTimedOut())
                            {
                                _logger.Warning(
                                    $"[BackendClient] Job {jobId}: {status.status}{stepInfo} " +
                                    $"({status.progress * ProgressPercentScale:F1}%) - " +
                                    $"Step taking longer than expected, but still running...");
                            }
                            else
                            {
                                _logger.Info(
                                    $"[BackendClient] Job {jobId}: {status.status}{stepInfo} " +
                                    $"({status.progress * ProgressPercentScale:F1}%)");
                            }

                            try
                            {
                                onProgress?.Invoke(status);
                            }
                            catch (Exception ex)
                            {
                                _logger.Warning($"[BackendClient] Progress callback error: {ex.Message}");
                            }

                            if (status.IsCompleted())
                            {
                                _logger.Info($"[BackendClient] ✓ Job {jobId} completed successfully");
                                finalStatus = status;
                                subscription?.Dispose();
                                taskCompletionSource.TrySetResult(status);
                            }
                            else if (status.IsFailed())
                            {
                                _logger.Error($"[BackendClient] ✗ Job {jobId} failed");
                                finalStatus = status;
                                subscription?.Dispose();
                                taskCompletionSource.TrySetResult(status);
                            }
                        });
                    },
                    error =>
                    {
                        UnityMainThreadDispatcher.Enqueue(() =>
                        {
                            _logger.Error($"[BackendClient] Job subscription error: {error.Message}");
                            subscription?.Dispose();
                            taskCompletionSource.TrySetException(error);
                        });
                    }
                );

                // Wait for job completion while checking for stalled realtime updates.
                while (!taskCompletionSource.Task.IsCompleted)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        subscription?.Dispose();
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    Task completedTask = await Task.WhenAny(
                        taskCompletionSource.Task,
                        Task.Delay(WatchdogTickMs, cancellationToken));

                    if (completedTask == taskCompletionSource.Task)
                    {
                        break;
                    }

                    if ((DateTime.UtcNow - lastStatusUpdateUtc).TotalMilliseconds < StallTimeoutMs)
                    {
                        continue;
                    }

                    _logger.Warning(
                        $"[BackendClient] No realtime job updates for {jobId} in {StallTimeoutMs}ms. " +
                        $"Polling backend status and attempting reconnect.");

                    try
                    {
                        var polledStatus = await jobs.GetStatusAsync(jobId, cancellationToken);
                        if (polledStatus != null)
                        {
                            lastStatusUpdateUtc = DateTime.UtcNow;

                            try
                            {
                                onProgress?.Invoke(polledStatus);
                            }
                            catch (Exception ex)
                            {
                                _logger.Warning(
                                    $"[BackendClient] Polled progress callback error: {ex.Message}");
                            }

                            if (polledStatus.IsCompleted() || polledStatus.IsFailed() || polledStatus.IsAborted())
                            {
                                _logger.Info(
                                    $"[BackendClient] Job {jobId} reached terminal state via polling: {polledStatus.status}");
                                subscription?.Dispose();
                                return polledStatus;
                            }
                        }
                    }
                    catch (Exception pollEx)
                    {
                        _logger.Warning(
                            $"[BackendClient] Failed to poll job status during realtime stall: {pollEx.Message}");
                    }

                    bool reconnected = await _connection.EnsureConnectedAsync(cancellationToken);
                    if (reconnected && _connection.IsConnected)
                    {
                        _logger.Info(
                            $"[BackendClient] Realtime connection recovered while monitoring job {jobId}.");
                    }
                    else
                    {
                        _logger.Warning(
                            $"[BackendClient] Realtime connection still unavailable while monitoring job {jobId}; " +
                            $"continuing with polling fallback.");
                    }
                }

                finalStatus = await taskCompletionSource.Task;
                return finalStatus;
            }, ex =>
            {
                if (ex is OperationCanceledException)
                {
                    _logger.Warning(
                        $"[BackendClient] Job status subscription was cancelled: {ex.Message}");
                    return;
                }

                _logger.Error($"[BackendClient] Failed to subscribe to job status: {ex.Message}");
            }, cancellationToken);
        }
    }
}
