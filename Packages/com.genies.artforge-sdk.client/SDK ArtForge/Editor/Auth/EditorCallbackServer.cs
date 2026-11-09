using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using UnityEngine;

namespace Genies.Sdk.ArtForge.Editor.Auth
{
    /// <summary>
    /// Local HTTP server for handling OAuth callbacks in the Unity Editor.
    /// Listens on localhost for browser redirects from WorkOS authentication.
    /// </summary>
    internal class EditorCallbackServer : IDisposable
    {
        private const int DefaultPort = 5173;
        private const int DefaultTimeoutMinutes = 5;
        private const int StatusOk = 200;
        private const int StatusBadRequest = 400;
        private const int OperationAbortedErrorCode = 995;

        private readonly ILogger _logger;
        private HttpListener _listener;
        private CancellationTokenSource _cancellationTokenSource;
        private TaskCompletionSource<OAuthCallbackResult> _callbackReceived;
        private readonly int _port;
        private bool _isDisposed;

        /// <summary>
        /// Result of an OAuth callback
        /// </summary>
        public class OAuthCallbackResult
        {
            public bool Success { get; set; }
            public string Code { get; set; }
            public string State { get; set; }
            public string Error { get; set; }
            public string ErrorDescription { get; set; }
        }

        /// <summary>
        /// Creates a new callback server on the specified port
        /// </summary>
        /// <param name="port">Port to listen on (default: 5173 to match WorkOS config)</param>
        public EditorCallbackServer(int port = DefaultPort, ILogger logger = null)
        {
            _port = port;
            _logger = logger ?? new UnityLogger();
        }

        /// <summary>
        /// Start the callback server and wait for OAuth redirect
        /// </summary>
        /// <param name="timeout">Maximum time to wait for callback (default: 5 minutes)</param>
        /// <returns>OAuth callback result</returns>
        public async Task<OAuthCallbackResult> StartAndWaitForCallbackAsync(TimeSpan? timeout = null)
        {
            if (_listener != null)
            {
                throw new InvalidOperationException("Server is already running");
            }

            timeout ??= TimeSpan.FromMinutes(DefaultTimeoutMinutes);
            _cancellationTokenSource = new CancellationTokenSource(timeout.Value);
            _callbackReceived = new TaskCompletionSource<OAuthCallbackResult>();

            try
            {
                // Create and start HTTP listener
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_port}/callback/");
                _listener.Start();

                _logger.Info($"[OAuth] Callback server started on http://localhost:{_port}/callback/");

                // Start listening for callback in background
                _ = Task.Run(ListenForCallbackAsync, _cancellationTokenSource.Token);

                // Wait for callback or timeout
                var completedTask = await Task.WhenAny(
                    _callbackReceived.Task,
                    Task.Delay(timeout.Value, _cancellationTokenSource.Token)
                );

                if (completedTask == _callbackReceived.Task)
                {
                    return await _callbackReceived.Task;
                }
                else
                {
                    throw new TimeoutException($"OAuth callback not received within {timeout.Value.TotalSeconds} seconds");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[OAuth] Callback server error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Listen for incoming HTTP requests
        /// </summary>
        private async Task ListenForCallbackAsync()
        {
            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested && _listener.IsListening)
                {
                    var context = await _listener.GetContextAsync();
                    await HandleCallbackAsync(context);
                }
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == OperationAbortedErrorCode) // ERROR_OPERATION_ABORTED
            {
                // Expected when stopping the listener
                _logger.Info("[OAuth] Callback server stopped");
            }
            catch (ObjectDisposedException)
            {
                // Expected when disposed
                _logger.Info("[OAuth] Callback server disposed");
            }
            catch (Exception ex)
            {
                _logger.Error($"[OAuth] Error listening for callbacks: {ex.Message}");
                _callbackReceived?.TrySetException(ex);
            }
        }

        /// <summary>
        /// Handle incoming OAuth callback
        /// </summary>
        private async Task HandleCallbackAsync(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                _logger.Info($"[OAuth] Received callback: {request.Url}");

                // Parse query parameters
                var queryParams = request.QueryString;
                var code = queryParams["code"];
                var state = queryParams["state"];
                var error = queryParams["error"];
                var errorDescription = queryParams["error_description"];

                // Create result
                var result = new OAuthCallbackResult
                {
                    Success = !string.IsNullOrEmpty(code),
                    Code = code,
                    State = state,
                    Error = error,
                    ErrorDescription = errorDescription
                };

                // Send response to browser
                string responseHtml;
                if (result.Success)
                {
                    responseHtml = GetSuccessHtml();
                    response.StatusCode = StatusOk;
                }
                else
                {
                    responseHtml = GetErrorHtml(error, errorDescription);
                    response.StatusCode = StatusBadRequest;
                }

                byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
                response.ContentLength64 = buffer.Length;
                response.ContentType = "text/html; charset=utf-8";
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.OutputStream.Close();

                // Signal completion
                _callbackReceived?.TrySetResult(result);

                // Stop server
                Stop();
            }
            catch (Exception ex)
            {
                _logger.Error($"[OAuth] Error handling callback: {ex.Message}");
                _callbackReceived?.TrySetException(ex);
            }
        }

        /// <summary>
        /// Stop the callback server
        /// </summary>
        public void Stop()
        {
            if (_listener != null && _listener.IsListening)
            {
                try
                {
                    _listener.Stop();
                    _listener.Close();
                    _logger.Info("[OAuth] Callback server stopped");
                }
                catch (Exception ex)
                {
                    _logger.Error($"[OAuth] Error stopping server: {ex.Message}");
                }
            }

            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// HTML response for successful authentication
        /// </summary>
        private string GetSuccessHtml()
        {
            return @"
<!DOCTYPE html>
<html>
<head>
    <title>Authentication Success</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        }
        .container {
            background: white;
            padding: 48px;
            border-radius: 16px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            text-align: center;
            max-width: 400px;
        }
        .icon {
            font-size: 64px;
            margin-bottom: 16px;
        }
        h1 {
            color: #2d3748;
            margin: 0 0 8px 0;
            font-size: 28px;
        }
        p {
            color: #718096;
            margin: 0;
            font-size: 16px;
        }
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>✓</div>
        <h1>Authentication Successful!</h1>
        <p>You can now close this window and return to Unity Editor.</p>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// HTML response for authentication error
        /// </summary>
        private string GetErrorHtml(string error, string errorDescription)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <title>Authentication Error</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
        }}
        .container {{
            background: white;
            padding: 48px;
            border-radius: 16px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            text-align: center;
            max-width: 400px;
        }}
        .icon {{
            font-size: 64px;
            margin-bottom: 16px;
        }}
        h1 {{
            color: #2d3748;
            margin: 0 0 8px 0;
            font-size: 28px;
        }}
        p {{
            color: #718096;
            margin: 0;
            font-size: 16px;
        }}
        .error-details {{
            margin-top: 16px;
            padding: 16px;
            background: #fed7d7;
            border-radius: 8px;
            color: #c53030;
            font-size: 14px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>✗</div>
        <h1>Authentication Failed</h1>
        <p>Please return to Unity Editor and try again.</p>
        <div class='error-details'>
            <strong>Error:</strong> {error ?? "Unknown error"}<br/>
            {(string.IsNullOrEmpty(errorDescription) ? "" : $"<strong>Details:</strong> {errorDescription}")}
        </div>
    </div>
</body>
</html>";
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            Stop();
            _listener?.Close();
            _cancellationTokenSource?.Dispose();
            _isDisposed = true;
        }
    }
}
