using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Genies.Convex.Exceptions;
using Genies.Convex.Transport;
using Genies.Convex.Values;
using UnityEngine;

namespace Genies.Convex.Core
{
    /// <summary>
    /// Simple HTTP-only client for Convex that wraps HttpTransport for convenience.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This client provides a simplified interface for making queries, mutations, and actions
    /// against a Convex backend using HTTP transport. It does not support subscriptions
    /// (which require WebSocket transport).
    /// </para>
    /// <para>
    /// The client provides three levels of API:
    /// <list type="bullet">
    /// <item><description>Async methods (QueryAsync, MutationAsync, ActionAsync) for use in async contexts</description></item>
    /// <item><description>Synchronous blocking methods (Query, Mutation, Action) for simple use cases</description></item>
    /// <item><description>Unity coroutine wrappers (QueryCoroutine, MutationCoroutine, ActionCoroutine) for Unity integration</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Thread Safety: This class is thread-safe for concurrent requests.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Initialize client
    /// var client = new ConvexHttpClient("https://example.convex.cloud");
    /// client.SetAuth("your-jwt-token");
    /// client.SetDebug(true);
    ///
    /// // Async usage
    /// var messages = await client.QueryAsync&lt;List&lt;Message&gt;&gt;("messages:list", new { limit = 10 });
    ///
    /// // Synchronous usage
    /// var user = client.Query&lt;User&gt;("users:get", new { id = "user123" });
    ///
    /// // Unity coroutine usage
    /// StartCoroutine(client.QueryCoroutine&lt;User&gt;(
    ///     "users:get",
    ///     new { id = "user123" },
    ///     onSuccess: user => Debug.Log($"User: {user.Name}"),
    ///     onError: error => Debug.LogError($"Error: {error.Message}")
    /// ));
    /// </code>
    /// </example>
    internal class ConvexHttpClient : IDisposable
    {
        private readonly HttpTransport _transport;
        private readonly string _deploymentUrl;
        private readonly TimeSpan _defaultTimeout;
        private bool _disposed;

        /// <summary>
        /// Gets the deployment URL this client is connected to.
        /// </summary>
        public string DeploymentUrl => _deploymentUrl;

        /// <summary>
        /// Gets a value indicating whether the client is connected.
        /// </summary>
        public bool IsConnected => _transport?.IsConnected ?? false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexHttpClient"/> class with a deployment URL.
        /// </summary>
        /// <param name="deploymentUrl">The Convex deployment URL (e.g., https://example.convex.cloud).</param>
        /// <param name="timeout">Optional default timeout for requests (default: 30 seconds).</param>
        /// <exception cref="ArgumentNullException">Thrown when deploymentUrl is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when deploymentUrl is not a valid URL.</exception>
        /// <example>
        /// <code>
        /// var client = new ConvexHttpClient("https://example.convex.cloud");
        /// var clientWithTimeout = new ConvexHttpClient("https://example.convex.cloud", TimeSpan.FromSeconds(60));
        /// </code>
        /// </example>
        public ConvexHttpClient(string deploymentUrl, TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(deploymentUrl))
            {
                throw new ArgumentNullException(nameof(deploymentUrl), "Deployment URL cannot be null or empty");
            }

            _deploymentUrl = deploymentUrl;
            _defaultTimeout = timeout ?? TimeSpan.FromSeconds(30);
            _transport = new HttpTransport(deploymentUrl);

            // Auto-connect for HTTP transport (it's connectionless)
            _transport.ConnectAsync(deploymentUrl).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexHttpClient"/> class with options.
        /// </summary>
        /// <param name="options">The client configuration options.</param>
        /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
        /// <exception cref="ArgumentException">Thrown when options are invalid.</exception>
        /// <example>
        /// <code>
        /// var options = new ConvexClientOptions
        /// {
        ///     DeploymentUrl = "https://example.convex.cloud",
        ///     ClientVersion = "1.0.0",
        ///     EnableDebugLogs = true,
        ///     DefaultTimeout = TimeSpan.FromSeconds(60)
        /// };
        /// var client = new ConvexHttpClient(options);
        /// </code>
        /// </example>
        public ConvexHttpClient(ConvexClientOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            options.Validate();

            _deploymentUrl = options.DeploymentUrl;
            _defaultTimeout = options.DefaultTimeout;
            _transport = new HttpTransport(options.DeploymentUrl, options.ClientVersion);

            if (options.EnableDebugLogs)
            {
                _transport.SetDebug(true);
            }

            // Auto-connect for HTTP transport (it's connectionless)
            _transport.ConnectAsync(_deploymentUrl).GetAwaiter().GetResult();
        }

        #region Query Methods

        /// <summary>
        /// Executes a Convex query asynchronously and returns the result.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="name">The query function name (e.g., "messages:list").</param>
        /// <param name="args">The arguments to pass to the query (anonymous object, dictionary, or null).</param>
        /// <param name="timeout">Optional timeout override (uses default if not specified).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation and contains the query result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        /// <exception cref="ConvexExecutionException">Thrown when the query execution fails.</exception>
        /// <exception cref="ConvexError">Thrown when the query throws a ConvexError with custom data.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <example>
        /// <code>
        /// // With anonymous object
        /// var messages = await client.QueryAsync&lt;List&lt;Message&gt;&gt;("messages:list", new { limit = 10 });
        ///
        /// // With dictionary
        /// var args = new Dictionary&lt;string, object&gt; { { "userId", "user123" } };
        /// var user = await client.QueryAsync&lt;User&gt;("users:get", args);
        ///
        /// // No arguments
        /// var count = await client.QueryAsync&lt;int&gt;("messages:count");
        /// </code>
        /// </example>
        public async Task<T> QueryAsync<T>(
            string name,
            object args = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateFunctionName(name);

            var convexArgs = ConvertArgsToConvexObject(args);
            var request = new QueryRequest(name, convexArgs);
            var result = await _transport.SendRequestAsync(
                request,
                timeout ?? _defaultTimeout,
                cancellationToken);

            return ConvertResult<T>(result, name);
        }

        /// <summary>
        /// Executes a Convex query synchronously and returns the result.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="name">The query function name (e.g., "messages:list").</param>
        /// <param name="args">The arguments to pass to the query (anonymous object, dictionary, or null).</param>
        /// <param name="timeout">Optional timeout override (uses default if not specified).</param>
        /// <returns>The query result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        /// <exception cref="ConvexExecutionException">Thrown when the query execution fails.</exception>
        /// <exception cref="ConvexError">Thrown when the query throws a ConvexError with custom data.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <remarks>
        /// This method blocks the calling thread until the query completes.
        /// Consider using QueryAsync for better performance in async contexts.
        /// </remarks>
        /// <example>
        /// <code>
        /// var messages = client.Query&lt;List&lt;Message&gt;&gt;("messages:list", new { limit = 10 });
        /// </code>
        /// </example>
        public T Query<T>(string name, object args = null, TimeSpan? timeout = null)
        {
            return QueryAsync<T>(name, args, timeout).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Executes a Convex query as a Unity coroutine.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="name">The query function name (e.g., "messages:list").</param>
        /// <param name="args">The arguments to pass to the query (anonymous object, dictionary, or null).</param>
        /// <param name="onSuccess">Callback invoked with the result on success.</param>
        /// <param name="onError">Callback invoked with the exception on error.</param>
        /// <param name="timeout">Optional timeout override (uses default if not specified).</param>
        /// <returns>An IEnumerator suitable for use with StartCoroutine.</returns>
        /// <exception cref="ArgumentNullException">Thrown when name, onSuccess, or onError is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <remarks>
        /// This method is designed for Unity's coroutine system and should be started with StartCoroutine.
        /// </remarks>
        /// <example>
        /// <code>
        /// StartCoroutine(client.QueryCoroutine&lt;List&lt;Message&gt;&gt;(
        ///     "messages:list",
        ///     new { limit = 10 },
        ///     onSuccess: messages => {
        ///         Debug.Log($"Received {messages.Count} messages");
        ///     },
        ///     onError: error => {
        ///         Debug.LogError($"Query failed: {error.Message}");
        ///     }
        /// ));
        /// </code>
        /// </example>
        public IEnumerator QueryCoroutine<T>(
            string name,
            object args,
            Action<T> onSuccess,
            Action<Exception> onError,
            TimeSpan? timeout = null)
        {
            if (onSuccess == null)
            {
                throw new ArgumentNullException(nameof(onSuccess));
            }

            if (onError == null)
            {
                throw new ArgumentNullException(nameof(onError));
            }

            var task = QueryAsync<T>(name, args, timeout);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                onError(task.Exception?.GetBaseException() ?? task.Exception);
            }
            else if (task.IsCanceled)
            {
                onError(new OperationCanceledException("Query was canceled"));
            }
            else
            {
                onSuccess(task.Result);
            }
        }

        #endregion

        #region Mutation Methods

        /// <summary>
        /// Executes a Convex mutation asynchronously and returns the result.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="name">The mutation function name (e.g., "messages:send").</param>
        /// <param name="args">The arguments to pass to the mutation (anonymous object, dictionary, or null).</param>
        /// <param name="timeout">Optional timeout override (uses default if not specified).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation and contains the mutation result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        /// <exception cref="ConvexExecutionException">Thrown when the mutation execution fails.</exception>
        /// <exception cref="ConvexError">Thrown when the mutation throws a ConvexError with custom data.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <example>
        /// <code>
        /// var messageId = await client.MutationAsync&lt;string&gt;("messages:send", new {
        ///     author = "Alice",
        ///     body = "Hello, world!"
        /// });
        /// </code>
        /// </example>
        public async Task<T> MutationAsync<T>(
            string name,
            object args = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateFunctionName(name);

            var convexArgs = ConvertArgsToConvexObject(args);
            var request = new MutationRequest(name, convexArgs);
            var result = await _transport.SendRequestAsync(
                request,
                timeout ?? _defaultTimeout,
                cancellationToken);

            return ConvertResult<T>(result, name);
        }

        /// <summary>
        /// Executes a Convex mutation synchronously and returns the result.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="name">The mutation function name (e.g., "messages:send").</param>
        /// <param name="args">The arguments to pass to the mutation (anonymous object, dictionary, or null).</param>
        /// <param name="timeout">Optional timeout override (uses default if not specified).</param>
        /// <returns>The mutation result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        /// <exception cref="ConvexExecutionException">Thrown when the mutation execution fails.</exception>
        /// <exception cref="ConvexError">Thrown when the mutation throws a ConvexError with custom data.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <remarks>
        /// This method blocks the calling thread until the mutation completes.
        /// Consider using MutationAsync for better performance in async contexts.
        /// </remarks>
        /// <example>
        /// <code>
        /// var messageId = client.Mutation&lt;string&gt;("messages:send", new {
        ///     author = "Alice",
        ///     body = "Hello, world!"
        /// });
        /// </code>
        /// </example>
        public T Mutation<T>(string name, object args = null, TimeSpan? timeout = null)
        {
            return MutationAsync<T>(name, args, timeout).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Executes a Convex mutation as a Unity coroutine.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="name">The mutation function name (e.g., "messages:send").</param>
        /// <param name="args">The arguments to pass to the mutation (anonymous object, dictionary, or null).</param>
        /// <param name="onSuccess">Callback invoked with the result on success.</param>
        /// <param name="onError">Callback invoked with the exception on error.</param>
        /// <param name="timeout">Optional timeout override (uses default if not specified).</param>
        /// <returns>An IEnumerator suitable for use with StartCoroutine.</returns>
        /// <exception cref="ArgumentNullException">Thrown when name, onSuccess, or onError is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <remarks>
        /// This method is designed for Unity's coroutine system and should be started with StartCoroutine.
        /// </remarks>
        /// <example>
        /// <code>
        /// StartCoroutine(client.MutationCoroutine&lt;string&gt;(
        ///     "messages:send",
        ///     new { author = "Alice", body = "Hello!" },
        ///     onSuccess: messageId => {
        ///         Debug.Log($"Message sent with ID: {messageId}");
        ///     },
        ///     onError: error => {
        ///         Debug.LogError($"Mutation failed: {error.Message}");
        ///     }
        /// ));
        /// </code>
        /// </example>
        public IEnumerator MutationCoroutine<T>(
            string name,
            object args,
            Action<T> onSuccess,
            Action<Exception> onError,
            TimeSpan? timeout = null)
        {
            if (onSuccess == null)
            {
                throw new ArgumentNullException(nameof(onSuccess));
            }

            if (onError == null)
            {
                throw new ArgumentNullException(nameof(onError));
            }

            var task = MutationAsync<T>(name, args, timeout);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                onError(task.Exception?.GetBaseException() ?? task.Exception);
            }
            else if (task.IsCanceled)
            {
                onError(new OperationCanceledException("Mutation was canceled"));
            }
            else
            {
                onSuccess(task.Result);
            }
        }

        #endregion

        #region Action Methods

        /// <summary>
        /// Executes a Convex action asynchronously and returns the result.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="name">The action function name (e.g., "email:send").</param>
        /// <param name="args">The arguments to pass to the action (anonymous object, dictionary, or null).</param>
        /// <param name="timeout">Optional timeout override (uses default if not specified).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation and contains the action result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        /// <exception cref="ConvexExecutionException">Thrown when the action execution fails.</exception>
        /// <exception cref="ConvexError">Thrown when the action throws a ConvexError with custom data.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <example>
        /// <code>
        /// var success = await client.ActionAsync&lt;bool&gt;("email:send", new {
        ///     to = "user@example.com",
        ///     subject = "Welcome",
        ///     body = "Welcome to our app!"
        /// });
        /// </code>
        /// </example>
        public async Task<T> ActionAsync<T>(
            string name,
            object args = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateFunctionName(name);

            var convexArgs = ConvertArgsToConvexObject(args);
            var request = new ActionRequest(name, convexArgs);
            var result = await _transport.SendRequestAsync(
                request,
                timeout ?? _defaultTimeout,
                cancellationToken);

            return ConvertResult<T>(result, name);
        }

        /// <summary>
        /// Executes a Convex action synchronously and returns the result.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="name">The action function name (e.g., "email:send").</param>
        /// <param name="args">The arguments to pass to the action (anonymous object, dictionary, or null).</param>
        /// <param name="timeout">Optional timeout override (uses default if not specified).</param>
        /// <returns>The action result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        /// <exception cref="ConvexExecutionException">Thrown when the action execution fails.</exception>
        /// <exception cref="ConvexError">Thrown when the action throws a ConvexError with custom data.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <remarks>
        /// This method blocks the calling thread until the action completes.
        /// Consider using ActionAsync for better performance in async contexts.
        /// </remarks>
        /// <example>
        /// <code>
        /// var success = client.Action&lt;bool&gt;("email:send", new {
        ///     to = "user@example.com",
        ///     subject = "Welcome",
        ///     body = "Welcome to our app!"
        /// });
        /// </code>
        /// </example>
        public T Action<T>(string name, object args = null, TimeSpan? timeout = null)
        {
            return ActionAsync<T>(name, args, timeout).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Executes a Convex action as a Unity coroutine.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="name">The action function name (e.g., "email:send").</param>
        /// <param name="args">The arguments to pass to the action (anonymous object, dictionary, or null).</param>
        /// <param name="onSuccess">Callback invoked with the result on success.</param>
        /// <param name="onError">Callback invoked with the exception on error.</param>
        /// <param name="timeout">Optional timeout override (uses default if not specified).</param>
        /// <returns>An IEnumerator suitable for use with StartCoroutine.</returns>
        /// <exception cref="ArgumentNullException">Thrown when name, onSuccess, or onError is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <remarks>
        /// This method is designed for Unity's coroutine system and should be started with StartCoroutine.
        /// </remarks>
        /// <example>
        /// <code>
        /// StartCoroutine(client.ActionCoroutine&lt;bool&gt;(
        ///     "email:send",
        ///     new { to = "user@example.com", subject = "Welcome" },
        ///     onSuccess: success => {
        ///         Debug.Log($"Email sent: {success}");
        ///     },
        ///     onError: error => {
        ///         Debug.LogError($"Action failed: {error.Message}");
        ///     }
        /// ));
        /// </code>
        /// </example>
        public IEnumerator ActionCoroutine<T>(
            string name,
            object args,
            Action<T> onSuccess,
            Action<Exception> onError,
            TimeSpan? timeout = null)
        {
            if (onSuccess == null)
            {
                throw new ArgumentNullException(nameof(onSuccess));
            }

            if (onError == null)
            {
                throw new ArgumentNullException(nameof(onError));
            }

            var task = ActionAsync<T>(name, args, timeout);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                onError(task.Exception?.GetBaseException() ?? task.Exception);
            }
            else if (task.IsCanceled)
            {
                onError(new OperationCanceledException("Action was canceled"));
            }
            else
            {
                onSuccess(task.Result);
            }
        }

        #endregion

        #region Authentication Methods

        /// <summary>
        /// Sets the authentication token for subsequent requests.
        /// </summary>
        /// <param name="token">The authentication token (typically a JWT).</param>
        /// <exception cref="ArgumentNullException">Thrown when token is null or empty.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <remarks>
        /// The token will be sent in the Authorization header as: Bearer {token}
        /// </remarks>
        /// <example>
        /// <code>
        /// client.SetAuth("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...");
        /// </code>
        /// </example>
        public void SetAuth(string token)
        {
            ThrowIfDisposed();
            _transport.SetAuth(token);
        }

        /// <summary>
        /// Sets the admin authentication key for subsequent requests.
        /// </summary>
        /// <param name="adminKey">The admin key for the deployment.</param>
        /// <exception cref="ArgumentNullException">Thrown when adminKey is null or empty.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <remarks>
        /// The admin key will be sent in the Authorization header as: Convex {adminKey}
        /// Admin keys provide full access to the deployment and should be kept secure.
        /// Not typically required for client applications.
        /// </remarks>
        /// <example>
        /// <code>
        /// client.SetAdminAuth("your-admin-key");
        /// </code>
        /// </example>
        public void SetAdminAuth(string adminKey)
        {
            ThrowIfDisposed();
            _transport.SetAdminAuth(adminKey);
        }

        /// <summary>
        /// Clears any previously set authentication.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <example>
        /// <code>
        /// client.ClearAuth();
        /// </code>
        /// </example>
        public void ClearAuth()
        {
            ThrowIfDisposed();
            _transport.ClearAuth();
        }

        #endregion

        #region Debug Methods

        /// <summary>
        /// Sets whether debug mode is enabled.
        /// </summary>
        /// <param name="enabled">True to enable debug mode, false to disable.</param>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <remarks>
        /// When debug mode is enabled, log lines from Convex functions will be included
        /// in responses and printed to the Unity console (development deployments only).
        /// </remarks>
        /// <example>
        /// <code>
        /// client.SetDebug(true);
        /// </code>
        /// </example>
        public void SetDebug(bool enabled)
        {
            ThrowIfDisposed();
            _transport.SetDebug(enabled);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Converts function arguments from various input formats to a ConvexValue.Object.
        /// </summary>
        /// <param name="args">The arguments (anonymous object, Dictionary, or null).</param>
        /// <returns>A ConvexValue.Object containing the converted arguments.</returns>
        /// <exception cref="ArgumentException">Thrown when args cannot be converted.</exception>
        private ConvexValue.Object ConvertArgsToConvexObject(object args)
        {
            if (args == null)
            {
                return new ConvexValue.Object();
            }

            // Handle Dictionary<string, object>
            if (args is Dictionary<string, object> dict)
            {
                return ConvertDictionaryToConvexObject(dict);
            }

            // Handle IDictionary<string, object>
            if (args is IDictionary<string, object> idict)
            {
                var tempDict = new Dictionary<string, object>();
                foreach (var kvp in idict)
                {
                    tempDict[kvp.Key] = kvp.Value;
                }
                return ConvertDictionaryToConvexObject(tempDict);
            }

            // Handle ConvexValue.Object directly
            if (args is ConvexValue.Object convexObj)
            {
                return convexObj;
            }

            // Handle anonymous objects and POCOs via reflection
            return ConvertObjectToConvexObject(args);
        }

        /// <summary>
        /// Converts a Dictionary to a ConvexValue.Object.
        /// </summary>
        private ConvexValue.Object ConvertDictionaryToConvexObject(Dictionary<string, object> dict)
        {
            var fields = new Dictionary<string, ConvexValue>();

            foreach (var kvp in dict)
            {
                fields[kvp.Key] = ConvertValueToConvex(kvp.Value);
            }

            return new ConvexValue.Object(fields);
        }

        /// <summary>
        /// Converts a POCO or anonymous object to a ConvexValue.Object using reflection.
        /// </summary>
        private ConvexValue.Object ConvertObjectToConvexObject(object obj)
        {
            var fields = new Dictionary<string, ConvexValue>();
            var type = obj.GetType();

            // Get all public instance properties
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (prop.CanRead)
                {
                    var value = prop.GetValue(obj);
                    fields[prop.Name] = ConvertValueToConvex(value);
                }
            }

            return new ConvexValue.Object(fields);
        }

        /// <summary>
        /// Converts a C# value to a ConvexValue.
        /// </summary>
        private ConvexValue ConvertValueToConvex(object value)
        {
            if (value == null)
            {
                return ConvexValue.Null.Instance;
            }

            // Handle ConvexValue directly
            if (value is ConvexValue convexValue)
            {
                return convexValue;
            }

            // Handle primitive types
            switch (value)
            {
                case bool boolValue:
                    return new ConvexValue.Boolean(boolValue);

                case string stringValue:
                    return new ConvexValue.String(stringValue);

                case long longValue:
                    return new ConvexValue.Int64(longValue);

                case int intValue:
                    // int can safely be represented as Float64
                    return new ConvexValue.Float64(intValue);

                case float floatValue:
                    return new ConvexValue.Float64(floatValue);

                case double doubleValue:
                    return new ConvexValue.Float64(doubleValue);

                case byte[] bytesValue:
                    return new ConvexValue.Bytes(bytesValue);
            }

            // Handle arrays and collections
            if (value is IList<object> list)
            {
                var elements = new List<ConvexValue>();
                foreach (var item in list)
                {
                    elements.Add(ConvertValueToConvex(item));
                }
                return new ConvexValue.Array(elements);
            }

            // Handle dictionaries
            if (value is Dictionary<string, object> dict)
            {
                var fields = new Dictionary<string, ConvexValue>();
                foreach (var kvp in dict)
                {
                    fields[kvp.Key] = ConvertValueToConvex(kvp.Value);
                }
                return new ConvexValue.Object(fields);
            }

            // Handle generic objects via reflection
            return ConvertObjectToConvexValue(value);
        }

        /// <summary>
        /// Converts a generic object to a ConvexValue using reflection.
        /// </summary>
        private ConvexValue ConvertObjectToConvexValue(object obj)
        {
            var fields = new Dictionary<string, ConvexValue>();
            var type = obj.GetType();

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (prop.CanRead)
                {
                    var value = prop.GetValue(obj);
                    fields[prop.Name] = ConvertValueToConvex(value);
                }
            }

            return new ConvexValue.Object(fields);
        }

        /// <summary>
        /// Converts a FunctionResult to a typed result or throws an exception.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="result">The function result.</param>
        /// <param name="functionName">The name of the function that was called.</param>
        /// <returns>The converted result value.</returns>
        /// <exception cref="ConvexExecutionException">Thrown when the result is an error message.</exception>
        /// <exception cref="ConvexError">Thrown when the result is a ConvexError with custom data.</exception>
        private T ConvertResult<T>(FunctionResult result, string functionName)
        {
            switch (result)
            {
                case FunctionResult.Value valueResult:
                    return ConvertConvexValueToType<T>(valueResult.Result);

                case FunctionResult.ConvexError errorResult:
                    throw new ConvexError(
                        errorResult.Message,
                        errorResult.Data,
                        functionName);

                case FunctionResult.ErrorMessage msgResult:
                    throw new ConvexExecutionException(
                        msgResult.Message,
                        functionName);

                default:
                    throw new ConvexException(
                        $"Unknown result type from {functionName}: {result.GetType().Name}");
            }
        }

        /// <summary>
        /// Converts a ConvexValue to a typed C# value.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="value">The ConvexValue to convert.</param>
        /// <returns>The converted value.</returns>
        private T ConvertConvexValueToType<T>(ConvexValue value)
        {
            var targetType = typeof(T);

            // Handle ConvexValue types directly
            if (targetType == typeof(ConvexValue) || targetType.IsSubclassOf(typeof(ConvexValue)))
            {
                return (T)(object)value;
            }

            // Handle null
            if (value is ConvexValue.Null)
            {
                return default(T);
            }

            // Handle primitive types
            if (value is ConvexValue.Boolean boolValue && targetType == typeof(bool))
            {
                return (T)(object)boolValue.Value;
            }

            if (value is ConvexValue.String stringValue && targetType == typeof(string))
            {
                return (T)(object)stringValue.Value;
            }

            if (value is ConvexValue.Int64 int64Value)
            {
                if (targetType == typeof(long))
                {
                    return (T)(object)int64Value.Value;
                }

                if (targetType == typeof(int))
                {
                    return (T)(object)(int)int64Value.Value;
                }
            }

            if (value is ConvexValue.Float64 floatValue)
            {
                if (targetType == typeof(double))
                {
                    return (T)(object)floatValue.Value;
                }

                if (targetType == typeof(float))
                {
                    return (T)(object)(float)floatValue.Value;
                }

                if (targetType == typeof(int))
                {
                    return (T)(object)(int)floatValue.Value;
                }

                if (targetType == typeof(long))
                {
                    return (T)(object)(long)floatValue.Value;
                }
            }

            if (value is ConvexValue.Bytes bytesValue && targetType == typeof(byte[]))
            {
                return (T)(object)bytesValue.ToArray();
            }

            // Handle arrays
            if (value is ConvexValue.Array arrayValue)
            {
                if (targetType.IsArray)
                {
                    var elementType = targetType.GetElementType();
                    var array = System.Array.CreateInstance(elementType, arrayValue.Count);
                    for (int i = 0; i < arrayValue.Count; i++)
                    {
                        var converted = ConvertConvexValueToType(arrayValue[i], elementType);
                        array.SetValue(converted, i);
                    }
                    return (T)(object)array;
                }

                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var elementType = targetType.GetGenericArguments()[0];
                    var listType = typeof(List<>).MakeGenericType(elementType);
                    var list = (System.Collections.IList)Activator.CreateInstance(listType);

                    foreach (var element in arrayValue.Value)
                    {
                        var converted = ConvertConvexValueToType(element, elementType);
                        list.Add(converted);
                    }
                    return (T)list;
                }
            }

            // Handle objects
            if (value is ConvexValue.Object objectValue)
            {
                if (targetType == typeof(Dictionary<string, object>))
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var kvp in objectValue.Value)
                    {
                        dict[kvp.Key] = ConvertConvexValueToObject(kvp.Value);
                    }
                    return (T)(object)dict;
                }

                // Deserialize to POCO
                return DeserializeConvexObjectToPoco<T>(objectValue);
            }

            throw new ConvexException(
                $"Cannot convert ConvexValue of type {value.GetType().Name} to {targetType.Name}");
        }

        /// <summary>
        /// Helper method for converting ConvexValue to a specific type via reflection.
        /// </summary>
        private object ConvertConvexValueToType(ConvexValue value, Type targetType)
        {
            var method = typeof(ConvexHttpClient).GetMethod(
                nameof(ConvertConvexValueToType),
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(ConvexValue) },
                null);

            var genericMethod = method.MakeGenericMethod(targetType);
            return genericMethod.Invoke(this, new object[] { value });
        }

        /// <summary>
        /// Converts a ConvexValue to a plain C# object (for Dictionary scenarios).
        /// </summary>
        private object ConvertConvexValueToObject(ConvexValue value)
        {
            switch (value)
            {
                case ConvexValue.Null _:
                    return null;
                case ConvexValue.Boolean b:
                    return b.Value;
                case ConvexValue.String s:
                    return s.Value;
                case ConvexValue.Int64 i:
                    return i.Value;
                case ConvexValue.Float64 f:
                    return f.Value;
                case ConvexValue.Bytes b:
                    return b.ToArray();
                case ConvexValue.Array a:
                    return a.Value.ConvertAll(ConvertConvexValueToObject);
                case ConvexValue.Object o:
                    var dict = new Dictionary<string, object>();
                    foreach (var kvp in o.Value)
                    {
                        dict[kvp.Key] = ConvertConvexValueToObject(kvp.Value);
                    }
                    return dict;
                default:
                    return value;
            }
        }

        /// <summary>
        /// Deserializes a ConvexValue.Object to a POCO using reflection.
        /// </summary>
        private T DeserializeConvexObjectToPoco<T>(ConvexValue.Object objectValue)
        {
            var targetType = typeof(T);
            var instance = Activator.CreateInstance<T>();

            var properties = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (prop.CanWrite && objectValue.TryGetValue(prop.Name, out var convexValue))
                {
                    var convertedValue = ConvertConvexValueToType(convexValue, prop.PropertyType);
                    prop.SetValue(instance, convertedValue);
                }
            }

            return instance;
        }

        /// <summary>
        /// Validates that a function name is not null or empty.
        /// </summary>
        private void ValidateFunctionName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name), "Function name cannot be null or empty");
            }
        }

        /// <summary>
        /// Throws ObjectDisposedException if this instance has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ConvexHttpClient));
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases all resources used by the ConvexHttpClient.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _transport?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }
}
