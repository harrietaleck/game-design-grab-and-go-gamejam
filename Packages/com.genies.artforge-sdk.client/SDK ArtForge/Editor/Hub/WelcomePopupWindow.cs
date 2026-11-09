using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Genies.Sdk.ArtForge.Editor.Hub
{
    internal sealed class WelcomePopupWindow : EditorWindow
    {
        private const float WindowWidth = 550f;
        private const float WindowHeight = 520f;
        private const string WelcomeImageResourceName = "WelcomeToGAF_SDK";
        private const string DocumentationUrl = "https://docs.genies.com/docs/sdk-art-forge";

        private static bool _isOpen;
        private static Texture2D _welcomeImage;
        private static float _welcomeImageHorizontalScale = 1f;
        private bool _settingsButtonClicked;
        private Button _openSettingsButton;
        private Toggle _showOnProjectOpenToggle;

        internal static void ShowPopupWindow()
        {
            if (_isOpen)
            {
                return;
            }

            _isOpen = true;
            _welcomeImage = Resources.Load<Texture2D>(WelcomeImageResourceName);
            _welcomeImageHorizontalScale = CalculateWelcomeImageHorizontalScale(_welcomeImage);

            var window = CreateInstance<WelcomePopupWindow>();
            window.minSize = new Vector2(WindowWidth, WindowHeight);
            window.maxSize = new Vector2(WindowWidth, WindowHeight);
            window.position = GetCenteredRect(WindowWidth, WindowHeight);
            window.ShowPopup();
            window.Focus();
        }

        private void OnEnable()
        {
            if (_welcomeImage == null)
            {
                _welcomeImage = Resources.Load<Texture2D>(WelcomeImageResourceName);
                _welcomeImageHorizontalScale = CalculateWelcomeImageHorizontalScale(_welcomeImage);
            }

            BuildUi();
        }

        private static Rect GetCenteredRect(float width, float height)
        {
            Rect mainWindowRect = EditorGUIUtility.GetMainWindowPosition();
            float x = mainWindowRect.x + (mainWindowRect.width - width) * 0.5f;
            float y = mainWindowRect.y + (mainWindowRect.height - height) * 0.5f;
            return new Rect(x, y, width, height);
        }

        private static float CalculateWelcomeImageHorizontalScale(Texture2D texture)
        {
            try
            {
                if (texture == null || texture.height <= 0)
                {
                    return 1f;
                }

                string assetPath = AssetDatabase.GetAssetPath(texture);
                if (string.IsNullOrEmpty(assetPath))
                {
                    return 1f;
                }

                if (!(AssetImporter.GetAtPath(assetPath) is TextureImporter importer))
                {
                    return 1f;
                }

                importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
                if (sourceWidth <= 0 || sourceHeight <= 0)
                {
                    return 1f;
                }

                float sourceAspect = sourceWidth / (float)sourceHeight;
                float importedAspect = texture.width / (float)texture.height;
                if (sourceAspect <= 0f || importedAspect <= 0f)
                {
                    return 1f;
                }

                // Counteracts NPOT "To Larger" stretch (e.g., 500x333 source imported as 512x512).
                return Mathf.Clamp(sourceAspect / importedAspect, 0.25f, 4f);
            }
            catch
            {
                return 1f;
            }
        }

        private void BuildUi()
        {
            var root = rootVisualElement;
            root.Clear();
            root.focusable = true;
            root.tabIndex = 0;
            root.style.paddingLeft = 12f;
            root.style.paddingRight = 12f;
            root.style.paddingTop = 12f;
            root.style.paddingBottom = 12f;
            root.style.backgroundColor = new Color(0.06f, 0.08f, 0.11f, 1f);
            root.style.flexDirection = FlexDirection.Column;

            var card = new VisualElement();
            card.style.flexGrow = 1f;
            card.style.paddingLeft = 14f;
            card.style.paddingRight = 14f;
            card.style.paddingTop = 12f;
            card.style.paddingBottom = 12f;
            card.style.backgroundColor = new Color(0.11f, 0.13f, 0.16f, 1f);
            card.style.borderTopLeftRadius = 14f;
            card.style.borderTopRightRadius = 14f;
            card.style.borderBottomLeftRadius = 14f;
            card.style.borderBottomRightRadius = 14f;
            card.style.borderTopWidth = 1f;
            card.style.borderBottomWidth = 1f;
            card.style.borderLeftWidth = 1f;
            card.style.borderRightWidth = 1f;
            card.style.borderTopColor = new Color(0.18f, 0.23f, 0.30f, 1f);
            card.style.borderBottomColor = new Color(0.18f, 0.23f, 0.30f, 1f);
            card.style.borderLeftColor = new Color(0.18f, 0.23f, 0.30f, 1f);
            card.style.borderRightColor = new Color(0.18f, 0.23f, 0.30f, 1f);
            root.Add(card);

            var headerRow = new VisualElement();
            headerRow.style.height = 22f;
            headerRow.style.marginBottom = 6f;
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            card.Add(headerRow);

            var leftSlot = new VisualElement();
            leftSlot.style.width = 86f;
            leftSlot.style.height = 18f;
            leftSlot.style.flexDirection = FlexDirection.Row;
            leftSlot.style.justifyContent = Justify.FlexStart;
            leftSlot.style.alignItems = Align.Center;
            headerRow.Add(leftSlot);

            var closeButton = new Button(OnCloseButtonClicked)
            {
                text = "x"
            };
            closeButton.style.width = 18f;
            closeButton.style.height = 18f;
            closeButton.style.paddingLeft = 0f;
            closeButton.style.paddingRight = 0f;
            closeButton.style.paddingTop = 0f;
            closeButton.style.paddingBottom = 0f;
            closeButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            closeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            closeButton.style.fontSize = 10f;
            closeButton.style.color = new Color(0.97f, 0.99f, 1f, 1f);
            closeButton.style.backgroundColor = new Color(0.20f, 0.74f, 0.83f, 1f);
            closeButton.style.borderTopLeftRadius = 9f;
            closeButton.style.borderTopRightRadius = 9f;
            closeButton.style.borderBottomLeftRadius = 9f;
            closeButton.style.borderBottomRightRadius = 9f;
            closeButton.style.borderTopWidth = 1f;
            closeButton.style.borderBottomWidth = 1f;
            closeButton.style.borderLeftWidth = 1f;
            closeButton.style.borderRightWidth = 1f;
            closeButton.style.borderTopColor = new Color(0.41f, 0.90f, 0.98f, 1f);
            closeButton.style.borderBottomColor = new Color(0.41f, 0.90f, 0.98f, 1f);
            closeButton.style.borderLeftColor = new Color(0.41f, 0.90f, 0.98f, 1f);
            closeButton.style.borderRightColor = new Color(0.41f, 0.90f, 0.98f, 1f);
            closeButton.RegisterCallback<MouseEnterEvent>(_ =>
                closeButton.style.backgroundColor = new Color(0.27f, 0.80f, 0.89f, 1f));
            closeButton.RegisterCallback<MouseLeaveEvent>(_ =>
                closeButton.style.backgroundColor = new Color(0.20f, 0.74f, 0.83f, 1f));
            leftSlot.Add(closeButton);

            var header = new Label("Welcome to Genies Art Forge!");
            header.style.flexGrow = 1f;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.unityTextAlign = TextAnchor.MiddleCenter;
            header.style.color = new Color(0.94f, 0.96f, 0.99f, 1f);
            header.style.whiteSpace = WhiteSpace.Normal;
            headerRow.Add(header);

            var rightSlot = new VisualElement();
            rightSlot.style.width = 86f;
            rightSlot.style.height = 18f;
            rightSlot.style.flexDirection = FlexDirection.Row;
            rightSlot.style.justifyContent = Justify.FlexEnd;
            rightSlot.style.alignItems = Align.Center;
            headerRow.Add(rightSlot);

            var docsHeaderButton = new Button(OpenDocumentation)
            {
                text = "Open Docs"
            };
            docsHeaderButton.style.height = 18f;
            docsHeaderButton.style.minWidth = 78f;
            docsHeaderButton.style.paddingLeft = 10f;
            docsHeaderButton.style.paddingRight = 10f;
            docsHeaderButton.style.fontSize = 10f;
            docsHeaderButton.style.color = new Color(0.97f, 0.99f, 1f, 1f);
            docsHeaderButton.style.backgroundColor = new Color(0.20f, 0.74f, 0.83f, 1f);
            docsHeaderButton.style.borderTopLeftRadius = 9f;
            docsHeaderButton.style.borderTopRightRadius = 9f;
            docsHeaderButton.style.borderBottomLeftRadius = 9f;
            docsHeaderButton.style.borderBottomRightRadius = 9f;
            docsHeaderButton.style.borderTopWidth = 1f;
            docsHeaderButton.style.borderBottomWidth = 1f;
            docsHeaderButton.style.borderLeftWidth = 1f;
            docsHeaderButton.style.borderRightWidth = 1f;
            docsHeaderButton.style.borderTopColor = new Color(0.41f, 0.90f, 0.98f, 1f);
            docsHeaderButton.style.borderBottomColor = new Color(0.41f, 0.90f, 0.98f, 1f);
            docsHeaderButton.style.borderLeftColor = new Color(0.41f, 0.90f, 0.98f, 1f);
            docsHeaderButton.style.borderRightColor = new Color(0.41f, 0.90f, 0.98f, 1f);
            docsHeaderButton.RegisterCallback<MouseEnterEvent>(_ =>
                docsHeaderButton.style.backgroundColor = new Color(0.27f, 0.80f, 0.89f, 1f));
            docsHeaderButton.RegisterCallback<MouseLeaveEvent>(_ =>
                docsHeaderButton.style.backgroundColor = new Color(0.20f, 0.74f, 0.83f, 1f));
            rightSlot.Add(docsHeaderButton);

            var imageContainer = new VisualElement();
            imageContainer.style.width = Length.Percent(100);
            imageContainer.style.alignSelf = Align.Center;
            imageContainer.style.marginBottom = 10f;
            imageContainer.style.borderTopLeftRadius = 12f;
            imageContainer.style.borderTopRightRadius = 12f;
            imageContainer.style.borderBottomLeftRadius = 12f;
            imageContainer.style.borderBottomRightRadius = 12f;
            imageContainer.style.overflow = Overflow.Hidden;
            imageContainer.style.borderTopWidth = 1f;
            imageContainer.style.borderBottomWidth = 1f;
            imageContainer.style.borderLeftWidth = 1f;
            imageContainer.style.borderRightWidth = 1f;
            imageContainer.style.borderTopColor = new Color(0.24f, 0.36f, 0.46f, 1f);
            imageContainer.style.borderBottomColor = new Color(0.24f, 0.36f, 0.46f, 1f);
            imageContainer.style.borderLeftColor = new Color(0.24f, 0.36f, 0.46f, 1f);
            imageContainer.style.borderRightColor = new Color(0.24f, 0.36f, 0.46f, 1f);
            imageContainer.style.backgroundColor = new Color(0.06f, 0.10f, 0.14f, 1f);
            card.Add(imageContainer);

            if (_welcomeImage != null)
            {
                var image = new Image
                {
                    image = _welcomeImage,
                    scaleMode = ScaleMode.ScaleToFit
                };
                image.style.width = Length.Percent(100);
                image.style.height = Length.Percent(100);
                if (!Mathf.Approximately(_welcomeImageHorizontalScale, 1f))
                {
                    image.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0f);
                    image.style.scale = new Scale(new Vector3(_welcomeImageHorizontalScale, 1f, 1f));
                }
                imageContainer.Add(image);
            }
            else
            {
                var missingImageLabel = new Label("Welcome image not found");
                missingImageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                missingImageLabel.style.color = new Color(0.72f, 0.74f, 0.78f, 1f);
                missingImageLabel.style.flexGrow = 1f;
                imageContainer.Add(missingImageLabel);
            }

            var infoBox = new VisualElement();
            infoBox.style.flexDirection = FlexDirection.Row;
            infoBox.style.alignItems = Align.Center;
            infoBox.style.backgroundColor = new Color(0.16f, 0.19f, 0.24f, 1f);
            infoBox.style.borderLeftWidth = 3f;
            infoBox.style.borderLeftColor = new Color(0.25f, 0.82f, 0.90f, 1f);
            infoBox.style.borderTopLeftRadius = 10f;
            infoBox.style.borderTopRightRadius = 10f;
            infoBox.style.borderBottomLeftRadius = 10f;
            infoBox.style.borderBottomRightRadius = 10f;
            infoBox.style.paddingLeft = 10f;
            infoBox.style.paddingRight = 10f;
            infoBox.style.paddingTop = 8f;
            infoBox.style.paddingBottom = 8f;
            infoBox.style.marginBottom = 10f;
            card.Add(infoBox);

            var infoIcon = new Label("!");
            infoIcon.style.width = 18f;
            infoIcon.style.height = 18f;
            infoIcon.style.unityTextAlign = TextAnchor.MiddleCenter;
            infoIcon.style.unityFontStyleAndWeight = FontStyle.Bold;
            infoIcon.style.color = new Color(0.98f, 0.99f, 1f, 1f);
            infoIcon.style.backgroundColor = new Color(0.25f, 0.82f, 0.90f, 1f);
            infoIcon.style.borderTopLeftRadius = 9f;
            infoIcon.style.borderTopRightRadius = 9f;
            infoIcon.style.borderBottomLeftRadius = 9f;
            infoIcon.style.borderBottomRightRadius = 9f;
            infoIcon.style.marginRight = 8f;
            infoBox.Add(infoIcon);

            var infoText = new Label("You have free credits available to start generating right away.");
            infoText.style.color = new Color(0.90f, 0.93f, 0.98f, 1f);
            infoText.style.whiteSpace = WhiteSpace.Normal;
            infoText.style.flexGrow = 1f;
            infoText.style.alignSelf = Align.Center;
            infoText.style.unityTextAlign = TextAnchor.MiddleLeft;
            infoText.style.marginTop = -1f;
            infoBox.Add(infoText);

            _showOnProjectOpenToggle = new Toggle("Show on project open");
            _showOnProjectOpenToggle.style.marginBottom = 10f;
            _showOnProjectOpenToggle.style.color = new Color(0.90f, 0.93f, 0.98f, 1f);
            _showOnProjectOpenToggle.SetValueWithoutNotify(GeniesArtForgeHub.IsWelcomePopupOnProjectOpenEnabled());
            _showOnProjectOpenToggle.RegisterValueChangedCallback(evt =>
                GeniesArtForgeHub.SetWelcomePopupOnProjectOpenEnabled(evt.newValue));
            card.Add(_showOnProjectOpenToggle);

            _openSettingsButton = new Button(OnOpenSettingsClicked)
            {
                text = "Start Creating With Genies Art Forge"
            };
            _openSettingsButton.style.height = 42f;
            _openSettingsButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _openSettingsButton.style.color = new Color(0.97f, 0.99f, 1f, 1f);
            _openSettingsButton.style.backgroundColor = new Color(0.20f, 0.74f, 0.83f, 1f);
            _openSettingsButton.style.borderTopLeftRadius = 10f;
            _openSettingsButton.style.borderTopRightRadius = 10f;
            _openSettingsButton.style.borderBottomLeftRadius = 10f;
            _openSettingsButton.style.borderBottomRightRadius = 10f;
            _openSettingsButton.style.borderTopWidth = 1f;
            _openSettingsButton.style.borderBottomWidth = 1f;
            _openSettingsButton.style.borderLeftWidth = 1f;
            _openSettingsButton.style.borderRightWidth = 1f;
            _openSettingsButton.style.borderTopColor = new Color(0.41f, 0.90f, 0.98f, 1f);
            _openSettingsButton.style.borderBottomColor = new Color(0.41f, 0.90f, 0.98f, 1f);
            _openSettingsButton.style.borderLeftColor = new Color(0.41f, 0.90f, 0.98f, 1f);
            _openSettingsButton.style.borderRightColor = new Color(0.41f, 0.90f, 0.98f, 1f);
            _openSettingsButton.style.marginBottom = 8f;
            _openSettingsButton.RegisterCallback<MouseEnterEvent>(_ =>
                _openSettingsButton.style.backgroundColor = new Color(0.27f, 0.80f, 0.89f, 1f));
            _openSettingsButton.RegisterCallback<MouseLeaveEvent>(_ =>
                _openSettingsButton.style.backgroundColor = new Color(0.20f, 0.74f, 0.83f, 1f));
            card.Add(_openSettingsButton);

            root.RegisterCallback<KeyDownEvent>(OnRootKeyDown);
            root.schedule.Execute(() => root.Focus());
        }

        private void OnOpenSettingsClicked()
        {
            _settingsButtonClicked = true;
            _isOpen = false;
            GeniesArtForgeHub.OpenSettingsFromWelcomePopup();
            Close();
        }

        private void OnCloseButtonClicked()
        {
            _isOpen = false;
            Close();
        }

        private static void OpenDocumentation()
        {
            Application.OpenURL(DocumentationUrl);
        }

        private void OnRootKeyDown(KeyDownEvent evt)
        {
            bool closeShortcut = evt.keyCode == KeyCode.W && (evt.commandKey || evt.ctrlKey);
            if (!closeShortcut)
            {
                return;
            }

            evt.StopPropagation();
            _isOpen = false;
            Close();
        }

        private void OnDestroy()
        {
            _isOpen = false;

            if (_settingsButtonClicked)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            GeniesArtForgeHub.OpenModelGeneratorFromWelcomePopupClose();
        }
    }
}
