using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using Genies.Sdk.ArtForge.Editor.Core.Application.Services;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Helpers;
using Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using Genies.Sdk.ArtForge.Editor.Core.UI;
using Genies.Sdk.ArtForge.Editor.Core.UI.Helpers;
using Genies.Sdk.ArtForge.Editor.Hub.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Genies.Sdk.ArtForge.Editor.Components.ModelEditor
{
    /// <summary>
    /// Model Editor component view controller.
    /// Edit existing 3D models with AI-guided modifications.
    /// Implements IViewController for hub integration.
    /// </summary>
    internal class ModelEditorView : IViewController
    {
        private const string NoAssetsAvailablePlaceholder = "No assets available";
        private const string AssetTypeProp = "prop";
        private const string DefaultPivot = "Base";
        private const string DefaultTopology = "Triangles";
        private const string DefaultTextureResolution = "1024";
        private const string ReferenceImageFileFilter = "png,jpg,jpeg";
        private const string EditedAssetSuffix = "_edited";
        private const int InstructionsMinLength = 5;
        private const int InstructionsMaxLength = 2000;

        private ILogger _logger;
        private VisualElement _root;
        private AssetService _assetService;

        private DropdownField _assetSelectionDropdown;
        private VisualElement _assetPreviewContainer;
        private TextField _editInstructionsField;
        private VisualElement _referenceImageContainer;
        private Button _addReferenceButton;
        private Button _clearReferenceButton;
        private QualitySettingsPanel _qualitySettingsPanel;

        private Button _editButton;
        private Label _statusMessage;

        private string _selectedAssetId;
        private string _referenceImagePath;
        private Texture2D _referenceTexture;

        private string _pivot = DefaultPivot;
        private string _topology = DefaultTopology;
        private int _polycount = QualityDefaults.Polycount;
        private bool _lods = false;
        private bool _collider = false;
        private string _textureRes = DefaultTextureResolution;
        private bool _ao = false;
        private bool _allowTopologySelection = false;

        public void Load(VisualElement container)
        {
            _logger = GeniesArtForgeService.Logger ?? new UnityLogger();
            _assetService = new AssetService();

            _root = BuildUI();
            container.Add(_root);

            LoadAvailableAssets();
        }

        private VisualElement BuildUI()
        {
            var root = new VisualElement();
            root.style.flexGrow = 1;

            var header = StyleHelpers.CreateHeader("Model Editor");
            root.Add(header);

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;

            var content = new VisualElement();
            content.style.paddingLeft = 20;
            content.style.paddingRight = 20;
            content.style.paddingTop = 10;
            content.style.paddingBottom = 20;

            content.Add(BuildAssetSelectionSection());
            content.Add(BuildEditConfigurationSection());
            content.Add(BuildReferenceImageSection());
            content.Add(BuildQualitySettingsSection());

            scrollView.Add(content);
            root.Add(scrollView);

            root.Add(BuildActionBar());

            return root;
        }

        private VisualElement BuildAssetSelectionSection()
        {
            var section = StyleHelpers.CreateSection("Select Asset");

            var label = new Label("Asset to Edit *");
            label.style.fontSize = 12;
            label.style.marginBottom = 5;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            section.Add(label);

            var hint = new Label("Select an existing model asset to edit");
            hint.style.fontSize = 11;
            hint.style.color = StyleHelpers.Colors.TextHint;
            hint.style.marginBottom = 8;
            section.Add(hint);

            _assetSelectionDropdown = new DropdownField("", new List<string> { NoAssetsAvailablePlaceholder }, 0);
            _assetSelectionDropdown.style.marginBottom = 10;
            _assetSelectionDropdown.RegisterValueChangedCallback(evt => OnAssetSelected(evt.newValue));
            section.Add(_assetSelectionDropdown);

            _assetPreviewContainer = new VisualElement();
            _assetPreviewContainer.style.minHeight = 150;
            _assetPreviewContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            _assetPreviewContainer.style.borderTopLeftRadius = 4;
            _assetPreviewContainer.style.borderTopRightRadius = 4;
            _assetPreviewContainer.style.borderBottomLeftRadius = 4;
            _assetPreviewContainer.style.borderBottomRightRadius = 4;
            _assetPreviewContainer.style.alignItems = Align.Center;
            _assetPreviewContainer.style.justifyContent = Justify.Center;
            _assetPreviewContainer.style.display = DisplayStyle.None;

            var previewPlaceholder = new Label("Asset preview will appear here");
            previewPlaceholder.style.color = StyleHelpers.Colors.TextHint;
            _assetPreviewContainer.Add(previewPlaceholder);

            section.Add(_assetPreviewContainer);

            return section;
        }

        private VisualElement BuildEditConfigurationSection()
        {
            var section = StyleHelpers.CreateSection("Edit Instructions");

            var label = new Label("Edit Description *");
            label.style.fontSize = 12;
            label.style.marginBottom = 5;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            section.Add(label);

            var hint = new Label("Describe the changes you want to make (5-2000 characters)");
            hint.style.fontSize = 11;
            hint.style.color = StyleHelpers.Colors.TextHint;
            hint.style.marginBottom = 8;
            section.Add(hint);

            var example = new Label("Examples: \"Add a hat\", \"Change color to red\", \"Make it more futuristic\"");
            example.style.fontSize = 10;
            example.style.color = StyleHelpers.Colors.TextHint;
            example.style.unityFontStyleAndWeight = FontStyle.Italic;
            example.style.marginBottom = 8;
            section.Add(example);

            _editInstructionsField = new TextField();
            _editInstructionsField.multiline = true;
            _editInstructionsField.style.minHeight = 100;
            _editInstructionsField.style.whiteSpace = WhiteSpace.Normal;
            section.Add(_editInstructionsField);

            return section;
        }

        private VisualElement BuildReferenceImageSection()
        {
            var section = StyleHelpers.CreateSection("Reference Image (Optional)");

            var hint = new Label("Add a reference image to guide the edit");
            hint.style.fontSize = 11;
            hint.style.color = StyleHelpers.Colors.TextHint;
            hint.style.marginBottom = 8;
            section.Add(hint);

            _referenceImageContainer = new VisualElement();
            _referenceImageContainer.style.width = 200;
            _referenceImageContainer.style.height = 200;
            _referenceImageContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            _referenceImageContainer.style.borderTopLeftRadius = 4;
            _referenceImageContainer.style.borderTopRightRadius = 4;
            _referenceImageContainer.style.borderBottomLeftRadius = 4;
            _referenceImageContainer.style.borderBottomRightRadius = 4;
            _referenceImageContainer.style.alignItems = Align.Center;
            _referenceImageContainer.style.justifyContent = Justify.Center;
            _referenceImageContainer.style.marginBottom = 10;

            var placeholder = new Label("No reference image");
            placeholder.style.color = StyleHelpers.Colors.TextHint;
            _referenceImageContainer.Add(placeholder);

            section.Add(_referenceImageContainer);

            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.marginBottom = 10;

            _addReferenceButton = new Button(OnAddReferenceImage);
            _addReferenceButton.text = "+ Add Reference";
            StyleHelpers.ApplySecondaryButtonStyle(_addReferenceButton);
            _addReferenceButton.style.marginRight = 10;
            buttonContainer.Add(_addReferenceButton);

            _clearReferenceButton = new Button(OnClearReferenceImage);
            _clearReferenceButton.text = "Clear";
            StyleHelpers.ApplySecondaryButtonStyle(_clearReferenceButton);
            _clearReferenceButton.SetEnabled(false);
            buttonContainer.Add(_clearReferenceButton);

            section.Add(buttonContainer);

            return section;
        }

        private VisualElement BuildQualitySettingsSection()
        {
            _qualitySettingsPanel = new QualitySettingsPanel();

            _qualitySettingsPanel.Initialize(
                () => _pivot, v => _pivot = v,
                () => _topology, v => _topology = v,
                () => _polycount, v => _polycount = v,
                () => _lods, v => _lods = v,
                () => _collider, v => _collider = v,
                () => _textureRes, v => _textureRes = v,
                () => _ao, v => _ao = v,
                allowTopologySelection: () => _allowTopologySelection,
                prefsKeyPrefix: "GAF_QualitySettings_ModelEditor"
            );

            return _qualitySettingsPanel.Root;
        }

        private VisualElement BuildActionBar()
        {
            var actionBar = new VisualElement();
            actionBar.style.flexDirection = FlexDirection.Row;
            actionBar.style.justifyContent = Justify.SpaceBetween;
            actionBar.style.alignItems = Align.Center;
            actionBar.style.paddingLeft = 20;
            actionBar.style.paddingRight = 20;
            actionBar.style.paddingTop = 15;
            actionBar.style.paddingBottom = 15;
            actionBar.style.borderTopWidth = 1;
            actionBar.style.borderTopColor = StyleHelpers.Colors.BorderDark;
            actionBar.style.backgroundColor = StyleHelpers.Colors.BackgroundDark;

            _statusMessage = new Label("");
            _statusMessage.style.fontSize = 12;
            _statusMessage.style.flexGrow = 1;
            actionBar.Add(_statusMessage);

            _editButton = new Button(OnEditClicked);
            _editButton.text = "Edit Model";
            StyleHelpers.ApplyPrimaryButtonStyle(_editButton);
            _editButton.style.minWidth = 150;
            _editButton.SetEnabled(false);
            actionBar.Add(_editButton);

            return actionBar;
        }

        private async void LoadAvailableAssets()
        {
            try
            {
                var assets = await _assetService.GetAllAssetsAsync();

                if (assets != null && assets.Any())
                {
                    var assetNames = assets
                        .Where(a => a != null && a.Name != null)
                        .Select(a => a.Name.Value)
                        .ToList();

                    if (assetNames.Any())
                    {
                        _assetSelectionDropdown.choices = assetNames;
                        _assetSelectionDropdown.index = 0;
                        ShowStatus($"Loaded {assetNames.Count} asset(s)", StyleHelpers.Colors.Success);
                    }
                    else
                    {
                        _assetSelectionDropdown.choices = new List<string> { NoAssetsAvailablePlaceholder };
                        _assetSelectionDropdown.index = 0;
                        ShowStatus("No valid assets found. Generate some models first.", StyleHelpers.Colors.TextHint);
                    }
                }
                else
                {
                    _assetSelectionDropdown.choices = new List<string> { NoAssetsAvailablePlaceholder };
                    _assetSelectionDropdown.index = 0;
                    ShowStatus("No assets found. Generate some models first.", StyleHelpers.Colors.TextHint);
                }
            }
            catch (Exception ex)
            {
                _assetSelectionDropdown.choices = new List<string> { NoAssetsAvailablePlaceholder };
                _assetSelectionDropdown.index = 0;
                ShowStatus($"Failed to load assets: {ex.Message}", StyleHelpers.Colors.Error);
                _logger.Error($"Failed to load assets: {ex}");
            }
        }

        private void OnAssetSelected(string assetName)
        {
            if (assetName == NoAssetsAvailablePlaceholder)
            {
                _editButton.SetEnabled(false);
                _assetPreviewContainer.style.display = DisplayStyle.None;
                UpdateTopologySelectionForAsset(null);
                return;
            }

            _selectedAssetId = assetName;
            _editButton.SetEnabled(true);
            _assetPreviewContainer.style.display = DisplayStyle.Flex;
            UpdateTopologySelectionForAsset(assetName);

            ShowStatus($"Selected: {assetName}", StyleHelpers.Colors.TextPrimary);
        }

        private void UpdateTopologySelectionForAsset(string assetName)
        {
            _allowTopologySelection = Job.IsDecorAssetTypeId(GetLatestAssetTypeId(assetName));
            _qualitySettingsPanel?.RefreshValues();
        }

        private string GetLatestAssetTypeId(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return null;
            }

            var queue = GeniesArtForgeService.Queue;
            if (queue == null)
            {
                return null;
            }

            var job = queue.GetAllJobs()
                .Where(j => j?.AssetName != null)
                .OrderByDescending(j => j.QueuedAt)
                .FirstOrDefault(j => string.Equals(j.AssetName.Value, assetName, StringComparison.OrdinalIgnoreCase));

            return job?.AssetTypeId;
        }

        private void OnAddReferenceImage()
        {
            var path = EditorUtility.OpenFilePanel("Select Reference Image", "", ReferenceImageFileFilter);
            if (!string.IsNullOrEmpty(path))
            {
                LoadReferenceImage(path);
            }
        }

        private void LoadReferenceImage(string path)
        {
            try
            {
                var fileData = File.ReadAllBytes(path);
                _referenceTexture = new Texture2D(2, 2);
                _referenceTexture.LoadImage(fileData);

                _referenceImagePath = path;

                _referenceImageContainer.Clear();
                _referenceImageContainer.style.backgroundImage = new StyleBackground(_referenceTexture);
                _referenceImageContainer.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);

                _clearReferenceButton.SetEnabled(true);
                _addReferenceButton.text = "Change Reference";

                ShowStatus("Reference image loaded", StyleHelpers.Colors.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to load image: {ex.Message}", StyleHelpers.Colors.Error);
                _logger.Error($"Failed to load reference image: {ex}");
            }
        }

        private void OnClearReferenceImage()
        {
            _referenceImagePath = null;

            if (_referenceTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(_referenceTexture);
                _referenceTexture = null;
            }

            _referenceImageContainer.Clear();
            _referenceImageContainer.style.backgroundImage = null;

            var placeholder = new Label("No reference image");
            placeholder.style.color = StyleHelpers.Colors.TextHint;
            _referenceImageContainer.Add(placeholder);

            _clearReferenceButton.SetEnabled(false);
            _addReferenceButton.text = "+ Add Reference";

            ShowStatus("Reference image cleared", StyleHelpers.Colors.TextHint);
        }

        private async void OnEditClicked()
        {
            var validation = ValidateInputs();
            if (!validation.IsSuccess)
            {
                ShowStatus(validation.Error, StyleHelpers.Colors.Error);
                return;
            }

            _editButton.SetEnabled(false);
            ShowStatus("Adding to queue...", StyleHelpers.Colors.TextPrimary);

            try
            {
                // Get the target asset
                var assetNameResult = AssetName.Create(_selectedAssetId);
                if (!assetNameResult.IsSuccess)
                {
                    ShowStatus("Invalid asset name", StyleHelpers.Colors.Error);
                    _editButton.SetEnabled(true);
                    return;
                }

                var targetAssetResult = await _assetService.GetAssetAsync(assetNameResult.Value);
                if (!targetAssetResult.IsSuccess || targetAssetResult.Value == null)
                {
                    ShowStatus("Asset not found", StyleHelpers.Colors.Error);
                    _editButton.SetEnabled(true);
                    return;
                }

                // Create edit job with new asset name (append _edited)
                var editedAssetNameResult = AssetName.Create($"{_selectedAssetId}{EditedAssetSuffix}");
                if (!editedAssetNameResult.IsSuccess)
                {
                    ShowStatus("Failed to create edit asset name", StyleHelpers.Colors.Error);
                    _editButton.SetEnabled(true);
                    return;
                }

                // Prepare reference images
                var referenceImages = new List<string>();
                if (!string.IsNullOrEmpty(_referenceImagePath))
                {
                    referenceImages.Add(_referenceImagePath);
                }

                // Create job
                var job = new Job(
                    JobId.NewId(),
                    editedAssetNameResult.Value,
                    JobType.ModelEdit,
                    _editInstructionsField.value.Trim(),
                    _editInstructionsField.value.Trim(), // Use instructions as prompt
                    AssetTypeProp, // Default asset type
                    referenceImages,
                    new JobQualitySettings
                    {
                        Pivot = _pivot,
                        TopologyType = _topology,
                        Polycount = _polycount,
                        TextureResolution = _textureRes,
                        GenerateLODs = _lods,
                        GenerateCollider = _collider,
                        GenerateAO = _ao,
                        GenerateTransparent = false,
                        GenerateNormalMap = _ao,
                        GenerateMetallicSmoothnessMap = _ao
                    },
                    targetAssetResult.Value.Id // Target asset for edit
                );

                // Enqueue job
                GeniesArtForgeService.Queue.EnqueueJob(job);

                ShowStatus($"Edit job for '{_selectedAssetId}' added to queue!", StyleHelpers.Colors.Success);
                ClearForm();
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}", StyleHelpers.Colors.Error);
                _logger.Error($"Failed to create edit job: {ex}");
            }
            finally
            {
                _editButton.SetEnabled(true);
            }
        }

        private Core.Domain.Common.Result ValidateInputs()
        {
            if (string.IsNullOrEmpty(_selectedAssetId) || _selectedAssetId == NoAssetsAvailablePlaceholder)
            {
                return Core.Domain.Common.Result.Failure("Please select an asset to edit");
            }

            var instructions = _editInstructionsField.value?.Trim() ?? "";
            if (string.IsNullOrEmpty(instructions))
            {
                return Core.Domain.Common.Result.Failure("Edit instructions are required");
            }

            if (instructions.Length < InstructionsMinLength)
            {
                return Core.Domain.Common.Result.Failure("Edit instructions must be at least 5 characters");
            }

            if (instructions.Length > InstructionsMaxLength)
            {
                return Core.Domain.Common.Result.Failure("Edit instructions must be less than 2000 characters");
            }

            return Core.Domain.Common.Result.Success();
        }

        private void ClearForm()
        {
            _editInstructionsField.value = "";
            OnClearReferenceImage();
            _qualitySettingsPanel.RefreshValues();
            _statusMessage.text = "";
        }

        private void ShowStatus(string message, Color color)
        {
            _statusMessage.text = message;
            _statusMessage.style.color = color;
        }

        public void Unload()
        {
            if (_referenceTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(_referenceTexture);
                _referenceTexture = null;
            }

            _root?.RemoveFromHierarchy();
            _root = null;
        }

        public void Update()
        {
        }
    }
}
