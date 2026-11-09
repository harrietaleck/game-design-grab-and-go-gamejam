using System;
using System.Collections.Generic;
using Genies.Sdk.ArtForge.Editor.Auth;
using UnityEditor;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.API
{
    /// <summary>
    /// Deployment environment descriptor for the Content API backend.
    /// </summary>
    internal sealed class DeploymentEnvironment
    {
        private const string ProductionDeploymentUrl = "https://content-api.genies.com";
        private const string ProductionHttpActionsUrl = "https://content.genies.com";
        private const string SettingsPrefPrefix = "GAF_Settings_";
        private const char CustomEnvironmentFieldSeparator = '|';
        private const int CustomEnvironmentMaxFieldCount = 6;

        public static DeploymentEnvironment Production { get; } =
            new DeploymentEnvironment(
                WorkOSAuthConstants.EnvironmentProduction,
                ProductionDeploymentUrl,
                ProductionHttpActionsUrl,
                WorkOSAuthConstants.ProductionClientId,
                true);

        public string Name { get; }
        public string Url { get; }
        public string ApiUrl { get; }
        public string HttpUrl { get; }
        public string ClientId { get; }
        public string AuthUrl { get; }
        public string TokenUrl { get; }
        public bool IsBuiltIn { get; }
        public bool UseLegacyAssetOwnerQueries { get; }

        public DeploymentEnvironment(string name, string url, bool isBuiltIn = false)
            : this(name, url, DeriveHttpActionsUrl(url), WorkOSAuthConstants.ProductionClientId, isBuiltIn)
        {
        }

        public DeploymentEnvironment(
            string name,
            string apiUrl,
            string httpUrl,
            string clientId,
            bool isBuiltIn = false,
            bool useLegacyAssetOwnerQueries = false,
            string authUrl = null,
            string tokenUrl = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Environment name cannot be null or empty.", nameof(name));
            }

            if (!TryNormalizeDeploymentUrl(apiUrl, out string normalizedApiUrl))
            {
                throw new ArgumentException("Environment API URL must be an absolute HTTP(S) URL.", nameof(apiUrl));
            }

            if (!TryNormalizeDeploymentUrl(httpUrl, out string normalizedHttpUrl))
            {
                throw new ArgumentException("Environment HTTP URL must be an absolute HTTP(S) URL.", nameof(httpUrl));
            }

            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentException("Environment WorkOS client id cannot be null or empty.", nameof(clientId));
            }

            string normalizedAuthUrl = null;
            if (!string.IsNullOrWhiteSpace(authUrl) && !TryNormalizeDeploymentUrl(authUrl, out normalizedAuthUrl))
            {
                throw new ArgumentException("Environment Auth URL must be an absolute HTTP(S) URL when provided.", nameof(authUrl));
            }

            string normalizedTokenUrl = null;
            if (!string.IsNullOrWhiteSpace(tokenUrl) && !TryNormalizeDeploymentUrl(tokenUrl, out normalizedTokenUrl))
            {
                throw new ArgumentException("Environment Token URL must be an absolute HTTP(S) URL when provided.", nameof(tokenUrl));
            }

            Name = name.Trim();
            ApiUrl = normalizedApiUrl;
            HttpUrl = normalizedHttpUrl;
            ClientId = clientId.Trim();
            AuthUrl = normalizedAuthUrl;
            TokenUrl = normalizedTokenUrl;
            Url = ApiUrl;
            IsBuiltIn = isBuiltIn;
            UseLegacyAssetOwnerQueries = useLegacyAssetOwnerQueries;
        }

        public override string ToString() => Name;

        /// <summary>
        /// Returns all selectable backend environments.
        /// Includes built-in Production and any custom environments declared by local developers.
        /// </summary>
        public static IReadOnlyList<DeploymentEnvironment> GetAvailableEnvironments()
        {
            var environments = new List<DeploymentEnvironment> { Production };
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Production.Name };

            foreach (var environment in LoadCustomEnvironments())
            {
                if (seenNames.Add(environment.Name))
                {
                    environments.Add(environment);
                }
            }

            return environments;
        }

        /// <summary>
        /// Resolves a saved/display name to a known environment. Falls back to Production when not found.
        /// </summary>
        public static DeploymentEnvironment ResolveEnvironment(string environmentName)
        {
            if (!string.IsNullOrWhiteSpace(environmentName))
            {
                string normalizedName = environmentName.Trim();
                foreach (var environment in GetAvailableEnvironments())
                {
                    if (string.Equals(environment.Name, normalizedName, StringComparison.OrdinalIgnoreCase))
                    {
                        return environment;
                    }
                }
            }

            return Production;
        }

        internal static string GetDeploymentUrl(DeploymentEnvironment environment)
            => (environment ?? Production).ApiUrl;

        internal static string GetPaymentsBaseUrl(DeploymentEnvironment environment)
        {
            if (environment == null)
            {
                return ProductionHttpActionsUrl.TrimEnd('/');
            }

            if (!string.IsNullOrWhiteSpace(environment.HttpUrl))
            {
                return environment.HttpUrl.TrimEnd('/');
            }

            return DeriveHttpActionsUrl(environment.ApiUrl);
        }

        private static IEnumerable<DeploymentEnvironment> LoadCustomEnvironments()
        {
            string prefKey = $"{SettingsPrefPrefix}{WorkOSAuthConstants.PrefKeyCustomEnvironments}";
            string editorPrefsValue = EditorPrefs.GetString(prefKey, string.Empty);
            if (string.IsNullOrWhiteSpace(editorPrefsValue))
            {
                editorPrefsValue = EditorPrefs.GetString(WorkOSAuthConstants.PrefKeyCustomEnvironments, string.Empty);
            }

            foreach (var environment in ParseCustomEnvironmentEntries(editorPrefsValue))
            {
                yield return environment;
            }
        }

        private static IEnumerable<DeploymentEnvironment> ParseCustomEnvironmentEntries(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            var entries = value.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawEntry in entries)
            {
                string entry = rawEntry?.Trim();
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                int separatorIndex = entry.IndexOf('=');
                if (separatorIndex <= 0 || separatorIndex >= entry.Length - 1)
                {
                    continue;
                }

                string name = entry.Substring(0, separatorIndex).Trim();
                string serializedValue = entry.Substring(separatorIndex + 1).Trim();
                if (string.IsNullOrWhiteSpace(name) ||
                    string.Equals(name, WorkOSAuthConstants.EnvironmentProduction, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!TryBuildEnvironmentFromSerializedValue(name, serializedValue, out DeploymentEnvironment environment))
                {
                    continue;
                }

                yield return environment;
            }
        }

        private static bool TryBuildEnvironmentFromSerializedValue(
            string name,
            string serializedValue,
            out DeploymentEnvironment environment)
        {
            environment = null;
            if (string.IsNullOrWhiteSpace(serializedValue))
            {
                return false;
            }

            if (TryParseExtendedEnvironmentValue(
                serializedValue,
                out string apiUrl,
                out string httpUrl,
                out string clientId,
                out bool useLegacyAssetOwnerQueries,
                out string authUrl,
                out string tokenUrl))
            {
                environment = new DeploymentEnvironment(
                    name, apiUrl, httpUrl, clientId, false,
                    useLegacyAssetOwnerQueries, authUrl, tokenUrl);
                return true;
            }

            if (!TryNormalizeDeploymentUrl(serializedValue, out string normalizedApiUrl))
            {
                return false;
            }

            environment = new DeploymentEnvironment(
                name, normalizedApiUrl, DeriveHttpActionsUrl(normalizedApiUrl),
                WorkOSAuthConstants.ProductionClientId);
            return true;
        }

        private static bool TryParseExtendedEnvironmentValue(
            string serializedValue,
            out string apiUrl,
            out string httpUrl,
            out string clientId,
            out bool useLegacyAssetOwnerQueries,
            out string authUrl,
            out string tokenUrl)
        {
            apiUrl = null;
            httpUrl = null;
            clientId = null;
            useLegacyAssetOwnerQueries = false;
            authUrl = null;
            tokenUrl = null;

            string[] fields = serializedValue.Split(
                new[] { CustomEnvironmentFieldSeparator },
                CustomEnvironmentMaxFieldCount,
                StringSplitOptions.None);

            if (fields.Length < 2)
            {
                return false;
            }

            if (!TryNormalizeDeploymentUrl(fields[0], out apiUrl))
            {
                return false;
            }

            if (!TryNormalizeDeploymentUrl(fields[1], out httpUrl))
            {
                return false;
            }

            clientId = fields.Length >= 3 ? fields[2]?.Trim() : WorkOSAuthConstants.ProductionClientId;
            if (string.IsNullOrWhiteSpace(clientId))
            {
                clientId = WorkOSAuthConstants.ProductionClientId;
            }

            useLegacyAssetOwnerQueries = ParseBooleanField(fields.Length >= 4 ? fields[3] : null, false);

            authUrl = fields.Length >= 5 ? fields[4]?.Trim() : null;
            if (!string.IsNullOrWhiteSpace(authUrl) && !TryNormalizeDeploymentUrl(authUrl, out authUrl))
            {
                return false;
            }

            tokenUrl = fields.Length >= 6 ? fields[5]?.Trim() : null;
            if (!string.IsNullOrWhiteSpace(tokenUrl) && !TryNormalizeDeploymentUrl(tokenUrl, out tokenUrl))
            {
                return false;
            }

            return true;
        }

        private static bool ParseBooleanField(string value, bool fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            string normalized = value.Trim();
            if (bool.TryParse(normalized, out bool parsed))
            {
                return parsed;
            }

            if (string.Equals(normalized, "1", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(normalized, "0", StringComparison.Ordinal))
            {
                return false;
            }

            return fallback;
        }

        private static bool TryNormalizeDeploymentUrl(string url, out string normalizedUrl)
        {
            normalizedUrl = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri uri))
            {
                return false;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            normalizedUrl = uri.AbsoluteUri.TrimEnd('/');
            return true;
        }

        private static string DeriveHttpActionsUrl(string deploymentUrl)
        {
            if (string.IsNullOrWhiteSpace(deploymentUrl))
            {
                return deploymentUrl;
            }

            if (!TryNormalizeDeploymentUrl(deploymentUrl, out string normalizedDeploymentUrl))
            {
                return deploymentUrl.TrimEnd('/');
            }

            if (string.Equals(normalizedDeploymentUrl, ProductionDeploymentUrl, StringComparison.OrdinalIgnoreCase))
            {
                return ProductionHttpActionsUrl.TrimEnd('/');
            }

            if (!Uri.TryCreate(normalizedDeploymentUrl, UriKind.Absolute, out var uri))
            {
                return deploymentUrl.TrimEnd('/');
            }

            const string ConvexCloudSuffix = ".convex.cloud";
            if (!uri.Host.EndsWith(ConvexCloudSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedDeploymentUrl;
            }

            string hostPrefix = uri.Host.Substring(0, uri.Host.Length - ConvexCloudSuffix.Length);
            var builder = new UriBuilder(uri)
            {
                Host = $"{hostPrefix}.convex.site",
                Path = string.Empty,
                Query = string.Empty,
                Fragment = string.Empty
            };

            return builder.Uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        }
    }
}
