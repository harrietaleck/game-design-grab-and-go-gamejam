using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using UnityEditor;
using UnityEngine;

namespace Genies.Sdk.ArtForge.Editor.Auth
{
    /// <summary>
    /// OAuth 2.0 flow implementation for WorkOS authentication in Unity Editor.
    /// Handles browser-based login with local callback server.
    /// </summary>
    internal class WorkOSOAuthFlow : IDisposable
    {
        private readonly ILogger _logger;
        private EditorCallbackServer _callbackServer;
        private string _codeVerifier;
        private string _state;
        private bool _isDisposed;

        private const int CallbackPort = 5173;
        private const string CallbackPath = "/callback";
        private const int CallbackTimeoutMinutes = 5;
        private const float TokenExchangeProgress = 0.8f;
        private const int PkceVerifierByteLength = 32;
        private const int StateByteLength = 16;
        private const int TokenExchangeTimeoutSeconds = 30;

        public WorkOSOAuthFlow(ILogger logger = null)
        {
            _logger = logger ?? new UnityLogger();
        }

        /// <summary>
        /// Start OAuth authentication flow
        /// </summary>
        /// <returns>JWT access token on success</returns>
        public async Task<string> AuthenticateAsync()
        {
            try
            {
                _logger.Info("[OAuth] Starting authentication flow...");

                // Step 1: Generate PKCE parameters (security)
                GeneratePkceParameters();

                // Step 2: Start local callback server
                _callbackServer = new EditorCallbackServer(CallbackPort, _logger);
                var callbackTask = _callbackServer.StartAndWaitForCallbackAsync(TimeSpan.FromMinutes(CallbackTimeoutMinutes));

                // Step 3: Build authorization URL
                string authUrl = BuildAuthorizationUrl();
                _logger.Info("[OAuth] Opening browser to authorization URL...");

                // Step 4: Open browser
                Application.OpenURL(authUrl);

                // Step 5: Wait for callback with cancellation support
                var callbackResult = await WaitForCallbackWithCancellation(callbackTask);

                EditorUtility.ClearProgressBar();
                
                if (callbackResult == null)
                {
                    _logger.Info("[OAuth] Authentication cancelled by user");
                    throw new OperationCanceledException("OAuth authentication was cancelled by user");
                }

                // Step 6: Validate callback
                if (!callbackResult.Success)
                {
                    throw new Exception($"OAuth error: {callbackResult.Error} - {callbackResult.ErrorDescription}");
                }

                if (callbackResult.State != _state)
                {
                    throw new Exception("OAuth state mismatch - possible CSRF attack");
                }

                _logger.Info("[OAuth] Callback received successfully");

                // Step 7: Exchange authorization code for access token
                EditorUtility.DisplayProgressBar("OAuth Authentication", "Exchanging code for token...", TokenExchangeProgress);

                string accessToken = await ExchangeCodeForTokenAsync(callbackResult.Code);

                EditorUtility.ClearProgressBar();

                _logger.Info("[OAuth] Authentication successful!");
                return accessToken;
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                _logger.Error($"[OAuth] Authentication failed: {ex.Message}");
                throw;
            }
            finally
            {
                _callbackServer?.Dispose();
            }
        }

        /// <summary>
        /// Generate PKCE (Proof Key for Code Exchange) parameters for security
        /// </summary>
        private void GeneratePkceParameters()
        {
            // Generate code verifier (random string)
            var bytes = new byte[PkceVerifierByteLength];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            _codeVerifier = Base64UrlEncode(bytes);

            // Generate state parameter (CSRF protection)
            var stateBytes = new byte[StateByteLength];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(stateBytes);
            }
            _state = Base64UrlEncode(stateBytes);

            _logger.Info($"[OAuth] Generated PKCE parameters (verifier length: {_codeVerifier.Length})");
        }

        /// <summary>
        /// Wait for callback with cancellation support via progress bar
        /// </summary>
        private async Task<EditorCallbackServer.OAuthCallbackResult> WaitForCallbackWithCancellation(
            Task<EditorCallbackServer.OAuthCallbackResult> callbackTask)
        {
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromMinutes(CallbackTimeoutMinutes);

            _ = callbackTask.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);

            var completionSource = new TaskCompletionSource<EditorCallbackServer.OAuthCallbackResult>();

            void Cleanup()
            {
                EditorApplication.update -= Update;
                EditorUtility.ClearProgressBar();
            }

            void Update()
            {
                if (completionSource.Task.IsCompleted)
                {
                    return;
                }

                if (callbackTask.IsCompleted)
                {
                    Cleanup();
                    if (callbackTask.IsFaulted)
                    {
                        var ex = callbackTask.Exception?.GetBaseException() ?? callbackTask.Exception;
                        completionSource.TrySetException(ex);
                    }
                    else if (callbackTask.IsCanceled)
                    {
                        completionSource.TrySetCanceled();
                    }
                    else
                    {
                        completionSource.TrySetResult(callbackTask.Result);
                    }
                    return;
                }

                var elapsed = DateTime.UtcNow - startTime;
                var progress = (float)(elapsed.TotalSeconds / timeout.TotalSeconds);

                if (EditorUtility.DisplayCancelableProgressBar(
                    "OAuth Authentication",
                    $"Waiting for login in browser... ({(int)(timeout.TotalSeconds - elapsed.TotalSeconds)}s remaining)\n\nClick Cancel to abort",
                    progress))
                {
                    Cleanup();
                    _callbackServer?.Stop();
                    completionSource.TrySetResult(null);
                    return;
                }

                if (elapsed >= timeout)
                {
                    Cleanup();
                    completionSource.TrySetException(new TimeoutException($"OAuth authentication timed out after {CallbackTimeoutMinutes} minutes"));
                }
            }

            EditorApplication.update += Update;
            return await completionSource.Task;
        }

        /// <summary>
        /// Build the OAuth authorization URL
        /// </summary>
        private string BuildAuthorizationUrl()
        {
            // Compute code challenge from verifier
            string codeChallenge;
            using (var sha256 = SHA256.Create())
            {
                var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(_codeVerifier));
                codeChallenge = Base64UrlEncode(challengeBytes);
            }

            var clientId = WorkOSAuthConstants.GetClientId();
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new InvalidOperationException("WorkOS client_id is not configured");
            }

            var redirectUri = $"http://localhost:{CallbackPort}{CallbackPath}";
            _logger.Info($"[OAuth] Authorize flow using environment='{WorkOSAuthConstants.GetEnvironment()}', client_id='{clientId}', redirect_uri='{redirectUri}'");

            // Build authorization URL
            var queryParams = new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "redirect_uri", redirectUri },
                { "response_type", "code" },
                { "state", _state },
                { "code_challenge", codeChallenge },
                { "code_challenge_method", "S256" },
                { "scope", WorkOSAuthConstants.AuthorizationScopes },
                { "provider", "authkit" } // Use WorkOS managed authentication
            };

            var queryString = string.Join("&",
                Array.ConvertAll(queryParams.Keys.ToArray(),
                key => $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(queryParams[key])}"));

            return $"{WorkOSAuthConstants.GetAuthorizeUrl()}?{queryString}";
        }

        /// <summary>
        /// Exchange authorization code for access token
        /// </summary>
        private async Task<string> ExchangeCodeForTokenAsync(string code)
        {
            var clientId = WorkOSAuthConstants.GetClientId();
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new Exception("WorkOS client_id is not configured");
            }

            var redirectUri = $"http://localhost:{CallbackPort}{CallbackPath}";
            _logger.Info($"[OAuth] Token exchange using environment='{WorkOSAuthConstants.GetEnvironment()}', client_id='{clientId}', redirect_uri='{redirectUri}'");

            var requestData = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", redirectUri },
                { "client_id", clientId },
                { "code_verifier", _codeVerifier }
            };

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(TokenExchangeTimeoutSeconds);
                var tokenEndpointUrl = WorkOSAuthConstants.GetApiUrl();
                _logger.Info($"[OAuth] Token endpoint URL: {tokenEndpointUrl}");

                var content = new FormUrlEncodedContent(requestData);
                var response = await client.PostAsync(tokenEndpointUrl, content);

                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.Error($"[OAuth] Token exchange failed: {responseText}");
                    throw new Exception($"Failed to exchange code for token: {response.StatusCode}");
                }

                var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseText);

                if (string.IsNullOrEmpty(tokenResponse?.access_token) &&
                    string.IsNullOrEmpty(tokenResponse?.id_token))
                {
                    throw new Exception("Token response did not contain access_token or id_token");
                }

                if (!string.IsNullOrEmpty(tokenResponse.access_token))
                {
                    WorkOSAuthService.StoreAccessToken(tokenResponse.access_token);
                }
                if (!string.IsNullOrEmpty(tokenResponse.id_token))
                {
                    WorkOSAuthService.StoreIdToken(tokenResponse.id_token);
                }
                if (!string.IsNullOrEmpty(tokenResponse.refresh_token))
                {
                    WorkOSAuthService.StoreRefreshToken(tokenResponse.refresh_token);
                }
                else
                {
                    _logger.Warning("[OAuth] Token response did not include refresh_token. Silent token refresh will not be available.");
                }
                if (!string.IsNullOrEmpty(tokenResponse.user?.email))
                {
                    WorkOSAuthService.StoreEmail(tokenResponse.user.email);
                }
                if (!string.IsNullOrEmpty(tokenResponse.user?.id))
                {
                    WorkOSAuthService.StoreUserId(tokenResponse.user.id);
                }
                
                bool usingIdTokenForBackend = !string.IsNullOrEmpty(tokenResponse.id_token);
                string backendToken = usingIdTokenForBackend ? tokenResponse.id_token : tokenResponse.access_token;

                _logger.Info($"[OAuth] Tokens received (has access_token={(!string.IsNullOrEmpty(tokenResponse.access_token))}, has id_token={usingIdTokenForBackend}, has refresh_token={(!string.IsNullOrEmpty(tokenResponse.refresh_token))})");
                _logger.Info($"[OAuth] Using {(usingIdTokenForBackend ? "id_token" : "access_token")} for backend auth (expires in: {tokenResponse.expires_in}s)");

                return backendToken;
            }
        }

        /// <summary>
        /// Base64 URL-safe encoding (for PKCE)
        /// </summary>
        private string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace("=", "");
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _callbackServer?.Dispose();
            _isDisposed = true;
        }

        /// <summary>
        /// Token response from WorkOS
        /// </summary>
        [Serializable]
        private class TokenResponse
        {
            public string access_token { get; set; }
            public string id_token { get; set; }
            public string refresh_token { get; set; }
            public int expires_in { get; set; }
            public string token_type { get; set; }
            public UserInfo user { get; set; }
        }

        [Serializable]
        private class UserInfo
        {
            public string id { get; set; }
            public string email { get; set; }
            public string first_name { get; set; }
            public string last_name { get; set; }
        }
    }
}
