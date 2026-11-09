using System;
using System.IO;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using Genies.Sdk.ArtForge.Editor.Core.Application.Services;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using Genies.Sdk.ArtForge.Editor.Core.UI;
using Genies.Sdk.ArtForge.Editor.Core.UI.Helpers;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Genies.Sdk.ArtForge.Editor.Components.ProjectSetup
{
    /// <summary>
    /// Project Setup component view controller.
    /// Manages scene description, art style, and style reference configuration.
    /// Implements IViewController for hub integration.
    /// </summary>
    internal class ProjectSetupView : IViewController
    {
        private const string AssetsFolderName = "Assets";
        private const string AssetsFolderSuffix = "/Assets";
        private const string PngExtension = ".png";
        private const string JpgExtension = ".jpg";
        private const string JpegExtension = ".jpeg";
        private const string StyleReferenceFilePrefix = "style_reference_";
        private const string StyleReferenceTimestampFormat = "yyyyMMdd_HHmmss";

        private ILogger _logger;
        private VisualElement _root;
        private VisualElement _content;
        private ProjectService _projectService;

        // UI Elements
        private TextField _sceneDescriptionField;
        private TextField _artStyleField;
        private VisualElement _styleReferencePreview;
        private VisualElement _dropArea;
        private Button _generateStyleRefButton;
        private Button _importImageButton;
        private Button _clearImageButton;
        private Button _saveButton;
        private Label _statusMessage;

        // State
        private string _styleReferencePath;
        private Texture2D _loadedImage;
        private bool _isGenerating = false;

        public void Load(VisualElement container)
        {
            _logger = GeniesArtForgeService.Logger ?? new UnityLogger();
            // Initialize services
            _projectService = new ProjectService();

            // Build UI
            _root = BuildUI();
            container.Add(_root);

            // Load current settings
            LoadCurrentSettings();
        }

        private VisualElement BuildUI()
        {
            var root = new VisualElement();
            root.style.flexGrow = 1;

            // Header
            var header = StyleHelpers.CreateHeader("Project Setup");
            root.Add(header);

            // Scrollable content
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;

            _content = new VisualElement();
            _content.style.paddingLeft = 20;
            _content.style.paddingRight = 20;
            _content.style.paddingTop = 10;
            _content.style.paddingBottom = 20;

            // Scene Description Section
            var sceneSection = StyleHelpers.CreateSection("Scene Description");

            var sceneHint = new Label("Describe the overall scene or game environment (minimum 20 characters)");
            sceneHint.style.fontSize = 11;
            sceneHint.style.color = StyleHelpers.Colors.TextHint;
            sceneHint.style.marginBottom = 8;
            sceneSection.Add(sceneHint);

            _sceneDescriptionField = new TextField();
            _sceneDescriptionField.multiline = true;
            _sceneDescriptionField.style.minHeight = 100;
            _sceneDescriptionField.style.whiteSpace = WhiteSpace.Normal;

            _sceneDescriptionField.RegisterValueChangedCallback(evt =>
            {
                AutoSaveProjectSettings();
            });
            sceneSection.Add(_sceneDescriptionField);

            _content.Add(sceneSection);

            // Art Style Section
            var artSection = StyleHelpers.CreateSection("Art Style");

            var artHint = new Label("Define the visual style for your assets (minimum 10 characters)");
            artHint.style.fontSize = 11;
            artHint.style.color = StyleHelpers.Colors.TextHint;
            artHint.style.marginBottom = 8;
            artSection.Add(artHint);

            _artStyleField = new TextField();
            _artStyleField.multiline = true;
            _artStyleField.style.minHeight = 80;
            _artStyleField.style.whiteSpace = WhiteSpace.Normal;

            _artStyleField.RegisterValueChangedCallback(evt =>
            {
                AutoSaveProjectSettings();
            });
            artSection.Add(_artStyleField);

            _content.Add(artSection);

            // Style Reference Section
            var styleSection = StyleHelpers.CreateSection("Style Reference");

            var styleHint = new Label("Generate from descriptions or import your own reference image");
            styleHint.style.fontSize = 11;
            styleHint.style.color = StyleHelpers.Colors.TextHint;
            styleHint.style.marginBottom = 8;
            styleSection.Add(styleHint);


            _generateStyleRefButton = new Button(OnGenerateStyleReference);
            _generateStyleRefButton.text = "Generate Style Reference";
            StyleHelpers.ApplyPrimaryButtonStyle(_generateStyleRefButton, "Project", 190);
            _generateStyleRefButton.style.alignSelf = Align.FlexEnd;
            _generateStyleRefButton.style.marginBottom = 15;
            styleSection.Add(_generateStyleRefButton);

            _styleReferencePreview = new VisualElement();
            _styleReferencePreview.style.maxHeight = 512;
            _styleReferencePreview.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            _styleReferencePreview.style.borderTopWidth = 1;
            _styleReferencePreview.style.borderBottomWidth = 1;
            _styleReferencePreview.style.borderLeftWidth = 1;
            _styleReferencePreview.style.borderRightWidth = 1;
            _styleReferencePreview.style.borderTopColor = new Color(0.4f, 0.4f, 0.4f);
            _styleReferencePreview.style.borderBottomColor = new Color(0.4f, 0.4f, 0.4f);
            _styleReferencePreview.style.borderLeftColor = new Color(0.4f, 0.4f, 0.4f);
            _styleReferencePreview.style.borderRightColor = new Color(0.4f, 0.4f, 0.4f);
            _styleReferencePreview.style.borderTopLeftRadius = 4;
            _styleReferencePreview.style.borderTopRightRadius = 4;
            _styleReferencePreview.style.borderBottomLeftRadius = 4;
            _styleReferencePreview.style.borderBottomRightRadius = 4;
            _styleReferencePreview.style.marginBottom = 10;
            _styleReferencePreview.style.alignSelf = Align.Center;
            styleSection.Add(_styleReferencePreview);


            var dropLabel = new Label("Import External Image");
            dropLabel.style.fontSize = 12;
            dropLabel.style.color = StyleHelpers.Colors.TextPrimary;
            dropLabel.style.marginTop = 10;
            dropLabel.style.marginBottom = 5;
            styleSection.Add(dropLabel);

            _dropArea = new VisualElement();
            _dropArea.style.height = 80;
            _dropArea.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            _dropArea.style.borderTopWidth = 2;
            _dropArea.style.borderBottomWidth = 2;
            _dropArea.style.borderLeftWidth = 2;
            _dropArea.style.borderRightWidth = 2;
            _dropArea.style.borderTopColor = new Color(0.4f, 0.4f, 0.4f);
            _dropArea.style.borderBottomColor = new Color(0.4f, 0.4f, 0.4f);
            _dropArea.style.borderLeftColor = new Color(0.4f, 0.4f, 0.4f);
            _dropArea.style.borderRightColor = new Color(0.4f, 0.4f, 0.4f);
            _dropArea.style.borderTopLeftRadius = 4;
            _dropArea.style.borderTopRightRadius = 4;
            _dropArea.style.borderBottomLeftRadius = 4;
            _dropArea.style.borderBottomRightRadius = 4;
            _dropArea.style.justifyContent = Justify.Center;
            _dropArea.style.alignItems = Align.Center;

            var dropText = new Label("Drag and drop an image here\nor click to browse");
            dropText.style.fontSize = 12;
            dropText.style.color = new Color(0.6f, 0.6f, 0.6f);
            dropText.style.unityTextAlign = TextAnchor.MiddleCenter;
            dropText.style.whiteSpace = WhiteSpace.Normal;
            _dropArea.Add(dropText);

            // Register drag-and-drop events
            _dropArea.RegisterCallback<DragEnterEvent>(OnDragEnter);
            _dropArea.RegisterCallback<DragLeaveEvent>(OnDragLeave);
            _dropArea.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            _dropArea.RegisterCallback<DragPerformEvent>(OnDragPerform);

            // Click to browse
            _dropArea.RegisterCallback<ClickEvent>(evt =>
            {
                string path = EditorUtility.OpenFilePanel("Select Style Reference Image", "", "png,jpg,jpeg");
                if (!string.IsNullOrEmpty(path))
                {
                    ImportExternalImage(path);
                }
            });

            styleSection.Add(_dropArea);

            _content.Add(styleSection);

            // Status Message
            _statusMessage = new Label();
            _statusMessage.style.marginTop = 15;
            _statusMessage.style.display = DisplayStyle.None;
            _statusMessage.style.paddingLeft = 10;
            _statusMessage.style.paddingRight = 10;
            _statusMessage.style.paddingTop = 10;
            _statusMessage.style.paddingBottom = 10;
            _statusMessage.style.borderTopLeftRadius = 3;
            _statusMessage.style.borderTopRightRadius = 3;
            _statusMessage.style.borderBottomLeftRadius = 3;
            _statusMessage.style.borderBottomRightRadius = 3;
            _content.Add(_statusMessage);

            scrollView.Add(_content);
            root.Add(scrollView);

            return root;
        }

        private async void LoadCurrentSettings()
        {
            try
            {
                var project = await _projectService.GetCurrentProjectAsync();

                if (project != null)
                {
                    _sceneDescriptionField.SetValueWithoutNotify(project.SceneDescription ?? string.Empty);
                    _artStyleField.SetValueWithoutNotify(project.ArtStyle ?? string.Empty);

                    if (!string.IsNullOrEmpty(project.StyleReferencePath) && File.Exists(project.StyleReferencePath))
                    {
                        _styleReferencePath = project.StyleReferencePath;
                        LoadStyleReferenceImage(project.StyleReferencePath);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to load settings: {ex.Message}");
            }
        }


        private async void AutoSaveProjectSettings()
        {
            var sceneDesc = _sceneDescriptionField.value ?? "";
            var artStyle = _artStyleField.value ?? "";

            if (string.IsNullOrWhiteSpace(sceneDesc) && string.IsNullOrWhiteSpace(artStyle))
            {
                return;
            }

            try
            {
                await _projectService.AutoSaveProjectSettingsAsync(sceneDesc, artStyle);
            }
            catch (System.Exception ex)
            {
                _logger.Warning($"Auto-save failed: {ex.Message}");
            }
        }


        private void OnGenerateStyleReference()
        {
            // Guard: Prevent re-entrance (multiple calls before flag is set)
            if (_isGenerating)
            {
                ShowError("Style reference generation already in progress");
                return;
            }

            _isGenerating = true;

            try
            {
                var sceneDesc = _sceneDescriptionField.value?.Trim() ?? "";
                var artStyle = _artStyleField.value?.Trim() ?? "";

                if (string.IsNullOrEmpty(sceneDesc) || string.IsNullOrEmpty(artStyle))
                {
                    ShowError("Please fill in both Scene Description and Art Style before generating");
                    _isGenerating = false;
                    return;
                }

                // Disable button and update text
                _generateStyleRefButton.SetEnabled(false);
                _generateStyleRefButton.text = "Generating...";

                // Create a job to generate style reference images
                string prompt = $"Style reference image for {artStyle} art style. Scene: {sceneDesc}";
                string assetNameStr = $"StyleRef_{artStyle.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}";

                var assetNameResult = AssetName.Create(assetNameStr);
                if (!assetNameResult.IsSuccess)
                {
                    ShowError($"Invalid asset name: {assetNameResult.Error}");
                    _generateStyleRefButton.SetEnabled(true);
                    _generateStyleRefButton.text = "Generate Style Reference";
                    _isGenerating = false;
                    return;
                }

                // Create job for style reference generation
                var job = new Job(
                    JobId.NewId(),
                    assetNameResult.Value,
                    JobType.StyleReferenceGeneration,
                    $"{sceneDesc} - {artStyle}",
                    prompt,
                    "style_reference",
                    null, // No reference images needed
                    new JobQualitySettings
                    {
                        TextureResolution = "1024",
                        GenerateCollider = false,
                        GenerateAO = false,
                        GenerateTransparent = false,
                        GenerateNormalMap = false,
                        GenerateMetallicSmoothnessMap = false
                    },
                    null // No target asset
                );

                // Enqueue job
                GeniesArtForgeService.Queue.EnqueueJob(job);

                ShowSuccess($"Style reference generation queued! Check the Queue panel for progress.");

                _generateStyleRefButton.SetEnabled(true);
                _generateStyleRefButton.text = "Generate Style Reference";
                _isGenerating = false;
            }
            catch (Exception ex)
            {
                ShowError($"Failed to queue style reference generation: {ex.Message}");
                _generateStyleRefButton.SetEnabled(true);
                _generateStyleRefButton.text = "Generate Style Reference";
                _isGenerating = false;
            }
        }


        private void OnDragEnter(DragEnterEvent evt)
        {
            Color projectColor = StyleHelpers.Colors.Accent;
            _dropArea.style.borderTopColor = projectColor;
            _dropArea.style.borderBottomColor = projectColor;
            _dropArea.style.borderLeftColor = projectColor;
            _dropArea.style.borderRightColor = projectColor;
            _dropArea.style.backgroundColor = new Color(projectColor.r, projectColor.g, projectColor.b, 0.1f);
        }

        private void OnDragLeave(DragLeaveEvent evt)
        {
            _dropArea.style.borderTopColor = new Color(0.4f, 0.4f, 0.4f);
            _dropArea.style.borderBottomColor = new Color(0.4f, 0.4f, 0.4f);
            _dropArea.style.borderLeftColor = new Color(0.4f, 0.4f, 0.4f);
            _dropArea.style.borderRightColor = new Color(0.4f, 0.4f, 0.4f);
            _dropArea.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (DragAndDrop.paths.Length > 0)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            }
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            _dropArea.style.borderTopColor = new Color(0.4f, 0.4f, 0.4f);
            _dropArea.style.borderBottomColor = new Color(0.4f, 0.4f, 0.4f);
            _dropArea.style.borderLeftColor = new Color(0.4f, 0.4f, 0.4f);
            _dropArea.style.borderRightColor = new Color(0.4f, 0.4f, 0.4f);
            _dropArea.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);

            if (DragAndDrop.paths.Length > 0)
            {
                string draggedPath = DragAndDrop.paths[0];
                ImportExternalImage(draggedPath);
            }
        }


        private async void ImportExternalImage(string sourcePath)
        {
            try
            {
                // Validate file exists
                if (!File.Exists(sourcePath))
                {
                    ShowError($"Image file not found: {sourcePath}");
                    return;
                }

                // Validate file extension
            string ext = Path.GetExtension(sourcePath).ToLower();
            if (ext != PngExtension && ext != JpgExtension && ext != JpegExtension)
            {
                ShowError("Only PNG and JPG images are supported");
                return;
            }

                // Get style references folder using BrandingConstants
                string styleRefFolder = BrandingConstants.AssetPaths.StyleReferences;
                string fullFolder = Path.Combine(Application.dataPath.Replace(AssetsFolderSuffix, ""), styleRefFolder);
                Directory.CreateDirectory(fullFolder);

                // Normalize paths for comparison
                string normalizedSourcePath = Path.GetFullPath(sourcePath);
                string normalizedStyleRefFolder = Path.GetFullPath(fullFolder);

                // Check if source is already in StyleReferences folder
                bool isAlreadyInStyleReferences = normalizedSourcePath.StartsWith(normalizedStyleRefFolder, StringComparison.OrdinalIgnoreCase);

                string filePath;
                string fullFilePath;

                if (isAlreadyInStyleReferences)
                {
                    fullFilePath = normalizedSourcePath;

                    // Convert to relative Unity path
                    if (fullFilePath.StartsWith(Application.dataPath))
                    {
                        filePath = AssetsFolderName + fullFilePath.Substring(Application.dataPath.Length);
            }
                    else
                    {
                        // Fallback: try to construct relative path
                        filePath = Path.Combine(styleRefFolder, Path.GetFileName(fullFilePath));
                    }
                }
                else
                {
                    // Generate filename with timestamp
                    string timestamp = DateTime.Now.ToString(StyleReferenceTimestampFormat);
                    string fileName = $"{StyleReferenceFilePrefix}{timestamp}{PngExtension}";
                    filePath = Path.Combine(styleRefFolder, fileName);
                    fullFilePath = Path.Combine(fullFolder, fileName);

                    // Ensure unique filename
                    int counter = 1;
                    while (File.Exists(fullFilePath))
                    {
                        fileName = $"{StyleReferenceFilePrefix}{timestamp}_{counter}{PngExtension}";
                        filePath = Path.Combine(styleRefFolder, fileName);
                        fullFilePath = Path.Combine(fullFolder, fileName);
                        counter++;
                    }

                    // Copy to StyleReferences folder (convert to PNG if needed)
                    if (ext == PngExtension)
                    {
                        File.Copy(sourcePath, fullFilePath, false);
                    }
                    else
                    {
                        // Load and convert to PNG
                        byte[] imageBytes = File.ReadAllBytes(sourcePath);
                        Texture2D tempTex = new Texture2D(2, 2);
                        tempTex.LoadImage(imageBytes);
                        byte[] pngBytes = tempTex.EncodeToPNG();
                        File.WriteAllBytes(fullFilePath, pngBytes);
                        UnityEngine.Object.DestroyImmediate(tempTex);
                    }

                    // Import the file
                    AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate);
                }

                // Store path and update preview
                _styleReferencePath = fullFilePath;
                LoadStyleReferenceImage(fullFilePath);

                // Update project settings
                await _projectService.SetStyleReferenceAsync(filePath);

                ShowSuccess(isAlreadyInStyleReferences ?
                    "Style reference loaded successfully!" :
                    "Style reference imported successfully!");
            }
            catch (Exception ex)
            {
                ShowError($"Failed to import image: {ex.Message}");
            }
        }


        private void LoadStyleReferenceImage(string path)
        {
            try
            {
                var fileData = File.ReadAllBytes(path);

                // Dispose old texture
                if (_loadedImage != null)
                {
                    UnityEngine.Object.DestroyImmediate(_loadedImage);
                }

                _loadedImage = new Texture2D(2, 2);
                _loadedImage.LoadImage(fileData);

                UpdateImagePreview();
            }
            catch (Exception ex)
            {
                ShowError($"Failed to load image: {ex.Message}");
                ClearStyleReferenceImage();
            }
        }


        private void UpdateImagePreview()
        {
            _styleReferencePreview.Clear();
            _styleReferencePreview.style.backgroundImage = Background.FromTexture2D(_loadedImage);

            if (_loadedImage != null)
            {
                // Calculate dimensions maintaining aspect ratio
                // Max height: 512px
                // Max width: container width
                float aspectRatio = (float)_loadedImage.width / _loadedImage.height;

                // Get the container width
                float maxWidth = _content.resolvedStyle.width;
                if (maxWidth <= 0)
                {
                    // Fallback if content width not resolved yet - schedule retry
                    EditorApplication.delayCall += UpdateImagePreview;
                    maxWidth = 512;
                }

                float maxHeight = 512;
                float width, height;

                if (aspectRatio > 1) // Wider than tall
                {
                    width = Mathf.Min(_loadedImage.width, maxWidth);
                    height = width / aspectRatio;

                    // If height exceeds max, recalculate from height
                    if (height > maxHeight)
                    {
                        height = maxHeight;
                        width = height * aspectRatio;
                    }
                }
                else // Taller than wide or square
                {
                    height = Mathf.Min(_loadedImage.height, maxHeight);
                    width = height * aspectRatio;

                    // If width exceeds max, recalculate from width
                    if (width > maxWidth)
                    {
                        width = maxWidth;
                        height = width / aspectRatio;
                    }
                }

                _styleReferencePreview.style.width = width;
                _styleReferencePreview.style.height = height;
            }
            else
            {
                // No image, use default square dimensions
                _styleReferencePreview.style.width = 512;
                _styleReferencePreview.style.height = 512;
            }
        }

        private void ClearStyleReferenceImage()
        {
            _styleReferencePreview.Clear();
            _styleReferencePreview.style.backgroundImage = StyleKeyword.None;
            var noImageLabel = new Label("No image selected");
            noImageLabel.style.color = StyleHelpers.Colors.TextHint;
            _styleReferencePreview.Add(noImageLabel);

            if (_loadedImage != null)
            {
                UnityEngine.Object.DestroyImmediate(_loadedImage);
                _loadedImage = null;
            }
        }

        private void ShowSuccess(string message)
        {
            _statusMessage.text = message;
            _statusMessage.style.display = DisplayStyle.Flex;
            _statusMessage.style.backgroundColor = new Color(0.0f, 0.5f, 0.25f, 0.3f);
            _statusMessage.style.color = StyleHelpers.Colors.Success;
            _statusMessage.style.borderTopWidth = 1;
            _statusMessage.style.borderBottomWidth = 1;
            _statusMessage.style.borderLeftWidth = 1;
            _statusMessage.style.borderRightWidth = 1;
            _statusMessage.style.borderTopColor = StyleHelpers.Colors.Success;
            _statusMessage.style.borderBottomColor = StyleHelpers.Colors.Success;
            _statusMessage.style.borderLeftColor = StyleHelpers.Colors.Success;
            _statusMessage.style.borderRightColor = StyleHelpers.Colors.Success;

            // Auto-hide after 3 seconds
            _statusMessage.schedule.Execute(() =>
            {
                _statusMessage.style.display = DisplayStyle.None;
            }).ExecuteLater(3000);
        }

        private void ShowError(string message)
        {
            _statusMessage.text = message;
            _statusMessage.style.display = DisplayStyle.Flex;
            _statusMessage.style.backgroundColor = new Color(0.5f, 0.0f, 0.0f, 0.3f);
            _statusMessage.style.color = StyleHelpers.Colors.Error;
            _statusMessage.style.borderTopWidth = 1;
            _statusMessage.style.borderBottomWidth = 1;
            _statusMessage.style.borderLeftWidth = 1;
            _statusMessage.style.borderRightWidth = 1;
            _statusMessage.style.borderTopColor = StyleHelpers.Colors.Error;
            _statusMessage.style.borderBottomColor = StyleHelpers.Colors.Error;
            _statusMessage.style.borderLeftColor = StyleHelpers.Colors.Error;
            _statusMessage.style.borderRightColor = StyleHelpers.Colors.Error;
        }

        public void Unload()
        {
            // Cleanup loaded image
            if (_loadedImage != null)
            {
                UnityEngine.Object.DestroyImmediate(_loadedImage);
                _loadedImage = null;
            }

            // Remove from hierarchy
            _root?.RemoveFromHierarchy();
            _root = null;
        }
    }
}
