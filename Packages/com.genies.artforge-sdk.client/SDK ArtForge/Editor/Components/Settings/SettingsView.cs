using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Genies.Sdk.ArtForge.Editor.Core.UI;
using Genies.Sdk.ArtForge.Editor.Core.UI.Helpers;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using Genies.Sdk.ArtForge.Editor.Hub;

namespace Genies.Sdk.ArtForge.Editor.Components.Settings
{
    internal interface IBackendSettingsSection
    {
        VisualElement Root { get; }
        void SetVisible(bool visible);
        void RefreshConnectionStatus();
    }

    /// <summary>
    /// View controller for application settings.
    /// Manages backend, logging, and project folder settings.
    /// </summary>
    internal class SettingsView : IViewController
    {
        private VisualElement _container;

        // Backend Settings
        private IBackendSettingsSection _backendSettingsSection;

        // Logging
        private Toggle _enableLogsToggle;
        private Toggle _showWelcomePopupToggle;

        // Project Folder
        private TextField _outputFolderField;
        private Button _browseButton;

        // Status
        private Label _statusLabel;

        private const string PrefKeyPrefix = "GAF_Settings_";

        public void Load(VisualElement container)
        {
            _container = container;
            _container.Clear();

            // Main container
            var mainContainer = new ScrollView();
            mainContainer.style.flexGrow = 1;
            _container.Add(mainContainer);

            // Header
            var header = new Label("Settings");
            header.style.fontSize = 24;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 20;
            header.style.color = StyleHelpers.Colors.TextPrimary;
            mainContainer.Add(header);

            // Description
            var description = new Label("Configure backend connection, logging, and project paths.");
            description.style.fontSize = 12;
            description.style.marginBottom = 30;
            description.style.color = StyleHelpers.Colors.TextSecondary;
            mainContainer.Add(description);

            // Backend Settings Section
            CreateBackendSettingsSection(mainContainer);

            // Logging Section
            CreateLoggingSection(mainContainer);

            // Welcome Popup Section
            CreateWelcomePopupSection(mainContainer);

            // Project Folder Section
            CreateProjectFolderSection(mainContainer);

            // Status label
            _statusLabel = new Label();
            _statusLabel.style.marginTop = 20;
            _statusLabel.style.fontSize = 12;
            mainContainer.Add(_statusLabel);

            // Load saved settings
            LoadSettings();
        }

        public void Unload()
        {
            _container?.Clear();
        }

        public void Update()
        {
            // Update backend connection status periodically
            _backendSettingsSection?.RefreshConnectionStatus();
        }

        private void CreateBackendSettingsSection(VisualElement parent)
        {
            var section = new VisualElement();
            section.style.marginBottom = 40;
            parent.Add(section);

            // Section title
            var title = new Label("Backend Settings");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 15;
            title.style.color = StyleHelpers.Colors.TextPrimary;
            section.Add(title);

            var desc = new Label("Configure connection to the Genies Backend API.");
            desc.style.fontSize = 11;
            desc.style.marginBottom = 15;
            desc.style.color = StyleHelpers.Colors.TextHint;
            section.Add(desc);

            // Backend settings (extracted)
            _backendSettingsSection = CreateBackendSettingsSectionInstance();
            if (_backendSettingsSection != null)
            {
                section.Add(_backendSettingsSection.Root);
                _backendSettingsSection.SetVisible(true); // Always visible now
                _backendSettingsSection.RefreshConnectionStatus();
            }
        }

        private IBackendSettingsSection CreateBackendSettingsSectionInstance()
        {
            // NOTE: Using reflection here avoids a hard compile-time dependency in this file,
            // which has been flaky in some Unity/IDE compilation setups in this repo.
            // BackendSettingsSection lives as a separate file and implements IBackendSettingsSection.
            try
            {
                var asm = typeof(SettingsView).Assembly;
                var type = asm.GetType("Genies.Sdk.ArtForge.Editor.Components.Settings.BackendSettingsSection");
                if (type == null)
                {
                    ShowStatus("Backend settings UI type not found. Please reimport/recompile.", false);
                    return null;
                }

                object instance = System.Activator.CreateInstance(
                    type,
                    GeniesArtForgeService.Generation,
                    new System.Func<string, string, string>(GetSettingString),
                    new System.Action<string, string>(SaveSetting),
                    new System.Action<string, bool>(ShowStatus),
                    GeniesArtForgeService.Logger
                );

                return instance as IBackendSettingsSection;
            }
            catch (System.Exception ex)
            {
                ShowStatus($"Failed to create backend settings UI: {ex.Message}", false);
                return null;
            }
        }


        private void CreateProjectFolderSection(VisualElement parent)
        {
            var section = new VisualElement();
            section.style.marginBottom = 30;
            parent.Add(section);

            // Section title
            var title = new Label("Project Folder");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 15;
            title.style.color = StyleHelpers.Colors.TextPrimary;
            section.Add(title);

            var desc = new Label("Where generated assets will be saved in your Unity project.");
            desc.style.fontSize = 11;
            desc.style.marginBottom = 15;
            desc.style.color = StyleHelpers.Colors.TextHint;
            section.Add(desc);

            // Folder path container
            var folderContainer = new VisualElement();
            folderContainer.style.flexDirection = FlexDirection.Row;
            folderContainer.style.marginBottom = 10;
            section.Add(folderContainer);

            // Output Folder Field
            _outputFolderField = new TextField("Output Folder");
            _outputFolderField.value = "Assets/Genies/ArtForge";
            _outputFolderField.style.flexGrow = 1;
            _outputFolderField.style.marginRight = 10;
            _outputFolderField.RegisterValueChangedCallback(evt =>
            {
                SaveSetting("OutputFolder", evt.newValue);
                ShowStatus("Project folder path saved", true);
            });
            folderContainer.Add(_outputFolderField);

            // Browse Button
            _browseButton = new Button(OnBrowseFolder)
            {
                text = "Browse"
            };
            _browseButton.style.width = 100;
            StyleHelpers.ApplySecondaryButtonStyle(_browseButton);
            folderContainer.Add(_browseButton);

            var folderHint = new Label("Assets will be organized into subfolders: Models/, Textures/, Materials/, etc.");
            folderHint.style.fontSize = 10;
            folderHint.style.marginLeft = 3;
            folderHint.style.color = StyleHelpers.Colors.TextHint;
            section.Add(folderHint);
        }

        private void CreateLoggingSection(VisualElement parent)
        {
            var section = new VisualElement();
            section.style.marginBottom = 40;
            parent.Add(section);

            var title = new Label("Logging");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 15;
            title.style.color = StyleHelpers.Colors.TextPrimary;
            section.Add(title);

            var desc = new Label("Control whether informational logs are written to the Console.");
            desc.style.fontSize = 11;
            desc.style.marginBottom = 15;
            desc.style.color = StyleHelpers.Colors.TextHint;
            section.Add(desc);

            _enableLogsToggle = new Toggle("Enable Logs");
            _enableLogsToggle.value = true;
            _enableLogsToggle.style.marginBottom = 15;
            _enableLogsToggle.RegisterValueChangedCallback(evt =>
            {
                SaveSetting("LogsEnabled", evt.newValue ? 1 : 0);
                UnityLogger.LogsEnabled = evt.newValue;
                GeniesArtForgeService.ContentClient?.SetConvexLoggingEnabled(evt.newValue);
                ShowStatus("Logging settings saved", true);
            });
            section.Add(_enableLogsToggle);

            var hint = new Label("Disabling logs hides info/warning/debug messages; errors always show.");
            hint.style.fontSize = 10;
            hint.style.marginBottom = 10;
            hint.style.marginTop = -10;
            hint.style.marginLeft = 3;
            hint.style.color = StyleHelpers.Colors.TextHint;
            section.Add(hint);
        }

        private void CreateWelcomePopupSection(VisualElement parent)
        {
            var section = new VisualElement();
            section.style.marginBottom = 40;
            parent.Add(section);

            var title = new Label("Welcome Popup");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 15;
            title.style.color = StyleHelpers.Colors.TextPrimary;
            section.Add(title);

            var desc = new Label("Control whether the welcome popup appears when this Unity project opens.");
            desc.style.fontSize = 11;
            desc.style.marginBottom = 15;
            desc.style.color = StyleHelpers.Colors.TextHint;
            section.Add(desc);

            _showWelcomePopupToggle = new Toggle("Show On Project Open");
            _showWelcomePopupToggle.value = true;
            _showWelcomePopupToggle.style.marginBottom = 15;
            _showWelcomePopupToggle.RegisterValueChangedCallback(evt =>
            {
                GeniesArtForgeHub.SetWelcomePopupOnProjectOpenEnabled(evt.newValue);
                ShowStatus("Welcome popup setting saved", true);
            });
            section.Add(_showWelcomePopupToggle);

            var hint = new Label("This setting is saved per project.");
            hint.style.fontSize = 10;
            hint.style.marginBottom = 10;
            hint.style.marginTop = -10;
            hint.style.marginLeft = 3;
            hint.style.color = StyleHelpers.Colors.TextHint;
            section.Add(hint);
        }

        private void OnBrowseFolder()
        {
            string currentPath = _outputFolderField.value;
            string absolutePath = Path.Combine(Application.dataPath, currentPath.Replace("Assets/", ""));

            string selectedPath = EditorUtility.OpenFolderPanel("Select Output Folder", absolutePath, "");

            if (!string.IsNullOrEmpty(selectedPath))
            {
                // Convert absolute path to Unity relative path
                if (selectedPath.StartsWith(Application.dataPath))
                {
                    string relativePath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                    _outputFolderField.value = relativePath;
                    SaveSetting("OutputFolder", relativePath);
                    ShowStatus("Project folder path updated", true);
                }
                else
                {
                    ShowStatus("Folder must be inside the Unity project Assets folder", false);
                }
            }
        }

        private void LoadSettings()
        {
            _outputFolderField.SetValueWithoutNotify(GetSettingString("OutputFolder", "Assets/Genies/ArtForge"));
            bool logsEnabled = GetSettingInt("LogsEnabled", 1) == 1;
            _enableLogsToggle.SetValueWithoutNotify(logsEnabled);
            UnityLogger.LogsEnabled = logsEnabled;
            _showWelcomePopupToggle?.SetValueWithoutNotify(GeniesArtForgeHub.IsWelcomePopupOnProjectOpenEnabled());
        }

        private void SaveSetting(string key, int value)
        {
            EditorPrefs.SetInt($"{PrefKeyPrefix}{key}", value);
        }

        private void SaveSetting(string key, string value)
        {
            EditorPrefs.SetString($"{PrefKeyPrefix}{key}", value);
        }

        private int GetSettingInt(string key, int defaultValue)
        {
            return EditorPrefs.GetInt($"{PrefKeyPrefix}{key}", defaultValue);
        }

        private string GetSettingString(string key, string defaultValue)
        {
            return EditorPrefs.GetString($"{PrefKeyPrefix}{key}", defaultValue);
        }

        private void ShowStatus(string message, bool isSuccess)
        {
            if (_statusLabel == null)
            {
                return;
            }

            _statusLabel.text = message;
            _statusLabel.style.color = isSuccess ? StyleHelpers.Colors.Success : StyleHelpers.Colors.Error;
        }
    }
}
