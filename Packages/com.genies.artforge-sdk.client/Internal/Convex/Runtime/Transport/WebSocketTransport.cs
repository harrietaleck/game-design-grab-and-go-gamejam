using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;
using Genies.Convex.Values;
using Genies.Convex.Unity;
using Genies.Convex.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Genies.Convex.Transport
{
    internal class QueryInfo
    {
        public int QueryId { get; set; }
        public string QueryToken { get; set; }
        public string UdfPath { get; set; }
        public ConvexValue.Object Args { get; set; }
        public Action<FunctionResult> Callback { get; set; }
    }

    internal class QueryAddition
    {
        public int queryId;
        public string udfPath;
        public JToken[] args;
    }

    /// <summary>
    /// WebSocket-based transport implementation for Convex client.
    /// </summary>
    /// <remarks>
    /// Provides real-time communication with Convex backend using WebSocket protocol.
    /// Supports queries, mutations, actions, and real-time subscriptions.
    /// </remarks>
    internal class WebSocketTransport : IDisposable
    {
        private readonly string _deploymentUrl;
        private readonly string _clientVersion;
        private readonly IConvexLogger _logger;
        private WebSocket _webSocket;
        private ConnectionState _state = ConnectionState.Disconnected;
        private bool _disposed;
        private string _authToken;
        private readonly object _stateLock = new object();
        private readonly Dictionary<int, TaskCompletionSource<FunctionResult>> _pendingRequests
            = new Dictionary<int, TaskCompletionSource<FunctionResult>>();
        private readonly Dictionary<string, QueryInfo> _querySet
            = new Dictionary<string, QueryInfo>();
        private int _nextRequestId = 1;
        private readonly object _requestLock = new object();
        private int _querySetVersion = 0;
        private int _nextQueryId = 0;
        private readonly object _querySetLock = new object();
        private int _identityVersion = 0;
        private TaskCompletionSource<bool> _connectionTcs;
        private int _reconnectAttempts = 0;
        private readonly bool _autoReconnect;
        private readonly int _maxReconnectAttempts;
        private CancellationTokenSource _reconnectCts;
        private readonly TimeSpan _heartbeatTimeout;
        private DateTime _lastReceiveUtc;
        private CancellationTokenSource _heartbeatCts;

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
        /// Event fired when connection state changes.
        /// </summary>
        public event Action<ConnectionState> OnConnectionStateChanged;

        /// <summary>
        /// Initializes a new instance of the WebSocketTransport class.
        /// </summary>
        /// <param name="deploymentUrl">The Convex deployment URL.</param>
        /// <param name="clientVersion">The client version identifier. Defaults to current protocol version.</param>
        public WebSocketTransport(
            string deploymentUrl,
            string clientVersion = null,
            bool autoReconnect = true,
            int maxReconnectAttempts = 5,
            TimeSpan? heartbeatTimeout = null,
            IConvexLogger logger = null)
        {
            if (string.IsNullOrWhiteSpace(deploymentUrl))
            {
                throw new ArgumentException("Deployment URL cannot be null or empty.", nameof(deploymentUrl));
            }

            _deploymentUrl = deploymentUrl;
            _clientVersion = clientVersion ?? ConvexVersion.ClientIdentifier;
            _autoReconnect = autoReconnect;
            _maxReconnectAttempts = Math.Max(0, maxReconnectAttempts);
            _heartbeatTimeout = heartbeatTimeout ?? TimeSpan.Zero;
            _logger = logger ?? new UnityConvexLogger();
        }

        public WebSocketTransport(
            string deploymentUrl,
            string clientVersion,
            bool autoReconnect,
            int maxReconnectAttempts,
            TimeSpan? heartbeatTimeout,
            bool enableLogging)
            : this(
                deploymentUrl,
                clientVersion,
                autoReconnect,
                maxReconnectAttempts,
                heartbeatTimeout,
                new UnityConvexLogger { IsEnabled = enableLogging })
        {
        }

        /// <summary>
        /// Gets or sets whether non-error logs are emitted.
        /// </summary>
        public bool EnableLogging
        {
            get => _logger.IsEnabled;
            set => _logger.IsEnabled = value;
        }

        public IConvexLogger Logger => _logger;

        /// <summary>
        /// Connects to the Convex deployment WebSocket endpoint.
        /// </summary>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WebSocketTransport));
            }

            lock (_stateLock)
            {
                if (_state == ConnectionState.Connected || _state == ConnectionState.Connecting || _state == ConnectionState.Reconnecting)
                {
                    return;
                }
                _state = ConnectionState.Connecting;
            }

            SetState(ConnectionState.Connecting);

            // Create TaskCompletionSource to wait for actual connection
            _connectionTcs = new TaskCompletionSource<bool>();

            try
            {
                // Convert HTTP(S) URL to WS(S) URL
                var wsUrl = _deploymentUrl.Replace("https://", "wss://").Replace("http://", "ws://");

                // Convex WebSocket endpoint requires API version in path: /api/{version}/sync
                // Using centralized protocol version from ConvexVersion
                wsUrl = $"{wsUrl}/api/{ConvexVersion.PROTOCOL_VERSION}/sync";

                _logger.Info($"[WebSocketTransport] Attempting connection to: {wsUrl}");
                _logger.Info($"[WebSocketTransport] Client version: {_clientVersion}");

                // Create WebSocket with websocket-sharp
                _logger.Info($"[WebSocketTransport] Creating WebSocket with URL: {wsUrl}");
                _webSocket = new WebSocket(wsUrl);

                // Configure TLS protocols for secure connections (TLS 1.2 + TLS 1.3)
                if (wsUrl.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        // Try to enable TLS 1.3 and TLS 1.2 via numeric cast
                        // TLS 1.3 = 12288 (0x3000) in SslProtocols enum
                        var tls13 = (SslProtocols)12288;
                        _webSocket.SslConfiguration.EnabledSslProtocols = tls13 | SslProtocols.Tls12;
                        _logger.Info("[WebSocketTransport] TLS configured: Tls13 | Tls12");
                    }
                    catch
                    {
                        // Fallback to TLS 1.2 if TLS 1.3 is not available on platform
                        _webSocket.SslConfiguration.EnabledSslProtocols = SslProtocols.Tls12;
                        _logger.Info("[WebSocketTransport] TLS configured: Tls12 (TLS 1.3 not available)");
                    }
                }

                // Register event handlers using websocket-sharp EventHandler pattern
                _webSocket.OnOpen += HandleWebSocketOpen;
                _webSocket.OnMessage += HandleWebSocketMessage;
                _webSocket.OnError += HandleWebSocketError;
                _webSocket.OnClose += HandleWebSocketClose;

                // Connect synchronously in background task
                await Task.Run(() => _webSocket.Connect());

                // Wait for HandleWebSocketOpen to fire, Authenticate to be sent, and Transition response
                // Use timeout to prevent hanging forever
                var timeoutTask = Task.Delay(10000); // 10 second timeout
                var completedTask = await Task.WhenAny(_connectionTcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException("Connection timeout waiting for Transition response from server");
                }

                await _connectionTcs.Task; // Re-await to propagate any exceptions
                _reconnectAttempts = 0;
            }
            catch (Exception ex)
            {
                SetState(ConnectionState.Disconnected);
                throw new InvalidOperationException($"Failed to connect WebSocket: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Executes a Convex query.
        /// </summary>
        public async Task<FunctionResult> QueryAsync(
            string functionName,
            ConvexValue.Object args = null,
            CancellationToken cancellationToken = default)
        {
            return await SendFunctionRequestAsync("Query", functionName, args, cancellationToken);
        }

        /// <summary>
        /// Executes a Convex mutation.
        /// </summary>
        public async Task<FunctionResult> MutationAsync(
            string functionName,
            ConvexValue.Object args = null,
            CancellationToken cancellationToken = default)
        {
            return await SendFunctionRequestAsync("Mutation", functionName, args, cancellationToken);
        }

        /// <summary>
        /// Executes a Convex action.
        /// </summary>
        public async Task<FunctionResult> ActionAsync(
            string functionName,
            ConvexValue.Object args = null,
            CancellationToken cancellationToken = default)
        {
            return await SendFunctionRequestAsync("Action", functionName, args, cancellationToken);
        }

        /// <summary>
        /// Subscribes to a Convex query and receives updates via callback.
        /// </summary>
        /// <returns>The query token that can be used to unsubscribe.</returns>
        public async Task<string> SubscribeAsync(
            string functionName,
            ConvexValue.Object args,
            Action<FunctionResult> onUpdate,
            CancellationToken cancellationToken = default)
        {
            if (_state != ConnectionState.Connected)
            {
                throw new InvalidOperationException("WebSocket is not connected");
            }

            if (onUpdate == null)
            {
                throw new ArgumentNullException(nameof(onUpdate));
            }

            string queryToken = GenerateQueryToken();
            int queryId;

            lock (_querySetLock)
            {
                queryId = _nextQueryId++;
                _querySet[queryToken] = new QueryInfo
                {
                    QueryId = queryId,
                    QueryToken = queryToken,
                    UdfPath = functionName,
                    Args = args,
                    Callback = onUpdate
                };
            }

            try
            {
                await SendModifyQuerySetAsync(
                    queryAdds: new[] { new QueryAddition { queryId = queryId, udfPath = functionName, args = new[] { SerializeArgs(args) } } },
                    queryRemoves: Array.Empty<int>()
                );

                return queryToken;
            }
            catch
            {
                lock (_querySetLock)
                {
                    _querySet.Remove(queryToken);
                }
                throw;
            }
        }

        /// <summary>
        /// Unsubscribes from a query subscription.
        /// </summary>
        public async Task UnsubscribeAsync(string queryToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(queryToken))
            {
                throw new ArgumentException("Query token cannot be null or empty.", nameof(queryToken));
            }

            int queryId;
            lock (_querySetLock)
            {
                if (!_querySet.TryGetValue(queryToken, out var queryInfo))
                {
                    return;
                }
                queryId = queryInfo.QueryId;
                _querySet.Remove(queryToken);
            }

            // Only send unsubscribe if connected
            if (_state == ConnectionState.Connected && _webSocket != null && _webSocket.ReadyState == WebSocketState.Open)
            {
                try
                {
                    await SendModifyQuerySetAsync(
                        queryAdds: Array.Empty<QueryAddition>(),
                        queryRemoves: new[] { queryId }
                    );
                }
                catch (Exception ex)
                {
                    _logger.Warning($"[WebSocketTransport] Failed to send unsubscribe (connection closed): {ex.Message}");
                }
            }
        }

        private async Task SendModifyQuerySetAsync(QueryAddition[] queryAdds, int[] queryRemoves)
        {
            int baseVersion;
            int newVersion;
            lock (_querySetLock)
            {
                baseVersion = _querySetVersion;
                newVersion = ++_querySetVersion;
            }

            var modifications = new List<object>();

            // Add query additions as "Add" modifications
            foreach (var add in queryAdds)
            {
                modifications.Add(new
                {
                    type = "Add",
                    queryId = add.queryId,
                    udfPath = add.udfPath,
                    args = add.args,
                    journal = (object)null
                });
            }

            // Add query removals as "Remove" modifications
            foreach (var queryId in queryRemoves)
            {
                modifications.Add(new
                {
                    type = "Remove",
                    queryId = queryId
                });
            }

            var request = new JObject
            {
                { "type", "ModifyQuerySet" },
                { "baseVersion", baseVersion },
                { "newVersion", newVersion },
                { "modifications", JArray.FromObject(modifications) }
            };

            await SendMessageAsync(request.ToString(Formatting.None));
        }

        private string GenerateQueryToken()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Sets the authentication token.
        /// </summary>
        public async Task SetAuthAsync(string token, CancellationToken cancellationToken = default)
        {
            _authToken = token;

            // If already connected, send auth update using Genies.Convex protocol
            if (_state == ConnectionState.Connected && _webSocket != null)
            {
                await SendAuthenticateMessage();
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Gets the current authentication token.
        /// </summary>
        /// <returns>The current auth token, or null if not set.</returns>
        public string GetCurrentAuthToken()
        {
            return _authToken;
        }

        /// <summary>
        /// Sends an Authenticate message to the Convex server following the official protocol.
        /// </summary>
        private async Task SendAuthenticateMessage()
        {
            if (_webSocket == null || _state != ConnectionState.Connected)
            {
                return;
            }

            var request = new JObject();
            request["type"] = "Authenticate";

            if (!string.IsNullOrEmpty(_authToken))
            {
                // User authentication with token
                request["tokenType"] = "User";
                request["value"] = _authToken;
                _logger.Info("[WebSocketTransport] Sending Authenticate message with token");
            }
            else
            {
                // No authentication
                request["tokenType"] = "None";
                _logger.Info("[WebSocketTransport] Sending Authenticate message without token");
            }

            request["baseVersion"] = _identityVersion;

            await SendMessageAsync(request.ToString(Formatting.None));
        }

        /// <summary>
        /// Closes the WebSocket connection.
        /// </summary>
        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            _reconnectCts?.Cancel();
            StopHeartbeatMonitor();

            if (_disposed || _state == ConnectionState.Closed)
            {
                return;
            }

            SetState(ConnectionState.Closed);

            if (_webSocket != null)
            {
                try
                {
                    // websocket-sharp Close is synchronous, wrap in Task
                    await Task.Run(() => _webSocket.Close(CloseStatusCode.Normal, "Client closing"));
                }
                catch (Exception ex)
                {
                    _logger.Warning($"[WebSocketTransport] Error closing WebSocket: {ex.Message}");
                }
            }

            SetState(ConnectionState.Closed);
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            StopHeartbeatMonitor();

            try
            {
                if (_webSocket != null)
                {
                    _webSocket.OnOpen -= HandleWebSocketOpen;
                    _webSocket.OnMessage -= HandleWebSocketMessage;
                    _webSocket.OnError -= HandleWebSocketError;
                    _webSocket.OnClose -= HandleWebSocketClose;
                }

                CloseAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // Ignore errors during disposal
            }

            _webSocket = null;
            _pendingRequests.Clear();

            lock (_querySetLock)
            {
                _querySet.Clear();
            }
        }

        private async Task<FunctionResult> SendFunctionRequestAsync(
            string requestType,
            string functionName,
            ConvexValue.Object args,
            CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WebSocketTransport));
            }

            if (_state != ConnectionState.Connected)
            {
                throw new InvalidOperationException($"Cannot send request. WebSocket state is {_state}");
            }

            int requestId;
            TaskCompletionSource<FunctionResult> tcs;

            lock (_requestLock)
            {
                requestId = _nextRequestId++;
                tcs = new TaskCompletionSource<FunctionResult>();
                _pendingRequests[requestId] = tcs;
            }

            try
            {
                var request = new JObject
                {
                    { "type", requestType },
                    { "requestId", requestId },
                    { "udfPath", functionName },
                    { "args", new JArray { SerializeArgs(args) } }
                };

                await SendMessageAsync(request.ToString(Formatting.None));

                // Wait for response with cancellation support
                using (cancellationToken.Register(() => tcs.TrySetCanceled()))
                {
                    return await tcs.Task;
                }
            }
            catch
            {
                // Clean up pending request on error
                lock (_requestLock)
                {
                    _pendingRequests.Remove(requestId);
                }
                throw;
            }
        }

        private async Task SendMessageAsync(string message)
        {
            if (_webSocket == null || _webSocket.ReadyState != WebSocketState.Open)
            {
                throw new InvalidOperationException($"WebSocket is not open (State: {_webSocket?.ReadyState.ToString() ?? "null"})");
            }

            _logger.Info($"[WebSocketTransport] → Sending: {message}");
            // websocket-sharp Send is synchronous, wrap in Task
            await Task.Run(() => _webSocket.Send(message));
        }

        private JToken SerializeArgs(ConvexValue.Object args)
        {
            if (args == null)
            {
                return new JObject();
            }

            string json = ValueSerializer.Serialize(args);
            return JToken.Parse(json);
        }

        private void UpdateLastReceiveUtc()
        {
            _lastReceiveUtc = DateTime.UtcNow;
        }

        private void StartHeartbeatMonitor()
        {
            if (_heartbeatTimeout <= TimeSpan.Zero)
            {
                return;
            }

            StopHeartbeatMonitor();
            UpdateLastReceiveUtc();

            _heartbeatCts = new CancellationTokenSource();
            var token = _heartbeatCts.Token;
            var checkIntervalMs = Math.Max(1000, Math.Min(_heartbeatTimeout.TotalMilliseconds / 2, 5000));

            Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(checkIntervalMs), token);

                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        if (_state != ConnectionState.Connected)
                        {
                            continue;
                        }

                        if (DateTime.UtcNow - _lastReceiveUtc > _heartbeatTimeout)
                        {
                            _logger.Warning("[WebSocketTransport] Heartbeat timeout detected, closing socket to trigger reconnect");
                            TriggerHeartbeatTimeoutClose();
                            return;
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // Ignore cancellation.
                }
            }, token);
        }

        private void StopHeartbeatMonitor()
        {
            if (_heartbeatCts == null)
            {
                return;
            }

            _heartbeatCts.Cancel();
            _heartbeatCts.Dispose();
            _heartbeatCts = null;
        }

        private void TriggerHeartbeatTimeoutClose()
        {
            if (_webSocket == null || _webSocket.ReadyState != WebSocketState.Open)
            {
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    _webSocket.Close(CloseStatusCode.Away, "Heartbeat timeout");
                }
                catch (Exception ex)
                {
                    _logger.Warning($"[WebSocketTransport] Heartbeat close failed: {ex.Message}");
                }
            });
        }

        private void HandleWebSocketOpen(object sender, EventArgs e)
        {
            _logger.Info("[WebSocketTransport] Connection opened");
            SetState(ConnectionState.Connected);
            UpdateLastReceiveUtc();
            StartHeartbeatMonitor();

            // Send Authenticate message immediately after connection opens
            // This follows the Convex protocol: connect first, then authenticate
            // Note: We wait for Transition response before signaling connection ready
            // We can call SendAuthenticateMessage directly from background thread since websocket-sharp Send() is thread-safe
            try
            {
                // Use Task.Run to avoid blocking the WebSocket callback thread
                Task.Run(async () =>
                {
                    try
                    {
                        await SendAuthenticateMessage();
                        _logger.Info("[WebSocketTransport] Authenticate message sent, waiting for Transition response...");
                        // Connection readiness will be signaled in HandleTransition()
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[WebSocketTransport] Failed to send Authenticate message: {ex.Message}");
                        _connectionTcs?.TrySetException(ex);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Error($"[WebSocketTransport] Error starting authentication task: {ex.Message}");
                _connectionTcs?.TrySetException(ex);
            }
        }

        private void HandleWebSocketMessage(object sender, MessageEventArgs e)
        {
            try
            {
                string message = e.Data;
                UpdateLastReceiveUtc();
                var json = JObject.Parse(message);
                string messageType = json["type"]?.Value<string>();
                if (!string.Equals(messageType, "Ping", StringComparison.Ordinal))
                {
                    _logger.Info($"[WebSocketTransport] ← Received: {message}");
                }

                switch (messageType)
                {
                    case "response":
                        HandleFunctionResponse(json);
                        break;

                    case "MutationResponse":
                        HandleFunctionResponse(json);
                        break;

                    case "ActionResponse":
                        HandleFunctionResponse(json);
                        break;

                    case "error":
                        HandleErrorMessage(json);
                        break;

                    case "AuthError":
                        HandleAuthError(json);
                        break;

                    case "FatalError":
                        HandleFatalError(json);
                        break;

                    case "Transition":
                        HandleTransition(json);
                        break;

                    case "TransitionChunk":
                        HandleTransitionChunk(json);
                        break;

                    case "Ping":
                        HandlePing();
                        break;

                    default:
                        _logger.Warning($"[WebSocketTransport] Unknown message type: {messageType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[WebSocketTransport] Error processing message: {ex.Message}");
            }
        }

        private void HandleFunctionResponse(JObject json)
        {
            int? requestId = json["requestId"]?.Value<int>();
            if (!requestId.HasValue)
            {
                _logger.Warning("[WebSocketTransport] Response missing requestId");
                return;
            }

            TaskCompletionSource<FunctionResult> tcs;
            lock (_requestLock)
            {
                if (!_pendingRequests.TryGetValue(requestId.Value, out tcs))
                {
                    _logger.Warning($"[WebSocketTransport] No pending request for ID {requestId}");
                    return;
                }
                _pendingRequests.Remove(requestId.Value);
            }

            try
            {
                // Check for new protocol format (success: boolean, result: object)
                bool? successFlag = json["success"]?.Value<bool>();

                if (successFlag.HasValue)
                {
                    // New protocol: { "success": true/false, "result": {...} } or { "success": false, "errorMessage": "..." }
                    if (successFlag.Value)
                    {
                        var resultToken = json["result"];
                        if (resultToken != null)
                        {
                            ConvexValue value = ValueSerializer.FromJToken(resultToken);
                            tcs.SetResult(new FunctionResult.Value(value));
                        }
                        else
                        {
                            tcs.SetException(new InvalidOperationException("Success response missing 'result' field"));
                        }
                    }
                    else
                    {
                        var errorDataToken = json["errorData"];
                        string errorMessage = ResolveErrorMessage(json, errorDataToken);

                        if (errorDataToken != null && errorDataToken.Type != JTokenType.Null)
                        {
                            ConvexValue errorData = ValueSerializer.FromJToken(errorDataToken);
                            tcs.SetResult(new FunctionResult.ConvexError(errorMessage, errorData));
                        }
                        else
                        {
                            tcs.SetResult(new FunctionResult.ErrorMessage(errorMessage));
                        }
                    }
                }
                else
                {
                    // Old protocol fallback: { "status": "success"/"error", "value": {...} }
                    string status = json["status"]?.Value<string>();

                    if (status == "success")
                    {
                        var valueToken = json["value"];
                        ConvexValue value = ValueSerializer.FromJToken(valueToken);
                        tcs.SetResult(new FunctionResult.Value(value));
                    }
                    else if (status == "error")
                    {
                        var errorDataToken = json["errorData"];
                        string errorMessage = ResolveErrorMessage(json, errorDataToken);

                        if (errorDataToken != null && errorDataToken.Type != JTokenType.Null)
                        {
                            ConvexValue errorData = ValueSerializer.FromJToken(errorDataToken);
                            tcs.SetResult(new FunctionResult.ConvexError(errorMessage, errorData));
                        }
                        else
                        {
                            tcs.SetResult(new FunctionResult.ErrorMessage(errorMessage));
                        }
                    }
                    else
                    {
                        tcs.SetException(new InvalidOperationException($"Unknown response status: {status}"));
                    }
                }
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }

        private void HandleErrorMessage(JObject json)
        {
            string errorMessage = json["message"]?.Value<string>() ?? "Unknown error";
            _logger.Error($"[WebSocketTransport] Server error: {errorMessage}");
        }

        private static string ResolveErrorMessage(JObject responseJson, JToken errorDataToken)
        {
            string errorMessage = responseJson["errorMessage"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                errorMessage = responseJson["message"]?.Value<string>();
            }

            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                errorMessage = TryExtractMessage(errorDataToken);
            }

            return string.IsNullOrWhiteSpace(errorMessage) ? "Unknown error" : errorMessage;
        }

        private static string TryExtractMessage(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            if (token.Type == JTokenType.String)
            {
                return token.Value<string>();
            }

            if (token is JObject obj)
            {
                string message = obj["message"]?.Value<string>()
                    ?? obj["errorMessage"]?.Value<string>()
                    ?? obj["error"]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return message;
                }

                return TryExtractMessage(obj["data"]);
            }

            return null;
        }

        private void HandleAuthError(JObject json)
        {
            string errorMessage = json["error"]?.Value<string>() ?? json["errorMessage"]?.Value<string>() ?? "Authentication failed";
            int? baseVersion = json["baseVersion"]?.Value<int>();

            if (baseVersion.HasValue)
            {
                _identityVersion = baseVersion.Value;
            }

            _logger.Error($"[WebSocketTransport] Authentication error: {errorMessage}");
            _logger.Error($"[WebSocketTransport] Identity version: {_identityVersion}");

            // Signal connection failure due to auth error
            _connectionTcs?.TrySetException(new InvalidOperationException($"Authentication error: {errorMessage}"));
        }

        private void HandleTransition(JObject json)
        {
            _logger.Info($"[WebSocketTransport] HandleTransition called. Message: {json.ToString(Newtonsoft.Json.Formatting.None)}");

            var endVersion = json["endVersion"];
            if (endVersion != null)
            {
                var querySetVersion = endVersion["querySet"]?.Value<int>();
                if (querySetVersion.HasValue)
                {
                    _logger.Info($"[WebSocketTransport] QuerySet version: {querySetVersion.Value}");
                }

                var identityVersion = endVersion["identity"]?.Value<int>();
                if (identityVersion.HasValue)
                {
                    _identityVersion = identityVersion.Value;
                    _logger.Info($"[WebSocketTransport] Identity version updated to: {_identityVersion}");
                }
            }

            var modifications = json["modifications"] as JArray;
            _logger.Info($"[WebSocketTransport] Modifications array: {(modifications == null ? "NULL" : $"Count={modifications.Count}")}");

            if (modifications != null && modifications.Count > 0)
            {
                _logger.Info($"[WebSocketTransport] Processing {modifications.Count} query modifications");

                foreach (var mod in modifications)
                {
                    try
                    {
                        var queryId = mod["queryId"]?.Value<int>();
                        _logger.Info($"[WebSocketTransport] Processing modification with queryId: {queryId}");

                        QueryInfo queryInfo = null;

                        // Try to find query by queryId first
                        if (queryId.HasValue)
                        {
                            lock (_querySetLock)
                            {
                                _logger.Info($"[WebSocketTransport] Searching _querySet ({_querySet.Count} entries) for queryId: {queryId.Value}");
                                queryInfo = _querySet.Values.FirstOrDefault(q => q.QueryId == queryId.Value);
                                _logger.Info($"[WebSocketTransport] Query lookup result: {(queryInfo == null ? "NOT FOUND" : $"FOUND with token {queryInfo.QueryToken}")}");
                            }
                        }

                        // Fallback to queryToken lookup
                        if (queryInfo == null)
                        {
                            string queryToken = mod["queryToken"]?.Value<string>();
                            if (!string.IsNullOrEmpty(queryToken))
                            {
                                lock (_querySetLock)
                                {
                                    _querySet.TryGetValue(queryToken, out queryInfo);
                                }
                            }
                        }

                        if (queryInfo == null)
                        {
                            _logger.Warning($"[WebSocketTransport] No query found for queryId: {queryId}");
                            continue;
                        }

                        _logger.Info($"[WebSocketTransport] Query found! Checking for 'value' field in modification...");
                        var valueToken = mod["value"];
                        _logger.Info($"[WebSocketTransport] valueToken: {(valueToken == null ? "NULL" : "EXISTS")}");
                        if (valueToken != null)
                        {
                            _logger.Info($"[WebSocketTransport] About to deserialize value with ValueSerializer.FromJToken...");
                            ConvexValue value = ValueSerializer.FromJToken(valueToken);
                            _logger.Info($"[WebSocketTransport] Deserialization successful! Enqueueing callback to main thread...");
                            UnityMainThreadDispatcher.Enqueue(() =>
                            {
                                _logger.Info($"[WebSocketTransport] MAIN THREAD CALLBACK EXECUTING for queryId: {queryInfo.QueryId}");
                                try
                                {
                                    _logger.Info($"[WebSocketTransport] Dispatching query update for queryId: {queryInfo.QueryId}");
                                    queryInfo.Callback(new FunctionResult.Value(value));
                                }
                                catch (Exception ex)
                                {
                                    _logger.Error($"[WebSocketTransport] Error in query callback for queryId {queryInfo.QueryId}: {ex.Message}");
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[WebSocketTransport] Error processing modification: {ex.Message}");
                    }
                }
            }

            if (_connectionTcs != null && !_connectionTcs.Task.IsCompleted)
            {
                _logger.Info("[WebSocketTransport] Received Transition response - connection fully established");
                _connectionTcs.TrySetResult(true);
            }
        }

        private void HandlePing()
        {
            // Server sent Ping to keep connection alive
            // NOTE: Convex Ping messages are one-way keepalive heartbeats - no response needed
            // The protocol does NOT expect a Pong response (it's not in the valid client message types)
            // Intentionally no-op to avoid noisy keepalive logs.
        }

        private void HandleFatalError(JObject json)
        {
            // FatalError represents a critical, unrecoverable error from the server
            string errorMessage = json["error"]?.Value<string>() ?? "Fatal error occurred";
            _logger.Error($"[WebSocketTransport] ============ FATAL ERROR ============");
            _logger.Error($"[WebSocketTransport] {errorMessage}");
            _logger.Error($"[WebSocketTransport] Connection will be terminated.");
            _logger.Error($"[WebSocketTransport] =====================================");

            // Signal connection failure
            _connectionTcs?.TrySetException(new InvalidOperationException($"Fatal error: {errorMessage}"));

            // Trigger reconnection logic by closing connection
            // Use Task.Run to avoid potential deadlock with main thread
            Task.Run(async () =>
            {
                try
                {
                    await CloseAsync();
                }
                catch (Exception ex)
                {
                    _logger.Error($"[WebSocketTransport] Error closing connection after fatal error: {ex.Message}");
                }
            });
        }

        private readonly Dictionary<string, List<string>> _transitionChunks = new Dictionary<string, List<string>>();

        private void HandleTransitionChunk(JObject json)
        {
            // TransitionChunk messages are used for large state transitions split into multiple parts
            try
            {
                string chunk = json["chunk"]?.Value<string>();
                int partNumber = json["partNumber"]?.Value<int>() ?? 0;
                int totalParts = json["totalParts"]?.Value<int>() ?? 0;
                int messageLength = json["messageLength"]?.Value<int>() ?? 0;

                // Use messageLength as a unique key for this chunked message
                string chunkKey = $"transition_{messageLength}_{totalParts}";

                if (!_transitionChunks.ContainsKey(chunkKey))
                {
                    _transitionChunks[chunkKey] = new List<string>(new string[totalParts]);
                }

                // Store the chunk at its position
                _transitionChunks[chunkKey][partNumber] = chunk;

                _logger.Info($"[WebSocketTransport] Received TransitionChunk {partNumber + 1}/{totalParts} ({chunk?.Length ?? 0} chars)");

                // Check if we have all chunks
                bool allChunksReceived = true;
                foreach (var c in _transitionChunks[chunkKey])
                {
                    if (string.IsNullOrEmpty(c))
                    {
                        allChunksReceived = false;
                        break;
                    }
                }

                if (allChunksReceived)
                {
                    // Reassemble the complete message
                    string completeMessage = string.Join("", _transitionChunks[chunkKey]);
                    _transitionChunks.Remove(chunkKey);

                    _logger.Info($"[WebSocketTransport] Reassembled complete Transition message ({completeMessage.Length} chars)");

                    // Parse and handle as a regular Transition message
                    var completeJson = JObject.Parse(completeMessage);
                    HandleTransition(completeJson);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[WebSocketTransport] Error processing TransitionChunk: {ex.Message}");
            }
        }

        private void HandleWebSocketError(object sender, ErrorEventArgs e)
        {
            string errorMsg = e.Message;
            _logger.Error($"[WebSocketTransport] ============ WebSocket Connection Error ============");
            _logger.Error($"[WebSocketTransport] Error Message: {errorMsg}");
            _logger.Error($"[WebSocketTransport] Deployment URL: {_deploymentUrl}");
            _logger.Error($"[WebSocketTransport] WebSocket State: {_webSocket?.ReadyState.ToString() ?? "null"}");
            _logger.Error($"[WebSocketTransport] Auth Token Set: {!string.IsNullOrEmpty(_authToken)}");
            if (e.Exception != null)
            {
                _logger.Error($"[WebSocketTransport] Exception: {e.Exception}");
            }

            SetState(ConnectionState.Disconnected);
            StopHeartbeatMonitor();

            // Signal connection failure if still waiting
            _connectionTcs?.TrySetException(new InvalidOperationException($"WebSocket error: {errorMsg}", e.Exception));
        }

        private void HandleWebSocketClose(object sender, CloseEventArgs e)
        {
            var closeReason = string.IsNullOrEmpty(e.Reason) ? "No reason provided" : e.Reason;
            var codeDescription = e.Code switch
            {
                1000 => "Normal Closure",
                1001 => "Going Away",
                1002 => "Protocol Error",
                1003 => "Unsupported Data",
                1005 => "No Status Rcvd (connection closed without status code)",
                1006 => "Abnormal Closure",
                1007 => "Invalid Frame Payload Data",
                1008 => "Policy Violation",
                1009 => "Message Too Big",
                1010 => "Mandatory Extension",
                1011 => "Internal Server Error",
                1015 => "TLS Handshake Failed",
                _ => "Unknown"
            };

            _logger.Info($"[WebSocketTransport] ============ Connection Closed ============");
            _logger.Info($"[WebSocketTransport] Close Code: {e.Code} ({codeDescription})");
            _logger.Info($"[WebSocketTransport] Reason: {closeReason}");
            _logger.Info($"[WebSocketTransport] Clean Closure: {e.WasClean}");
            _logger.Info($"[WebSocketTransport] =======================================");

            StopHeartbeatMonitor();

            bool shouldReconnect = _autoReconnect
                && _maxReconnectAttempts > 0
                && ShouldReconnect(e.Code, e.WasClean);

            if (shouldReconnect)
            {
                if (_reconnectAttempts >= _maxReconnectAttempts)
                {
                    _logger.Error($"[WebSocketTransport] Max reconnection attempts reached ({_maxReconnectAttempts})");
                    FinalizeClose($"WebSocket closed: {codeDescription} (Code {e.Code})");
                    return;
                }

                _logger.Info("[WebSocketTransport] Attempting reconnection");
                SetState(ConnectionState.Reconnecting);
                ScheduleReconnect();
            }
            else
            {
                FinalizeClose($"WebSocket closed: {codeDescription} (Code {e.Code})");
            }
        }

        private bool ShouldReconnect(ushort closeCode, bool wasClean)
        {
            if (closeCode == 1000 && wasClean)
            {
                _logger.Info("[WebSocketTransport] Normal closure, not reconnecting");
                return false;
            }

            if (closeCode == 1008)
            {
                _logger.Info("[WebSocketTransport] Policy violation, not reconnecting");
                return false;
            }

            if (closeCode == 1011)
            {
                _logger.Info("[WebSocketTransport] Server error, not reconnecting");
                return false;
            }

            _logger.Info($"[WebSocketTransport] Close code {closeCode} is recoverable, will attempt reconnection");
            return true;
        }

        private void ScheduleReconnect()
        {
            _reconnectCts?.Cancel();
            _reconnectCts = new CancellationTokenSource();

            var token = _reconnectCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        if (_reconnectAttempts >= _maxReconnectAttempts)
                        {
                            _logger.Error($"[WebSocketTransport] Max reconnection attempts reached ({_maxReconnectAttempts})");
                            FinalizeClose("WebSocket closed: Max reconnection attempts reached");
                            return;
                        }

                        _reconnectAttempts++;
                        int delayMs = GetReconnectDelayMs(_reconnectAttempts);
                        _logger.Info($"[WebSocketTransport] Scheduling reconnection in {delayMs}ms");

                        await Task.Delay(delayMs, token);

                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        SetState(ConnectionState.Reconnecting);
                        _logger.Info("[WebSocketTransport] Attempting to reconnect...");
                        try
                        {
                            await ConnectAsync(token);
                            _reconnectAttempts = 0;
                            _logger.Info("[WebSocketTransport] Reconnection successful");
                            return;
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[WebSocketTransport] Reconnection failed: {ex.Message}");
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // Ignore cancellation.
                }
            }, token);
        }

        private int GetReconnectDelayMs(int attempt)
        {
            int delayMs = (int)Math.Pow(2, attempt - 1) * 1000;
            return Math.Min(delayMs, 16000);
        }

        private void FinalizeClose(string reason)
        {
            SetState(ConnectionState.Closed);
            _connectionTcs?.TrySetException(new InvalidOperationException(reason));

            lock (_requestLock)
            {
                foreach (var tcs in _pendingRequests.Values)
                {
                    tcs.TrySetException(new InvalidOperationException("WebSocket connection closed"));
                }
                _pendingRequests.Clear();
            }
        }

        private void SetState(ConnectionState newState)
        {
            bool changed = false;

            lock (_stateLock)
            {
                if (_state != newState)
                {
                    _state = newState;
                    changed = true;
                }
            }

            if (changed)
            {
                try
                {
                    OnConnectionStateChanged?.Invoke(newState);
                }
                catch (Exception ex)
                {
                    _logger.Error($"[WebSocketTransport] Error in state changed handler: {ex.Message}");
                }
            }
        }
    }
}
