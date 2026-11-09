using System;
using Genies.Convex.Logging;

namespace Genies.Convex.Core
{
    /// <summary>
    /// Configuration options for ConvexClient and ConvexHttpClient.
    /// </summary>
    /// <remarks>
    /// Provides settings for connection, authentication, timeouts, and retry behavior.
    /// </remarks>
    internal class ConvexClientOptions
    {
        /// <summary>
        /// Gets or sets the Convex deployment URL.
        /// </summary>
        /// <example>
        /// <code>
        /// var options = new ConvexClientOptions
        /// {
        ///     DeploymentUrl = "https://example-lion-123.convex.cloud"
        /// };
        /// </code>
        /// </example>
        public string DeploymentUrl { get; set; }

        /// <summary>
        /// Gets or sets the client version identifier.
        /// Defaults to the current protocol version from ConvexVersion.
        /// </summary>
        public string ClientVersion { get; set; } = ConvexVersion.ClientIdentifier;

        /// <summary>
        /// Gets or sets the request timeout.
        /// Defaults to 30 seconds.
        /// </summary>
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the request timeout in milliseconds.
        /// Defaults to 30000ms (30 seconds).
        /// </summary>
        public int TimeoutMs
        {
            get => (int)DefaultTimeout.TotalMilliseconds;
            set => DefaultTimeout = TimeSpan.FromMilliseconds(value);
        }

        /// <summary>
        /// Gets or sets whether the client should automatically reconnect on connection loss.
        /// Defaults to true.
        /// </summary>
        /// <remarks>
        /// Only applies to WebSocket-based ConvexClient. HTTP client does not maintain persistent connections.
        /// </remarks>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of reconnection attempts.
        /// Defaults to 10 attempts.
        /// </summary>
        /// <remarks>
        /// Reconnection uses exponential backoff with jitter. After max attempts, the client remains disconnected.
        /// </remarks>
        public int MaxReconnectAttempts { get; set; } = 10;

        /// <summary>
        /// Gets or sets the WebSocket heartbeat timeout.
        /// Defaults to 120 seconds. Set to 0 to disable heartbeat monitoring.
        /// </summary>
        public TimeSpan WebSocketHeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(120);

        /// <summary>
        /// Gets or sets the WebSocket heartbeat timeout in milliseconds.
        /// Defaults to 120000ms (120 seconds). Set to 0 to disable.
        /// </summary>
        public int WebSocketHeartbeatTimeoutMs
        {
            get => (int)WebSocketHeartbeatTimeout.TotalMilliseconds;
            set => WebSocketHeartbeatTimeout = TimeSpan.FromMilliseconds(value);
        }

        /// <summary>
        /// Gets or sets whether debug logging is enabled.
        /// Defaults to false.
        /// </summary>
        /// <remarks>
        /// When enabled, logs detailed information about WebSocket messages, connection state changes,
        /// and internal operations. Useful for debugging but may impact performance.
        /// </remarks>
        public bool Debug { get; set; } = false;

        /// <summary>
        /// Gets or sets the logger used by Convex transports.
        /// Defaults to UnityConvexLogger when not provided.
        /// </summary>
        public IConvexLogger Logger { get; set; }

        /// <summary>
        /// Gets or sets whether debug logs from Convex functions are enabled.
        /// Defaults to false.
        /// </summary>
        public bool EnableDebugLogs
        {
            get => Debug;
            set => Debug = value;
        }

        /// <summary>
        /// Validates the configuration options.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when options are invalid.</exception>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(DeploymentUrl))
            {
                throw new ArgumentException("DeploymentUrl cannot be null or empty.", nameof(DeploymentUrl));
            }

            if (TimeoutMs <= 0)
            {
                throw new ArgumentException("TimeoutMs must be positive.", nameof(TimeoutMs));
            }

            if (MaxReconnectAttempts < 0)
            {
                throw new ArgumentException("MaxReconnectAttempts cannot be negative.", nameof(MaxReconnectAttempts));
            }

            if (WebSocketHeartbeatTimeout < TimeSpan.Zero)
            {
                throw new ArgumentException("WebSocketHeartbeatTimeout cannot be negative.", nameof(WebSocketHeartbeatTimeout));
            }

            if (string.IsNullOrWhiteSpace(ClientVersion))
            {
                throw new ArgumentException("ClientVersion cannot be null or empty.", nameof(ClientVersion));
            }
        }
    }
}
