using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Genies.Sdk.ArtForge.Editor.Auth;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using Genies.Sdk.ArtForge.Editor.Core.Application.Services;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.API;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using Genies.Sdk.ArtForge.Editor.Core.UI.Helpers;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Genies.Sdk.ArtForge.Editor.Components.Settings
{
    /// <summary>
    /// Backend settings UI + auth/disconnect + connection status.
    /// Uses GenerationService directly (backend-specific component).
    /// </summary>
    internal sealed class BackendSettingsSection : IBackendSettingsSection
    {
        // ------------------------------------------------------------
        // Telemetry signals (Observer subscribes to these)
        // ------------------------------------------------------------
        public static event Action<string, Dictionary<string, object>> ArtForgeAuthTelemetry;

        private static bool _tokenInvalidDetectedSent;

        private readonly GenerationService _backendService;
        private readonly Func<string, string, string> _getSettingString;
        private readonly Action<string, string> _saveSettingString;
        private readonly Action<string, bool> _showStatus;
        private readonly ILogger _logger;

        private readonly VisualElement _root;
        private readonly List<DeploymentEnvironment> _availableEnvironments;
        private readonly DropdownField _environmentDropdown;
        private readonly Label _environmentHintLabel;
        private readonly Button _authenticateButton;
        private readonly Button _disconnectButton;
        private readonly Label _connectionStatusLabel;
        private static readonly Color AuthenticateButtonEnabledBackground = StyleHelpers.GetCategoryColor("Project");
        private static readonly Color AuthenticateButtonDisabledBackground = new Color(0.25f, 0.25f, 0.25f);
        private static readonly Color AuthenticateButtonDisabledText = StyleHelpers.Colors.TextHint;
        private const float DisabledButtonOpacity = 0.7f;

        private bool _isAuthenticating;
        private bool _isSwitchingEnvironment;

        public VisualElement Root => _root;

        public BackendSettingsSection(
            GenerationService backendService,
            Func<string, string, string> getSettingString,
            Action<string, string> saveSettingString,
            Action<string, bool> showStatus)
            : this(backendService, getSettingString, saveSettingString, showStatus, null)
        {
        }

        public BackendSettingsSection(
            GenerationService backendService,
            Func<string, string, string> getSettingString,
            Action<string, string> saveSettingString,
            Action<string, bool> showStatus,
            ILogger logger)
        {
            _backendService = backendService ?? throw new ArgumentNullException(nameof(backendService));
            _getSettingString = getSettingString ?? throw new ArgumentNullException(nameof(getSettingString));
            _saveSettingString = saveSettingString ?? throw new ArgumentNullException(nameof(saveSettingString));
            _showStatus = showStatus ?? throw new ArgumentNullException(nameof(showStatus));
            _logger = logger ?? GeniesArtForgeService.Logger ?? new UnityLogger();

            _root = new VisualElement();
            _root.style.paddingLeft = 15;
            _root.style.paddingTop = 10;
            _root.style.paddingBottom = 10;
            _root.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.3f);
            _root.style.borderTopWidth = 1;
            _root.style.borderBottomWidth = 1;
            _root.style.borderLeftWidth = 1;
            _root.style.borderRightWidth = 1;
            _root.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            _root.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            _root.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            _root.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            _root.style.borderTopLeftRadius = 4;
            _root.style.borderTopRightRadius = 4;
            _root.style.borderBottomLeftRadius = 4;
            _root.style.borderBottomRightRadius = 4;
            _root.style.marginBottom = 15;

            var backendTitle = new Label("Backend Configuration");
            backendTitle.style.fontSize = 14;
            backendTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            backendTitle.style.marginBottom = 10;
            backendTitle.style.color = StyleHelpers.Colors.TextPrimary;
            _root.Add(backendTitle);

            _availableEnvironments = new List<DeploymentEnvironment>(ContentClient.GetAvailableEnvironments());

            string savedEnvironment = _getSettingString(
                WorkOSAuthConstants.PrefKeyEnvironment,
                WorkOSAuthConstants.EnvironmentProduction);
            var initialEnvironment = ContentClient.ResolveEnvironment(savedEnvironment);

            // Normalize persisted value to a valid option for the current environment list.
            _saveSettingString(WorkOSAuthConstants.PrefKeyEnvironment, initialEnvironment.Name);

            _environmentDropdown = new DropdownField("Environment");
            _environmentDropdown.choices = _availableEnvironments.Select(environment => environment.Name).ToList();
            _environmentDropdown.value = initialEnvironment.Name;
            _environmentDropdown.style.marginBottom = 10;
            _environmentDropdown.RegisterValueChangedCallback(evt =>
            {
                _ = HandleEnvironmentChangedAsync(evt.newValue);
            });
            _root.Add(_environmentDropdown);

            _environmentHintLabel = new Label("Select the backend environment to connect to.");
            _environmentHintLabel.style.fontSize = 10;
            _environmentHintLabel.style.marginBottom = 15;
            _environmentHintLabel.style.marginTop = -5;
            _environmentHintLabel.style.marginLeft = 3;
            _environmentHintLabel.style.color = StyleHelpers.Colors.TextHint;
            _root.Add(_environmentHintLabel);

            bool shouldShowEnvironmentSelector = _availableEnvironments.Count > 1;
            _environmentDropdown.style.display = shouldShowEnvironmentSelector ? DisplayStyle.Flex : DisplayStyle.None;
            _environmentHintLabel.style.display = shouldShowEnvironmentSelector ? DisplayStyle.Flex : DisplayStyle.None;

            _connectionStatusLabel = new Label();
            _connectionStatusLabel.style.fontSize = 12;
            _connectionStatusLabel.style.marginBottom = 15;
            _connectionStatusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _root.Add(_connectionStatusLabel);

            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.marginBottom = 10;
            _root.Add(buttonContainer);

            _authenticateButton = new Button(OnOAuthClicked) { text = "Login" };
            _authenticateButton.style.width = 150;
            _authenticateButton.style.marginRight = 10;
            StyleHelpers.ApplyPrimaryButtonStyle(_authenticateButton);
            buttonContainer.Add(_authenticateButton);

            _disconnectButton = new Button(OnDisconnectClicked) { text = "Disconnect" };
            _disconnectButton.style.width = 150;
            StyleHelpers.ApplySecondaryButtonStyle(_disconnectButton);
            buttonContainer.Add(_disconnectButton);

            RefreshConnectionStatus();
        }

        public void SetVisible(bool visible)
        {
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void RefreshConnectionStatus()
        {
            bool isAuthenticated = _backendService.IsBackendAvailable;

            if (isAuthenticated)
            {
                ContentClient backendClient = _backendService.GetBackendClient();
                bool isWebSocketConnected = backendClient?.IsConnected ?? false;

                _connectionStatusLabel.text = isWebSocketConnected
                    ? "● Connected (WebSocket active)"
                    : "● Authenticated (WebSocket pending)";

                _connectionStatusLabel.style.color = StyleHelpers.Colors.Success;
                SetAuthenticateButtonEnabled(false);
                _disconnectButton.SetEnabled(true);
            }
            else
            {
                _connectionStatusLabel.text = "○ Not Authenticated";
                _connectionStatusLabel.style.color = StyleHelpers.Colors.TextSecondary;
                SetAuthenticateButtonEnabled(true);
                _disconnectButton.SetEnabled(false);

                if (!_tokenInvalidDetectedSent && WorkOSAuthService.HasStoredTokens())
                {
                    _tokenInvalidDetectedSent = true;

                    ArtForgeAuthTelemetry?.Invoke(
                        "auth_token_invalid_detected",
                        new Dictionary<string, object>
                        {
                            { "reason", "expired_or_invalid" }
                        }
                    );
                }
            }
        }

        private async void OnOAuthClicked()
        {
            if (_isAuthenticating)
            {
                return;
            }

            ArtForgeAuthTelemetry?.Invoke(
                "auth_login_cta_clicked",
                new Dictionary<string, object>
                {
                    { "tab_name", "settings" }
                }
            );

            _isAuthenticating = true;
            SetAuthenticateButtonEnabled(false);
            _authenticateButton.text = "Logging in...";
            string clientId = WorkOSAuthConstants.GetClientId();
            if (string.IsNullOrWhiteSpace(clientId))
            {
                ArtForgeAuthTelemetry?.Invoke(
                    "auth_login_failed",
                    new Dictionary<string, object>
                    {
                        { "error_code", "missing_client_id" },
                        { "error_message_short", "Client ID is missing" },
                        { "latency_ms", 0L }
                    }
                );

                _showStatus("Client ID is missing from auth constants.", false);
                SetAuthenticateButtonEnabled(true);
                _authenticateButton.text = "Login";
                _isAuthenticating = false;
                return;
            }

            _showStatus("Opening browser for login...", true);

            long startedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            ArtForgeAuthTelemetry?.Invoke(
                "auth_login_started",
                new Dictionary<string, object>
                {
                    { "auth_provider", "workos_oauth" }
                }
            );

            try
            {
                var environment = GetSelectedEnvironment();

                // Start OAuth flow
                using (var oauthFlow = new WorkOSOAuthFlow(_logger))
                {
                    string jwtToken = await oauthFlow.AuthenticateAsync();

                    long latencyMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startedAtMs;

                    if (string.IsNullOrEmpty(jwtToken))
                    {
                        ArtForgeAuthTelemetry?.Invoke(
                            "auth_login_failed",
                            new Dictionary<string, object>
                            {
                                { "error_code", "oauth_failed" },
                                { "error_message_short", "OAuth authentication failed" },
                                { "latency_ms", latencyMs }
                            }
                        );

                        _showStatus("✗ OAuth authentication failed.", false);
                        SetAuthenticateButtonEnabled(true);
                        return;
                    }

                    bool success = await _backendService.InitializeBackendAsync(jwtToken, environment);
                    if (success)
                    {
                        ArtForgeAuthTelemetry?.Invoke(
                            "auth_login_succeeded",
                            new Dictionary<string, object>
                            {
                                { "latency_ms", latencyMs }
                            }
                        );

                        string email = WorkOSAuthService.GetStoredEmail();
                        _showStatus($"✓ Authenticated as {email}!", true);
                    }
                    else
                    {
                        ArtForgeAuthTelemetry?.Invoke(
                            "auth_login_failed",
                            new Dictionary<string, object>
                            {
                                { "error_code", "backend_init_failed" },
                                { "error_message_short", "Backend initialization failed" },
                                { "latency_ms", latencyMs }
                            }
                        );

                        _showStatus("✗ Backend initialization failed.", false);
                        SetAuthenticateButtonEnabled(true);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                long latencyMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startedAtMs;

                ArtForgeAuthTelemetry?.Invoke(
                    "auth_login_failed",
                    new Dictionary<string, object>
                    {
                        { "error_code", "cancelled" },
                        { "error_message_short", "Login cancelled" },
                        { "latency_ms", latencyMs }
                    }
                );

                _logger.Info("[BackendSettings] OAuth login cancelled by user");
                _showStatus("Login cancelled", false);
                SetAuthenticateButtonEnabled(true);
            }
            catch (Exception ex)
            {
                long latencyMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startedAtMs;

                ArtForgeAuthTelemetry?.Invoke(
                    "auth_login_failed",
                    new Dictionary<string, object>
                    {
                        { "error_code", ex.GetType().Name },
                        { "error_message_short", ShortenError(ex.Message) },
                        { "latency_ms", latencyMs }
                    }
                );

                _logger.Error($"[BackendSettings] OAuth error: {ex.Message}");
                _showStatus($"✗ Login failed: {ex.Message}", false);
                SetAuthenticateButtonEnabled(true);
            }
            finally
            {
                _isAuthenticating = false;
                _authenticateButton.text = "Login";
                RefreshConnectionStatus();
            }
        }

        private async void OnDisconnectClicked()
        {
            ArtForgeAuthTelemetry?.Invoke(
                "auth_logout_clicked",
                new Dictionary<string, object>()
            );

            string disconnectError = null;
            try
            {
                await _backendService.DisconnectBackendAsync();
            }
            catch (Exception ex)
            {
                disconnectError = ex.Message;
            }
            finally
            {
                // Local logout must always clear cached tokens, even if backend disconnect throws.
                ClearCachedAuthState();
                RefreshConnectionStatus();
            }

            if (string.IsNullOrWhiteSpace(disconnectError))
            {
                _showStatus("Backend disconnected", true);
            }
            else
            {
                _showStatus($"Disconnected locally, but backend disconnect failed: {disconnectError}", false);
            }
        }

        private async Task HandleEnvironmentChangedAsync(string newEnvironment)
        {
            if (_isSwitchingEnvironment)
            {
                return;
            }

            if (_isAuthenticating)
            {
                string currentEnvironment = _getSettingString(
                    WorkOSAuthConstants.PrefKeyEnvironment,
                    WorkOSAuthConstants.EnvironmentProduction);
                _environmentDropdown.SetValueWithoutNotify(ContentClient.ResolveEnvironment(currentEnvironment).Name);
                _showStatus("Cannot switch environment while login is in progress.", false);
                return;
            }

            _isSwitchingEnvironment = true;
            if (_availableEnvironments.Count > 1)
            {
                _environmentDropdown.SetEnabled(false);
            }
            try
            {
                string selectedEnvironment = ContentClient.ResolveEnvironment(newEnvironment).Name;
                _saveSettingString(WorkOSAuthConstants.PrefKeyEnvironment, selectedEnvironment);

                bool hadBackendSession = _backendService.IsBackendAvailable;
                bool hadStoredTokens = WorkOSAuthService.HasStoredTokens();

                if (!hadBackendSession && !hadStoredTokens)
                {
                    _showStatus($"Environment set to {selectedEnvironment}", true);
                    RefreshConnectionStatus();
                    return;
                }

                bool disconnectFailed = false;
                string disconnectError = string.Empty;

                if (hadBackendSession)
                {
                    _showStatus($"Environment set to {selectedEnvironment}. Disconnecting current session...", true);
                    try
                    {
                        await _backendService.DisconnectBackendAsync();
                    }
                    catch (Exception ex)
                    {
                        disconnectFailed = true;
                        disconnectError = ex.Message;
                        _logger.Error($"[BackendSettings] Failed to disconnect during environment switch: {ex.Message}");
                    }
                }

                ClearCachedAuthState();
                RefreshConnectionStatus();

                if (disconnectFailed)
                {
                    _showStatus(
                        $"Environment switched to {selectedEnvironment}. Previous auth was cleared, but disconnect failed: {disconnectError}",
                        false);
                    return;
                }

                _showStatus($"Environment switched to {selectedEnvironment}. Please login again.", true);
            }
            finally
            {
                _isSwitchingEnvironment = false;
                if (_availableEnvironments.Count > 1)
                {
                    _environmentDropdown.SetEnabled(true);
                }
            }
        }

        private DeploymentEnvironment GetSelectedEnvironment()
        {
            if (_availableEnvironments.Count == 1)
            {
                return _availableEnvironments[0];
            }

            return ContentClient.ResolveEnvironment(_environmentDropdown.value);
        }

        private void ClearCachedAuthState()
        {
            WorkOSAuthService.ClearStoredTokens();

            EditorPrefs.SetInt("GAF_DisconnectEpoch", Environment.TickCount);
        }

        private void SetAuthenticateButtonEnabled(bool isEnabled)
        {
            _authenticateButton.SetEnabled(isEnabled);

            if (isEnabled)
            {
                _authenticateButton.style.backgroundColor = AuthenticateButtonEnabledBackground;
                _authenticateButton.style.color = Color.white;
                _authenticateButton.style.opacity = 1f;
                return;
            }

            _authenticateButton.style.backgroundColor = AuthenticateButtonDisabledBackground;
            _authenticateButton.style.color = AuthenticateButtonDisabledText;
            _authenticateButton.style.opacity = DisabledButtonOpacity;
        }

        private static string ShortenError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "unknown_error";
            }

            string cleaned = message.Replace("\r", " ").Replace("\n", " ").Trim();
            const int maxLen = 120;

            if (cleaned.Length <= maxLen)
            {
                return cleaned;
            }

            return cleaned.Substring(0, maxLen);
        }
    }
}
