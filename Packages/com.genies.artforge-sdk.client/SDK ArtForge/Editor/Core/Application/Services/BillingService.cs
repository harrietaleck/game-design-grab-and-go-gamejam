using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Helpers;
using UnityEditor;

namespace Genies.Sdk.ArtForge.Editor.Core.Application.Services
{
    /// <summary>
    /// Coordinates balance refreshes and emits UI-ready billing state.
    /// </summary>
    internal sealed class BillingService : IDisposable
    {
        private const double BalanceRefreshIntervalSeconds = 60d;
        private const double ActiveJobBalanceScanIntervalSeconds = 0.5d;
        private const double StateDrivenBalanceRefreshMinIntervalSeconds = 5d;
        private const string UnknownBalanceText = "Balance: --";
        private const string DefaultTooltip = "Click to open billing portal and top up.";
        private const string LowBalanceTooltip = "Low balance warning active. Click to top up.";
        private const string RefreshFailedTooltip = "Unable to refresh balance. Click to open billing portal.";
        private const string AuthRequiredTooltip = "Authenticate in Settings to view balance.";

        private readonly ILogger _logger;
        private readonly Dictionary<string, string> _activeJobBalanceSignatures = new Dictionary<string, string>();

        private bool _disposed;
        private bool _isRefreshingBalance;
        private bool _lastBackendAuthenticated;
        private bool _forceBalanceRefreshRequested;
        private bool _pendingStateDrivenBalanceRefresh;

        private double _nextBalanceRefreshAt;
        private double _nextStateDrivenBalanceRefreshAt;
        private double _nextActiveJobBalanceScanAt;

        private string _lastKnownBalanceText = "--";

        public event Action<BalanceState> BalanceStateChanged;

        public BillingService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Initialize()
        {
            if (_disposed)
            {
                return;
            }

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            double now = EditorApplication.timeSinceStartup;
            _nextBalanceRefreshAt = now;
            _nextActiveJobBalanceScanAt = now;
            PublishUnavailableState(allowPortalClick: false);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            EditorApplication.update -= OnEditorUpdate;
            ResetActiveJobBalanceTracking();
        }

        public void RequestImmediateRefresh(string trigger = null)
        {
            if (_disposed)
            {
                return;
            }

            QueueBalanceRefresh(trigger);
        }

        private void OnEditorUpdate()
        {
            if (_disposed || _isRefreshingBalance)
            {
                return;
            }

            bool isBackendAuthenticated = GeniesArtForgeService.Generation?.IsBackendAvailable ?? false;
            if (isBackendAuthenticated != _lastBackendAuthenticated)
            {
                _lastBackendAuthenticated = isBackendAuthenticated;
                if (!isBackendAuthenticated)
                {
                    PublishUnavailableState(allowPortalClick: false);
                    ResetActiveJobBalanceTracking();
                }

                _nextBalanceRefreshAt = EditorApplication.timeSinceStartup;
            }

            if (!isBackendAuthenticated)
            {
                return;
            }

            TrackActiveJobStateChangesForBalanceRefresh();

            if (!_forceBalanceRefreshRequested && EditorApplication.timeSinceStartup < _nextBalanceRefreshAt)
            {
                return;
            }

            _forceBalanceRefreshRequested = false;
            _ = RefreshBalanceAsync();
        }

        private async Task RefreshBalanceAsync()
        {
            _isRefreshingBalance = true;
            try
            {
                var contentClient = GeniesArtForgeService.ContentClient;
                if (contentClient == null)
                {
                    PublishUnavailableState(allowPortalClick: false);
                    return;
                }

                var balance = await contentClient.GetPaymentsBalanceAsync();
                string formattedBalance = balance.Balance.ToString("0.##", CultureInfo.InvariantCulture);
                _lastKnownBalanceText = formattedBalance;

                PublishState(new BalanceState(
                    labelText: $"Balance: {formattedBalance}",
                    tooltip: balance.LowBalanceWarning ? LowBalanceTooltip : DefaultTooltip,
                    isButtonEnabled: true,
                    showLowBalanceWarning: balance.LowBalanceWarning,
                    balanceTextForTelemetry: formattedBalance));
            }
            catch (Exception ex)
            {
                _logger.Warning($"[BillingService] Failed to refresh balance: {ex.Message}");
                PublishUnavailableState(allowPortalClick: true);
            }
            finally
            {
                _isRefreshingBalance = false;
                _nextBalanceRefreshAt = EditorApplication.timeSinceStartup + BalanceRefreshIntervalSeconds;
            }
        }

        private void TrackActiveJobStateChangesForBalanceRefresh()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextActiveJobBalanceScanAt)
            {
                FlushPendingStateDrivenBalanceRefresh(now);
                return;
            }

            _nextActiveJobBalanceScanAt = now + ActiveJobBalanceScanIntervalSeconds;

            var queue = GeniesArtForgeService.Queue;
            bool stateChanged = false;

            if (queue == null)
            {
                if (_activeJobBalanceSignatures.Count > 0)
                {
                    _activeJobBalanceSignatures.Clear();
                    stateChanged = true;
                }

                HandleStateDrivenBalanceRefreshSignal(stateChanged, now);
                return;
            }

            var activeJobIds = new HashSet<string>();
            var allJobs = queue.GetAllJobs();
            foreach (var job in allJobs)
            {
                if (!IsActiveForBalanceTracking(job))
                {
                    continue;
                }

                string jobId = job?.Id?.ToString();
                if (string.IsNullOrWhiteSpace(jobId))
                {
                    continue;
                }

                activeJobIds.Add(jobId);

                string signature = BuildBalanceSignature(job);
                if (!_activeJobBalanceSignatures.TryGetValue(jobId, out var previousSignature) ||
                    !string.Equals(previousSignature, signature, StringComparison.Ordinal))
                {
                    _activeJobBalanceSignatures[jobId] = signature;
                    stateChanged = true;
                }
            }

            var staleJobIds = new List<string>();
            foreach (var kvp in _activeJobBalanceSignatures)
            {
                if (!activeJobIds.Contains(kvp.Key))
                {
                    staleJobIds.Add(kvp.Key);
                }
            }

            if (staleJobIds.Count > 0)
            {
                stateChanged = true;
                foreach (var staleId in staleJobIds)
                {
                    _activeJobBalanceSignatures.Remove(staleId);
                }
            }

            HandleStateDrivenBalanceRefreshSignal(stateChanged, now);
        }

        private void HandleStateDrivenBalanceRefreshSignal(bool stateChanged, double now)
        {
            if (stateChanged)
            {
                if (now >= _nextStateDrivenBalanceRefreshAt)
                {
                    QueueBalanceRefresh("active_job_state_changed");
                    _nextStateDrivenBalanceRefreshAt = now + StateDrivenBalanceRefreshMinIntervalSeconds;
                    _pendingStateDrivenBalanceRefresh = false;
                }
                else
                {
                    _pendingStateDrivenBalanceRefresh = true;
                }

                return;
            }

            FlushPendingStateDrivenBalanceRefresh(now);
        }

        private void FlushPendingStateDrivenBalanceRefresh(double now)
        {
            if (!_pendingStateDrivenBalanceRefresh || now < _nextStateDrivenBalanceRefreshAt)
            {
                return;
            }

            QueueBalanceRefresh("active_job_state_changed_batched");
            _nextStateDrivenBalanceRefreshAt = now + StateDrivenBalanceRefreshMinIntervalSeconds;
            _pendingStateDrivenBalanceRefresh = false;
        }

        private void QueueBalanceRefresh(string trigger)
        {
            _forceBalanceRefreshRequested = true;
            _nextBalanceRefreshAt = EditorApplication.timeSinceStartup;

            if (!string.IsNullOrWhiteSpace(trigger))
            {
                _logger.Info($"[BillingService] Balance refresh requested ({trigger})");
            }
        }

        private void ResetActiveJobBalanceTracking()
        {
            _activeJobBalanceSignatures.Clear();
            _pendingStateDrivenBalanceRefresh = false;
            _nextStateDrivenBalanceRefreshAt = 0d;
            _nextActiveJobBalanceScanAt = EditorApplication.timeSinceStartup;
        }

        private void PublishUnavailableState(bool allowPortalClick)
        {
            _lastKnownBalanceText = "--";
            PublishState(new BalanceState(
                labelText: UnknownBalanceText,
                tooltip: allowPortalClick ? RefreshFailedTooltip : AuthRequiredTooltip,
                isButtonEnabled: allowPortalClick,
                showLowBalanceWarning: false,
                balanceTextForTelemetry: _lastKnownBalanceText));
        }

        private void PublishState(BalanceState state)
        {
            if (_disposed)
            {
                return;
            }

            BalanceStateChanged?.Invoke(state);
        }

        private static bool IsActiveForBalanceTracking(Job job)
        {
            if (job == null)
            {
                return false;
            }

            if (GapSubmissionTracker.IsInProgress(job))
            {
                return true;
            }

            return !job.IsTerminal();
        }

        private static string BuildBalanceSignature(Job job)
        {
            string state = job?.State.ToString() ?? "";
            string step = job?.CurrentStep ?? "";
            string gapStatus = job?.GAPStatus ?? "";
            bool imageConfirmed = job?.ImageConfirmed ?? false;
            return $"{state}|{step}|{gapStatus}|{imageConfirmed}";
        }
    }

    internal readonly struct BalanceState
    {
        public BalanceState(
            string labelText,
            string tooltip,
            bool isButtonEnabled,
            bool showLowBalanceWarning,
            string balanceTextForTelemetry)
        {
            LabelText = labelText;
            Tooltip = tooltip;
            IsButtonEnabled = isButtonEnabled;
            ShowLowBalanceWarning = showLowBalanceWarning;
            BalanceTextForTelemetry = balanceTextForTelemetry;
        }

        public string LabelText { get; }
        public string Tooltip { get; }
        public bool IsButtonEnabled { get; }
        public bool ShowLowBalanceWarning { get; }
        public string BalanceTextForTelemetry { get; }
    }
}
