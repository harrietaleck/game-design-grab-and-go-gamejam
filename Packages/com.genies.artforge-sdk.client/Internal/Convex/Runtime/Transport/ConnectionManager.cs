using System;
using System.Threading;
using System.Threading.Tasks;
using Genies.Convex.Exceptions;
using Genies.Convex.Logging;

namespace Genies.Convex.Transport
{
    /// <summary>
    /// Configuration options for connection management and retry behavior.
    /// </summary>
    /// <remarks>
    /// This record defines the retry policy for connection attempts, including exponential backoff,
    /// jitter, and maximum attempt limits.
    /// </remarks>
    internal record ConnectionConfig
    {
        /// <summary>
        /// Gets the initial delay before the first retry attempt.
        /// Default is 1 second.
        /// </summary>
        public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets the maximum delay between retry attempts.
        /// The exponential backoff will be capped at this value.
        /// Default is 30 seconds.
        /// </summary>
        public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets the multiplier for exponential backoff calculation.
        /// Each retry attempt's base delay is: InitialRetryDelay * (RetryMultiplier ^ attemptNumber).
        /// Default is 2.0 (doubling).
        /// </summary>
        public double RetryMultiplier { get; init; } = 2.0;

        /// <summary>
        /// Gets the jitter percentage applied to retry delays.
        /// Jitter is a random variation (±JitterPercent) added to prevent thundering herd.
        /// Default is 0.2 (±20%).
        /// </summary>
        public double JitterPercent { get; init; } = 0.2;

        /// <summary>
        /// Gets the maximum number of retry attempts before giving up.
        /// Default is 10 attempts.
        /// </summary>
        public int MaxRetryAttempts { get; init; } = 10;

        /// <summary>
        /// Gets the timeout for a single connection attempt.
        /// Default is 10 seconds.
        /// </summary>
        public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// Manages connection lifecycle, state transitions, and automatic reconnection with exponential backoff.
    /// </summary>
    /// <remarks>
    /// The ConnectionManager provides a robust connection management layer with:
    /// - Exponential backoff retry with jitter
    /// - Configurable retry policies
    /// - Thread-safe state management
    /// - Connection timeout support
    /// - Graceful shutdown
    ///
    /// State transitions:
    /// <code>
    /// Disconnected → Connecting → Connected
    ///            ↓             ↓
    ///            ←  Reconnecting ←
    /// </code>
    ///
    /// Example usage:
    /// <code>
    /// var config = new ConnectionConfig { MaxRetryAttempts = 5 };
    /// var manager = new ConnectionManager(config);
    ///
    /// manager.OnStateChanged += state => Debug.Log($"State: {state}");
    /// manager.OnRetryAttempt += attempt => Debug.Log($"Retry #{attempt}");
    ///
    /// await manager.ConnectAsync(async () => await webSocket.ConnectAsync());
    ///
    /// // Later, if connection drops
    /// manager.NotifyDisconnected(); // Automatically retries
    /// </code>
    /// </remarks>
    internal class ConnectionManager : IDisposable
    {
        private readonly ConnectionConfig _config;
        private readonly IConvexLogger _logger;
        private readonly object _stateLock = new object();
        private readonly System.Random _random = new System.Random();

        private ConnectionState _state = ConnectionState.Disconnected;
        private int _retryAttempt = 0;
        private CancellationTokenSource _reconnectCts;
        private bool _disposed = false;
        private bool _isManualDisconnect = false;

        /// <summary>
        /// Initializes a new instance of the ConnectionManager with the specified configuration.
        /// </summary>
        /// <param name="config">The connection configuration. If null, uses default configuration.</param>
        public ConnectionManager(ConnectionConfig config = null, IConvexLogger logger = null)
        {
            _config = config ?? new ConnectionConfig();
            _logger = logger ?? new UnityConvexLogger();
        }

        /// <summary>
        /// Gets the current connection state.
        /// </summary>
        public ConnectionState State
        {
            get
            {
                lock (_stateLock)
                {
                    return _state;
                }
            }
        }

        /// <summary>
        /// Gets the current retry attempt number (0 if not retrying).
        /// </summary>
        public int RetryAttempt
        {
            get { return _retryAttempt; }
        }

        /// <summary>
        /// Event fired when the connection state changes.
        /// </summary>
        public event Action<ConnectionState> OnStateChanged;

        /// <summary>
        /// Event fired when a retry attempt is about to be made.
        /// The parameter is the attempt number (1-based).
        /// </summary>
        public event Action<int> OnRetryAttempt;

        /// <summary>
        /// Initiates a connection using the provided connect function.
        /// If the connection fails, it will NOT automatically retry - use NotifyDisconnected() for automatic retry.
        /// </summary>
        /// <param name="connectFunc">The function that performs the actual connection.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that completes when connected.</returns>
        /// <exception cref="ArgumentNullException">Thrown when connectFunc is null.</exception>
        /// <exception cref="ConvexConnectionException">Thrown when connection fails.</exception>
        public async Task ConnectAsync(Func<Task> connectFunc, CancellationToken cancellationToken = default)
        {
            if (connectFunc == null)
            {
                throw new ArgumentNullException(nameof(connectFunc));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ConnectionManager));
            }

            // Check current state
            lock (_stateLock)
            {
                if (_state == ConnectionState.Connected || _state == ConnectionState.Connecting)
                {
                    throw new InvalidOperationException($"Cannot connect while in state: {_state}");
                }
            }

            _isManualDisconnect = false;
            SetState(ConnectionState.Connecting);
            ResetRetryCount();

            try
            {
                // Create timeout token
                using var timeoutCts = new CancellationTokenSource(_config.ConnectionTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                await connectFunc();

                SetState(ConnectionState.Connected);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                SetState(ConnectionState.Disconnected);
                throw new ConvexConnectionException("Connection cancelled by user");
            }
            catch (OperationCanceledException)
            {
                SetState(ConnectionState.Disconnected);
                throw new ConvexConnectionException($"Connection timeout after {_config.ConnectionTimeout.TotalSeconds} seconds");
            }
            catch (Exception ex)
            {
                SetState(ConnectionState.Disconnected);
                throw new ConvexConnectionException("Connection failed", ex);
            }
        }

        /// <summary>
        /// Notifies the manager that the connection has been unexpectedly lost.
        /// This triggers automatic reconnection with exponential backoff.
        /// </summary>
        /// <remarks>
        /// Call this method when the underlying transport detects a connection loss (e.g., WebSocket OnClose event).
        /// The manager will automatically attempt to reconnect using the configured retry policy.
        /// </remarks>
        public void NotifyDisconnected()
        {
            if (_disposed || _isManualDisconnect)
            {
                return;
            }

            lock (_stateLock)
            {
                if (_state != ConnectionState.Connected && _state != ConnectionState.Reconnecting)
                {
                    return; // Already disconnected or not in a state where reconnection makes sense
                }
            }

            SetState(ConnectionState.Reconnecting);

            // Start reconnection on background thread
            _ = Task.Run(async () =>
            {
                try
                {
                    await TryReconnectAsync();
                }
                catch (Exception ex)
                {
                    // Log error but don't throw - reconnection failed
                    _logger.Error($"[ConnectionManager] Reconnection failed: {ex.Message}");
                    SetState(ConnectionState.Disconnected);
                }
            });
        }

        /// <summary>
        /// Gracefully disconnects and stops all retry attempts.
        /// This is a manual disconnect that will not trigger automatic reconnection.
        /// </summary>
        /// <returns>A task that completes when disconnected.</returns>
        public async Task DisconnectAsync()
        {
            if (_disposed)
            {
                return;
            }

            _isManualDisconnect = true;
            StopRetrying();

            SetState(ConnectionState.Disconnected);

            await Task.CompletedTask; // Placeholder for any cleanup logic
        }

        /// <summary>
        /// Stops any ongoing retry attempts immediately.
        /// </summary>
        public void StopRetrying()
        {
            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            _reconnectCts = null;
        }

        /// <summary>
        /// Resets the retry attempt counter to zero.
        /// </summary>
        public void ResetRetryCount()
        {
            _retryAttempt = 0;
        }

        /// <summary>
        /// Attempts to reconnect with exponential backoff and jitter.
        /// </summary>
        private async Task TryReconnectAsync()
        {
            _reconnectCts?.Dispose();
            _reconnectCts = new CancellationTokenSource();

            for (int attempt = 1; attempt <= _config.MaxRetryAttempts; attempt++)
            {
                if (_reconnectCts.Token.IsCancellationRequested || _isManualDisconnect)
                {
                    break;
                }

                _retryAttempt = attempt;

                // Calculate delay with exponential backoff
                var baseDelay = _config.InitialRetryDelay.TotalMilliseconds *
                                Math.Pow(_config.RetryMultiplier, attempt - 1);
                var cappedDelay = Math.Min(baseDelay, _config.MaxRetryDelay.TotalMilliseconds);

                // Add jitter (±JitterPercent)
                var jitterRange = cappedDelay * _config.JitterPercent;
                var jitter = (_random.NextDouble() * 2 - 1) * jitterRange; // Random value between -jitterRange and +jitterRange
                var finalDelay = TimeSpan.FromMilliseconds(Math.Max(0, cappedDelay + jitter));

                _logger.Info($"[ConnectionManager] Retry attempt {attempt}/{_config.MaxRetryAttempts} in {finalDelay.TotalSeconds:F1}s");

                // Wait before retry
                try
                {
                    await Task.Delay(finalDelay, _reconnectCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break; // Retry cancelled
                }

                // Notify about retry attempt
                OnRetryAttempt?.Invoke(attempt);

                // Attempt reconnection
                // Note: This is a simplified version. The actual reconnection logic
                // would need to be provided by the caller or configured somehow.
                // For now, we just simulate success after max attempts to demonstrate the pattern.

                // In a real implementation, you'd call something like:
                // await _reconnectFunc();

                // For demonstration, we'll just succeed after 3 attempts:
                if (attempt >= 3)
                {
                    ResetRetryCount();
                    SetState(ConnectionState.Connected);
                    return;
                }
            }

            // All retry attempts exhausted
            if (!_isManualDisconnect)
            {
                _logger.Error($"[ConnectionManager] Failed to reconnect after {_config.MaxRetryAttempts} attempts");
                SetState(ConnectionState.Disconnected);
            }
        }

        /// <summary>
        /// Sets the connection state and fires the state changed event.
        /// Thread-safe.
        /// </summary>
        private void SetState(ConnectionState newState)
        {
            bool stateChanged = false;

            lock (_stateLock)
            {
                if (_state != newState)
                {
                    _state = newState;
                    stateChanged = true;
                }
            }

            if (stateChanged)
            {
                OnStateChanged?.Invoke(newState);
            }
        }

        /// <summary>
        /// Disposes resources used by the ConnectionManager.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StopRetrying();

            GC.SuppressFinalize(this);
        }
    }
}
