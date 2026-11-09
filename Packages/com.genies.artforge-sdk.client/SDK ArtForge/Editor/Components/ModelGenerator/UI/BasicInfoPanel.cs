using System;
using UnityEngine.UIElements;
using Genies.Sdk.ArtForge.Editor.Core.UI.Helpers;
using UnityEngine;

namespace Genies.Sdk.ArtForge.Editor.Components.ModelGenerator.UI
{
    /// <summary>
    /// Panel for basic asset information (name and description).
    /// </summary>
    internal sealed class BasicInfoPanel
    {
        private const int MinDescriptionLines = 2;
        private const int MaxDescriptionLines = 6;
        private const float ApproxCharacterWidth = 8.5f;
        private const float DescriptionLineHeight = 16f;
        private const float DescriptionFieldVerticalPadding = 12f;
        private const int FallbackCharsPerLine = 100;
        private const int MinimumCharsPerLine = 40;

        private VisualElement _root;
        private TextField _nameField;
        private TextField _descriptionField;
        private Label _descriptionLabel;
        private Label _descriptionHint;
        private float _lastDescriptionHeight = -1f;

        public VisualElement Root => _root;
        public TextField NameField => _nameField;
        public TextField DescriptionField => _descriptionField;

        /// <summary>
        /// Raised when name or description changes
        /// </summary>
        public event Action OnChanged;

        public BasicInfoPanel()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            _root = StyleHelpers.CreateSection("Basic Information");

            // Description field
            _descriptionLabel = new Label("Description *");
            _descriptionLabel.style.fontSize = 12;
            _descriptionLabel.style.marginBottom = 5;
            _descriptionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _root.Add(_descriptionLabel);

            _descriptionHint = new Label("Describe what you want to generate (5-2000 characters)");
            _descriptionHint.style.fontSize = 11;
            _descriptionHint.style.color = StyleHelpers.Colors.TextHint;
            _descriptionHint.style.marginBottom = 8;
            _root.Add(_descriptionHint);

            _descriptionField = new TextField();
            _descriptionField.multiline = true;
            _descriptionField.style.flexGrow = 0;
            _descriptionField.style.flexShrink = 0;
            _descriptionField.style.minHeight = 0;
            _descriptionField.style.maxHeight = (MaxDescriptionLines * DescriptionLineHeight) + DescriptionFieldVerticalPadding;
            _descriptionField.style.whiteSpace = WhiteSpace.Normal;
            _descriptionField.style.marginBottom = 10;
            _descriptionField.RegisterValueChangedCallback(evt =>
            {
                UpdateDescriptionFieldHeight(evt.newValue);
                OnChanged?.Invoke();
            });
            _descriptionField.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                UpdateDescriptionFieldHeight(_descriptionField.value);
            });
            _root.Add(_descriptionField);
            UpdateDescriptionFieldHeight(_descriptionField.value);

            // Name field
            var nameLabel = new Label("Asset Name *");
            nameLabel.style.fontSize = 12;
            nameLabel.style.marginBottom = 5;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _root.Add(nameLabel);

            var nameHint = new Label("Unique name for this asset (2-30 characters, alphanumeric)");
            nameHint.style.fontSize = 11;
            nameHint.style.color = StyleHelpers.Colors.TextHint;
            nameHint.style.marginBottom = 8;
            _root.Add(nameHint);

            _nameField = new TextField();
            _nameField.RegisterValueChangedCallback(evt => OnChanged?.Invoke());
            _root.Add(_nameField);
        }

        /// <summary>
        /// Bind to state (set initial values without triggering events)
        /// </summary>
        public void Bind(ModelGeneratorState state)
        {
            _nameField.SetValueWithoutNotify(state.AssetName);
            _descriptionField.SetValueWithoutNotify(state.Description);
            UpdateDescriptionFieldHeight(state.Description);
        }

        /// <summary>
        /// Update state from UI
        /// </summary>
        public void UpdateState(ModelGeneratorState state)
        {
            state.AssetName = _nameField.value?.Trim() ?? "";
            state.Description = _descriptionField.value?.Trim() ?? "";
        }

        /// <summary>
        /// Show or hide description field based on feature availability
        /// </summary>
        public void SetDescriptionVisible(bool visible)
        {
            var display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _descriptionLabel.style.display = display;
            _descriptionHint.style.display = display;
            _descriptionField.style.display = display;
            if (visible)
            {
                UpdateDescriptionFieldHeight(_descriptionField.value);
            }
        }

        /// <summary>
        /// Clear form
        /// </summary>
        public void Clear()
        {
            _nameField.value = "";
            _descriptionField.value = "";
            UpdateDescriptionFieldHeight(_descriptionField.value);
        }

        private void UpdateDescriptionFieldHeight(string value)
        {
            if (_descriptionField == null)
            {
                return;
            }

            float fieldWidth = _descriptionField.resolvedStyle.width;
            int charsPerLine = FallbackCharsPerLine;
            if (!float.IsNaN(fieldWidth) && fieldWidth > 0f)
            {
                charsPerLine = Mathf.Max(MinimumCharsPerLine, Mathf.FloorToInt(fieldWidth / ApproxCharacterWidth));
            }

            int wrappedLines = EstimateWrappedLineCount(value, charsPerLine);
            int visibleLines = Mathf.Clamp(wrappedLines, MinDescriptionLines, MaxDescriptionLines);
            float targetHeight = (visibleLines * DescriptionLineHeight) + DescriptionFieldVerticalPadding;

            if (Mathf.Abs(_lastDescriptionHeight - targetHeight) < 0.5f)
            {
                return;
            }

            _lastDescriptionHeight = targetHeight;
            _descriptionField.style.height = targetHeight;
            _descriptionField.style.minHeight = targetHeight;
        }

        private static int EstimateWrappedLineCount(string value, int charsPerLine)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 1;
            }

            int lineCount = 0;
            string[] explicitLines = value.Replace("\r", string.Empty).Split('\n');
            foreach (var explicitLine in explicitLines)
            {
                int length = explicitLine.Length;
                lineCount += Mathf.Max(1, Mathf.CeilToInt(length / (float)charsPerLine));
            }

            return Mathf.Max(1, lineCount);
        }
    }
}
