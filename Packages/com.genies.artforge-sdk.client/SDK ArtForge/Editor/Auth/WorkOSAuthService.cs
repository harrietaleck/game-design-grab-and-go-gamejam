using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Genies.Sdk.ArtForge.Editor.Auth
{
    /// <summary>
    /// Shared WorkOS token storage and refresh helpers.
    /// </summary>
    internal static class WorkOSAuthService
    {
        private const string PrefixKey = "GAF_Settings_";
        private const int RefreshLockCount = 1;
        private const int JwtPartsCount = 3;
        private const int HttpTimeoutSeconds = 30;
        private const int Base64PadModulo = 4;
        private const int Base64PadCaseTwo = 2;
        private const int Base64PadCaseThree = 3;
        private const int Base64PadCaseZero = 0;
        private const string OAuthErrorInvalidGrant = "invalid_grant";
        private const string OAuthErrorInvalidRefreshToken = "invalid_refresh_token";
        private const string OAuthErrorInvalidClient = "invalid_client";
        private const string OAuthErrorUnauthorizedClient = "unauthorized_client";
        private static readonly SemaphoreSlim RefreshLock = new SemaphoreSlim(RefreshLockCount, RefreshLockCount);

        public static bool HasStoredTokens()
        {
            return !string.IsNullOrWhiteSpace(GetStoredIdToken()) ||
                   !string.IsNullOrWhiteSpace(GetStoredAccessToken()) ||
                   !string.IsNullOrWhiteSpace(GetStoredRefreshToken());
        }

        public static string GetStoredIdToken()
        {
            string token = EditorPrefs.GetString($"{PrefixKey}{WorkOSAuthConstants.PrefKeyIdToken}", "");
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }

            // Legacy (non-prefixed) fallback.
            token = EditorPrefs.GetString(WorkOSAuthConstants.PrefKeyIdToken, "");
            if (!string.IsNullOrWhiteSpace(token))
            {
                StoreIdToken(token);
                EditorPrefs.DeleteKey(WorkOSAuthConstants.PrefKeyIdToken);
            }

            return token;
        }

        public static string GetStoredAccessToken()
        {
            string token = EditorPrefs.GetString($"{PrefixKey}{WorkOSAuthConstants.PrefKeyAccessToken}", "");
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }

            // Legacy (non-prefixed) fallback.
            token = EditorPrefs.GetString(WorkOSAuthConstants.PrefKeyAccessToken, "");
            if (!string.IsNullOrWhiteSpace(token))
            {
                StoreAccessToken(token);
                EditorPrefs.DeleteKey(WorkOSAuthConstants.PrefKeyAccessToken);
            }

            return token;
        }

        public static string GetStoredRefreshToken()
        {
            string token = EditorPrefs.GetString($"{PrefixKey}{WorkOSAuthConstants.PrefKeyRefreshToken}", "");
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }

            // Legacy (non-prefixed) fallback.
            token = EditorPrefs.GetString(WorkOSAuthConstants.PrefKeyRefreshToken, "");
            if (!string.IsNullOrWhiteSpace(token))
            {
                StoreRefreshToken(token);
                EditorPrefs.DeleteKey(WorkOSAuthConstants.PrefKeyRefreshToken);
            }

            return token;
        }

        public static void StoreIdToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            EditorPrefs.SetString($"{PrefixKey}{WorkOSAuthConstants.PrefKeyIdToken}", token);
        }

        public static void StoreAccessToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            EditorPrefs.SetString($"{PrefixKey}{WorkOSAuthConstants.PrefKeyAccessToken}", token);
        }

        public static void StoreRefreshToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            EditorPrefs.SetString($"{PrefixKey}{WorkOSAuthConstants.PrefKeyRefreshToken}", token);
        }

        public static void StoreEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            EditorPrefs.SetString($"{PrefixKey}{WorkOSAuthConstants.PrefKeyEmail}", email);
        }

        public static string GetStoredEmail()
        {
            string email = EditorPrefs.GetString($"{PrefixKey}{WorkOSAuthConstants.PrefKeyEmail}", "");
            if (!string.IsNullOrWhiteSpace(email))
            {
                return email;
            }

            // Legacy (non-prefixed) fallback.
            email = EditorPrefs.GetString(WorkOSAuthConstants.PrefKeyEmail, "");
            if (!string.IsNullOrWhiteSpace(email))
            {
                StoreEmail(email);
                EditorPrefs.DeleteKey(WorkOSAuthConstants.PrefKeyEmail);
                return email;
            }

            return "unknown";
        }

        public static void ClearAccessToken()
        {
            EditorPrefs.DeleteKey($"{PrefixKey}{WorkOSAuthConstants.PrefKeyAccessToken}");
            EditorPrefs.DeleteKey(WorkOSAuthConstants.PrefKeyAccessToken);
        }

        public static void ClearIdToken()
        {
            EditorPrefs.DeleteKey($"{PrefixKey}{WorkOSAuthConstants.PrefKeyIdToken}");
            EditorPrefs.DeleteKey(WorkOSAuthConstants.PrefKeyIdToken);
        }

        public static void ClearRefreshToken()
        {
            EditorPrefs.DeleteKey($"{PrefixKey}{WorkOSAuthConstants.PrefKeyRefreshToken}");
            EditorPrefs.DeleteKey(WorkOSAuthConstants.PrefKeyRefreshToken);
        }

        public static void ClearStoredTokens()
        {
            ClearIdToken();
            ClearAccessToken();
            ClearRefreshToken();
            ClearUserId();
            EditorPrefs.DeleteKey($"{PrefixKey}{WorkOSAuthConstants.PrefKeyEmail}");
            EditorPrefs.DeleteKey(WorkOSAuthConstants.PrefKeyEmail);
        }

        public static bool IsValidJwtFormat(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            var parts = token.Split('.');
            if (parts.Length != JwtPartsCount)
            {
                return false;
            }

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsTokenExpired(string token, out TimeSpan expiredBy, Action<string> warn = null)
        {
            expiredBy = TimeSpan.Zero;

            if (!TryGetJwtExpirationUtc(token, out var expiryUtc, warn))
            {
                return false;
            }

            if (DateTime.UtcNow <= expiryUtc)
            {
                return false;
            }

            expiredBy = DateTime.UtcNow - expiryUtc;
            return true;
        }

        public static async Task<string> GetValidBackendAuthTokenAsync(
            Action<string> info = null,
            Action<string> warn = null)
        {
            var jwtToken = GetValidStoredJwtToken(warn);
            if (!string.IsNullOrWhiteSpace(jwtToken))
            {
                return jwtToken;
            }

            warn?.Invoke("[WorkOSAuth] No valid backend JWT token found, attempting refresh...");

            await RefreshAccessTokenAsync(info, warn);
            jwtToken = GetValidStoredJwtToken(warn);
            if (!string.IsNullOrWhiteSpace(jwtToken))
            {
                return jwtToken;
            }

            warn?.Invoke("[WorkOSAuth] Refresh did not yield a valid backend JWT token.");
            return null;
        }

        public static async Task<string> RefreshBackendAuthTokenAsync(
            Action<string> info = null,
            Action<string> warn = null)
        {
            return await RefreshAccessTokenAsync(info, warn);
        }

        public static async Task<string> RefreshAccessTokenAsync(
            Action<string> info = null,
            Action<string> warn = null)
        {
            string refreshToken = GetStoredRefreshToken();
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                warn?.Invoke("[WorkOSAuth] No refresh token available for auto-login");
                return null;
            }

            await RefreshLock.WaitAsync();
            try
            {
                refreshToken = GetStoredRefreshToken();
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    warn?.Invoke("[WorkOSAuth] Refresh token cleared during auto-login attempt");
                    return null;
                }

                string refreshTokenFingerprint = GetTokenFingerprint(refreshToken);

                string clientId = WorkOSAuthConstants.GetClientId();
                if (string.IsNullOrWhiteSpace(clientId))
                {
                    warn?.Invoke("[WorkOSAuth] WorkOS client_id is not configured");
                    return null;
                }
                string configuredScope = WorkOSAuthConstants.RefreshScopes;
                info?.Invoke($"[WorkOSAuth] Refresh flow using environment='{WorkOSAuthConstants.GetEnvironment()}', client_id='{clientId}', scope='{configuredScope}'");
                info?.Invoke($"[WorkOSAuth] Refresh token fingerprint (request): {refreshTokenFingerprint}");

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds);
                    var tokenEndpointUrl = WorkOSAuthConstants.GetApiUrl();
                    info?.Invoke($"[WorkOSAuth] Refresh token endpoint URL: {tokenEndpointUrl}");
                    var scopeAttempts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(configuredScope))
                    {
                        scopeAttempts.Add(configuredScope);
                    }

                    // Some providers reject refresh requests when scope is included.
                    // If the first attempt reports invalid_refresh_token, retry once without scope.
                    scopeAttempts.Add(null);

                    for (int attemptIndex = 0; attemptIndex < scopeAttempts.Count; attemptIndex++)
                    {
                        string attemptScope = scopeAttempts[attemptIndex];
                        var requestData = new Dictionary<string, string>
                        {
                            { "grant_type", "refresh_token" },
                            { "refresh_token", refreshToken },
                            { "client_id", clientId }
                        };

                        if (!string.IsNullOrWhiteSpace(attemptScope))
                        {
                            requestData["scope"] = attemptScope;
                        }

                        var content = new FormUrlEncodedContent(requestData);
                        var response = await client.PostAsync(tokenEndpointUrl, content);
                        var responseText = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            warn?.Invoke($"[WorkOSAuth] Refresh token exchange failed: {response.StatusCode} {responseText}");

                            bool isInvalidRefreshTokenResponse = IsInvalidRefreshTokenResponse(responseText);
                            bool hasFallbackAttempt = attemptIndex < scopeAttempts.Count - 1;
                            if (isInvalidRefreshTokenResponse && hasFallbackAttempt && !string.IsNullOrWhiteSpace(attemptScope))
                            {
                                warn?.Invoke("[WorkOSAuth] Provider returned invalid_refresh_token with scoped refresh request. Retrying refresh once without scope.");
                                continue;
                            }

                            if (response.StatusCode == HttpStatusCode.BadRequest ||
                                response.StatusCode == HttpStatusCode.Unauthorized)
                            {
                                if (ShouldClearRefreshToken(responseText, out var clearReason))
                                {
                                    warn?.Invoke($"[WorkOSAuth] Terminal refresh-token failure detected ({clearReason}); preserving stored refresh token.");
                                }
                                else
                                {
                                    warn?.Invoke($"[WorkOSAuth] Preserving stored refresh token ({clearReason}).");
                                }
                            }

                            return null;
                        }

                        var tokenResponse = JObject.Parse(responseText);
                        string accessToken = tokenResponse["access_token"]?.Value<string>();
                        string idToken = tokenResponse["id_token"]?.Value<string>();
                        string newRefreshToken = tokenResponse["refresh_token"]?.Value<string>();
                        string tokenType = tokenResponse["token_type"]?.Value<string>();
                        int? expiresInSeconds = tokenResponse["expires_in"]?.Value<int?>();
                        string geniesId = tokenResponse["user"]?["genies_id"]?.Value<string>();
                        info?.Invoke(
                            $"[WorkOSAuth] Refresh response received (token_type='{tokenType ?? "unknown"}', expires_in={(expiresInSeconds?.ToString() ?? "unknown")}s, has_access_token={!string.IsNullOrEmpty(accessToken)}, has_id_token={!string.IsNullOrEmpty(idToken)}, has_refresh_token={!string.IsNullOrEmpty(newRefreshToken)}).");

                        if (string.IsNullOrEmpty(accessToken))
                        {
                            warn?.Invoke("[WorkOSAuth] Refresh token exchange missing access token");
                            return null;
                        }

                        if (string.IsNullOrEmpty(idToken))
                        {
                            warn?.Invoke("[WorkOSAuth] Refresh response did not include id_token. Backend auth will rely on access_token only.");
                        }

                        bool shouldStoreAccessToken = true;
                        if (IsValidJwtFormat(accessToken) &&
                            !IsJwtUsableForBackend(accessToken, clientId, warn, "refreshed_access_token"))
                        {
                            warn?.Invoke("[WorkOSAuth] Refreshed access token audience mismatch. Ignoring refreshed access token for backend auth.");
                            shouldStoreAccessToken = false;
                            ClearAccessToken();

                            if (string.IsNullOrEmpty(idToken))
                            {
                                warn?.Invoke("[WorkOSAuth] Refresh response missing id_token and access_token is not backend-compatible.");
                                return null;
                            }
                        }

                        if (!IsValidJwtFormat(accessToken))
                        {
                            warn?.Invoke("[WorkOSAuth] Refreshed access token is opaque (non-JWT). If id_token is missing/expired, backend JWT auth may fail.");
                        }

                        if (shouldStoreAccessToken)
                        {
                            StoreAccessToken(accessToken);
                        }
                        if (!string.IsNullOrEmpty(idToken))
                        {
                            StoreIdToken(idToken);
                        }

                        if (!string.IsNullOrEmpty(geniesId))
                        {
                            StoreUserId(geniesId);
                        }
                        else
                        {
                            // Warn the developer, but we should be able to continue.
                            warn?.Invoke("[WorkOSAuth] Refresh token exchange missing genies id");
                        }

                        if (!string.IsNullOrEmpty(newRefreshToken))
                        {
                            string newFingerprint = GetTokenFingerprint(newRefreshToken);
                            bool rotated = !string.Equals(newRefreshToken, refreshToken, StringComparison.Ordinal);
                            StoreRefreshToken(newRefreshToken);
                            info?.Invoke($"[WorkOSAuth] Refresh token fingerprint (response): {newFingerprint} (rotated={rotated})");
                        }
                        else
                        {
                            info?.Invoke("[WorkOSAuth] Refresh response did not include a new refresh_token. Keeping previously stored refresh token.");
                        }

                        string email = tokenResponse["user"]?["email"]?.Value<string>();
                        if (!string.IsNullOrEmpty(email))
                        {
                            StoreEmail(email);
                        }

                        info?.Invoke("[WorkOSAuth] OAuth token refreshed");
                        string backendToken = GetValidStoredJwtToken(warn);
                        if (string.IsNullOrWhiteSpace(backendToken))
                        {
                            warn?.Invoke("[WorkOSAuth] Refresh completed but no backend-compatible JWT token is currently stored.");
                            return null;
                        }

                        bool tokenMatchesId = string.Equals(backendToken, GetStoredIdToken(), StringComparison.Ordinal);
                        bool tokenMatchesAccess = string.Equals(backendToken, GetStoredAccessToken(), StringComparison.Ordinal);
                        string tokenSource = tokenMatchesId ? "id_token" : (tokenMatchesAccess ? "access_token" : "unknown");
                        info?.Invoke($"[WorkOSAuth] Backend-compatible token selected after refresh: {tokenSource} (len={backendToken.Length}).");
                        return backendToken;
                    }

                    return null;
                }
            }
            catch (Exception ex)
            {
                warn?.Invoke($"[WorkOSAuth] Refresh token exchange error: {ex.Message}");
                return null;
            }
            finally
            {
                RefreshLock.Release();
            }
        }

        private static bool IsInvalidRefreshTokenResponse(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return false;
            }

            try
            {
                var errorResponse = JObject.Parse(responseText);
                var error = errorResponse["error"]?.Value<string>();
                if (string.Equals(error, OAuthErrorInvalidRefreshToken, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // Fall through to string check.
            }

            return responseText.IndexOf(OAuthErrorInvalidRefreshToken, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetValidStoredJwtToken(Action<string> warn)
        {
            string expectedClientId = WorkOSAuthConstants.GetClientId();

            string idToken = GetStoredIdToken();
            if (!string.IsNullOrWhiteSpace(idToken))
            {
                if (IsJwtUsableForBackend(idToken, expectedClientId, warn, "id_token"))
                {
                    return idToken;
                }
            }

            string accessToken = GetStoredAccessToken();
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                if (IsJwtUsableForBackend(accessToken, expectedClientId, warn, "access_token"))
                {
                    return accessToken;
                }
            }

            return null;
        }

        private static bool IsJwtUsableForBackend(string token, string expectedAudience, Action<string> warn, string tokenLabel)
        {
            if (!IsValidJwtFormat(token))
            {
                return false;
            }

            if (IsTokenExpired(token, out _, warn))
            {
                warn?.Invoke($"[WorkOSAuth] Stored {tokenLabel} is expired");
                return false;
            }

            if (string.IsNullOrWhiteSpace(expectedAudience))
            {
                return true;
            }

            if (!TryGetJwtAudience(token, out var audienceValues))
            {
                warn?.Invoke($"[WorkOSAuth] Stored {tokenLabel} missing/invalid aud claim");
                return false;
            }

            foreach (var aud in audienceValues)
            {
                if (string.Equals(aud, expectedAudience, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            warn?.Invoke($"[WorkOSAuth] Stored {tokenLabel} aud mismatch. Expected '{expectedAudience}', got '{string.Join(", ", audienceValues)}'");
            return false;
        }

        private static bool TryGetJwtAudience(string token, out List<string> audienceValues)
        {
            audienceValues = new List<string>();

            try
            {
                var parts = token.Split('.');
                if (parts.Length != JwtPartsCount)
                {
                    return false;
                }

                var payloadJson = DecodeBase64Url(parts[1]);
                if (string.IsNullOrEmpty(payloadJson))
                {
                    return false;
                }

                var payload = JObject.Parse(payloadJson);
                var audToken = payload["aud"];
                if (audToken == null)
                {
                    return false;
                }

                if (audToken.Type == JTokenType.String)
                {
                    var aud = audToken.Value<string>();
                    if (!string.IsNullOrWhiteSpace(aud))
                    {
                        audienceValues.Add(aud);
                    }
                }
                else if (audToken.Type == JTokenType.Array)
                {
                    foreach (var aud in audToken.Values<string>())
                    {
                        if (!string.IsNullOrWhiteSpace(aud))
                        {
                            audienceValues.Add(aud);
                        }
                    }
                }

                return audienceValues.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetJwtExpirationUtc(string token, out DateTime expiryUtc, Action<string> warn)
        {
            expiryUtc = default;

            try
            {
                var parts = token.Split('.');
                if (parts.Length != JwtPartsCount)
                {
                    return false;
                }

                var payloadJson = DecodeBase64Url(parts[1]);
                if (string.IsNullOrEmpty(payloadJson))
                {
                    return false;
                }

                var payload = JObject.Parse(payloadJson);
                var expToken = payload["exp"];
                if (expToken == null)
                {
                    return false;
                }

                long expSeconds;
                if (expToken.Type == JTokenType.Integer)
                {
                    expSeconds = expToken.Value<long>();
                }
                else if (expToken.Type == JTokenType.String && long.TryParse(expToken.Value<string>(), out var parsed))
                {
                    expSeconds = parsed;
                }
                else
                {
                    return false;
                }

                expiryUtc = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
                return true;
            }
            catch (Exception ex)
            {
                warn?.Invoke($"[WorkOSAuth] Failed to parse JWT expiration: {ex.Message}");
                return false;
            }
        }

        private static string DecodeBase64Url(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            string base64 = value.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % Base64PadModulo)
            {
                case Base64PadCaseTwo:
                    base64 += "==";
                    break;
                case Base64PadCaseThree:
                    base64 += "=";
                    break;
                case Base64PadCaseZero:
                    break;
                default:
                    return null;
            }

            var bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }

        private static bool ShouldClearRefreshToken(string responseText, out string reason)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                reason = "provider response body was empty";
                return false;
            }

            string error = null;
            string errorDescription = null;
            string errorMessage = null;
            try
            {
                var errorResponse = JObject.Parse(responseText);
                error = errorResponse["error"]?.Value<string>();
                errorDescription = errorResponse["error_description"]?.Value<string>();
                errorMessage = errorResponse["message"]?.Value<string>();
                if (string.Equals(error, OAuthErrorInvalidClient, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(error, OAuthErrorUnauthorizedClient, StringComparison.OrdinalIgnoreCase))
                {
                    reason = $"oauth error '{error}' indicates client configuration/authorization issue, not refresh-token invalidation";
                    return false;
                }

                if (string.Equals(error, OAuthErrorInvalidRefreshToken, StringComparison.OrdinalIgnoreCase))
                {
                    reason = $"oauth error '{OAuthErrorInvalidRefreshToken}'";
                    return true;
                }

                if (string.Equals(error, OAuthErrorInvalidGrant, StringComparison.OrdinalIgnoreCase))
                {
                    if (IsRefreshTokenRevokedLikeDescription(errorDescription) ||
                        IsRefreshTokenRevokedLikeDescription(errorMessage))
                    {
                        reason = "oauth error 'invalid_grant' with explicit refresh-token invalidation detail";
                        return true;
                    }

                    reason = "oauth error 'invalid_grant' without explicit refresh-token invalidation detail";
                    return false;
                }

                if (IsRefreshTokenRevokedLikeDescription(errorDescription) ||
                    IsRefreshTokenRevokedLikeDescription(errorMessage))
                {
                    reason = "provider response explicitly describes an invalid/expired/revoked refresh token";
                    return true;
                }
            }
            catch
            {
                // Fall through to string checks for non-JSON responses.
            }

            if (responseText.IndexOf(OAuthErrorInvalidClient, StringComparison.OrdinalIgnoreCase) >= 0 ||
                responseText.IndexOf(OAuthErrorUnauthorizedClient, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                reason = "raw provider response indicates client configuration/authorization issue";
                return false;
            }

            if (responseText.IndexOf(OAuthErrorInvalidRefreshToken, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                reason = $"raw provider response includes '{OAuthErrorInvalidRefreshToken}'";
                return true;
            }

            if (responseText.IndexOf(OAuthErrorInvalidGrant, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (IsRefreshTokenRevokedLikeDescription(responseText))
                {
                    reason = "raw provider response includes invalid_grant plus explicit refresh-token invalidation detail";
                    return true;
                }

                reason = "raw provider response includes invalid_grant without explicit refresh-token invalidation detail";
                return false;
            }

            if (IsRefreshTokenRevokedLikeDescription(responseText))
            {
                reason = "raw provider response explicitly describes an invalid/expired/revoked refresh token";
                return true;
            }

            reason = "provider response did not conclusively indicate refresh-token invalidation";
            return false;
        }

        private static string GetTokenFingerprint(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return "<missing>";
            }

            byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
            byte[] hashBytes;
            using (var sha = SHA256.Create())
            {
                hashBytes = sha.ComputeHash(tokenBytes);
            }
            string hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty);
            const int prefixLength = 12;
            string shortHash = hash.Length > prefixLength ? hash.Substring(0, prefixLength) : hash;
            return $"{shortHash} (len={token.Length})";
        }

        private static bool IsRefreshTokenRevokedLikeDescription(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            bool mentionsRefreshToken =
                text.IndexOf("refresh", StringComparison.OrdinalIgnoreCase) >= 0 &&
                text.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0;

            bool invalidationHint =
                text.IndexOf("expired", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("revoked", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0;

            return mentionsRefreshToken && invalidationHint;
        }

        public static void StoreUserId(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            EditorPrefs.SetString($"{PrefixKey}{WorkOSAuthConstants.PrefKeyUserId}", userId);
        }

        public static string GetStoredUserId()
        {
            string userId = EditorPrefs.GetString($"{PrefixKey}{WorkOSAuthConstants.PrefKeyUserId}", "");
            if (!string.IsNullOrWhiteSpace(userId))
            {
                return userId;
            }

            // Legacy (non-prefixed) fallback.
            userId = EditorPrefs.GetString(WorkOSAuthConstants.PrefKeyUserId, "");
            if (!string.IsNullOrWhiteSpace(userId))
            {
                StoreUserId(userId);
                EditorPrefs.DeleteKey(WorkOSAuthConstants.PrefKeyUserId);
                return userId;
            }

            return "";
        }

        public static void ClearUserId()
        {
            EditorPrefs.DeleteKey($"{PrefixKey}{WorkOSAuthConstants.PrefKeyUserId}");
            EditorPrefs.DeleteKey(WorkOSAuthConstants.PrefKeyUserId);
        }
    }
}
