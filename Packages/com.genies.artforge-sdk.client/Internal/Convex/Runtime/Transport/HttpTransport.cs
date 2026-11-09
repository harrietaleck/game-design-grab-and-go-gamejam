using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Genies.Convex.Exceptions;
using Genies.Convex.Values;
using Genies.Convex.Logging;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Genies.Convex.Transport
{
    /// <summary>
    /// HTTP-based transport implementation for Convex client.
    /// </summary>
    /// <remarks>
    /// This transport uses UnityWebRequest for HTTP communication with Convex backends.
    /// It supports queries, mutations, and actions but does not support subscriptions
    /// (which require WebSocket transport).
    ///
    /// Thread Safety: This class is thread-safe for concurrent requests.
    /// </remarks>
    /// <example>
    /// <code>
    /// var transport = new HttpTransport("https://example.convex.cloud");
    /// transport.SetAuth("your-jwt-token");
    /// transport.SetDebug(true);
    ///
    /// var request = new QueryRequest("messages:list", new ConvexValue.Object());
    /// var result = await transport.SendRequestAsync(request, TimeSpan.FromSeconds(30));
    /// Console.WriteLine(result.Value);
    /// </code>
    /// </example>
    internal class HttpTransport : ITransport
    {
        private readonly string _deploymentUrl;
        private readonly string _clientVersion;
        private string _authToken;
        private readonly IConvexLogger _logger;
        private bool _isDisposed;
        private readonly object _authLock = new object();

        /// <summary>
        /// Gets a value indicating whether the transport is connected.
        /// </summary>
        /// <remarks>
        /// HTTP is connectionless, so this returns true after ConnectAsync has been called
        /// and validated the deployment URL.
        /// </remarks>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpTransport"/> class.
        /// </summary>
        /// <param name="deploymentUrl">The Convex deployment URL (e.g., https://example.convex.cloud).</param>
        /// <param name="clientVersion">The client version string. Defaults to current protocol version.</param>
        /// <exception cref="ArgumentNullException">Thrown when deploymentUrl is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when deploymentUrl is not a valid URL.</exception>
        public HttpTransport(string deploymentUrl, string clientVersion = null, IConvexLogger logger = null)
        {
            if (string.IsNullOrWhiteSpace(deploymentUrl))
            {
                throw new ArgumentNullException(nameof(deploymentUrl), "Deployment URL cannot be null or empty");
            }

            // Validate URL format
            if (!Uri.TryCreate(deploymentUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                throw new ArgumentException(
                    $"Invalid deployment URL: {deploymentUrl}. Must be a valid HTTP or HTTPS URL.",
                    nameof(deploymentUrl));
            }

            // Remove trailing slash for consistency
            _deploymentUrl = deploymentUrl.TrimEnd('/');
            _clientVersion = clientVersion ?? ConvexVersion.ClientIdentifier;
            _logger = logger ?? new UnityConvexLogger();
            IsConnected = false;
        }

        /// <summary>
        /// Connects to the Convex backend.
        /// </summary>
        /// <param name="url">The deployment URL (optional, uses constructor URL if not provided).</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous connect operation.</returns>
        /// <remarks>
        /// For HTTP transport, this method simply validates the URL format as HTTP is connectionless.
        /// The actual connection is established with each request.
        /// </remarks>
        public Task ConnectAsync(string url, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            // HTTP is connectionless - just validate URL and mark as connected
            var targetUrl = string.IsNullOrWhiteSpace(url) ? _deploymentUrl : url;

            if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                throw new ConvexConnectionException(
                    $"Invalid deployment URL: {targetUrl}. Must be a valid HTTP or HTTPS URL.",
                    targetUrl);
            }

            IsConnected = true;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sends a request to the Convex backend and returns the result.
        /// </summary>
        /// <typeparam name="TRequest">The type of request message.</typeparam>
        /// <param name="request">The request message to send.</param>
        /// <param name="timeout">The maximum time to wait for a response.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous request operation and contains the function result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
        /// <exception cref="ConvexConnectionException">Thrown when network connection fails.</exception>
        /// <exception cref="ConvexExecutionException">Thrown when the function execution fails on the server.</exception>
        /// <exception cref="ConvexError">Thrown when the function throws a ConvexError with custom data.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is canceled or times out.</exception>
        public async Task<FunctionResult> SendRequestAsync<TRequest>(
            TRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
            where TRequest : TransportRequest
        {
            ThrowIfDisposed();

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!IsConnected)
            {
                throw new ConvexConnectionException(
                    "Transport is not connected. Call ConnectAsync first.",
                    _deploymentUrl);
            }

            // Determine endpoint based on request type
            string endpoint = GetEndpointForRequest(request);
            string url = $"{_deploymentUrl}{endpoint}";

            // Serialize request
            string requestJson = SerializeRequest(request);

            // Create cancellation source combining timeout and user cancellation token
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            try
            {
                // Send request and get response
                TransportResponse response = await SendHttpRequestAsync(
                    url, requestJson, request.Path, linkedCts.Token);

                // Handle response
                if (response.Status == ResponseStatus.Success)
                {
                    // Print debug logs if enabled
                    if (_logger.IsEnabled && response.LogLines != null && response.LogLines.Count > 0)
                    {
                        foreach (var logLine in response.LogLines)
                        {
                            _logger.Info($"[Convex] {logLine}");
                        }
                    }

                    return new FunctionResult.Value(response.Value, response.LogLines);
                }
                else
                {
                    // Handle error response
                    if (response.ErrorData != null)
                    {
                        // ConvexError with custom data
                        return new FunctionResult.ConvexError(
                            response.ErrorMessage ?? "Convex function error",
                            response.ErrorData,
                            response.LogLines);
                    }
                    else
                    {
                        // Generic error message
                        return new FunctionResult.ErrorMessage(
                            response.ErrorMessage ?? "Unknown Convex execution error",
                            response.LogLines);
                    }
                }
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Timeout occurred
                throw new OperationCanceledException(
                    $"Request to {request.Path} timed out after {timeout.TotalSeconds} seconds");
            }
        }

        /// <summary>
        /// Sets the authentication token for subsequent requests.
        /// </summary>
        /// <param name="token">The authentication token (typically a JWT).</param>
        /// <exception cref="ArgumentNullException">Thrown when token is null or empty.</exception>
        public void SetAuth(string token)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentNullException(nameof(token), "Auth token cannot be null or empty");
            }

            lock (_authLock)
            {
                _authToken = $"Bearer {token}";
            }
        }

        /// <summary>
        /// Sets the admin authentication key for subsequent requests.
        /// </summary>
        /// <param name="adminKey">The admin key for the deployment.</param>
        /// <exception cref="ArgumentNullException">Thrown when adminKey is null or empty.</exception>
        public void SetAdminAuth(string adminKey)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(adminKey))
            {
                throw new ArgumentNullException(nameof(adminKey), "Admin key cannot be null or empty");
            }

            lock (_authLock)
            {
                _authToken = $"Convex {adminKey}";
            }
        }

        /// <summary>
        /// Clears any previously set authentication.
        /// </summary>
        public void ClearAuth()
        {
            ThrowIfDisposed();

            lock (_authLock)
            {
                _authToken = null;
            }
        }

        /// <summary>
        /// Sets whether debug mode is enabled.
        /// </summary>
        /// <param name="enabled">True to enable debug mode, false to disable.</param>
        public void SetDebug(bool enabled)
        {
            ThrowIfDisposed();
            _logger.IsEnabled = enabled;
        }

        /// <summary>
        /// Releases all resources used by the HttpTransport.
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                IsConnected = false;
                _authToken = null;
            }
        }

        /// <summary>
        /// Gets the API endpoint for a given request type.
        /// </summary>
        private string GetEndpointForRequest<TRequest>(TRequest request)
            where TRequest : TransportRequest
        {
            return request switch
            {
                QueryRequest _ => "/api/query",
                MutationRequest _ => "/api/mutation",
                ActionRequest _ => "/api/action",
                _ => throw new ArgumentException(
                    $"Unknown request type: {request.GetType().Name}",
                    nameof(request))
            };
        }

        /// <summary>
        /// Serializes a request to JSON format expected by Convex backend.
        /// </summary>
        private string SerializeRequest<TRequest>(TRequest request)
            where TRequest : TransportRequest
        {
            // Serialize args to JSON using ValueSerializer
            string argsJson = ValueSerializer.Serialize(request.Args);

            // Build request object
            var requestObj = new JObject
            {
                { "path", request.Path },
                { "format", "convex_encoded_json" },
                { "args", JToken.Parse(argsJson) }
            };

            return requestObj.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>
        /// Sends an HTTP request using UnityWebRequest and returns the parsed response.
        /// </summary>
        private async Task<TransportResponse> SendHttpRequestAsync(
            string url,
            string requestJson,
            string functionName,
            CancellationToken cancellationToken)
        {
            // Create UnityWebRequest
            byte[] requestBytes = Encoding.UTF8.GetBytes(requestJson);
            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(requestBytes);
            request.downloadHandler = new DownloadHandlerBuffer();

            // Set headers
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Convex-Client", $"csharp-{_clientVersion}");

            // Add auth header if present
            string authHeader;
            lock (_authLock)
            {
                authHeader = _authToken;
            }
            if (!string.IsNullOrEmpty(authHeader))
            {
                request.SetRequestHeader("Authorization", authHeader);
            }

            // Send request asynchronously
            var operation = request.SendWebRequest();

            // Wait for completion with cancellation support
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    throw new OperationCanceledException(cancellationToken);
                }

                await Task.Yield();
            }

            // Check for network errors
            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.DataProcessingError)
            {
                throw new ConvexConnectionException(
                    $"Network error while calling {functionName}: {request.error}",
                    url,
                    new Exception(request.error));
            }

            // Get response data
            string responseText = request.downloadHandler?.text;
            long responseCode = request.responseCode;

            // Try to parse JSON response
            JObject responseJson;
            try
            {
                if (string.IsNullOrWhiteSpace(responseText))
                {
                    // No response body - handle HTTP errors
                    if (responseCode >= 400)
                    {
                        throw new ConvexExecutionException(
                            $"HTTP {responseCode} error calling {functionName}",
                            functionName,
                            (int)responseCode);
                    }

                    throw new ConvexExecutionException(
                        $"Unexpected empty response from {functionName}",
                        functionName);
                }

                responseJson = JObject.Parse(responseText);
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                // Failed to parse JSON - likely an infrastructure error
                if (responseCode >= 400 && responseCode < 600)
                {
                    throw new ConvexExecutionException(
                        $"HTTP {responseCode} error calling {functionName}: {responseText}",
                        functionName,
                        (int)responseCode);
                }

                throw new ConvexExecutionException(
                    $"Invalid JSON response from {functionName}: {ex.Message}",
                    functionName,
                    ex);
            }

            // Check for HTTP errors even with JSON response
            if (responseCode >= 400 && responseCode < 600)
            {
                // Extract error details from JSON if available
                string code = responseJson["code"]?.Value<string>();
                string message = responseJson["message"]?.Value<string>();

                if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(message))
                {
                    throw new ConvexExecutionException(
                        $"HTTP {responseCode} {code}: {message}",
                        functionName,
                        (int)responseCode);
                }

                throw new ConvexExecutionException(
                    $"HTTP {responseCode} error calling {functionName}",
                    functionName,
                    (int)responseCode);
            }

            // Parse response
            return ParseResponse(responseJson);
        }

        /// <summary>
        /// Parses a JSON response from Convex backend into a TransportResponse.
        /// </summary>
        private TransportResponse ParseResponse(JObject responseJson)
        {
            string status = responseJson["status"]?.Value<string>();

            // Parse log lines if present
            List<string> logLines = null;
            if (responseJson["logLines"] is JArray logLinesArray)
            {
                logLines = new List<string>();
                foreach (var line in logLinesArray)
                {
                    logLines.Add(line.Value<string>());
                }
            }

            if (status == "success")
            {
                // Success response
                var valueToken = responseJson["value"];
                if (valueToken == null)
                {
                    throw new ConvexExecutionException(
                        "Success response missing 'value' field");
                }

                ConvexValue value = ValueSerializer.FromJToken(valueToken);
                return new TransportResponse(value, logLines);
            }
            else if (status == "error")
            {
                // Error response
                JToken errorDataToken = responseJson["errorData"];
                string errorMessage = ResolveErrorMessage(responseJson, errorDataToken);

                ConvexValue errorData = null;
                if (errorDataToken != null &&
                    errorDataToken.Type != JTokenType.Null)
                {
                    errorData = ValueSerializer.FromJToken(errorDataToken);
                }

                return new TransportResponse(errorMessage, errorData, logLines);
            }
            else
            {
                throw new ConvexExecutionException(
                    $"Unexpected response status: {status ?? "(null)"}");
            }
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

        /// <summary>
        /// Throws ObjectDisposedException if this instance has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(HttpTransport));
            }
        }
    }
}
