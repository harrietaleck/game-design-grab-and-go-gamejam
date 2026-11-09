using System;
using System.Threading;
using System.Threading.Tasks;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.API;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;

namespace Genies.Sdk.ArtForge.Editor.Core.Application.Services
{
    /// <summary>
    /// Backend generation service (Genies Backend API + Convex subscriptions).
    /// Handles all asset generation through the Genies backend.
    /// </summary>
    internal sealed class GenerationService : IDisposable
    {
        private const float BackendSubmitProgress = 0.05f;
        private const float BackendSubmitAcceptedProgress = 0.11f;
        private const float ProgressComplete = 1.0f;
        private const float ProgressFailed = 0.0f;
        private const float EstimatedCostBackend = 0f;
        private const int BackendSubmitTimeoutMs = 45000;
        private const int SubmitWatchdogTickMs = 500;
        private const int SubmitCancellationGraceMs = 2000;
        private const string SubmitProgressMessage = "Submitting backend job...";
        private const string BackendSubmittedStep = "backend_submitted";

        private readonly ILogger _logger;
        private ContentClient _backendClient;
        private AssetOrchestrator _orchestrator;
        private DeploymentEnvironment _environment = DeploymentEnvironment.Production;

        /// <summary>
        /// Event fired when generation progress updates
        /// Args: (jobId, progress 0-1, message)
        /// </summary>
        public event Action<JobId, float, string> OnProgressUpdated;

        /// <summary>
        /// Backend is considered available when authenticated.
        /// </summary>
        public bool IsBackendAvailable => _backendClient?.IsAuthenticated ?? false;

        /// <summary>
        /// Backend is fully ready when authenticated and orchestrator is initialized.
        /// </summary>
        public bool IsBackendReady => IsBackendAvailable && _orchestrator != null;

        public GenerationService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.Info("GenerationService initialized (Backend mode)");
        }

        /// <summary>
        /// Initialize backend mode with authentication.
        /// </summary>
        /// <param name="jwtToken">JWT token for backend authentication</param>
        /// <param name="environment">Backend environment</param>
        public async Task<bool> InitializeBackendAsync(string jwtToken, DeploymentEnvironment environment)
        {
            try
            {
                environment ??= DeploymentEnvironment.Production;
                _environment = environment;
                _logger.Info($"[GenerationService] Initializing backend ({environment})...");

                if (_backendClient == null)
                {
                    _backendClient = new ContentClient(_logger, environment);
                }

                bool success = await _backendClient.AuthenticateAsync(jwtToken);
                if (!success)
                {
                    _logger.Warning("[GenerationService] Backend authentication failed");
                    return false;
                }

                _orchestrator = new AssetOrchestrator(_backendClient, _logger);
                _logger.Info("[GenerationService] ✓ Backend initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"[GenerationService] Failed to initialize backend: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the backend client instance for direct access to backend methods.
        /// </summary>
        public ContentClient GetBackendClient()
        {
            return _backendClient;
        }

        /// <summary>
        /// Disconnect from backend.
        /// </summary>
        public async Task DisconnectBackendAsync()
        {
            try
            {
                _logger.Info("[GenerationService] Disconnecting from backend...");

                if (_backendClient != null)
                {
                    await _backendClient.DisconnectAsync();
                    _backendClient = null;
                }

                _orchestrator = null;
                _logger.Info("[GenerationService] ✓ Disconnected from backend");
            }
            catch (Exception ex)
            {
                _logger.Error($"[GenerationService] Failed to disconnect from backend: {ex.Message}");
                throw;
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Generate a 3D model asset from job parameters.
        /// </summary>
        /// <param name="job">Job containing generation parameters</param>
        /// <returns>Result with paths to generated files</returns>
        public async Task<GenerationResult> GenerateAssetAsync(Job job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            if (_orchestrator == null)
            {
                return new GenerationResult
                {
                    Success = false,
                    ErrorMessage = "Backend not initialized. Please authenticate first."
                };
            }

            _logger.Info($"[GenerationService] Generating asset via backend: {job.AssetName.Value}");

            try
            {
                // Backend orchestrator performs multi-step flow (images → approval → model/textures).
                OnProgressUpdated?.Invoke(job.Id, BackendSubmitProgress, SubmitProgressMessage);

                using var submissionCancellation = new CancellationTokenSource();
                var orchestratorTask = _orchestrator.GenerateAssetAsync(job, submissionCancellation.Token);
                DateTime submitPhaseStartedAt = DateTime.UtcNow;

                // Cap the "Submitting backend job..." state so disconnected jobs do not hang indefinitely.
                while (!orchestratorTask.IsCompleted && !HasMovedPastSubmitPhase(job))
                {
                    Task completed = await Task.WhenAny(orchestratorTask, Task.Delay(SubmitWatchdogTickMs));
                    if (completed == orchestratorTask)
                    {
                        break;
                    }

                    if ((DateTime.UtcNow - submitPhaseStartedAt).TotalMilliseconds < BackendSubmitTimeoutMs)
                    {
                        continue;
                    }

                    _logger.Warning($"[GenerationService] Backend submission timed out after {BackendSubmitTimeoutMs}ms for job {job.Id}.");
                    string timeoutErrorMessage = $"Submitting backend job timed out after {BackendSubmitTimeoutMs / 1000}s. Please reconnect and retry.";
                    submissionCancellation.Cancel();

                    // Allow cooperative cancellation a brief moment to settle.
                    Task cancelledCompletion = await Task.WhenAny(orchestratorTask, Task.Delay(SubmitCancellationGraceMs));
                    if (cancelledCompletion == orchestratorTask)
                    {
                        // The task may have completed by throwing (e.g. OperationCanceledException).
                        // Swallow it here — we're already reporting a timeout to the caller.
                        try { await orchestratorTask; } catch { }
                        return new GenerationResult { Success = false, ErrorMessage = timeoutErrorMessage };
                    }

                    _ = orchestratorTask.ContinueWith(
                        t => _logger.Warning($"[GenerationService] Backend submission task faulted after timeout for job {job.Id}: {t.Exception?.GetBaseException().Message}"),
                        TaskContinuationOptions.OnlyOnFaulted);

                    return new GenerationResult
                    {
                        Success = false,
                        ErrorMessage = timeoutErrorMessage
                    };
                }

                var result = await orchestratorTask;
                OnProgressUpdated?.Invoke(job.Id, result.Success ? ProgressComplete : ProgressFailed, result.Success ? "Backend job completed" : "Backend job failed");
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"[GenerationService] Backend generation failed: {ex.Message}");
                return new GenerationResult
                {
                    Success = false,
                    ErrorMessage = $"Backend generation error: {ex.Message}"
                };
            }
        }

        private static bool HasMovedPastSubmitPhase(Job job)
        {
            if (job == null)
            {
                return false;
            }

            if (string.Equals(job.CurrentStep, BackendSubmittedStep, StringComparison.Ordinal))
            {
                return true;
            }

            if (job.Progress >= BackendSubmitAcceptedProgress)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Validate that backend is authenticated and ready.
        /// </summary>
        /// <param name="jobType">Type of job to validate for</param>
        /// <returns>True if backend is authenticated</returns>
        public bool ValidateApiKeys(JobType jobType)
        {
            // Backend mode requires authentication.
            return IsBackendAvailable;
        }

        /// <summary>
        /// Get estimated cost for a generation job.
        /// </summary>
        /// <param name="jobType">Type of job</param>
        /// <returns>Estimated cost in USD (0 for backend mode)</returns>
        public float GetEstimatedCost(JobType jobType)
        {
            // Backend cost is not currently estimated client-side.
            return EstimatedCostBackend;
        }

        public void Dispose()
        {
            try
            {
                _backendClient?.Dispose();
            }
            catch
            {
                // Ignore dispose errors.
            }
        }
    }

    /// <summary>
    /// Result of asset generation operation
    /// </summary>
    internal class GenerationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        // Generated file paths
        public string ModelPath { get; set; }
        public string AlbedoTexturePath { get; set; }
        public string NormalTexturePath { get; set; }
        public string MetallicTexturePath { get; set; }
        public string RoughnessTexturePath { get; set; }
        public string MaterialPath { get; set; }

        // Approval workflow
        public bool RequiresApproval { get; set; }

        // Metadata
        public float TotalCost { get; set; }
        public long DurationMs { get; set; }
        public string ReferenceImagePath { get; set; }
    }
}
