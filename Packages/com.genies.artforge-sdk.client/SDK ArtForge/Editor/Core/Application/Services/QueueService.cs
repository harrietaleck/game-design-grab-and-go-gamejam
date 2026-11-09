using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Helpers;
using Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.API;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Helpers;
using GAFAssetImporter = Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Import.AssetImporter;

namespace Genies.Sdk.ArtForge.Editor.Core.Application.Services
{
    /// <summary>
    /// Service for managing the job processing queue.
    /// Handles job lifecycle, parallel processing, and persistence.
    /// </summary>
    internal class QueueService
    {
        private const int DefaultMaxConcurrentJobs = 3;
        private const bool DefaultAutoStartQueue = true;
        private const bool DefaultRetryFailedJobs = false;
        private const int DefaultMaxRetries = 2;
        private const int StaleGapSubmissionHours = 1;
        private const int MinConcurrentJobs = 1;
        private const int MaxConcurrentJobs = 5;
        private const int MinRetries = 1;
        private const int MaxRetries = 3;
        private const float AwaitingApprovalProgress = 0.3f;
        private const float ImportingAssetsProgress = 0.95f;
        private const float CreatingMaterialProgress = 0.97f;
        private const string AwaitingLoginMessage = "Login to continue";
        private const string ReconnectingBackendMessage = "Reconnecting backend...";
        private const string QueuedMessage = "Queued";
        private const string BackendNotReadyErrorPrefix = "Backend not initialized";
        private const int BackendReconnectAttemptTimeoutMs = 5000;
        private const int BackendReconnectAttemptCooldownMs = 3000;
        private const int PrefBoolTrue = 1;
        private const int PrefBoolFalse = 0;

        private readonly ILogger _logger;
        private readonly GenerationService _generationService;
        private readonly GAFAssetImporter _assetImporter;
        private readonly List<Job> _queuedJobs = new List<Job>();
        private readonly List<Job> _processingJobs = new List<Job>();
        private readonly List<Job> _completedJobs = new List<Job>();

        private bool _isPaused = false;
        private bool _isInitialized = false;
        private bool _hasAttemptedFailed3DRecovery = false;
        private bool _isRecoveringFailed3DJobs = false;
        private bool _hasAttemptedGapRecovery = false;
        private bool _isRecoveringGapJobs = false;
        private bool _isEnsuringBackendConnection = false;
        private DateTime _lastBackendReconnectAttemptUtc = DateTime.MinValue;

        // ------------------------------------------------------------
        // 2D Preview telemetry signals (Observer subscribes to these)
        // ------------------------------------------------------------
        public event Action<string, Dictionary<string, object>> Preview2DJobStarted;
        public event Action<string, Dictionary<string, object>> Preview2DJobSucceeded;
        public event Action<string, Dictionary<string, object>> Preview2DJobFailed;

        // Track start times for initial 2D preview pipeline (keyed by job id string)
        private readonly Dictionary<string, double> _preview2DStartedAt = new Dictionary<string, double>();

        // ------------------------------------------------------------
        // 3D Gen telemetry signals (Observer subscribes to these)
        // ------------------------------------------------------------
        public event Action<string, Dictionary<string, object>> Model3DJobStarted;
        public event Action<string, Dictionary<string, object>> Model3DJobSucceeded;
        public event Action<string, Dictionary<string, object>> Model3DJobFailed;

        // Track start times for 3D generation pipeline (keyed by job id string)
        private readonly Dictionary<string, double> _model3DStartedAt = new Dictionary<string, double>();

        // ------------------------------------------------------------
        // History telemetry signals (Observer subscribes to these)
        // ------------------------------------------------------------
        public event Action<string, Dictionary<string, object>> HistoryRemoveAll;
        public event Action<string, Dictionary<string, object>> HistoryJobRemoved;

        // ------------------------------------------------------------
        // Errors / API telemetry signals (Observer subscribes to these)
        // ------------------------------------------------------------
        public event Action<string, Dictionary<string, object>> ApiRequestFailed;

        public bool IsPaused => _isPaused;
        public int MaxRetryAttempts => _maxRetries;

        // Settings (will be loaded from EditorPrefs)
        private int _maxConcurrentJobs = DefaultMaxConcurrentJobs;
        private bool _autoStartQueue = DefaultAutoStartQueue;
        private bool _retryFailedJobs = DefaultRetryFailedJobs;
        private int _maxRetries = DefaultMaxRetries;

        // Events
        public event Action<Job> OnJobQueued;
        public event Action<Job> OnJobStarted;
        public event Action<Job> OnJobCompleted;
        public event Action<Job> OnJobFailed;
        public event Action<Job> OnJobCancelled;
        public event Action<Job> OnJobStateChanged;
        public event Action OnQueueChanged;

        public QueueService(ILogger logger, GenerationService generationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));
            _assetImporter = new GAFAssetImporter(_logger);
        }

        /// <summary>
        /// Initialize the queue service and start monitoring
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            LoadSettings();
            LoadPersistedJobs();

            // Register for Unity editor updates
            EditorApplication.update += Update;

            _isInitialized = true;
            _logger.Info("Queue service initialized");
        }

        /// <summary>
        /// Add a job to the queue
        /// </summary>
        public void EnqueueJob(Job job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            _queuedJobs.Add(job);
            job.SetState(JobState.Queued);

            _logger.Info($"Job queued: {job.AssetName} (ID: {job.Id})");

            OnJobQueued?.Invoke(job);
            OnQueueChanged?.Invoke();
            SavePersistedJobs();

            // Auto-start if enabled
            if (_autoStartQueue && !_isPaused)
            {
                Update();
            }
        }

        /// <summary>
        /// Cancel a job
        /// </summary>
        public void CancelJob(JobId jobId)
        {
            var job = FindJob(jobId);
            if (job == null)
            {
                _logger.Warning($"Cannot cancel job - not found: {jobId}");
                return;
            }

            if (job.IsTerminal())
            {
                _logger.Warning($"Cannot cancel job - already in terminal state: {job.State}");
                return;
            }

            job.SetState(JobState.Cancelled);

            // Remove from processing/queued lists
            _processingJobs.Remove(job);
            _queuedJobs.Remove(job);
            _completedJobs.Add(job);

            _logger.Info($"Job cancelled: {job.AssetName}");

            OnJobCancelled?.Invoke(job);
            OnQueueChanged?.Invoke();
            SavePersistedJobs();
        }

        /// <summary>
        /// Retry a failed job
        /// </summary>
        public void RetryJob(JobId jobId)
        {
            var job = _completedJobs.FirstOrDefault(j => j.Id == jobId && j.State == JobState.Failed);
            if (job == null)
            {
                _logger.Warning($"Cannot retry job - not found or not failed: {jobId}");
                return;
            }

            if (!job.CanRetry(_maxRetries))
            {
                _logger.Warning($"Cannot retry job - max retries reached: {job.RetryCount}/{_maxRetries}");
                return;
            }

            job.ResetForRetry();
            _completedJobs.Remove(job);
            _queuedJobs.Add(job);

            _logger.Info($"Job queued for retry: {job.AssetName} (attempt {job.RetryCount + 1})");

            OnQueueChanged?.Invoke();
            SavePersistedJobs();
        }

        /// <summary>
        /// Approve a job that is awaiting image confirmation
        /// </summary>
        public void ApproveJob(JobId jobId)
        {
            var job = GetAllJobs().FirstOrDefault(j => j.Id == jobId);
            if (job == null)
            {
                _logger.Warning($"Cannot approve job - not found: {jobId}");
                return;
            }

            if (job.State != JobState.AwaitingImageConfirm)
            {
                _logger.Warning($"Cannot approve job - not awaiting confirmation: {jobId} (state: {job.State})");
                return;
            }

            _logger.Info($"Job approved: {job.AssetName}");

            // Mark as confirmed and either complete immediately (Decor 2D) or re-queue for continued processing.
            job.ImageConfirmed = true;

            if (job.IsDecor2DAsset)
            {
                string referenceImagePath = BrandingConstants.AssetPaths.GetReferenceImagePath(job.AssetName.Value);
                job.SetGeneratedAssetPath(referenceImagePath);
                job.UpdateProgress(1f, "Completed");
                job.SetState(JobState.Completed);

                _processingJobs.Remove(job);
                _queuedJobs.Remove(job);

                OnJobStateChanged?.Invoke(job);
                CompleteJob(job);
                return;
            }

            job.SetState(JobState.Queued);

            // Move from processing to queued list if needed
            if (_processingJobs.Contains(job))
            {
                _processingJobs.Remove(job);
                _queuedJobs.Add(job);
            }

            OnJobStateChanged?.Invoke(job);
            OnQueueChanged?.Invoke();
            SavePersistedJobs();
        }

        /// <summary>
        /// Save job state changes (called after modifying a job externally)
        /// </summary>
        public void SaveJobState(Job job)
        {
            if (job == null)
            {
                return;
            }

            OnJobStateChanged?.Invoke(job);
            OnQueueChanged?.Invoke();
            SavePersistedJobs();
        }

        /// <summary>
        /// Pause queue processing
        /// </summary>
        public void PauseQueue()
        {
            _isPaused = true;
            _logger.Info("Queue paused");
            OnQueueChanged?.Invoke();
        }

        /// <summary>
        /// Resume queue processing
        /// </summary>
        public void ResumeQueue()
        {
            _isPaused = false;
            _logger.Info("Queue resumed");
            OnQueueChanged?.Invoke();

            // Resume processing
            Update();
        }

        /// <summary>
        /// Clear all completed jobs from queue display (but keep in history)
        /// </summary>
        public void ClearCompleted()
        {
            int count = 0;
            foreach (var job in _completedJobs)
            {
                if (job.IsVisibleInQueue)
                {
                    job.IsVisibleInQueue = false;
                    count++;
                }
            }
            _logger.Info($"Hid {count} completed jobs from queue (still available in History)");
            OnQueueChanged?.Invoke();
            SavePersistedJobs();
        }

        /// <summary>
        /// Remove a completed/failed job from persisted history.
        /// </summary>
        public bool RemoveCompletedJob(JobId jobId)
        {
            var job = _completedJobs.FirstOrDefault(j => j.Id == jobId);
            if (job == null)
            {
                _logger.Warning($"Cannot remove completed job - not found: {jobId}");
                return false;
            }

            _completedJobs.Remove(job);
            _logger.Info($"Removed completed job from history: {job.AssetName} (ID: {job.Id})");

            HistoryJobRemoved?.Invoke(
                "history_job_removed",
                new Dictionary<string, object>
                {
                    { "job_id", job.Id?.ToString() ?? "" }
                }
            );

            OnQueueChanged?.Invoke();
            SavePersistedJobs();
            return true;
        }

        /// <summary>
        /// Remove all completed/failed jobs from persisted history.
        /// </summary>
        public int RemoveAllCompletedJobs()
        {
            int removedCount = _completedJobs.Count;
            if (removedCount == 0)
            {
                return 0;
            }

            _completedJobs.Clear();
            _logger.Info($"Removed {removedCount} completed jobs from history");

            HistoryRemoveAll?.Invoke(
                "history_remove_all",
                new Dictionary<string, object>()
            );

            OnQueueChanged?.Invoke();
            SavePersistedJobs();
            return removedCount;
        }

        /// <summary>
        /// Get all jobs (for history view - includes hidden jobs)
        /// </summary>
        public List<Job> GetAllJobs()
        {
            var allJobs = new List<Job>();
            allJobs.AddRange(_queuedJobs);
            allJobs.AddRange(_processingJobs);
            allJobs.AddRange(_completedJobs);
            return allJobs;
        }

        /// <summary>
        /// Get visible jobs for queue panel (excludes jobs hidden by "Clear Done")
        /// </summary>
        public List<Job> GetVisibleJobs()
        {
            var visibleJobs = new List<Job>();
            visibleJobs.AddRange(_queuedJobs);
            visibleJobs.AddRange(_processingJobs);
            visibleJobs.AddRange(_completedJobs.Where(j => j.IsVisibleInQueue));
            return visibleJobs;
        }

        /// <summary>
        /// Get jobs by state
        /// </summary>
        public List<Job> GetJobsByState(JobState state)
        {
            return GetAllJobs().Where(j => j.State == state).ToList();
        }

        /// <summary>
        /// Get queue statistics
        /// </summary>
        public (int queued, int processing, int completed, int failed) GetStats()
        {
            return (
                _queuedJobs.Count,
                _processingJobs.Count,
                _completedJobs.Count(j => j.State == JobState.Completed),
                _completedJobs.Count(j => j.State == JobState.Failed)
            );
        }

        /// <summary>
        /// Update queue settings
        /// </summary>
        public void UpdateSettings(int maxConcurrentJobs, bool autoStartQueue, bool retryFailedJobs, int maxRetries)
        {
            _maxConcurrentJobs = Math.Clamp(maxConcurrentJobs, MinConcurrentJobs, MaxConcurrentJobs);
            _autoStartQueue = autoStartQueue;
            _retryFailedJobs = retryFailedJobs;
            _maxRetries = Math.Clamp(maxRetries, MinRetries, MaxRetries);

            SaveSettings();
            _logger.Info("Queue settings updated");
        }

        private void Update()
        {
            if (!_isInitialized || _isPaused)
            {
                return;
            }

            // Do not process/reconcile jobs on cold start until backend auth exists.
            bool backendReady = _generationService.IsBackendReady;
            bool backendConnected = false;
            if (!backendReady)
            {
                MarkQueuedJobsAsAwaitingLogin();
            }
            else
            {
                backendConnected = IsRealtimeBackendConnected();
                if (!backendConnected)
                {
                    MarkQueuedJobsAsReconnectingBackend();
                    TryEnsureBackendConnectionForQueue();
                }
                else
                {
                    ClearQueuedBackendSubstates();

                    // Start new jobs if we have capacity
                    while (_processingJobs.Count < _maxConcurrentJobs && _queuedJobs.Any())
                    {
                        var nextJob = _queuedJobs.First();
                        StartJob(nextJob);
                    }
                }
            }

            // Check for completed processing jobs
            foreach (var job in _processingJobs.ToList())
            {
                if (job.IsTerminal())
                {
                    CompleteJob(job);
                }
            }

            if (!backendReady || !backendConnected)
            {
                return;
            }

            // Auto-retry failed jobs if enabled
            if (_retryFailedJobs)
            {
                foreach (var job in _completedJobs.Where(j => j.State == JobState.Failed && j.CanRetry(_maxRetries)).ToList())
                {
                    RetryJob(job.Id);
                }
            }

            // One-time recovery pass after load: reconcile failed client jobs with backend truth.
            TryRecoverFailed3DJobsFromBackend();

            // One-time recovery pass after load: reconcile interrupted GAP submissions.
            TryRecoverGapJobsFromBackend();
        }

        private void MarkQueuedJobsAsAwaitingLogin()
        {
            SetQueuedJobsSubstate(JobQueueSubstate.AwaitingLogin, AwaitingLoginMessage);
        }

        private void MarkQueuedJobsAsReconnectingBackend()
        {
            SetQueuedJobsSubstate(JobQueueSubstate.ReconnectingBackend, ReconnectingBackendMessage);
        }

        private void ClearQueuedBackendSubstates()
        {
            bool changed = false;
            foreach (var job in _queuedJobs)
            {
                if (job.QueueSubstate == JobQueueSubstate.None)
                {
                    continue;
                }

                job.SetQueueSubstate(JobQueueSubstate.None, QueuedMessage);
                changed = true;
            }

            if (changed)
            {
                OnQueueChanged?.Invoke();
                SavePersistedJobs();
            }
        }

        private void SetQueuedJobsSubstate(JobQueueSubstate substate, string message)
        {
            bool changed = false;
            foreach (var job in _queuedJobs)
            {
                if (job.QueueSubstate == substate && job.ProgressMessage == message)
                {
                    continue;
                }

                job.SetQueueSubstate(substate, message);
                changed = true;
            }

            if (changed)
            {
                OnQueueChanged?.Invoke();
                SavePersistedJobs();
            }
        }

        private bool IsRealtimeBackendConnected()
        {
            var backendClient = _generationService.GetBackendClient();
            return backendClient != null && backendClient.IsConnected;
        }

        private void TryEnsureBackendConnectionForQueue()
        {
            if (_isEnsuringBackendConnection || _queuedJobs.Count == 0)
            {
                return;
            }

            if ((DateTime.UtcNow - _lastBackendReconnectAttemptUtc).TotalMilliseconds < BackendReconnectAttemptCooldownMs)
            {
                return;
            }

            var backendClient = _generationService.GetBackendClient();
            if (backendClient == null)
            {
                return;
            }

            _isEnsuringBackendConnection = true;
            _ = EnsureBackendConnectionForQueueAsync(backendClient);
        }

        private async Task EnsureBackendConnectionForQueueAsync(ContentClient backendClient)
        {
            try
            {
                using var reconnectTimeout = new CancellationTokenSource(BackendReconnectAttemptTimeoutMs);
                bool connected = await backendClient.EnsureConnectedAsync(reconnectTimeout.Token);
                if (connected && backendClient.IsConnected)
                {
                    _logger.Info("[QueueService] Realtime backend reconnected; queued jobs can resume.");
                }
                else
                {
                    _logger.Warning("[QueueService] Realtime backend reconnect attempt did not establish a connection.");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Warning($"[QueueService] Realtime backend reconnect attempt timed out after {BackendReconnectAttemptTimeoutMs}ms.");
            }
            catch (Exception ex)
            {
                _logger.Warning($"[QueueService] Realtime backend reconnect attempt failed: {ex.Message}");
            }
            finally
            {
                _isEnsuringBackendConnection = false;
                _lastBackendReconnectAttemptUtc = DateTime.UtcNow;
            }
        }

        private void TryRecoverFailed3DJobsFromBackend()
        {
            if (_hasAttemptedFailed3DRecovery || _isRecoveringFailed3DJobs)
            {
                return;
            }

            var candidates = _completedJobs
                .Where(j => j != null && j.CanRecoverFailed3DGeneration)
                .ToList();

            if (candidates.Count == 0)
            {
                _hasAttemptedFailed3DRecovery = true;
                return;
            }

            var contentClient = GeniesArtForgeService.ContentClient;
            if (contentClient == null || !contentClient.IsAuthenticated)
            {
                // Wait until backend is authenticated, then retry in a future Update() tick.
                return;
            }

            _ = RecoverFailed3DJobsFromBackendAsync(candidates, contentClient);
        }

        private async Task RecoverFailed3DJobsFromBackendAsync(List<Job> candidates, ContentClient contentClient)
        {
            _isRecoveringFailed3DJobs = true;

            try
            {
                int recoveredCount = 0;

                foreach (var job in candidates)
                {
                    // Job may have changed while we were waiting.
                    if (job == null || !job.CanRecoverFailed3DGeneration)
                    {
                        continue;
                    }

                    double startedAt = EditorApplication.timeSinceStartup;

                    try
                    {
                        var status = await contentClient.GetJobStatusAsync(job.BackendModelGenerationJobId);
                        if (status == null)
                        {
                            continue;
                        }

                        bool backendCanResume = status.IsCompleted() || status.IsInProgress() || status.IsTimedOut();
                        if (!backendCanResume)
                        {
                            continue;
                        }

                        if (_completedJobs.Contains(job))
                        {
                            job.ResetForRecovery();
                            _completedJobs.Remove(job);
                            _queuedJobs.Add(job);
                            recoveredCount++;

                            _logger.Info(
                                $"Recovered failed 3D job '{job.AssetName.Value}' from backend status '{status.status}' (jobId: {job.BackendModelGenerationJobId})");
                        }
                    }
                    catch (Exception ex)
                    {
                        long latencyMs = (long)Math.Max(0d, (EditorApplication.timeSinceStartup - startedAt) * 1000d);

                        ApiRequestFailed?.Invoke(
                            "api_request_failed",
                            new Dictionary<string, object>
                            {
                                { "endpoint_name", "GetJobStatusAsync" },
                                { "http_status", "unknown" },
                                { "error_code", ex.GetType().Name },
                                { "latency_ms", latencyMs }
                            }
                        );

                        _logger.Warning(
                            $"Failed to reconcile backend status for job '{job.AssetName.Value}' ({job.BackendModelGenerationJobId}): {ex.Message}");
                    }
                }

                if (recoveredCount > 0)
                {
                    OnQueueChanged?.Invoke();
                    SavePersistedJobs();
                }
            }
            finally
            {
                _isRecoveringFailed3DJobs = false;
                _hasAttemptedFailed3DRecovery = true;
            }
        }

        private void TryRecoverGapJobsFromBackend()
        {
            if (_hasAttemptedGapRecovery || _isRecoveringGapJobs)
            {
                return;
            }

            var candidates = _completedJobs
                .Where(CanRecoverGapJob)
                .ToList();

            if (candidates.Count == 0)
            {
                _hasAttemptedGapRecovery = true;
                return;
            }

            var contentClient = GeniesArtForgeService.ContentClient;
            if (contentClient == null || !contentClient.IsAuthenticated)
            {
                // Wait until backend is authenticated, then retry in a future Update() tick.
                return;
            }

            _ = RecoverGapJobsFromBackendAsync(candidates, contentClient);
        }

        private async Task RecoverGapJobsFromBackendAsync(List<Job> candidates, ContentClient contentClient)
        {
            _isRecoveringGapJobs = true;

            try
            {
                bool changed = false;

                foreach (var job in candidates)
                {
                    if (!CanRecoverGapJob(job))
                    {
                        continue;
                    }

                    // We can't recover server status without a GAP job ID; allow user to resubmit.
                    if (string.IsNullOrEmpty(job.GAPJobId))
                    {
                        job.GAPStatus = GapStatus.NotSubmitted;
                        job.UpdateProgress(1f, "Completed");
                        changed = true;
                        _logger.Info($"Reset GAP submission state for '{job.AssetName.Value}' (no GAP job ID persisted).");
                        continue;
                    }

                    double startedAt = EditorApplication.timeSinceStartup;

                    try
                    {
                        var status = await contentClient.GetJobStatusAsync(job.GAPJobId);
                        if (status == null)
                        {
                            continue;
                        }

                        if (status.IsCompleted())
                        {
                            job.GAPStatus = GapStatus.Completed;
                            job.UpdateProgress(1f, "Completed", status.step);
                            changed = true;
                            _logger.Info($"Recovered GAP job '{job.AssetName.Value}' as completed (jobId: {job.GAPJobId}).");
                            continue;
                        }

                        if (status.IsFailed() || status.IsAborted())
                        {
                            job.GAPStatus = GapStatus.Failed;
                            job.UpdateProgress(1f, "Failed", status.step);
                            changed = true;
                            _logger.Info($"Recovered GAP job '{job.AssetName.Value}' as failed (jobId: {job.GAPJobId}).");
                            continue;
                        }

                        // Still running/pending/timed_out: resume live tracking.
                        job.GAPStatus = status.status;
                        string stepMessage = JobStepFormatter.GetProgressMessage(status.step, status.progress);
                        job.UpdateProgress(status.progress, stepMessage, status.step);
                        changed = true;

                        _logger.Info($"Resuming GAP tracking for '{job.AssetName.Value}' (jobId: {job.GAPJobId}, status: {status.status}).");

                        _ = GapSubmissionTracker.TrackAsync(
                            job,
                            contentClient,
                            SaveJobState,
                            () => OnQueueChanged?.Invoke(),
                            message => _logger.Error($"Failed to recover GAP tracking for '{job.AssetName.Value}': {message}"));
                    }
                    catch (Exception ex)
                    {
                        long latencyMs = (long)Math.Max(0d, (EditorApplication.timeSinceStartup - startedAt) * 1000d);

                        ApiRequestFailed?.Invoke(
                            "api_request_failed",
                            new Dictionary<string, object>
                            {
                                { "endpoint_name", "GetJobStatusAsync" },
                                { "http_status", "unknown" },
                                { "error_code", ex.GetType().Name },
                                { "latency_ms", latencyMs }
                            }
                        );

                        _logger.Warning($"Failed to reconcile GAP status for '{job.AssetName.Value}' ({job.GAPJobId}): {ex.Message}");
                    }
                }

                if (changed)
                {
                    OnQueueChanged?.Invoke();
                    SavePersistedJobs();
                }
            }
            finally
            {
                _isRecoveringGapJobs = false;
                _hasAttemptedGapRecovery = true;
            }
        }

        private static bool CanRecoverGapJob(Job job)
        {
            return job != null &&
                   job.State == JobState.Completed &&
                   GapSubmissionTracker.IsInProgress(job);
        }

        private void StartJob(Job job)
        {
            _queuedJobs.Remove(job);
            _processingJobs.Add(job);

            job.SetState(JobState.Processing);

            _logger.Info($"Job started: {job.AssetName}");

            OnJobStarted?.Invoke(job);
            OnQueueChanged?.Invoke();
            SavePersistedJobs();

            // Process job asynchronously
            ProcessJobAsync(job);
        }

        private async void ProcessJobAsync(Job job)
        {
            // Determine which pipeline(s) apply for this run:
            // - 2D preview pipeline: RequiresApproval and not yet confirmed
            // - 3D pipeline: everything else (either does not require approval OR approval already confirmed)
            bool isPreview2DFlow = job != null && job.RequiresApprovalConfirmation;
            bool isModel3DFlow = job != null && (!job.RequiresApproval || job.ImageConfirmed);

            string jobIdStr = job?.Id?.ToString() ?? "";

            if (isPreview2DFlow && !string.IsNullOrEmpty(jobIdStr))
            {
                int attempt = job.CurrentIteration;
                _preview2DStartedAt[jobIdStr] = EditorApplication.timeSinceStartup;

                Preview2DJobStarted?.Invoke(
                    "preview2d_job_started",
                    new Dictionary<string, object>
                    {
                        { "job_id", jobIdStr },
                        { "attempt", attempt },
                    }
                );
            }

            if (isModel3DFlow && !string.IsNullOrEmpty(jobIdStr))
            {
                int attempt = Math.Max(0, job.RetryCount);
                _model3DStartedAt[jobIdStr] = EditorApplication.timeSinceStartup;

                Model3DJobStarted?.Invoke(
                    "model3d_job_started",
                    new Dictionary<string, object>
                    {
                        { "job_id", jobIdStr },
                        { "attempt", attempt },
                        
                        // asset_category / wearable_type not available here without Job fields; left out intentionally.
                    }
                );
            }

            try
            {
                // Wire up progress updates from GenerationService
                Action<JobId, float, string> progressHandler = (jobId, progress, message) =>
                {
                    if (jobId == job.Id)
                    {
                        job.UpdateProgress(progress, message);

                        // Check for cancellation/pause during progress updates
                        if (job.IsTerminal())
                        {
                            _logger.Info($"Job terminated during generation: {job.AssetName}");
                        }
                    }
                };

                _generationService.OnProgressUpdated += progressHandler;

                try
                {
                    // Call real API generation
                    var result = await _generationService.GenerateAssetAsync(job);

                    // Check if job was cancelled during generation
                    if (job.IsTerminal())
                    {
                        _logger.Info($"Job was terminated during processing: {job.AssetName}");
                        return;
                    }

                    // Handle result
                    if (result.Success)
                    {
                        // Check if job requires approval before continuing
                        if (result.RequiresApproval && job.RequiresApprovalConfirmation)
                        {
                            _logger.Info($"Job requires approval - setting state to AwaitingImageConfirm: {job.AssetName}");

                            _logger.Info("Backend workflow - images already in History folder");

                            // Copy the first (selected) version to the main reference location
                            // so the ApprovalReviewDialog can display it
                            var selectedVersion = job.GetSelectedVersion();
                            if (selectedVersion != null && File.Exists(Path.Combine(UnityEngine.Application.dataPath, "..", selectedVersion.ImagePath)))
                            {
                                string currentRefPath = BrandingConstants.AssetPaths.GetReferenceImagePath(job.AssetName.Value);
                                string fullCurrentRefPath = Path.Combine(UnityEngine.Application.dataPath, "..", currentRefPath);
                                string fullSelectedPath = Path.Combine(UnityEngine.Application.dataPath, "..", selectedVersion.ImagePath);

                                Directory.CreateDirectory(Path.GetDirectoryName(fullCurrentRefPath));
                                File.Copy(fullSelectedPath, fullCurrentRefPath, true);
                                _logger.Info($"Copied selected backend image to current reference: {currentRefPath}");
                            }

                            // Set job to awaiting approval state
                            job.SetState(JobState.AwaitingImageConfirm);
                            job.UpdateProgress(AwaitingApprovalProgress, "Awaiting user approval");

                            // Save job state after adding image version
                            // This ensures CurrentIteration is persisted for next regeneration
                            OnJobStateChanged?.Invoke(job);
                            OnQueueChanged?.Invoke();
                            SavePersistedJobs();

                            _logger.Info($"Job persisted with {job.ImageVersions.Count} versions, CurrentIteration = {job.CurrentIteration}");

                            // Emit succeeded for initial preview pipeline if we tracked a start.
                            if (isPreview2DFlow && !string.IsNullOrEmpty(jobIdStr))
                            {
                                double startedAt;
                                long latencyMs = 0;

                                if (_preview2DStartedAt.TryGetValue(jobIdStr, out startedAt))
                                {
                                    latencyMs = (long)Math.Max(0d, (EditorApplication.timeSinceStartup - startedAt) * 1000d);
                                    _preview2DStartedAt.Remove(jobIdStr);
                                }

                                int attempt = job.CurrentIteration;

                                Preview2DJobSucceeded?.Invoke(
                                    "preview2d_job_succeeded",
                                    new Dictionary<string, object>
                                    {
                                        { "job_id", jobIdStr },
                                        { "attempt", attempt },
                                        { "latency_ms", latencyMs },
                                        { "image_count", job.ImageVersions?.Count ?? 0 },
                                    }
                                );

                                EditorSfx.PlayPreviewReady(jobIdStr);
                            }

                            // Show approval dialog
                            ShowApprovalDialog(job);

                            return; // Stop processing until user approves
                        }

                        // Import generated assets into Unity
                        job.UpdateProgress(ImportingAssetsProgress, "Importing assets into Unity...");

                        var texturePaths = new List<string>();
                        if (!string.IsNullOrEmpty(result.AlbedoTexturePath))
                        {
                            texturePaths.Add(result.AlbedoTexturePath);
                        }

                        if (!string.IsNullOrEmpty(result.NormalTexturePath))
                        {
                            texturePaths.Add(result.NormalTexturePath);
                        }

                        if (!string.IsNullOrEmpty(result.MetallicTexturePath))
                        {
                            texturePaths.Add(result.MetallicTexturePath);
                        }

                        if (!string.IsNullOrEmpty(result.RoughnessTexturePath))
                        {
                            texturePaths.Add(result.RoughnessTexturePath);
                        }

                        // Use the same iteration number as the current image version
                        // This ensures textures are versioned consistently with images
                        int? textureIteration = null;
                        if (job.CurrentIteration >= 0)
                        {
                            textureIteration = job.CurrentIteration;
                            _logger.Info($"Using iteration {textureIteration} for texture versioning (matches current image iteration)");
                        }
                        else
                        {
                            _logger.Info("No iteration number available - textures will be saved without versioning");
                        }

                        var importedAsset = await _assetImporter.ImportGeneratedAssetAsync(
                            job.AssetName,
                            result.ModelPath,
                            texturePaths.ToArray(),
                            result.ReferenceImagePath,
                            textureIteration, // Pass iteration number for consistent versioning with images
                            job.QualitySettings,
                            job.AssetTypeId
                        );

                        // Create material and prefab (if we have a 3D model)
                        if (!string.IsNullOrEmpty(result.ModelPath))
                        {
                            job.UpdateProgress(CreatingMaterialProgress, "Creating material and prefab...");
                            _logger.Info($"Creating material and prefab for: {job.AssetName}");

                            bool pivotAtBase = !string.Equals(job.Pivot, "Center", StringComparison.OrdinalIgnoreCase);
                            bool generateCollider = job.GenerateCollider;

                            var assetProcessor = new Infrastructure.Processing.UnityAssetProcessor(_logger);
                            assetProcessor.CreateMaterialAndPrefab(job.AssetName.Value, pivotAtBase, generateCollider);

                            // Final refresh to ensure prefab and material are visible
                            UnityEditor.AssetDatabase.Refresh();

                            // Update path to point to the prefab (the ready-to-use asset)
                            string prefabPath = BrandingConstants.AssetPaths.GetPrefabPath(job.AssetName.Value);
                            job.SetGeneratedAssetPath(prefabPath);
                            _logger.Info($"Created prefab: {prefabPath}");
                        }
                        else if (importedAsset != null)
                        {
                            // Update job with generated asset path (texture or reference image)
                            job.SetGeneratedAssetPath(importedAsset.FilePath);
                        }

                        job.SetState(JobState.Completed);
                        _logger.Info($"Job completed: {job.AssetName}");

                        // Emit succeeded for 3D pipeline if we tracked a start.
                        if (isModel3DFlow && !string.IsNullOrEmpty(jobIdStr))
                        {
                            double startedAt;
                            long latencyMs = 0;

                            if (_model3DStartedAt.TryGetValue(jobIdStr, out startedAt))
                            {
                                latencyMs = (long)Math.Max(0d, (EditorApplication.timeSinceStartup - startedAt) * 1000d);
                                _model3DStartedAt.Remove(jobIdStr);
                            }

                            int attempt = Math.Max(0, job.RetryCount);

                            Model3DJobSucceeded?.Invoke(
                                "model3d_job_succeeded",
                                new Dictionary<string, object>
                                {
                                    { "job_id", jobIdStr },
                                    { "attempt", attempt },
                                    { "latency_ms", latencyMs },
                                }
                            );

                            EditorSfx.PlayModel3DCompleted(jobIdStr);
                        }
                    }
                    else
                    {
                        if (ShouldRequeueForBackendRecovery(result.ErrorMessage))
                        {
                            RequeueJobForBackendRecovery(job);
                            return;
                        }

                        EmitFailedPipelineTelemetry(job, isPreview2DFlow, isModel3DFlow, jobIdStr, "generation_failed");

                        job.SetError($"Generation failed: {result.ErrorMessage}");
                        _logger.Error($"Job failed: {job.AssetName} - {result.ErrorMessage}");
                    }
                }
                finally
                {
                    _generationService.OnProgressUpdated -= progressHandler;
                }
            }
            catch (Exception ex)
            {
                if (!job.IsTerminal())
                {
                    if (ShouldRequeueForBackendRecovery(ex.Message))
                    {
                        RequeueJobForBackendRecovery(job);
                        return;
                    }

                    EmitFailedPipelineTelemetry(job, isPreview2DFlow, isModel3DFlow, jobIdStr, ex.GetType().Name);

                    job.SetError($"Job processing failed: {ex.Message}");
                    _logger.Error($"Job failed: {job.AssetName}", ex);
                }
            }
        }

        private void EmitFailedPipelineTelemetry(
            Job job,
            bool isPreview2DFlow,
            bool isModel3DFlow,
            string jobIdStr,
            string errorCode)
        {
            if (job == null || string.IsNullOrEmpty(jobIdStr))
            {
                return;
            }

            if (isPreview2DFlow)
            {
                long latencyMs = ConsumeLatencyMs(_preview2DStartedAt, jobIdStr);

                Preview2DJobFailed?.Invoke(
                    "preview2d_job_failed",
                    new Dictionary<string, object>
                    {
                        { "job_id", jobIdStr },
                        { "attempt", job.CurrentIteration },
                        { "error_code", errorCode },
                        { "latency_ms", latencyMs },
                    }
                );
            }

            if (isModel3DFlow)
            {
                long latencyMs = ConsumeLatencyMs(_model3DStartedAt, jobIdStr);

                Model3DJobFailed?.Invoke(
                    "model3d_job_failed",
                    new Dictionary<string, object>
                    {
                        { "job_id", jobIdStr },
                        { "attempt", Math.Max(0, job.RetryCount) },
                        { "error_code", errorCode },
                        { "latency_ms", latencyMs },
                        { "is_retryable", job.CanRetry(_maxRetries) },
                    }
                );
            }
        }

        private static long ConsumeLatencyMs(IDictionary<string, double> startedAtMap, string jobIdStr)
        {
            if (startedAtMap == null || string.IsNullOrEmpty(jobIdStr))
            {
                return 0;
            }

            double startedAt;
            if (!startedAtMap.TryGetValue(jobIdStr, out startedAt))
            {
                return 0;
            }

            startedAtMap.Remove(jobIdStr);
            return (long)Math.Max(0d, (EditorApplication.timeSinceStartup - startedAt) * 1000d);
        }

        private bool ShouldRequeueForBackendRecovery(string errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage))
            {
                return !_generationService.IsBackendReady;
            }

            bool backendNotReadyError = errorMessage.StartsWith(BackendNotReadyErrorPrefix, StringComparison.OrdinalIgnoreCase);
            bool recoverableConnectionError = BackendConnectionRecoveryHelper.IsRecoverableConnectionError(errorMessage);

            if (recoverableConnectionError)
            {
                _logger.Warning($"Transient backend connection issue detected. Re-queuing without consuming retry: {errorMessage}");
            }

            return backendNotReadyError || recoverableConnectionError || !_generationService.IsBackendReady;
        }

        private void RequeueJobForBackendRecovery(Job job)
        {
            if (job == null)
            {
                return;
            }

            _processingJobs.Remove(job);
            if (!_queuedJobs.Contains(job))
            {
                _queuedJobs.Add(job);
            }

            job.ResetForRecovery();
            ApplyQueuedBackendSubstate(job);

            OnQueueChanged?.Invoke();
            SavePersistedJobs();
        }

        private void ApplyQueuedBackendSubstate(Job job)
        {
            if (job == null || job.State != JobState.Queued)
            {
                return;
            }

            if (!_generationService.IsBackendReady)
            {
                job.SetQueueSubstate(JobQueueSubstate.AwaitingLogin, AwaitingLoginMessage);
                return;
            }

            if (!IsRealtimeBackendConnected())
            {
                job.SetQueueSubstate(JobQueueSubstate.ReconnectingBackend, ReconnectingBackendMessage);
                return;
            }

            job.SetQueueSubstate(JobQueueSubstate.None, QueuedMessage);
        }

        private void CompleteJob(Job job)
        {
            _processingJobs.Remove(job);

            // Only add to completed if not already there (cancelled jobs are already moved)
            if (!_completedJobs.Contains(job))
            {
                _completedJobs.Add(job);
            }

            if (job.State == JobState.Completed)
            {
                _logger.Info($"Job completed: {job.AssetName}");
                OnJobCompleted?.Invoke(job);
            }
            else if (job.State == JobState.Failed)
            {
                _logger.Error($"Job failed: {job.AssetName} - {job.ErrorMessage}");
                InsufficientBalanceDialogHelper.ShowDialogForJobIfNeeded(job, _logger, "[QueueService]");
                OnJobFailed?.Invoke(job);
            }
            // Cancelled jobs are already handled by CancelJob(), no need to fire event again

            OnQueueChanged?.Invoke();
            SavePersistedJobs();
        }

        private Job FindJob(JobId jobId)
        {
            return GetAllJobs().FirstOrDefault(j => j.Id == jobId);
        }

        private void LoadSettings()
        {
            _maxConcurrentJobs = EditorPrefs.GetInt("GAF_Settings_MaxConcurrentJobs", DefaultMaxConcurrentJobs);
            _autoStartQueue = EditorPrefs.GetInt("GAF_Settings_AutoStartQueue", PrefBoolTrue) == PrefBoolTrue;
            _retryFailedJobs = EditorPrefs.GetInt("GAF_Settings_RetryFailedJobs", PrefBoolFalse) == PrefBoolTrue;
            _maxRetries = EditorPrefs.GetInt("GAF_Settings_MaxRetries", DefaultMaxRetries);
        }

        private void SaveSettings()
        {
            EditorPrefs.SetInt("GAF_Settings_MaxConcurrentJobs", _maxConcurrentJobs);
            EditorPrefs.SetInt("GAF_Settings_AutoStartQueue", _autoStartQueue ? PrefBoolTrue : PrefBoolFalse);
            EditorPrefs.SetInt("GAF_Settings_RetryFailedJobs", _retryFailedJobs ? PrefBoolTrue : PrefBoolFalse);
            EditorPrefs.SetInt("GAF_Settings_MaxRetries", _maxRetries);
        }

        private void LoadPersistedJobs()
        {
            try
            {
                string persistencePath = GetJobPersistencePath();

                if (!File.Exists(persistencePath))
                {
                    _logger.Debug("No persisted jobs found, starting with empty queue");
                    return;
                }

                string json = File.ReadAllText(persistencePath);

                // Configure JSON settings with custom converters for value objects
                var settings = new JsonSerializerSettings
                {
                    // Use custom contract resolver to allow deserialization to properties with private/protected setters
                    ContractResolver = new PrivateSetterContractResolver(),
                    ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                    ObjectCreationHandling = ObjectCreationHandling.Replace,
                    Converters = new List<JsonConverter>
                    {
                        new AssetNameConverter(),
                        new JobIdConverter()
                    }
                };

                var persistedData = JsonConvert.DeserializeObject<JobPersistenceData>(json, settings);

                if (persistedData?.Jobs == null || persistedData.Jobs.Count == 0)
                {
                    _logger.Debug("No jobs in persistence file");
                    return;
                }

                // Restore all jobs to appropriate lists based on their state
                foreach (var job in persistedData.Jobs)
                {
                    // Reset stale GAP submissions so users can resubmit after relaunch
                    if (GapSubmissionTracker.IsInProgress(job) &&
                        job.GAPSubmittedAt.HasValue &&
                        (DateTime.UtcNow - job.GAPSubmittedAt.Value).TotalHours > StaleGapSubmissionHours)
                    {
                        job.GAPStatus = GapStatus.NotSubmitted;
                        job.GAPJobId = null;
                    }

                    if (job.State == JobState.Queued)
                    {
                        NormalizeQueuedJobSubstate(job);
                        _queuedJobs.Add(job);
                        _logger.Debug($"Restored queued job: {job.Id.Value}");
                    }
                    else if (job.State == JobState.AwaitingImageConfirm)
                    {
                        // Keep approval-ready jobs out of the runnable queue so they don't regenerate from scratch.
                        // They stay in processing list until user approves/cancels from the preview dialog/queue panel.
                        _processingJobs.Add(job);
                        _logger.Debug($"Restored awaiting-approval job: {job.Id.Value}");
                    }
                    else if (job.State == JobState.Processing)
                    {
                        if (job.ShouldRestoreAsAwaitingApproval)
                        {
                            // If preview versions already exist, resume at approval step instead of regenerating.
                            job.SetState(JobState.AwaitingImageConfirm);
                            _processingJobs.Add(job);
                            _logger.Debug($"Restored interrupted job as awaiting approval: {job.Id.Value}");
                        }
                        else
                        {
                            // Reset interrupted processing jobs back to queued
                            job.SetState(JobState.Queued);
                            NormalizeQueuedJobSubstate(job);
                            _queuedJobs.Add(job);
                            _logger.Debug($"Restored interrupted job as queued: {job.Id.Value}");
                        }
                    }
                    else if (job.IsTerminal())
                    {
                        // Restore completed/failed/cancelled jobs to history
                        _completedJobs.Add(job);
                        _logger.Debug($"Restored completed job: {job.Id.Value} (State: {job.State})");
                    }
                }

                int totalRestored = _queuedJobs.Count + _completedJobs.Count;
                _logger.Info($"Loaded {totalRestored} persisted jobs from previous session ({_queuedJobs.Count} queued, {_completedJobs.Count} completed)");

                // Auto-start queue if enabled (Update() is already registered, will process automatically)
                if (_autoStartQueue && _queuedJobs.Count > 0)
                {
                    _logger.Debug("Auto-starting queue with restored jobs");
                }

                OnQueueChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to load persisted jobs: {ex.Message}");
            }
        }

        private static void NormalizeQueuedJobSubstate(Job job)
        {
            if (job == null || job.State != JobState.Queued)
            {
                return;
            }

            if (job.QueueSubstate == JobQueueSubstate.None)
            {
                // Backward compatibility for older persisted data that used message strings only.
                if (job.ProgressMessage == AwaitingLoginMessage)
                {
                    job.SetQueueSubstate(JobQueueSubstate.AwaitingLogin, AwaitingLoginMessage);
                    return;
                }

                if (job.ProgressMessage == ReconnectingBackendMessage)
                {
                    job.SetQueueSubstate(JobQueueSubstate.ReconnectingBackend, ReconnectingBackendMessage);
                    return;
                }
            }

            // Ensure queued jobs always have a visible status message.
            if (string.IsNullOrWhiteSpace(job.ProgressMessage))
            {
                job.SetQueueSubstate(job.QueueSubstate, QueuedMessage);
            }
        }

        private void SavePersistedJobs()
        {
            try
            {
                // Collect ALL jobs including completed ones (for history view)
                var jobsToPersist = new List<Job>();
                jobsToPersist.AddRange(_queuedJobs);
                jobsToPersist.AddRange(_processingJobs);
                jobsToPersist.AddRange(_completedJobs);

                var persistenceData = new JobPersistenceData
                {
                    SavedAt = DateTime.UtcNow,
                    Jobs = jobsToPersist
                };

                // Configure JSON settings with custom converters for value objects
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    // Use custom contract resolver to allow serialization/deserialization of properties with private/protected setters
                    ContractResolver = new PrivateSetterContractResolver(),
                    ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                    ObjectCreationHandling = ObjectCreationHandling.Replace,
                    Converters = new List<JsonConverter>
                    {
                        new AssetNameConverter(),
                        new JobIdConverter()
                    }
                };

                string json = JsonConvert.SerializeObject(persistenceData, settings);
                string persistencePath = GetJobPersistencePath();

                // Ensure directory exists
                string directory = Path.GetDirectoryName(persistencePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(persistencePath, json);
                _logger.Debug($"Persisted {jobsToPersist.Count} jobs (including {_completedJobs.Count} completed)");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to save persisted jobs: {ex.Message}");
            }
        }

        /// <summary>
        /// Show the approval dialog for a job awaiting confirmation
        /// </summary>
        private void ShowApprovalDialog(Job job)
        {
            EditorApplication.delayCall += () =>
            {
                if (job.State != JobState.AwaitingImageConfirm)
                {
                    return;
                }

                // Get all jobs awaiting approval
                var awaitingJobs = GetJobsByState(JobState.AwaitingImageConfirm);

                if (awaitingJobs.Count == 0)
                {
                    _logger.Warning("No jobs awaiting approval");
                    return;
                }

                // Show the approval review dialog
                Genies.Sdk.ArtForge.Editor.Hub.UI.ApprovalReviewDialog.Show(awaitingJobs, _logger, job);
            };
        }

        private string GetJobPersistencePath()
        {
            return BrandingConstants.AssetPaths.GetQueuePersistencePath();
        }

        /// <summary>
        /// Custom contract resolver that allows writing to properties with private/protected setters
        /// </summary>
        private class PrivateSetterContractResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(
                System.Reflection.MemberInfo member,
                MemberSerialization memberSerialization)
            {
                var prop = base.CreateProperty(member, memberSerialization);

                // Allow writing to all properties, even those with private/protected setters
                if (!prop.Writable)
                {
                    var property = member as System.Reflection.PropertyInfo;
                    if (property != null)
                    {
                        var hasPrivateSetter = property.GetSetMethod(true) != null;
                        prop.Writable = hasPrivateSetter;
                    }
                }

                return prop;
            }
        }

        [Serializable]
        private class JobPersistenceData
        {
            public DateTime SavedAt { get; set; }
            public List<Job> Jobs { get; set; }
        }
    }
}
