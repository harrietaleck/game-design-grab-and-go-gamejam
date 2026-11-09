using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Genies.Sdk.ArtForge.Editor.Auth;
using Newtonsoft.Json.Linq;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.API
{
    /// <summary>
    /// HTTP client for the Genies Payments API balance endpoint.
    /// </summary>
    internal sealed class PaymentsClient
    {
        private const int HttpTimeoutSeconds = 30;
        private const string BalanceRoute = "/api/payments/balance";

        private readonly string _baseUrl;
        private readonly ILogger _logger;
        private readonly Action<string, string> _logAuthDiagnostics;

        internal PaymentsClient(
            string baseUrl,
            ILogger logger,
            Action<string, string> logAuthDiagnostics)
        {
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logAuthDiagnostics = logAuthDiagnostics;
        }

        internal async Task<PaymentsBalanceSnapshot> GetBalanceAsync(CancellationToken cancellationToken = default)
        {
            string authToken = await WorkOSAuthService.GetValidBackendAuthTokenAsync(
                _logger.Info, _logger.Warning);
            if (string.IsNullOrWhiteSpace(authToken))
            {
                throw new InvalidOperationException("No valid WorkOS backend auth token available.");
            }

            string endpoint = $"{_baseUrl}{BalanceRoute}";
            _logger.Info(
                $"[BackendClient][PaymentsAuthDiag] environment='{WorkOSAuthConstants.GetEnvironment()}', " +
                $"client_id='{WorkOSAuthConstants.GetClientId()}', endpoint='{endpoint}'");
            _logAuthDiagnostics?.Invoke("[BackendClient][PaymentsAuthDiag]", authToken);

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds) };
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            using var response = await client.SendAsync(request, cancellationToken);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Failed to fetch balance: {(int)response.StatusCode} {response.ReasonPhrase} {responseBody}\n" +
                    $"Try curl: curl -i -H \"Authorization: Bearer $WORKOS_BACKEND_TOKEN\" \"{endpoint}\"");
            }

            return ParseSnapshot(responseBody);
        }

        private static PaymentsBalanceSnapshot ParseSnapshot(string responseBody)
        {
            var root = JObject.Parse(responseBody);
            var payload = root["data"] as JObject ?? root;

            return new PaymentsBalanceSnapshot
            {
                Balance = ReadNumericField(payload, "balance"),
                FreeUsageBalance = ReadNumericField(payload, "freeUsageBalance"),
                FifoBalance = ReadNumericField(payload, "fifoBalance"),
                LowBalanceWarning = ReadBoolField(payload, "lowBalanceWarning")
            };
        }

        private static double ReadNumericField(JObject payload, string fieldName)
        {
            if (payload == null ||
                !payload.TryGetValue(fieldName, StringComparison.OrdinalIgnoreCase, out var token) ||
                token == null)
            {
                return 0d;
            }

            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                return token.Value<double>();
            }

            if (token.Type == JTokenType.String &&
                double.TryParse(
                    token.Value<string>(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                return parsed;
            }

            return 0d;
        }

        private static bool ReadBoolField(JObject payload, string fieldName)
        {
            if (payload == null ||
                !payload.TryGetValue(fieldName, StringComparison.OrdinalIgnoreCase, out var token) ||
                token == null)
            {
                return false;
            }

            if (token.Type == JTokenType.Boolean)
            {
                return token.Value<bool>();
            }

            if (token.Type == JTokenType.String &&
                bool.TryParse(token.Value<string>(), out var parsed))
            {
                return parsed;
            }

            return false;
        }
    }

    /// <summary>
    /// Snapshot of the current payments balance for the authenticated user.
    /// </summary>
    internal sealed class PaymentsBalanceSnapshot
    {
        public double Balance { get; set; }
        public double FreeUsageBalance { get; set; }
        public double FifoBalance { get; set; }
        public bool LowBalanceWarning { get; set; }
    }
}
