using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using Genies.Sdk.ArtForge.Editor.Core.UI.Helpers;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;

namespace Genies.Sdk.ArtForge.Editor.Components.ModelGenerator.UI
{
    /// <summary>
    /// Panel for managing reference images with proper texture lifecycle.
    /// Handles adding, removing, and displaying image thumbnails.
    /// </summary>
    internal sealed class ReferenceImagesSection : IDisposable
    {
        private const int MaxImages = 4;
        private const int ThumbnailSize = 100;
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB
        private const string ReferenceImageFileExtensions = "png,jpg,jpeg";
        private const string LastReferenceImageFolderPrefKey = "GAF_LastReferenceImageFolder_ModelGenerator";

        private readonly ILogger _logger;
        private VisualElement _root;
        private VisualElement _imagesContainer;
        private Button _addButton;

        private List<string> _imagePaths = new List<string>();
        private List<Texture2D> _loadedTextures = new List<Texture2D>();
        private List<VisualElement> _thumbnailCards = new List<VisualElement>();

        public VisualElement Root => _root;
        public IReadOnlyList<string> ImagePaths => _imagePaths;

        /// <summary>
        /// Raised when images are added or removed
        /// </summary>
        public event Action OnChanged;

        public ReferenceImagesSection(ILogger logger = null)
        {
            _logger = logger ?? GeniesArtForgeService.Logger ?? new UnityLogger();
            BuildUI();
        }

        private void BuildUI()
        {
            _root = StyleHelpers.CreateSection("Reference Images (Optional)");

            var hint = new Label("Add 1-4 reference images to guide generation");
            hint.style.fontSize = 11;
            hint.style.color = StyleHelpers.Colors.TextHint;
            hint.style.marginBottom = 8;
            _root.Add(hint);

            _imagesContainer = new VisualElement();
            _imagesContainer.style.flexDirection = FlexDirection.Row;
            _imagesContainer.style.flexWrap = Wrap.Wrap;
            _imagesContainer.style.marginBottom = 10;
            _root.Add(_imagesContainer);

            _addButton = new Button(OnAddImageClicked);
            _addButton.text = "+ Add Image";
            StyleHelpers.ApplySecondaryButtonStyle(_addButton);
            _addButton.style.alignSelf = Align.FlexStart;
            _root.Add(_addButton);
        }

        /// <summary>
        /// Set image paths from state (on load or reset)
        /// </summary>
        public void SetImages(List<string> paths)
        {
            // Clear existing
            Clear();

            // Add new images
            if (paths != null)
            {
                foreach (var path in paths)
                {
                    if (File.Exists(path))
                    {
                        AddImageInternal(path);
                    }
                }
            }

            UpdateAddButtonState();
        }

        private void OnAddImageClicked()
        {
            if (_imagePaths.Count >= MaxImages)
            {
                EditorUtility.DisplayDialog("Maximum Images", $"Maximum {MaxImages} reference images allowed", "OK");
                return;
            }

            string initialFolder = GetInitialFolderForPicker();
            var path = EditorUtility.OpenFilePanel("Select Reference Image", initialFolder, ReferenceImageFileExtensions);
            if (!string.IsNullOrEmpty(path))
            {
                SaveLastSelectedFolder(path);

                // Validate file size
                var fileInfo = new FileInfo(path);
                if (fileInfo.Length > MaxFileSizeBytes)
                {
                    EditorUtility.DisplayDialog("File Too Large", $"Image file is too large (max 10MB). File size: {fileInfo.Length / (1024 * 1024)}MB", "OK");
                    return;
                }

                AddImageInternal(path);
                UpdateAddButtonState();
                OnChanged?.Invoke();
            }
        }

        private static string GetInitialFolderForPicker()
        {
            string persistedFolder = EditorPrefs.GetString(LastReferenceImageFolderPrefKey, string.Empty);
            return !string.IsNullOrEmpty(persistedFolder) && Directory.Exists(persistedFolder)
                ? persistedFolder
                : string.Empty;
        }

        private static void SaveLastSelectedFolder(string selectedFilePath)
        {
            if (string.IsNullOrEmpty(selectedFilePath))
            {
                return;
            }

            string folder = Path.GetDirectoryName(selectedFilePath);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                EditorPrefs.SetString(LastReferenceImageFolderPrefKey, folder);
            }
        }

        private void AddImageInternal(string path)
        {
            _imagePaths.Add(path);

            // Load texture
            Texture2D texture = LoadTexture(path);
            _loadedTextures.Add(texture);

            // Create thumbnail card
            var card = CreateThumbnailCard(texture, _imagePaths.Count - 1);
            _thumbnailCards.Add(card);
            _imagesContainer.Add(card);
        }

        private Texture2D LoadTexture(string path)
        {
            try
            {
                var fileData = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2);
                texture.LoadImage(fileData);
                return texture;
            }
            catch (Exception ex)
            {
                _logger.Error($"[ReferenceImagesPanel] Failed to load image: {ex.Message}");
                return null;
            }
        }

        private VisualElement CreateThumbnailCard(Texture2D texture, int index)
        {
            var card = new VisualElement();
            card.style.width = ThumbnailSize;
            card.style.height = ThumbnailSize;
            card.style.marginRight = 10;
            card.style.marginBottom = 10;
            card.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            card.style.borderTopLeftRadius = 4;
            card.style.borderTopRightRadius = 4;
            card.style.borderBottomLeftRadius = 4;
            card.style.borderBottomRightRadius = 4;
            card.style.overflow = Overflow.Hidden;

            if (texture != null)
            {
                card.style.backgroundImage = new StyleBackground(texture);
                card.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            }
            else
            {
                var errorLabel = new Label("?");
                errorLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                errorLabel.style.fontSize = 24;
                errorLabel.style.color = StyleHelpers.Colors.Error;
                card.Add(errorLabel);
            }

            // Remove button
            var removeBtn = new Button(() => RemoveImage(index));
            removeBtn.text = "×";
            removeBtn.style.position = Position.Absolute;
            removeBtn.style.top = 2;
            removeBtn.style.right = 2;
            removeBtn.style.width = 24;
            removeBtn.style.height = 24;
            removeBtn.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f, 0.9f);
            removeBtn.style.color = Color.white;
            removeBtn.style.fontSize = 18;
            removeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            removeBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            removeBtn.style.paddingLeft = 0;
            removeBtn.style.paddingRight = 0;
            removeBtn.style.paddingTop = 0;
            removeBtn.style.paddingBottom = 0;
            card.Add(removeBtn);

            return card;
        }

        private void RemoveImage(int index)
        {
            if (index < 0 || index >= _imagePaths.Count)
            {
                return;
            }

            // Destroy texture
            if (_loadedTextures[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(_loadedTextures[index]);
            }

            // Remove from lists
            _imagePaths.RemoveAt(index);
            _loadedTextures.RemoveAt(index);

            // Rebuild thumbnails (to update indices)
            RebuildThumbnails();

            UpdateAddButtonState();
            OnChanged?.Invoke();
        }

        private void RebuildThumbnails()
        {
            _imagesContainer.Clear();
            _thumbnailCards.Clear();

            for (int i = 0; i < _imagePaths.Count; i++)
            {
                var card = CreateThumbnailCard(_loadedTextures[i], i);
                _thumbnailCards.Add(card);
                _imagesContainer.Add(card);
            }
        }

        private void UpdateAddButtonState()
        {
            _addButton.SetEnabled(_imagePaths.Count < MaxImages);
        }

        /// <summary>
        /// Clear all images
        /// </summary>
        public void Clear()
        {
            // Destroy all loaded textures
            foreach (var texture in _loadedTextures)
            {
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            _loadedTextures.Clear();
            _imagePaths.Clear();
            _thumbnailCards.Clear();
            _imagesContainer.Clear();

            UpdateAddButtonState();
        }

        /// <summary>
        /// Dispose resources (textures)
        /// </summary>
        public void Dispose()
        {
            Clear();
        }
    }
}
