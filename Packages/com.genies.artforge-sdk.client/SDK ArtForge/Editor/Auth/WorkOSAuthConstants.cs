using UnityEditor;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.API;

namespace Genies.Sdk.ArtForge.Editor.Auth
{
    /// <summary>
    /// Centralized constants for WorkOS authentication used across Editor tools.
    /// Single source of truth for credentials storage and API configuration.
    /// </summary>
    internal static class WorkOSAuthConstants
    {
        // EditorPrefs keys for credential storage
        public const string PrefKeyEmail = "GeniesArtForge_Email";
        public const string PrefKeyEnvironment = "GeniesArtForge_Environment";
        public const string PrefKeyCustomEnvironments = "GeniesArtForge_CustomBackendEnvironments";
        public const string PrefKeyAccessToken = "GeniesArtForge_AccessToken";
        public const string PrefKeyRefreshToken = "GeniesArtForge_RefreshToken";
        public const string PrefKeyIdToken = "GeniesArtForge_IdToken";
        public static string PrefKeyUserId = "GeniesArtForge_UserId";

        public const string EnvironmentProduction = "Production";
        public const string AuthorizationScopes = "openid profile email offline_access";
        public const string RefreshScopes = "openid profile email";

        // WorkOS production public client id for OAuth PKCE.
        internal const string ProductionClientId = "client_01KK02AFAX363DKPK2AXM5RQQD";
        private const string PrefKeyPrefix = "GAF_Settings_";
        private const string DefaultEnvironment = EnvironmentProduction;

        // AuthKit base URL and endpoint paths.
        private const string BaseApiUrl = "https://fastidious-food-91.authkit.app/";
        private const string TokenApiPath = "/oauth2/token";
        private const string AuthorizeApiPath = "/oauth2/authorize";

        public static string GetEnvironment()
        {
            return NormalizeEnvironment(EditorPrefs.GetString($"{PrefKeyPrefix}{PrefKeyEnvironment}", DefaultEnvironment));
        }

        /// <summary>
        /// Gets WorkOS client ID for the currently selected backend environment.
        /// </summary>
        public static string GetClientId()
        {
            var environment = ContentClient.ResolveEnvironment(GetEnvironment());
            if (!string.IsNullOrWhiteSpace(environment?.ClientId))
            {
                return environment.ClientId;
            }

            return ProductionClientId;
        }

        /// <summary>
        /// Gets the OAuth token endpoint URL.
        /// </summary>
        public static string GetApiUrl()
        {
            var environment = ContentClient.ResolveEnvironment(GetEnvironment());
            if (!string.IsNullOrWhiteSpace(environment?.TokenUrl))
            {
                return environment.TokenUrl;
            }

            return BuildApiUrl(TokenApiPath);
        }

        /// <summary>
        /// Gets the OAuth authorize endpoint URL.
        /// </summary>
        public static string GetAuthorizeUrl()
        {
            var environment = ContentClient.ResolveEnvironment(GetEnvironment());
            if (!string.IsNullOrWhiteSpace(environment?.AuthUrl))
            {
                return environment.AuthUrl;
            }

            return BuildApiUrl(AuthorizeApiPath);
        }

        private static string NormalizeEnvironment(string environment)
        {
            return string.IsNullOrWhiteSpace(environment) ? DefaultEnvironment : environment.Trim();
        }

        private static string BuildApiUrl(string path)
        {
            return $"{BaseApiUrl.TrimEnd('/')}/{path.TrimStart('/')}";
        }
    }
}
