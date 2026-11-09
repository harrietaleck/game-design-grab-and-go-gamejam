using System;
using System.Collections.Generic;
using System.IO;
using Genies.Sdk.ArtForge.Editor.Auth;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using Genies.Sdk.ArtForge.Editor.Core.Application.Services;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.API;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Helpers;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using Genies.Sdk.ArtForge.Editor.Core.UI;
using Genies.Sdk.ArtForge.Editor.Hub.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Genies.Sdk.ArtForge.Editor.Hub
{
    /// <summary>
    /// Main Hub window for GeniesArtForge.
    /// Completely dynamic navigation - add new components by updating NavigationConfig only!
    /// </summary>
    internal class GeniesArtForgeHub : EditorWindow
    {
        // ------------------------------------------------------------
        // Telemetry signals (Observer subscribes to these)
        // ------------------------------------------------------------
        public static event Action<string, Dictionary<string, object>> HubOpened;
        public static event Action<string, Dictionary<string, object>> HubClosed;
        public static event Action<string, Dictionary<string, object>> HubTabViewed;
        public static event Action<string, Dictionary<string, object>> BalanceClicked;
        public static event Action<string, Dictionary<string, object>> PackageFirstInstalled;

        private static string _openSource = "unknown";
        private static string _closeSource = "x";
        private const string FirstInstallTelemetryKey = "Genies.ArtForge.Telemetry.FirstInstallTracked";
        private const string WelcomePopupEnabledKey = "Genies.ArtForge.Welcome.ShowOnProjectOpen";
        private const string WelcomePopupShownThisSessionKey = "Genies.ArtForge.Welcome.ShownThisSession";

        [InitializeOnLoadMethod]
        private static void InstallCloseReasonHooks()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.quitting += OnEditorQuitting;

            // Delay to allow telemetry observer static initialization/subscriptions to complete.
            EditorApplication.delayCall -= EmitFirstInstallTelemetryIfNeeded;
            EditorApplication.delayCall += EmitFirstInstallTelemetryIfNeeded;

            ScheduleWelcomePopupOnProjectOpenIfNeeded();
        }

        private static void OnBeforeAssemblyReload()
        {
            _closeSource = "domain_reload";
        }

        private static void OnEditorQuitting()
        {
            _closeSource = "unity_quit";
        }

        private static string GetOpenSourceForTelemetry()
        {
            var value = string.IsNullOrEmpty(_openSource) ? "unknown" : _openSource;
            _openSource = "unknown";
            return value;
        }

        private static string GetCloseSourceForTelemetry()
        {
            var value = string.IsNullOrEmpty(_closeSource) ? "x" : _closeSource;
            _closeSource = "x";
            return value;
        }

        private static string GetDockStateForTelemetry()
        {
            return "unknown";
        }

        internal static bool IsWelcomePopupOnProjectOpenEnabled()
        {
            string configValue = EditorUserSettings.GetConfigValue(WelcomePopupEnabledKey);
            if (string.IsNullOrEmpty(configValue))
            {
                return true;
            }

            return !string.Equals(configValue, "false", StringComparison.OrdinalIgnoreCase);
        }

        internal static void SetWelcomePopupOnProjectOpenEnabled(bool isEnabled)
        {
            EditorUserSettings.SetConfigValue(WelcomePopupEnabledKey, isEnabled ? "true" : "false");
        }

        private static void ScheduleWelcomePopupOnProjectOpenIfNeeded()
        {
            if (SessionState.GetBool(WelcomePopupShownThisSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(WelcomePopupShownThisSessionKey, true);
            if (!IsWelcomePopupOnProjectOpenEnabled())
            {
                return;
            }

            EditorApplication.delayCall -= WelcomePopupWindow.ShowPopupWindow;
            EditorApplication.delayCall += WelcomePopupWindow.ShowPopupWindow;
        }

        private static void EmitFirstInstallTelemetryIfNeeded()
        {
            if (EditorUserSettings.GetConfigValue(FirstInstallTelemetryKey) == "true")
            {
                return;
            }

            if (PackageFirstInstalled == null)
            {
                EditorApplication.delayCall -= EmitFirstInstallTelemetryIfNeeded;
                EditorApplication.delayCall += EmitFirstInstallTelemetryIfNeeded;
                return;
            }

            PackageFirstInstalled?.Invoke(
                "art_forge_package_first_installed",
                new Dictionary<string, object>
                {
                    { "install_scope", "project" }
                }
            );

            EditorUserSettings.SetConfigValue(FirstInstallTelemetryKey, "true");
        }

        private static GeniesArtForgeHub _instance;
        private static string _pendingViewName;
        private ILogger _logger;

        // Navigation configuration
        private NavigationConfig _navigationConfig;

        // UI Elements
        private VisualElement _root;
        private VisualElement _contentArea;
        private VisualElement _navButtonsContainer;

        // Queue Panel Controller
        private QueuePanelController _queuePanelController;
        private Button _balanceButton;
        private Label _balanceLabel;
        private Label _balanceWarningIcon;
        private BillingService _billingService;

        // Cached balance for telemetry
        private string _lastKnownBalanceText = "--";

        // Current active view
        private string _currentViewName = "";
        private IViewController _currentView;

        // Track UI elements for cleanup
        private Dictionary<string, Button> _navButtons = new Dictionary<string, Button>();
        private Dictionary<string, VisualElement> _navChildrenContainers = new Dictionary<string, VisualElement>();
        private HashSet<string> _expandedSections = new HashSet<string>();

        private const float HubWindowDefaultWidth = 1100f;
        private const float HubWindowDefaultHeight = 680f;

        [MenuItem(BrandingConstants.MenuPath + " %^g")]  // Command+Control+G (Mac) / Control+G (Windows)
        public static void ShowWindow()
        {
            if (string.IsNullOrEmpty(_openSource) || _openSource == "unknown")
            {
                _openSource = "menu_or_shortcut";
            }

            var window = GetWindow<GeniesArtForgeHub>();
            window.titleContent = new GUIContent("Genies Art Forge");
            window.minSize = new Vector2(HubWindowDefaultWidth, HubWindowDefaultHeight);
            if (_instance == null)
            {
                var rect = window.position;
                rect.width = HubWindowDefaultWidth;
                rect.height = HubWindowDefaultHeight;
                window.position = rect;
            }
            window.Show();
        }

        internal static void OpenSettingsFromWelcomePopup()
        {
            _openSource = "first_install_popup_open_settings";
            RestartAndOpen("Settings");
        }

        internal static void OpenModelGeneratorFromWelcomePopupClose()
        {
            _openSource = "first_install_popup_close";
            RestartAndOpen("ModelGenerator");
        }

        /// <summary>
        /// Restart the hub window and open the requested view after restart.
        /// Useful when switching generation mode so feature availability/navigation is rebuilt cleanly.
        /// </summary>
        public static void RestartAndOpen(string viewName)
        {
            _pendingViewName = viewName;

            // Close current instance (if any) and reopen next tick.
            // Important: do not call GetWindow when no instance exists, because it creates a new
            // window immediately and can consume _pendingViewName before the intended reopen.
            if (_instance != null)
            {
                _instance.Close();
            }
            else if (HasOpenInstances<GeniesArtForgeHub>())
            {
                GetWindow<GeniesArtForgeHub>().Close();
            }

            EditorApplication.delayCall -= ShowWindow;
            EditorApplication.delayCall += ShowWindow;
        }

        /// <summary>
        /// Get the current hub instance (if open)
        /// </summary>
        public static GeniesArtForgeHub GetInstance()
        {
            return _instance;
        }

        /// <summary>
        /// Update feature availability from backend and rebuild navigation
        /// </summary>
        internal void UpdateFeatureAvailability(List<FeatureAvailability> availabilityData)
        {
            if (_navigationConfig == null)
            {
                EnsureLogger();
                _logger.Warning("NavigationConfig not initialized");
                return;
            }

            _navigationConfig.UpdateFeatureAvailability(availabilityData);
            RebuildNavigation();
        }

        /// <summary>
        /// Rebuild navigation UI (useful after changing feature availability)
        /// </summary>
        public void RebuildNavigation()
        {
            if (_navButtonsContainer == null)
            {
                EnsureLogger();
                _logger.Warning("Navigation container not initialized");
                return;
            }

            BuildNavigation();

            // Reload current view if it's no longer available
            if (!string.IsNullOrEmpty(_currentViewName))
            {
                var currentView = _currentViewName;
                _currentViewName = ""; // Force reload
                LoadView(currentView);
            }
        }

        private void CreateGUI()
        {
            _instance = this;
            EnsureLogger();

            // Initialize navigation configuration
            _navigationConfig = new NavigationConfig();

            // Ensure minimum window size
            minSize = new Vector2(HubWindowDefaultWidth, HubWindowDefaultHeight);

            // Load UXML
            var uxmlPath = GetRelativePath("HubWindow.uxml");
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);

            if (visualTree == null)
            {
                _logger.Error($"Could not load HubWindow.uxml at: {uxmlPath}");
                return;
            }

            visualTree.CloneTree(rootVisualElement);

            // Load USS
            var ussPath = GetRelativePath("HubWindow.uss");
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);

            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }
            else
            {
                _logger.Warning($"Could not load HubWindow.uss at: {ussPath}");
            }

            // Get UI element references
            _root = rootVisualElement.Q<VisualElement>("root");
            _contentArea = _root.Q<VisualElement>("content-area");
            _navButtonsContainer = _root.Q<VisualElement>("nav-buttons");

            HubOpened?.Invoke(
                "art_forge_hub_opened",
                new Dictionary<string, object>
                {
                    { "open_source", GetOpenSourceForTelemetry() },
                    { "dock_state", GetDockStateForTelemetry() },
                }
            );

            // Build navigation dynamically from config
            BuildNavigation();

            // Initialize queue panel controller
            _queuePanelController = new QueuePanelController(_root, _logger);
            InitializeBalanceUi();
            InitializeBillingService();

            // Load default view (force reload even if _currentViewName matches)
            string defaultView = !string.IsNullOrEmpty(_pendingViewName) ? _pendingViewName : _navigationConfig.GetDefaultView();
            _pendingViewName = null;
            _currentViewName = ""; // Clear to force reload
            LoadView(defaultView);

            // Attempt auto-authentication if credentials are saved
            TryAutoAuthenticateAsync();
        }

        /// <summary>
        /// Dynamically builds navigation from NavigationConfig
        /// </summary>
        private void BuildNavigation()
        {
            // Clear existing navigation (in case of rebuild)
            _navButtonsContainer.Clear();
            _navButtons.Clear();
            _navChildrenContainers.Clear();

            var navigationTree = _navigationConfig.GetNavigationTree();

            foreach (var navItem in navigationTree)
            {
                if (navItem.IsParent)
                {
                    // Create parent button
                    var parentButton = new Button();
                    parentButton.name = navItem.Id;
                    parentButton.text = $"▶ {navItem.Label}";
                    parentButton.AddToClassList("nav-button");
                    parentButton.AddToClassList("nav-parent");

                    // Store reference
                    _navButtons[navItem.Id] = parentButton;

                    // Create children container
                    var childrenContainer = new VisualElement();
                    childrenContainer.name = $"{navItem.Id}-children";
                    childrenContainer.AddToClassList("nav-children");
                    childrenContainer.style.display = DisplayStyle.None;

                    // Store reference
                    _navChildrenContainers[navItem.Id] = childrenContainer;

                    // Register toggle handler
                    parentButton.clicked += () => ToggleNavSection(navItem.Label, parentButton, childrenContainer);

                    // Add children buttons
                    foreach (var child in navItem.Children)
                    {
                        var childButton = new Button();
                        childButton.name = child.Id;
                        childButton.text = $"  • {child.Label}";
                        childButton.AddToClassList("nav-button");
                        childButton.AddToClassList("nav-child");

                        // Store reference
                        _navButtons[child.Id] = childButton;

                        // Register click handler to load view
                        var viewName = child.ViewName;  // Capture for closure
                        childButton.clicked += () => LoadView(viewName);

                        childrenContainer.Add(childButton);
                    }

                    // Add to navigation container
                    _navButtonsContainer.Add(parentButton);
                    _navButtonsContainer.Add(childrenContainer);

                    // Expand default section
                    if (navItem.Label == _navigationConfig.GetDefaultExpandedSection())
                    {
                        childrenContainer.style.display = DisplayStyle.Flex;
                        parentButton.text = $"▼ {navItem.Label}";
                        _expandedSections.Add(navItem.Label);
                    }
                }
                else
                {
                    // Leaf item (no children) - create a regular button
                    var leafButton = new Button();
                    leafButton.name = navItem.Id;
                    leafButton.text = navItem.Label;
                    leafButton.AddToClassList("nav-button");

                    // Store reference
                    _navButtons[navItem.Id] = leafButton;

                    // Register click handler to load view
                    var viewName = navItem.ViewName;  // Capture for closure
                    leafButton.clicked += () => LoadView(viewName);

                    // Add to navigation container
                    _navButtonsContainer.Add(leafButton);
                }
            }
        }

        /// <summary>
        /// Toggle navigation section expand/collapse
        /// </summary>
        private void ToggleNavSection(string sectionName, Button parentButton, VisualElement childrenContainer)
        {
            bool isExpanded = _expandedSections.Contains(sectionName);

            if (isExpanded)
            {
                // Collapse
                childrenContainer.style.display = DisplayStyle.None;
                parentButton.text = $"▶ {sectionName}";
                _expandedSections.Remove(sectionName);
            }
            else
            {
                // Expand
                childrenContainer.style.display = DisplayStyle.Flex;
                parentButton.text = $"▼ {sectionName}";
                _expandedSections.Add(sectionName);
            }
        }

        /// <summary>
        /// Load a view by name using the factory
        /// </summary>
        private void LoadView(string viewName)
        {
            if (_currentViewName == viewName)
            {
                return;
            }

            var previous = _currentViewName;

            HubTabViewed?.Invoke(
                "art_forge_hub_tab_viewed",
                new Dictionary<string, object>
                {
                    { "tab_name", viewName },
                    { "previous_tab_name", string.IsNullOrEmpty(previous) ? "none" : previous },
                }
            );

            _logger.Info($"Loading view: {viewName}");

            // Unload current view
            _currentView?.Unload();
            _contentArea.Clear();

            // Clear active state from all nav buttons
            ClearActiveNavButtons();

            // Load new view using factory
            _currentView = _navigationConfig.CreateViewController(viewName);

            if (_currentView != null)
            {
                _currentView.Load(_contentArea);
                _currentViewName = viewName;

                // Set active state on the corresponding nav button
                SetActiveNavButton(viewName);
            }
            else
            {
                // Show "coming soon" message
                var label = new Label($"{viewName} - Coming Soon");
                label.AddToClassList("placeholder-text");
                _contentArea.Add(label);
                _currentViewName = "";
            }
        }

        /// <summary>
        /// Clear active class from all navigation buttons
        /// </summary>
        private void ClearActiveNavButtons()
        {
            foreach (var button in _navButtons.Values)
            {
                button.RemoveFromClassList("active");
            }
        }

        /// <summary>
        /// Set active class on the button corresponding to the view
        /// </summary>
        private void SetActiveNavButton(string viewName)
        {
            // Find the button with this view name
            var navigationTree = _navigationConfig.GetNavigationTree();

            foreach (var navItem in navigationTree)
            {
                // Check if this is a leaf item with matching view name
                if (!navItem.IsParent && navItem.ViewName == viewName && _navButtons.TryGetValue(navItem.Id, out var button))
                {
                    button.AddToClassList("active");
                    return;
                }

                // Check children for parent items (for backwards compatibility)
                foreach (var child in navItem.Children)
                {
                    if (child.ViewName == viewName && _navButtons.TryGetValue(child.Id, out var childButton))
                    {
                        childButton.AddToClassList("active");
                        return;
                    }
                }
            }
        }

        private void Update()
        {
            // Call Update on the current view if it needs per-frame updates
            _currentView?.Update();

            // Update queue panel
            _queuePanelController?.Update();
        }

        private void OnDestroy()
        {
            HubClosed?.Invoke(
                "art_forge_hub_closed",
                new Dictionary<string, object>
                {
                    { "close_source", GetCloseSourceForTelemetry() }
                }
            );

            // Cleanup queue panel controller
            _queuePanelController?.Dispose();
            _queuePanelController = null;

            if (_balanceButton != null)
            {
                _balanceButton.clicked -= OnBalanceClicked;
            }

            DisposeBillingService();

            // Cleanup current view
            _currentView?.Unload();
            _currentView = null;
            _instance = null;
        }

        /// <summary>
        /// Attempts to auto-reconnect using stored OAuth token from previous session.
        /// Runs asynchronously and silently - only logs on success or error.
        /// </summary>
        private async void TryAutoAuthenticateAsync()
        {
            try
            {
                // Check if we have a stored OAuth access token
                const string PrefixKey = "GAF_Settings_";
                string environmentString = EditorPrefs.GetString(
                    $"{PrefixKey}{WorkOSAuthConstants.PrefKeyEnvironment}",
                    WorkOSAuthConstants.EnvironmentProduction);

                if (!WorkOSAuthService.HasStoredTokens())
                {
                    _logger.Info("[GeniesArtForge] No stored OAuth token found, skipping auto-reconnect");
                    return;
                }

                string backendToken = await WorkOSAuthService.GetValidBackendAuthTokenAsync(_logger.Info, _logger.Warning);
                if (string.IsNullOrWhiteSpace(backendToken))
                {
                    _logger.Info("[GeniesArtForge] Stored tokens were present but no valid backend JWT token was available. Preserving tokens for future silent refresh attempts.");
                    return;
                }

                _logger.Info("[GeniesArtForge] Stored OAuth token found, attempting auto-reconnect...");

                // Parse environment
                DeploymentEnvironment environment = ContentClient.ResolveEnvironment(environmentString);

                // Try to initialize backend with stored token
                bool success = await GeniesArtForgeService.Generation.InitializeBackendAsync(backendToken, environment);

                if (success)
                {
                    _logger.Info($"[GeniesArtForge] ✓ Auto-reconnect successful ({environment.Name})");
                    _billingService?.RequestImmediateRefresh("auto_reconnect_success");
                }
                else
                {
                    _logger.Info("[GeniesArtForge] Auto-reconnect failed. Preserving stored tokens so future retries can refresh silently.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[GeniesArtForge] Auto-reconnect error: {ex.Message}");
            }
        }

        private void InitializeBalanceUi()
        {
            _balanceButton = _root?.Q<Button>("balance-button");
            _balanceLabel = _root?.Q<Label>("balance-label");
            _balanceWarningIcon = _root?.Q<Label>("balance-warning-icon");

            if (_balanceButton == null || _balanceLabel == null)
            {
                _logger.Warning("[GeniesArtForge] Balance button UI not found in HubWindow.uxml");
                return;
            }

            _balanceButton.clicked += OnBalanceClicked;
        }

        private void InitializeBillingService()
        {
            if (_balanceButton == null || _balanceLabel == null)
            {
                return;
            }

            DisposeBillingService();

            _billingService = new BillingService(_logger);
            _billingService.BalanceStateChanged += OnBalanceStateChanged;
            _billingService.Initialize();
        }

        private void DisposeBillingService()
        {
            if (_billingService == null)
            {
                return;
            }

            _billingService.BalanceStateChanged -= OnBalanceStateChanged;
            _billingService.Dispose();
            _billingService = null;
        }

        private void OnBalanceStateChanged(BalanceState state)
        {
            if (_balanceButton == null || _balanceLabel == null)
            {
                return;
            }

            _lastKnownBalanceText = string.IsNullOrWhiteSpace(state.BalanceTextForTelemetry)
                ? "--"
                : state.BalanceTextForTelemetry;
            _balanceLabel.text = state.LabelText;
            _balanceButton.tooltip = state.Tooltip;
            _balanceButton.SetEnabled(state.IsButtonEnabled);

            if (_balanceWarningIcon != null)
            {
                _balanceWarningIcon.style.display = state.ShowLowBalanceWarning ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void OnBalanceClicked()
        {
            BalanceClicked?.Invoke(
                "balance_clicked",
                new Dictionary<string, object>
                {
                    { "current_balance", _lastKnownBalanceText }
                }
            );

            BillingPortalHelper.OpenBillingPortal(_logger, "[GeniesArtForge]");
        }

        private string GetRelativePath(string filename)
        {
            // Find this script's location dynamically (version-independent)
            var guids = AssetDatabase.FindAssets($"t:Script {nameof(GeniesArtForgeHub)}");

            if (guids.Length > 0)
            {
                var scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                var directory = Path.GetDirectoryName(scriptPath);
                return Path.Combine(directory, filename);
            }

            _logger.Error($"Could not find {nameof(GeniesArtForgeHub)}.cs script path");
            return string.Empty;
        }

        private void EnsureLogger()
        {
            if (_logger != null)
            {
                return;
            }

            _logger = GeniesArtForgeService.Logger ?? new UnityLogger();
        }
    }
}
