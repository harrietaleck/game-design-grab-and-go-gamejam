using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using Genies.Sdk.ArtForge.Editor.Core.UI.Helpers;

namespace Genies.Sdk.ArtForge.Editor.Hub.UI
{
    /// <summary>
    /// Reusable panel for displaying reference images (Project + Additional user-uploaded).
    /// Used in ProjectSetup for style reference management.
    /// </summary>
    internal class ReferenceImagesPanel
    {
        private readonly ILogger _logger;
        // Visual element root
        public VisualElement Root { get; private set; }

        // Events
        public event Action<string> OnAdditionalImageAdded;
        public event Action OnAdditionalImageRemoved;

        // State
        public string AdditionalImagePath { get; private set; }
        public bool HasAdditionalImage => !string.IsNullOrEmpty(AdditionalImagePath);

        // UI Elements
        private Image _projectImageThumbnail;
        private VisualElement _additionalSlot;
        private VisualElement _dropZone;
        private Image _additionalImageThumbnail;
        private Label _additionalLabel; // Label that appears when image is shown
        private Texture2D _projectTexture;
        private Texture2D _additionalTexture;

        // User references storage
        private static string TempFolderPath => BrandingConstants.AssetPaths.UserReferences;

        // Colors (using StyleHelpers colors)
        private static readonly Color BorderNormal = new Color(0.25f, 0.25f, 0.25f);
        private static readonly Color BorderHover = new Color(0.4f, 0.4f, 0.4f);
        private static Color BorderDragOver => StyleHelpers.Colors.Accent; // Use StyleHelpers accent color
        private static readonly Color DropZoneBackground = new Color(0.2f, 0.2f, 0.2f);
        private static readonly Color ThumbnailBackground = new Color(0.15f, 0.15f, 0.15f);

        private string _projectStyleReferencePath; // Set via constructor

        public ReferenceImagesPanel(string projectStyleReferencePath = null, ILogger logger = null)
        {
            _logger = logger ?? GeniesArtForgeService.Logger ?? new UnityLogger();
            _projectStyleReferencePath = projectStyleReferencePath;
            BuildUI();
            LoadProjectStyleReference();
        }

        private void BuildUI()
        {
            // Root container (width includes padding)
            Root = new VisualElement();
            Root.style.width = 140; // 120px content + 10px left padding + 10px right padding
            Root.style.flexShrink = 0;
            Root.style.paddingLeft = 10;
            Root.style.paddingRight = 0; // No right padding to prevent overflow
            Root.style.paddingTop = 10;
            Root.style.paddingBottom = 10;

            // Header
            var header = new Label("Reference Images");
            header.style.fontSize = 13;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = StyleHelpers.Colors.TextPrimary;
            header.style.marginBottom = 8;
            Root.Add(header);

            // Project Image Thumbnail Slot
            BuildProjectSlot();

            // Spacer
            var spacer = new VisualElement();
            spacer.style.height = 10;
            Root.Add(spacer);

            // Additional Image Slot (Drop Zone or Thumbnail)
            BuildAdditionalSlot();
        }

        private void BuildProjectSlot()
        {
            var projectSlot = new VisualElement();
            projectSlot.style.width = 120;
            projectSlot.style.height = 120; // Same as width for square
            projectSlot.style.backgroundColor = ThumbnailBackground;
            projectSlot.style.borderTopWidth = 1;
            projectSlot.style.borderBottomWidth = 1;
            projectSlot.style.borderLeftWidth = 1;
            projectSlot.style.borderRightWidth = 1;
            projectSlot.style.borderTopColor = BorderNormal;
            projectSlot.style.borderBottomColor = BorderNormal;
            projectSlot.style.borderLeftColor = BorderNormal;
            projectSlot.style.borderRightColor = BorderNormal;
            projectSlot.style.borderTopLeftRadius = 4;
            projectSlot.style.borderTopRightRadius = 4;
            projectSlot.style.borderBottomLeftRadius = 4;
            projectSlot.style.borderBottomRightRadius = 4;
            projectSlot.style.alignItems = Align.Center;
            projectSlot.style.justifyContent = Justify.Center;

            // Thumbnail image
            _projectImageThumbnail = new Image();
            _projectImageThumbnail.style.maxWidth = 110;
            _projectImageThumbnail.style.maxHeight = 110; // Match square aspect
            _projectImageThumbnail.scaleMode = ScaleMode.ScaleToFit;
            projectSlot.Add(_projectImageThumbnail);

            Root.Add(projectSlot);

            // Label below thumbnail
            var label = new Label("Project Ref Img");
            label.style.fontSize = 9;
            label.style.color = StyleHelpers.Colors.TextHint;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.marginTop = 2;
            Root.Add(label);
        }

        private void BuildAdditionalSlot()
        {
            _additionalSlot = new VisualElement();
            _additionalSlot.style.width = 120;
            _additionalSlot.style.height = 30; // Compact strip when empty (25% of 120)

            // Build drop zone (shown when no image) - compact version
            BuildDropZone();
            _additionalSlot.Add(_dropZone);

            Root.Add(_additionalSlot);

            // Note: Label "User Reference" will only appear when an image is added
        }

        private void BuildDropZone()
        {
            _dropZone = new VisualElement();
            _dropZone.style.width = 120;
            _dropZone.style.height = 30; // Compact strip
            _dropZone.style.backgroundColor = DropZoneBackground;
            _dropZone.style.borderTopWidth = 2;
            _dropZone.style.borderBottomWidth = 2;
            _dropZone.style.borderLeftWidth = 2;
            _dropZone.style.borderRightWidth = 2;
            _dropZone.style.borderTopColor = BorderNormal;
            _dropZone.style.borderBottomColor = BorderNormal;
            _dropZone.style.borderLeftColor = BorderNormal;
            _dropZone.style.borderRightColor = BorderNormal;
            _dropZone.style.borderTopLeftRadius = 4;
            _dropZone.style.borderTopRightRadius = 4;
            _dropZone.style.borderBottomLeftRadius = 4;
            _dropZone.style.borderBottomRightRadius = 4;
            _dropZone.style.alignItems = Align.Center;
            _dropZone.style.justifyContent = Justify.Center;
            _dropZone.style.cursor = StyleKeyword.Auto;

            // Compact text (no icon)
            var text = new Label("Drop reference");
            text.style.fontSize = 9;
            text.style.color = StyleHelpers.Colors.TextHint;
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            _dropZone.Add(text);

            // Register drag-and-drop events
            _dropZone.RegisterCallback<DragEnterEvent>(OnDragEnter);
            _dropZone.RegisterCallback<DragLeaveEvent>(OnDragLeave);
            _dropZone.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            _dropZone.RegisterCallback<DragPerformEvent>(OnDragPerform);

            // Click to browse fallback
            _dropZone.RegisterCallback<ClickEvent>(evt =>
            {
                string path = EditorUtility.OpenFilePanel("Select Reference Image", "", "png,jpg,jpeg");
                if (!string.IsNullOrEmpty(path))
                {
                    ImportAndDisplayImage(path);
                }
            });
        }

        private void LoadProjectStyleReference()
        {
            // Load project style reference if it exists
            string stylePath = _projectStyleReferencePath;
            if (!string.IsNullOrEmpty(stylePath) && File.Exists(stylePath))
            {
                try
                {
                    byte[] imageData = File.ReadAllBytes(stylePath);
                    _projectTexture = new Texture2D(2, 2);
                    _projectTexture.LoadImage(imageData);
                    _projectImageThumbnail.image = _projectTexture;
                }
                catch (Exception ex)
                {
                    _logger.Warning($"[ReferenceImagesPanel] Failed to load project style reference: {ex.Message}");
                }
            }
        }

        // Drag-and-drop event handlers
        private void OnDragEnter(DragEnterEvent evt)
        {
            SetBorderColor(BorderDragOver);
        }

        private void OnDragLeave(DragLeaveEvent evt)
        {
            SetBorderColor(BorderNormal);
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            SetBorderColor(BorderNormal);

            if (DragAndDrop.paths.Length > 0)
            {
                string path = DragAndDrop.paths[0];
                ImportAndDisplayImage(path);
            }

            DragAndDrop.AcceptDrag();
        }

        private void SetBorderColor(Color color)
        {
            _dropZone.style.borderTopColor = color;
            _dropZone.style.borderBottomColor = color;
            _dropZone.style.borderLeftColor = color;
            _dropZone.style.borderRightColor = color;
        }

        private void ImportAndDisplayImage(string sourcePath)
        {
            // Validate file exists
            if (!File.Exists(sourcePath))
            {
                _logger.Error($"[ReferenceImagesPanel] Image file not found: {sourcePath}");
                return;
            }

            // Validate file extension
            string ext = Path.GetExtension(sourcePath).ToLower();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
            {
                _logger.Error("[ReferenceImagesPanel] Only PNG and JPG images are supported");
                return;
            }

            try
            {
                // Create temp folder if needed
                string tempFolder = Path.Combine(UnityEngine.Application.dataPath.Replace("/Assets", ""), TempFolderPath);
                Directory.CreateDirectory(tempFolder);

                // Generate unique filename
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string fileName = $"userRef_{timestamp}.png";
                string destPath = Path.Combine(tempFolder, fileName);

                // Copy and import image
                File.Copy(sourcePath, destPath, true);
                AssetDatabase.ImportAsset(TempFolderPath + "/" + fileName);

                // Store path and display
                AdditionalImagePath = destPath;
                DisplayAdditionalImage(destPath);

                // Notify listeners
                OnAdditionalImageAdded?.Invoke(destPath);

                _logger.Info($"[ReferenceImagesPanel] Imported additional reference: {fileName}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[ReferenceImagesPanel] Failed to import image: {ex.Message}");
            }
        }

        private void DisplayAdditionalImage(string imagePath)
        {
            try
            {
                // Load texture
                byte[] imageData = File.ReadAllBytes(imagePath);
                if (_additionalTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(_additionalTexture);
                }
                _additionalTexture = new Texture2D(2, 2);
                _additionalTexture.LoadImage(imageData);

                // Expand slot to full size
                _additionalSlot.style.height = 120;

                // Replace drop zone with thumbnail
                _additionalSlot.Clear();

                var thumbnailContainer = new VisualElement();
                thumbnailContainer.style.width = 120;
                thumbnailContainer.style.height = 120; // Same as width for square
                thumbnailContainer.style.backgroundColor = ThumbnailBackground;
                thumbnailContainer.style.borderTopWidth = 1;
                thumbnailContainer.style.borderBottomWidth = 1;
                thumbnailContainer.style.borderLeftWidth = 1;
                thumbnailContainer.style.borderRightWidth = 1;
                thumbnailContainer.style.borderTopColor = BorderNormal;
                thumbnailContainer.style.borderBottomColor = BorderNormal;
                thumbnailContainer.style.borderLeftColor = BorderNormal;
                thumbnailContainer.style.borderRightColor = BorderNormal;
                thumbnailContainer.style.borderTopLeftRadius = 4;
                thumbnailContainer.style.borderTopRightRadius = 4;
                thumbnailContainer.style.borderBottomLeftRadius = 4;
                thumbnailContainer.style.borderBottomRightRadius = 4;
                thumbnailContainer.style.alignItems = Align.Center;
                thumbnailContainer.style.justifyContent = Justify.Center;

                _additionalImageThumbnail = new Image();
                _additionalImageThumbnail.image = _additionalTexture;
                _additionalImageThumbnail.style.maxWidth = 110;
                _additionalImageThumbnail.style.maxHeight = 110; // Match square aspect
                _additionalImageThumbnail.scaleMode = ScaleMode.ScaleToFit;
                thumbnailContainer.Add(_additionalImageThumbnail);

                // Context menu for removal
                thumbnailContainer.RegisterCallback<ContextClickEvent>(evt =>
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Remove"), false, () => ClearAdditionalImage());
                    menu.ShowAsContext();
                });

                _additionalSlot.Add(thumbnailContainer);

                // Add label below thumbnail
                _additionalLabel = new Label("User Reference");
                _additionalLabel.style.fontSize = 9;
                _additionalLabel.style.color = StyleHelpers.Colors.TextHint;
                _additionalLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                _additionalLabel.style.marginTop = 2;
                Root.Add(_additionalLabel);
            }
            catch (Exception ex)
            {
                _logger.Error($"[ReferenceImagesPanel] Failed to display image: {ex.Message}");
            }
        }

        // Public API
        public void SetAdditionalImage(string imagePath)
        {
            if (File.Exists(imagePath))
            {
                AdditionalImagePath = imagePath;
                DisplayAdditionalImage(imagePath);
            }
        }

        public void ClearAdditionalImage()
        {
            AdditionalImagePath = null;

            if (_additionalTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(_additionalTexture);
                _additionalTexture = null;
            }

            // Remove label if it exists
            if (_additionalLabel != null)
            {
                Root.Remove(_additionalLabel);
                _additionalLabel = null;
            }

            // Collapse slot back to compact size
            _additionalSlot.style.height = 30;

            // Restore drop zone
            _additionalSlot.Clear();
            BuildDropZone();
            _additionalSlot.Add(_dropZone);

            // Notify listeners
            OnAdditionalImageRemoved?.Invoke();

            _logger.Info("[ReferenceImagesPanel] Cleared additional reference");
        }

        public void SetEnabled(bool enabled)
        {
            Root.SetEnabled(enabled);
        }

        /// <summary>
        /// Update the project style reference image
        /// Call this when the project settings change
        /// </summary>
        public void UpdateProjectStyleReference(string newPath)
        {
            _projectStyleReferencePath = newPath;

            // Dispose old texture
            if (_projectTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(_projectTexture);
                _projectTexture = null;
            }

            // Load new texture
            LoadProjectStyleReference();
        }
    }
}
