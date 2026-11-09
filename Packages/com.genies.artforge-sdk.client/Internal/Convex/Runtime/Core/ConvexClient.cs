using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Genies.Convex.Exceptions;
using Genies.Convex.Logging;
using Genies.Convex.Transport;
using Genies.Convex.Unity;
using Genies.Convex.Values;
using UnityEngine;

namespace Genies.Convex.Core
{
    /// <summary>
    /// WebSocket-based Convex client with real-time subscription support.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ConvexClient provides a full-featured interface to Convex deployments, including:
    /// - Query, Mutation, and Action execution
    /// - Real-time query subscriptions with automatic updates
    /// - WebSocket connectivity with automatic reconnection
    /// - Bearer token authentication
    /// - Unity coroutine support for all operations
    /// </para>
    /// <para>
    /// This client wraps WebSocketTransport and manages subscription lifecycle, value conversion,
    /// and main thread marshalling for Unity integration.
    /// </para>
    /// <para>
    /// Thread Safety: All public methods are thread-safe. Callbacks and events are automatically
    /// marshalled to Unity's main thread.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create client
    /// var client = new ConvexClient("https://example-lion-123.convex.cloud");
    ///
    /// // Execute query
    /// var messages = await client.QueryAsync&lt;List&lt;Message&gt;&gt;("messages:list");
    ///
    /// // Execute mutation
    /// await client.MutationAsync("messages:send", new {
    ///     author = "Unity Player",
    ///     body = "Hello from Unity!"
    /// });
    ///
    /// // Subscribe to updates
    /// var subscription = client.Subscribe("messages:list", null);
    /// subscription.OnNext(
    ///     result => UpdateUI(result),
    ///     error => Debug.LogError(error)
    /// );
    ///
    /// // Clean up
    /// subscription.Dispose();
    /// client.Dispose();
    /// </code>
    /// </example>
    internal class ConvexClient : IDisposable
    {
        private readonly WebSocketTransport _transport;
        private HttpTransport _httpTransport;
        private readonly ConvexClientOptions _options;
        private readonly IConvexLogger _logger;
        private readonly Dictionary<int, QuerySubscription> _subscriptions;
        private readonly object _subscriptionLock = new object();
        private int _nextSubscriptionId = 1;
        private bool _disposed;

        /// <summary>
        /// Gets the current connection state.
        /// </summary>
        public ConnectionState State => _transport.State;

        /// <summary>
        /// Occurs when the connection state changes.
        /// </summary>
        /// <remarks>
        /// Event handlers are invoked on Unity's main thread.
        /// </remarks>
        public event Action<ConnectionState> OnStateChanged;

        /// <summary>
        /// Occurs when an error is encountered during subscription updates or connection management.
        /// </summary>
        /// <remarks>
        /// Event handlers are invoked on Unity's main thread.
        /// This event does not fire for errors in Query/Mutation/Action operations,
        /// which are returned as exceptions from the async methods.
        /// </remarks>
#pragma warning disable 0067 // Event is declared but never invoked (intentional - errors handled via exceptions)
        public event Action<Exception> OnError;
#pragma warning restore 0067

        /// <summary>
        /// Initializes a new instance of the ConvexClient class with a deployment URL.
        /// </summary>
        /// <param name="deploymentUrl">
        /// The Convex deployment URL (e.g., "https://example-lion-123.convex.cloud").
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when deploymentUrl is null or empty.
        /// </exception>
        /// <example>
        /// <code>
        /// var client = new ConvexClient("https://example-lion-123.convex.cloud");
        /// </code>
        /// </example>
        public ConvexClient(string deploymentUrl)
            : this(new ConvexClientOptions { DeploymentUrl = deploymentUrl })
        {
        }

        /// <summary>
        /// Initializes a new instance of the ConvexClient class with configuration options.
        /// </summary>
        /// <param name="options">The client configuration options.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when options is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when options contains invalid configuration.
        /// </exception>
        /// <example>
        /// <code>
        /// var client = new ConvexClient(new ConvexClientOptions
        /// {
        ///     DeploymentUrl = "https://example.convex.cloud",
        ///     TimeoutMs = 60000,
        ///     AutoReconnect = true,
        ///     Debug = false
        /// });
        /// </code>
        /// </example>
        public ConvexClient(ConvexClientOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();

            _subscriptions = new Dictionary<int, QuerySubscription>();
            _logger = _options.Logger ?? new UnityConvexLogger();
            _logger.IsEnabled = _options.Debug;
            UnityMainThreadDispatcher.SetLogger(_logger);
            _transport = new WebSocketTransport(
                _options.DeploymentUrl,
                _options.ClientVersion,
                _options.AutoReconnect,
                _options.MaxReconnectAttempts,
                _options.WebSocketHeartbeatTimeout,
                _logger);

            InitializeEvents();
        }

        #region Core Operations

        /// <summary>
        /// Executes a Convex query function and returns the result.
        /// </summary>
        /// <typeparam name="T">The expected result type.</typeparam>
        /// <param name="name">
        /// The query function name (e.g., "messages:list").
        /// </param>
        /// <param name="args">
        /// Optional query arguments as a C# object. Will be converted to ConvexValue.
        /// Pass null or omit for no arguments.
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The query result deserialized to type T.</returns>
        /// <exception cref="ConvexError">
        /// Thrown when the query function throws a ConvexError.
        /// </exception>
        /// <exception cref="ConvexExecutionException">
        /// Thrown when the query execution fails on the server.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown when the operation exceeds the configured timeout.
        /// </exception>
        /// <example>
        /// <code>
        /// // Simple query
        /// var count = await client.QueryAsync&lt;int&gt;("messages:count");
        ///
        /// // Query with arguments
        /// var messages = await client.QueryAsync&lt;List&lt;Message&gt;&gt;("messages:list", new {
        ///     limit = 10,
        ///     author = "Unity Player"
        /// });
        /// </code>
        /// </example>
        public async Task<T> QueryAsync<T>(string name, object args = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Query name cannot be null or empty.", nameof(name));
            }

            // HTTP queries (match official Python client): POST /api/query
            if (_httpTransport == null)
            {
                _httpTransport = new HttpTransport(_options.DeploymentUrl, _options.ClientVersion, _logger);

                // Set auth on newly created HttpTransport if auth was previously set on WebSocketTransport
                var currentAuth = _transport.GetCurrentAuthToken();
                if (!string.IsNullOrEmpty(currentAuth))
                {
                    _httpTransport.SetAuth(currentAuth);
                }
            }

            if (!_httpTransport.IsConnected)
            {
                await _httpTransport.ConnectAsync(_options.DeploymentUrl, cancellationToken);
            }

            var convexArgs = ConvertArgs(args);
            var request = new QueryRequest(name, convexArgs);
            var result = await _httpTransport.SendRequestAsync(request, _options.DefaultTimeout, cancellationToken);

            return ConvertResult<T>(result);
        }

        /// <summary>
        /// Executes a Convex mutation function and returns the result.
        /// </summary>
        /// <typeparam name="T">The expected result type.</typeparam>
        /// <param name="name">
        /// The mutation function name (e.g., "messages:send").
        /// </param>
        /// <param name="args">
        /// Optional mutation arguments as a C# object. Will be converted to ConvexValue.
        /// Pass null or omit for no arguments.
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The mutation result deserialized to type T.</returns>
        /// <exception cref="ConvexError">
        /// Thrown when the mutation function throws a ConvexError.
        /// </exception>
        /// <exception cref="ConvexExecutionException">
        /// Thrown when the mutation execution fails on the server.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown when the operation exceeds the configured timeout.
        /// </exception>
        /// <example>
        /// <code>
        /// // Mutation with arguments
        /// var messageId = await client.MutationAsync&lt;string&gt;("messages:send", new {
        ///     author = "Unity Player",
        ///     body = "Hello from Unity!"
        /// });
        ///
        /// // Mutation without return value
        /// await client.MutationAsync&lt;object&gt;("messages:delete", new { id = messageId });
        /// </code>
        /// </example>
        public async Task<T> MutationAsync<T>(string name, object args = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Mutation name cannot be null or empty.", nameof(name));
            }

            await EnsureConnectedAsync(cancellationToken);

            var convexArgs = ConvertArgs(args);
            var result = await _transport.MutationAsync(name, convexArgs, cancellationToken);

            return ConvertResult<T>(result);
        }

        /// <summary>
        /// Executes a Convex action function and returns the result.
        /// </summary>
        /// <typeparam name="T">The expected result type.</typeparam>
        /// <param name="name">
        /// The action function name (e.g., "actions:sendEmail").
        /// </param>
        /// <param name="args">
        /// Optional action arguments as a C# object. Will be converted to ConvexValue.
        /// Pass null or omit for no arguments.
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The action result deserialized to type T.</returns>
        /// <exception cref="ConvexError">
        /// Thrown when the action function throws a ConvexError.
        /// </exception>
        /// <exception cref="ConvexExecutionException">
        /// Thrown when the action execution fails on the server.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown when the operation exceeds the configured timeout.
        /// </exception>
        /// <example>
        /// <code>
        /// // Execute action
        /// var response = await client.ActionAsync&lt;EmailResponse&gt;("actions:sendEmail", new {
        ///     to = "user@example.com",
        ///     subject = "Hello",
        ///     body = "Email from Unity"
        /// });
        /// </code>
        /// </example>
        public async Task<T> ActionAsync<T>(string name, object args = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Action name cannot be null or empty.", nameof(name));
            }

            await EnsureConnectedAsync(cancellationToken);

            var convexArgs = ConvertArgs(args);
            var result = await _transport.ActionAsync(name, convexArgs, cancellationToken);

            return ConvertResult<T>(result);
        }

        #endregion

        #region Subscriptions

        /// <summary>
        /// Subscribes to a Convex query and receives real-time updates.
        /// </summary>
        /// <param name="name">
        /// The query function name (e.g., "messages:list").
        /// </param>
        /// <param name="args">
        /// Optional query arguments as a C# object. Will be converted to ConvexValue.
        /// Pass null or omit for no arguments.
        /// </param>
        /// <returns>
        /// An IQuerySubscription that provides access to query updates.
        /// Must be disposed when no longer needed to free resources.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when name is null or empty.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected or has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The subscription begins immediately and will receive the initial query result
        /// followed by updates whenever the result changes. Updates are delivered on
        /// Unity's main thread.
        /// </para>
        /// <para>
        /// The subscription remains active across reconnections. If the WebSocket connection
        /// is lost and successfully reconnected, the subscription will automatically be
        /// re-established.
        /// </para>
        /// <para>
        /// Always dispose subscriptions when no longer needed to prevent memory leaks
        /// and unnecessary server load.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Create subscription
        /// var subscription = client.Subscribe("messages:list", new { limit = 10 });
        ///
        /// // Receive updates
        /// subscription.OnNext(
        ///     result =>
        ///     {
        ///         var messages = result.As&lt;ConvexValue.Array&gt;();
        ///         UpdateChatUI(messages);
        ///     },
        ///     error => Debug.LogError($"Subscription error: {error.Message}")
        /// );
        ///
        /// // Later: clean up
        /// subscription.Dispose();
        /// </code>
        /// </example>
        public IQuerySubscription Subscribe(string name, object args = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Query name cannot be null or empty.", nameof(name));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ConvexClient));
            }

            EnsureConnected();

            var convexArgs = ConvertArgs(args);

            int subscriptionId = Interlocked.Increment(ref _nextSubscriptionId);

            // Create subscription before registering with transport
            var subscription = new QuerySubscription(subscriptionId, _logger, (result) =>
            {
                // This will be called when transport receives updates
            }, () =>
            {
                UnsubscribeInternal(subscriptionId);
            });

            lock (_subscriptionLock)
            {
                _subscriptions[subscriptionId] = subscription;
            }

            _logger.Info($"[ConvexClient] About to start Task.Run for subscription: {name}, subscriptionId: {subscriptionId}");

            // Subscribe to transport using async pattern
            _ = Task.Run(async () =>
            {
                _logger.Info($"[ConvexClient] Task.Run lambda ENTERED for subscription: {name}");
                try
                {
                    _logger.Info($"[ConvexClient] Starting subscription for: {name}");
                    string queryToken = await _transport.SubscribeAsync(name, convexArgs, subscription.OnTransportUpdate);
                    _logger.Info($"[ConvexClient] Subscription successful, queryToken: {queryToken}");
                    subscription.SetQueryToken(queryToken, _transport);
                }
                catch (Exception ex)
                {
                    _logger.Error($"[ConvexClient] Subscription failed: {ex.Message}");
                    _logger.Error($"[ConvexClient] Stack trace: {ex.StackTrace}");
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        subscription.OnTransportError(ex);
                    });
                }
            });

            return subscription;
        }

        private void UnsubscribeInternal(int subscriptionId)
        {
            lock (_subscriptionLock)
            {
                _subscriptions.Remove(subscriptionId);
            }
        }

        #endregion

        #region Authentication

        /// <summary>
        /// Sets the authentication token for subsequent Convex function calls.
        /// </summary>
        /// <param name="token">
        /// The authentication token (typically a JWT from an auth provider).
        /// Pass null to clear authentication.
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <remarks>
        /// <para>
        /// The authentication token is sent with all subsequent Query, Mutation, and Action calls.
        /// Subscriptions created after calling SetAuth will use the new token.
        /// Existing subscriptions will automatically use the new token on reconnection.
        /// </para>
        /// <para>
        /// If the connection is not active, the token is stored and will be sent
        /// when the connection is established.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Set auth token from your auth provider
        /// await client.SetAuthAsync(jwtToken);
        ///
        /// // Now queries run with authentication
        /// var userData = await client.QueryAsync&lt;User&gt;("users:me");
        ///
        /// // Clear auth (logout)
        /// await client.ClearAuthAsync();
        /// </code>
        /// </example>
        public async Task SetAuthAsync(string token, CancellationToken cancellationToken = default)
        {
            await _transport.SetAuthAsync(token, cancellationToken);
            _httpTransport?.SetAuth(token);
        }

        /// <summary>
        /// Clears the current authentication token.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <remarks>
        /// Equivalent to calling SetAuthAsync(null). Subsequent function calls will be unauthenticated.
        /// </remarks>
        /// <example>
        /// <code>
        /// await client.ClearAuthAsync();
        /// </code>
        /// </example>
        public async Task ClearAuthAsync(CancellationToken cancellationToken = default)
        {
            await _transport.SetAuthAsync(null, cancellationToken);
            _httpTransport?.ClearAuth();
        }

        /// <summary>
        /// Sets admin authentication for the deployment.
        /// </summary>
        /// <param name="adminKey">The admin key from the Convex dashboard.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <exception cref="NotImplementedException">
        /// Currently not implemented in WebSocket transport.
        /// </exception>
        /// <remarks>
        /// Admin auth is typically not required for normal client operations.
        /// Use SetAuthAsync with a JWT for user authentication instead.
        /// </remarks>
        public Task SetAdminAuthAsync(string adminKey, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException(
                "Admin auth is not yet supported in WebSocket client. " +
                "Use ConvexHttpClient for admin operations or SetAuthAsync for user authentication.");
        }

        #endregion

        #region Connection Management

        /// <summary>
        /// Explicitly connects to the Convex deployment.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <remarks>
        /// This method is typically not needed as the client automatically connects
        /// when the first operation is performed. Call this method if you want to
        /// establish the connection proactively.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown when already connected or connection fails.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown when connection times out.
        /// </exception>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            await _transport.ConnectAsync(cancellationToken);
        }

        /// <summary>
        /// Disconnects from the Convex deployment.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <remarks>
        /// <para>
        /// Gracefully closes the WebSocket connection. All active subscriptions
        /// will be paused until reconnection (if AutoReconnect is enabled) or
        /// disposed if the client is disposed.
        /// </para>
        /// <para>
        /// The client can be reconnected by calling ConnectAsync or by performing
        /// any operation that requires a connection.
        /// </para>
        /// </remarks>
        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            await _transport.CloseAsync(cancellationToken);
        }

        #endregion

        #region Unity Coroutine Support

        /// <summary>
        /// Executes a query as a Unity coroutine.
        /// </summary>
        /// <typeparam name="T">The expected result type.</typeparam>
        /// <param name="name">The query function name.</param>
        /// <param name="args">Optional query arguments.</param>
        /// <param name="onSuccess">Callback invoked on success with the result.</param>
        /// <param name="onError">Callback invoked on error with the exception.</param>
        /// <returns>An IEnumerator suitable for Unity's StartCoroutine.</returns>
        /// <example>
        /// <code>
        /// StartCoroutine(client.QueryCoroutine&lt;List&lt;Message&gt;&gt;(
        ///     "messages:list",
        ///     null,
        ///     messages => UpdateUI(messages),
        ///     error => Debug.LogError(error.Message)
        /// ));
        /// </code>
        /// </example>
        public IEnumerator QueryCoroutine<T>(
            string name,
            object args,
            Action<T> onSuccess,
            Action<Exception> onError)
        {
            var task = QueryAsync<T>(name, args);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                onError?.Invoke(task.Exception?.GetBaseException() ?? task.Exception);
            }
            else if (task.IsCompletedSuccessfully)
            {
                onSuccess?.Invoke(task.Result);
            }
        }

        /// <summary>
        /// Executes a mutation as a Unity coroutine.
        /// </summary>
        /// <typeparam name="T">The expected result type.</typeparam>
        /// <param name="name">The mutation function name.</param>
        /// <param name="args">Optional mutation arguments.</param>
        /// <param name="onSuccess">Callback invoked on success with the result.</param>
        /// <param name="onError">Callback invoked on error with the exception.</param>
        /// <returns>An IEnumerator suitable for Unity's StartCoroutine.</returns>
        /// <example>
        /// <code>
        /// StartCoroutine(client.MutationCoroutine&lt;string&gt;(
        ///     "messages:send",
        ///     new { author = "Unity", body = "Hello" },
        ///     id => Debug.Log($"Sent message {id}"),
        ///     error => Debug.LogError(error.Message)
        /// ));
        /// </code>
        /// </example>
        public IEnumerator MutationCoroutine<T>(
            string name,
            object args,
            Action<T> onSuccess,
            Action<Exception> onError)
        {
            var task = MutationAsync<T>(name, args);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                onError?.Invoke(task.Exception?.GetBaseException() ?? task.Exception);
            }
            else if (task.IsCompletedSuccessfully)
            {
                onSuccess?.Invoke(task.Result);
            }
        }

        /// <summary>
        /// Executes an action as a Unity coroutine.
        /// </summary>
        /// <typeparam name="T">The expected result type.</typeparam>
        /// <param name="name">The action function name.</param>
        /// <param name="args">Optional action arguments.</param>
        /// <param name="onSuccess">Callback invoked on success with the result.</param>
        /// <param name="onError">Callback invoked on error with the exception.</param>
        /// <returns>An IEnumerator suitable for Unity's StartCoroutine.</returns>
        /// <example>
        /// <code>
        /// StartCoroutine(client.ActionCoroutine&lt;bool&gt;(
        ///     "actions:sendEmail",
        ///     new { to = "user@example.com" },
        ///     success => Debug.Log("Email sent"),
        ///     error => Debug.LogError(error.Message)
        /// ));
        /// </code>
        /// </example>
        public IEnumerator ActionCoroutine<T>(
            string name,
            object args,
            Action<T> onSuccess,
            Action<Exception> onError)
        {
            var task = ActionAsync<T>(name, args);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                onError?.Invoke(task.Exception?.GetBaseException() ?? task.Exception);
            }
            else if (task.IsCompletedSuccessfully)
            {
                onSuccess?.Invoke(task.Result);
            }
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Enables or disables debug logging.
        /// </summary>
        /// <param name="enabled">True to enable debug logging, false to disable.</param>
        /// <remarks>
        /// Debug logging provides detailed information about WebSocket messages,
        /// connection state changes, and internal operations. Useful for troubleshooting
        /// but may impact performance.
        /// </remarks>
        public void SetDebug(bool enabled)
        {
            _options.Debug = enabled;
            _logger.IsEnabled = enabled;
            if (enabled)
            {
                _logger.Info("[ConvexClient] Debug mode enabled");
            }
            _httpTransport?.SetDebug(enabled);
        }

        #endregion

        #region Private Methods

        private void InitializeEvents()
        {
            _transport.OnConnectionStateChanged += (state) =>
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    OnStateChanged?.Invoke(state);
                });
            };
        }

        private void EnsureConnected()
        {
            if (_transport.State != ConnectionState.Connected)
            {
                throw new InvalidOperationException($"Not connected. Current state: {_transport.State}");
            }
        }

        private async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            if (_transport.State != ConnectionState.Connected)
            {
                await _transport.ConnectAsync(cancellationToken);
            }
        }

        private ConvexValue.Object ConvertArgs(object args)
        {
            if (args == null)
            {
                return new ConvexValue.Object();
            }

            // If already a ConvexValue.Object, return as-is
            if (args is ConvexValue.Object convexObj)
            {
                return convexObj;
            }

            // Convert using ValueSerializer
            var convexValue = ValueSerializer.FromObject(args);

            // Must be an object type for function arguments
            if (convexValue is ConvexValue.Object obj)
            {
                return obj;
            }

            throw new ArgumentException(
                $"Arguments must serialize to a Convex object type, got {convexValue?.GetType().Name ?? "null"}",
                nameof(args));
        }

        private T ConvertResult<T>(FunctionResult result)
        {
            switch (result)
            {
                case FunctionResult.Value valueResult:
                    // If T is ConvexValue, return the value directly without conversion
                    // This avoids trying to deserialize to the abstract ConvexValue type
                    if (typeof(T) == typeof(ConvexValue))
                    {
                        return (T)(object)valueResult.Result;
                    }

                    // Convert ConvexValue to target type T
                    return ValueSerializer.ToObject<T>(valueResult.Result);

                case FunctionResult.ErrorMessage errorResult:
                    throw new ConvexExecutionException(errorResult.Message);

                case FunctionResult.ConvexError convexErrorResult:
                    throw new ConvexError(convexErrorResult.Message, convexErrorResult.Data);

                default:
                    throw new InvalidOperationException($"Unknown result type: {result?.GetType().Name ?? "null"}");
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Releases all resources used by the ConvexClient.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Disposes all active subscriptions, closes the WebSocket connection,
        /// and releases managed resources. After disposal, the client cannot be reused.
        /// </para>
        /// <para>
        /// Always dispose the client when it is no longer needed to prevent resource leaks.
        /// In Unity, this is typically done in OnDestroy() of your MonoBehaviour.
        /// </para>
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Dispose all active subscriptions
            QuerySubscription[] subscriptionsToDispose;
            lock (_subscriptionLock)
            {
                subscriptionsToDispose = new QuerySubscription[_subscriptions.Count];
                _subscriptions.Values.CopyTo(subscriptionsToDispose, 0);
                _subscriptions.Clear();
            }

            foreach (var subscription in subscriptionsToDispose)
            {
                try
                {
                    subscription.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Error($"[ConvexClient] Error disposing subscription: {ex.Message}");
                }
            }

            // Dispose transports
            _httpTransport?.Dispose();
            _transport?.Dispose();

            GC.SuppressFinalize(this);
        }

        #endregion
    }

    #region QuerySubscription Implementation

    /// <summary>
    /// Internal implementation of IQuerySubscription.
    /// </summary>
    internal class QuerySubscription : IQuerySubscription
    {
        private readonly int _id;
        private readonly Action _onDispose;
        private readonly IConvexLogger _logger;
        private bool _disposed;
        private Action<ConvexValue> _onResult;
        private Action<Exception> _onError;
        private string _queryToken;
        private WebSocketTransport _transport;
        private readonly Queue<FunctionResult> _pendingUpdates = new Queue<FunctionResult>();
        private readonly object _callbackLock = new object();

        public int Id => _id;

        public QuerySubscription(int id, IConvexLogger logger, Action<FunctionResult> onUpdate, Action onDispose)
        {
            _id = id;
            _onDispose = onDispose;
            _logger = logger ?? new UnityConvexLogger();
            // Note: onUpdate parameter is intentionally unused here.
            // The actual update callback is set later via OnNext() method.
            // This design allows users to configure callbacks after subscription creation.
        }

        public void SetQueryToken(string token, WebSocketTransport transport)
        {
            _queryToken = token;
            _transport = transport;
        }

        public void OnNext(Action<ConvexValue> onResult, Action<Exception> onError)
        {
            if (onResult == null)
            {
                throw new ArgumentNullException(nameof(onResult));
            }

            if (onError == null)
            {
                throw new ArgumentNullException(nameof(onError));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(QuerySubscription));
            }

            Queue<FunctionResult> pendingToProcess = null;

            lock (_callbackLock)
            {
                _onResult = onResult;
                _onError = onError;

                // Collect any pending updates that arrived before callbacks were set
                if (_pendingUpdates.Count > 0)
                {
                    _logger.Info($"[QuerySubscription] Processing {_pendingUpdates.Count} pending updates for subscription {_id}");
                    pendingToProcess = new Queue<FunctionResult>(_pendingUpdates);
                    _pendingUpdates.Clear();
                }
            }

            // Process pending updates outside the lock to avoid potential deadlocks
            if (pendingToProcess != null)
            {
                foreach (var result in pendingToProcess)
                {
                    OnTransportUpdate(result);
                }
            }
        }

        public void OnTransportUpdate(FunctionResult result)
        {
            if (_disposed)
            {
                return;
            }

            bool shouldBuffer = false;
            bool hasCallbacks = false;

            lock (_callbackLock)
            {
                hasCallbacks = _onResult != null && _onError != null;

                if (!hasCallbacks)
                {
                    // Buffer the update if callbacks aren't set yet
                    _pendingUpdates.Enqueue(result);
                    shouldBuffer = true;
                }
            }

            _logger.Info($"[QuerySubscription] OnTransportUpdate called for subscription {_id}. Callbacks set: {hasCallbacks}, Buffering: {shouldBuffer}");

            if (shouldBuffer)
            {
                _logger.Warning($"[QuerySubscription] Subscription {_id} callbacks not set yet, buffering update. Total buffered: {_pendingUpdates.Count}");
                return;
            }

            // Process result on main thread
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (_disposed)
                {
                    _logger.Info($"[QuerySubscription] Subscription {_id} is disposed, ignoring update");
                    return;
                }

                Action<ConvexValue> currentOnResult;
                Action<Exception> currentOnError;

                lock (_callbackLock)
                {
                    currentOnResult = _onResult;
                    currentOnError = _onError;
                }

                if (currentOnResult == null || currentOnError == null)
                {
                    _logger.Warning($"[QuerySubscription] Subscription {_id} callbacks became null, ignoring update");
                    return;
                }

                try
                {
                    switch (result)
                    {
                        case FunctionResult.Value valueResult:
                            _logger.Info($"[QuerySubscription] Calling _onResult callback for subscription {_id}");
                            currentOnResult(valueResult.Result);
                            break;

                        case FunctionResult.ErrorMessage errorResult:
                            _logger.Info($"[QuerySubscription] Calling _onError callback (ErrorMessage) for subscription {_id}");
                            currentOnError(new ConvexExecutionException(errorResult.Message));
                            break;

                        case FunctionResult.ConvexError convexErrorResult:
                            _logger.Info($"[QuerySubscription] Calling _onError callback (ConvexError) for subscription {_id}");
                            currentOnError(new ConvexError(convexErrorResult.Message, convexErrorResult.Data));
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"[QuerySubscription] Error processing result: {ex}");
                    try
                    {
                        currentOnError?.Invoke(ex);
                    }
                    catch (Exception callbackEx)
                    {
                        _logger.Error($"[QuerySubscription] Error in error callback: {callbackEx}");
                    }
                }
            });
        }

        public void OnTransportError(Exception ex)
        {
            if (_disposed)
            {
                return;
            }

            // Error callback on main thread
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (!_disposed && _onError != null)
                {
                    _onError(ex);
                }
            });
        }

        public IEnumerator Subscribe(Action<ConvexValue> onUpdate, Action<Exception> onError)
        {
            var queue = new Queue<ConvexValue>();
            var errors = new Queue<Exception>();

            OnNext(
                value => queue.Enqueue(value),
                error => errors.Enqueue(error)
            );

            while (!_disposed)
            {
                // Process queued updates
                if (queue.Count > 0)
                {
                    var value = queue.Dequeue();
                    onUpdate?.Invoke(value);
                }

                // Process queued errors
                if (errors.Count > 0)
                {
                    var error = errors.Dequeue();
                    onError?.Invoke(error);
                }

                yield return null;
            }
        }

        public void Unsubscribe()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Send unsubscribe if we have a query token
            if (!string.IsNullOrEmpty(_queryToken) && _transport != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _transport.UnsubscribeAsync(_queryToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[QuerySubscription] Error unsubscribing: {ex.Message}");
                    }
                });
            }

            _onResult = null;
            _onError = null;
            _onDispose?.Invoke();
        }
    }

    #endregion
}
