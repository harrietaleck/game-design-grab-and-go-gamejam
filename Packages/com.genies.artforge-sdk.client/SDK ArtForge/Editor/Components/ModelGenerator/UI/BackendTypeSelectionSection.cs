using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Genies.Sdk.ArtForge.Editor.Components.ModelGenerator.Config;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using Genies.Sdk.ArtForge.Editor.Core.UI.Helpers;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Genies.Sdk.ArtForge.Editor.Components.ModelGenerator.UI
{
    /// <summary>
    /// Type selection UI for backend mode.
    /// Handles category selection and category-scoped specific type persistence.
    /// </summary>
    internal class BackendTypeSelectionSection : ITypeSelectionSection
    {
        private const string PrefKeyBackendSpecificType = "GAF_ModelGenerator_BackendSpecificType";
        private const string PrefKeyBackendSpecificTypeDisplayName = "GAF_ModelGenerator_BackendSpecificTypeDisplayName";
        private const string PrefKeyBackendSubcategory = "GAF_ModelGenerator_BackendSubcategory";

        // Session-level fallback when switching tabs/views in the same editor session.
        private static readonly Dictionary<string, string> s_LastSpecificTypeByCategory =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> s_LastSpecificTypeDisplayByCategory =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> s_LastSubcategoryByDecorType =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private VisualElement _root;
        private Label _loadingLabel;
        private Label _errorLabel;
        private Button _retryButton;
        private DropdownField _categoryDropdown;
        private DropdownField _specificTypeDropdown;
        private DropdownField _subcategoryDropdown;

        private BackendConfigResult _config;
        private string _currentCategory;
        private bool _isSynchronizingUi;

        public VisualElement Root => _root;
        public event Action OnSelectionChanged;
        public event Action OnRetryRequested;

        public BackendTypeSelectionSection()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            _root = new VisualElement();

            var hint = new Label("Select the category of asset to generate");
            hint.style.fontSize = 11;
            hint.style.color = StyleHelpers.Colors.TextHint;
            hint.style.marginBottom = 8;
            _root.Add(hint);

            // Loading indicator
            _loadingLabel = new Label("Loading backend configuration...");
            _loadingLabel.style.fontSize = 12;
            _loadingLabel.style.color = StyleHelpers.Colors.TextSecondary;
            _loadingLabel.style.marginBottom = 10;
            _loadingLabel.style.display = DisplayStyle.None;
            _root.Add(_loadingLabel);

            // Error label
            _errorLabel = new Label("");
            _errorLabel.style.fontSize = 12;
            _errorLabel.style.color = StyleHelpers.Colors.Error;
            _errorLabel.style.marginBottom = 10;
            _errorLabel.style.whiteSpace = WhiteSpace.Normal;
            _errorLabel.style.flexShrink = 1;
            _errorLabel.style.maxWidth = Length.Percent(100);
            _errorLabel.style.alignSelf = Align.Stretch;
            _errorLabel.style.display = DisplayStyle.None;
            _root.Add(_errorLabel);

            // Retry button
            _retryButton = new Button(() => OnRetryRequested?.Invoke());
            _retryButton.text = "Retry Loading Config";
            StyleHelpers.ApplySecondaryButtonStyle(_retryButton);
            _retryButton.style.display = DisplayStyle.None;
            _retryButton.style.marginBottom = 10;
            _root.Add(_retryButton);

            // Category dropdown
            var categories = new List<string>(AssetCategories.GetAvailableForUI());
            _categoryDropdown = new DropdownField("Asset Category", categories, 0);
            _categoryDropdown.style.marginBottom = 10;
            _categoryDropdown.style.display = DisplayStyle.None;
            _categoryDropdown.RegisterValueChangedCallback(evt =>
            {
                if (_isSynchronizingUi)
                {
                    return;
                }

                _currentCategory = evt.newValue;
                UpdateSpecificTypeDropdown();
                OnSelectionChanged?.Invoke();
            });
            _root.Add(_categoryDropdown);

            // Specific type dropdown
            _specificTypeDropdown = new DropdownField("Specific Type", new List<string>(), 0);
            _specificTypeDropdown.style.marginBottom = 10;
            _specificTypeDropdown.style.display = DisplayStyle.None;
            _specificTypeDropdown.RegisterValueChangedCallback(evt =>
            {
                if (_isSynchronizingUi)
                {
                    return;
                }

                if (HasSpecificTypeChoices(_currentCategory))
                {
                    PersistSpecificTypeSelection(_currentCategory, evt.newValue ?? "");
                }

                UpdateSubcategoryDropdown();
                OnSelectionChanged?.Invoke();
            });
            _root.Add(_specificTypeDropdown);

            // Subcategory dropdown (decor only)
            _subcategoryDropdown = new DropdownField("Subcategory", new List<string>(), 0);
            _subcategoryDropdown.style.marginBottom = 10;
            _subcategoryDropdown.style.display = DisplayStyle.None;
            _subcategoryDropdown.RegisterValueChangedCallback(evt =>
            {
                if (_isSynchronizingUi)
                {
                    return;
                }

                PersistSubcategorySelection(GetCurrentDecorTypeStoredValue(), evt.newValue ?? "");
                OnSelectionChanged?.Invoke();
            });
            _root.Add(_subcategoryDropdown);
        }

        public void Bind(ModelGeneratorState state, BackendConfigResult config)
        {
            _config = config;

            if (config != null && config.IsSuccess)
            {
                _isSynchronizingUi = true;
                try
                {
                    _categoryDropdown.SetValueWithoutNotify(state.BackendCategory);
                    _currentCategory = _categoryDropdown.value;
                    UpdateSpecificTypeDropdown(GetPreferredStoredTypeForCategory(_currentCategory, state.BackendSpecificType));
                    UpdateSubcategoryDropdown(state.BackendSubcategory);
                    state.BackendCategory = _categoryDropdown.value;
                    state.BackendSpecificType = HasSpecificTypeChoices(_currentCategory)
                        ? GetStoredValueForCategory(_currentCategory, _specificTypeDropdown.value)
                        : "";
                    state.BackendSubcategory = GetSelectedSubcategory();
                }
                finally
                {
                    _isSynchronizingUi = false;
                }

                _categoryDropdown.style.display = DisplayStyle.Flex;
            }
        }

        public void UpdateState(ModelGeneratorState state)
        {
            state.BackendCategory = _categoryDropdown.value;

            if (HasSpecificTypeChoices(_currentCategory))
            {
                state.BackendSpecificType = GetStoredValueForCategory(_currentCategory, _specificTypeDropdown.value);
                PersistSpecificTypeSelection(_currentCategory, _specificTypeDropdown.value);
            }
            else
            {
                state.BackendSpecificType = "";
            }

            state.BackendSubcategory = GetSelectedSubcategory();
            PersistSubcategorySelection(GetCurrentDecorTypeStoredValue(), state.BackendSubcategory);
        }

        public AssetTypeSelection GetSelection()
        {
            if (_currentCategory == AssetCategories.Wearable)
            {
                return new WearableTypeSelection(GetWearableTypeFromName(_specificTypeDropdown.value));
            }
            else if (_currentCategory == AssetCategories.Decor)
            {
                return new DecorTypeSelection(GetDecorTypeFromName(_specificTypeDropdown.value));
            }

            string specificType = HasSpecificTypeChoices(_currentCategory)
                ? GetStoredValueForCategory(_currentCategory, _specificTypeDropdown.value)
                : string.Empty;
            return new GenericTypeSelection(_currentCategory, specificType);
        }

        private void UpdateSpecificTypeDropdown(string preferredStoredType = null)
        {
            if (_config == null || !_config.IsSuccess)
            {
                return;
            }

            bool previousSyncState = _isSynchronizingUi;
            _isSynchronizingUi = true;
            try
            {
                _specificTypeDropdown.choices.Clear();

                if (HasSpecificTypeChoices(_currentCategory))
                {
                    var choices = GetSpecificTypeDisplayChoicesForCategory(_currentCategory);
                    _specificTypeDropdown.choices = choices;
                    _specificTypeDropdown.label = GetSpecificTypeLabelForCategory(_currentCategory);
                    _specificTypeDropdown.style.display = DisplayStyle.Flex;

                    if (!TrySetSpecificTypeForCategory(_currentCategory, preferredStoredType))
                    {
                        string fallbackStoredType = GetPreferredStoredTypeForCategory(_currentCategory, null);
                        if (!TrySetSpecificTypeForCategory(_currentCategory, fallbackStoredType))
                        {
                            _specificTypeDropdown.SetValueWithoutNotify(choices[0]);
                        }
                    }
                }
                else
                {
                    _specificTypeDropdown.style.display = DisplayStyle.None;
                }
            }
            finally
            {
                _isSynchronizingUi = previousSyncState;
            }

            UpdateSubcategoryDropdown();
        }

        private string GetWearableTypeFromName(string displayName)
        {
            if (_config?.WearableConfig != null)
            {
                foreach (var type in _config.WearableConfig.types)
                {
                    if (string.Equals(type.name, displayName, StringComparison.OrdinalIgnoreCase))
                    {
                        return type.type ?? type.name;
                    }
                }
            }
            return displayName;
        }

        private string GetWearableDisplayNameFromStoredType(string storedType)
        {
            if (string.IsNullOrEmpty(storedType))
            {
                return storedType;
            }

            if (_config?.WearableConfig != null)
            {
                foreach (var type in _config.WearableConfig.types)
                {
                    if (string.Equals(type.type, storedType, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(type.name, storedType, StringComparison.OrdinalIgnoreCase))
                    {
                        return type.name;
                    }
                }
            }

            return storedType;
        }

        private string GetDecorTypeFromName(string displayName)
        {
            if (_config?.DecorConfig != null)
            {
                foreach (var type in _config.DecorConfig.types)
                {
                    if (string.Equals(type.name, displayName, StringComparison.OrdinalIgnoreCase))
                    {
                        return type.type ?? type.name;
                    }
                }
            }
            return displayName;
        }

        private string GetDecorDisplayNameFromStoredType(string storedType)
        {
            if (string.IsNullOrEmpty(storedType))
            {
                return storedType;
            }

            if (_config?.DecorConfig != null)
            {
                foreach (var type in _config.DecorConfig.types)
                {
                    if (string.Equals(type.type, storedType, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(type.name, storedType, StringComparison.OrdinalIgnoreCase))
                    {
                        return type.name;
                    }
                }
            }

            return storedType;
        }

        private bool TrySetSpecificTypeForCategory(string category, string storedType)
        {
            string specificTypeDisplayName = GetDisplayNameForCategory(category, storedType);
            if (string.IsNullOrWhiteSpace(specificTypeDisplayName))
            {
                return false;
            }

            string matchingChoice = _specificTypeDropdown.choices
                .FirstOrDefault(choice => string.Equals(choice, specificTypeDisplayName, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(matchingChoice))
            {
                return false;
            }

            _specificTypeDropdown.SetValueWithoutNotify(matchingChoice);
            return true;
        }

        private void PersistSpecificTypeSelection(string category, string displayName)
        {
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(displayName))
            {
                return;
            }

            string storedValue = GetStoredValueForCategory(category, displayName) ?? "";
            string normalizedDisplayName = displayName ?? "";

            s_LastSpecificTypeByCategory[category] = storedValue;
            s_LastSpecificTypeDisplayByCategory[category] = normalizedDisplayName;

            EditorPrefs.SetString(GetSpecificTypeValuePrefKey(category), storedValue);
            EditorPrefs.SetString(GetSpecificTypeDisplayNamePrefKey(category), normalizedDisplayName);

            // Backward compatibility for pre-category-specific storage.
            if (string.Equals(category, AssetCategories.Wearable, StringComparison.OrdinalIgnoreCase))
            {
                EditorPrefs.SetString(PrefKeyBackendSpecificType, storedValue);
                EditorPrefs.SetString(PrefKeyBackendSpecificTypeDisplayName, normalizedDisplayName);
            }
        }

        private string GetPreferredStoredTypeForCategory(string category, string preferred)
        {
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                return preferred;
            }

            if (s_LastSpecificTypeByCategory.TryGetValue(category, out string sessionValue) &&
                !string.IsNullOrWhiteSpace(sessionValue))
            {
                return sessionValue;
            }

            string prefValue = EditorPrefs.GetString(GetSpecificTypeValuePrefKey(category), "");
            if (!string.IsNullOrWhiteSpace(prefValue))
            {
                return prefValue;
            }

            if (s_LastSpecificTypeDisplayByCategory.TryGetValue(category, out string sessionDisplay) &&
                !string.IsNullOrWhiteSpace(sessionDisplay))
            {
                return sessionDisplay;
            }

            string prefDisplay = EditorPrefs.GetString(GetSpecificTypeDisplayNamePrefKey(category), "");
            if (!string.IsNullOrWhiteSpace(prefDisplay))
            {
                return prefDisplay;
            }

            // Backward compatibility for historical wearable-only key.
            if (string.Equals(category, AssetCategories.Wearable, StringComparison.OrdinalIgnoreCase))
            {
                string legacyValue = EditorPrefs.GetString(PrefKeyBackendSpecificType, "");
                if (!string.IsNullOrWhiteSpace(legacyValue))
                {
                    return legacyValue;
                }

                string legacyDisplay = EditorPrefs.GetString(PrefKeyBackendSpecificTypeDisplayName, "");
                if (!string.IsNullOrWhiteSpace(legacyDisplay))
                {
                    return legacyDisplay;
                }
            }

            return "";
        }

        private List<string> GetSpecificTypeDisplayChoicesForCategory(string category)
        {
            if (string.Equals(category, AssetCategories.Wearable, StringComparison.OrdinalIgnoreCase) &&
                _config?.WearableConfig != null &&
                _config.WearableConfig.types.Length > 0)
            {
                return _config.WearableConfig.types
                    .Select(t => t.name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();
            }

            if (string.Equals(category, AssetCategories.Decor, StringComparison.OrdinalIgnoreCase) &&
                _config?.DecorConfig != null &&
                _config.DecorConfig.types.Length > 0)
            {
                return _config.DecorConfig.types
                    .Select(t => t.name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();
            }

            return new List<string>();
        }

        private bool HasSpecificTypeChoices(string category)
        {
            return GetSpecificTypeDisplayChoicesForCategory(category).Count > 0;
        }

        private string GetDisplayNameForCategory(string category, string storedType)
        {
            if (string.Equals(category, AssetCategories.Wearable, StringComparison.OrdinalIgnoreCase))
            {
                return GetWearableDisplayNameFromStoredType(storedType);
            }
            if (string.Equals(category, AssetCategories.Decor, StringComparison.OrdinalIgnoreCase))
            {
                return GetDecorDisplayNameFromStoredType(storedType);
            }

            return storedType;
        }

        private string GetStoredValueForCategory(string category, string displayName)
        {
            if (string.Equals(category, AssetCategories.Wearable, StringComparison.OrdinalIgnoreCase))
            {
                return GetWearableTypeFromName(displayName);
            }
            if (string.Equals(category, AssetCategories.Decor, StringComparison.OrdinalIgnoreCase))
            {
                return GetDecorTypeFromName(displayName);
            }

            return displayName;
        }

        private string GetSpecificTypeLabelForCategory(string category)
        {
            if (string.Equals(category, AssetCategories.Wearable, StringComparison.OrdinalIgnoreCase))
            {
                return "Wearable Type";
            }
            if (string.Equals(category, AssetCategories.Decor, StringComparison.OrdinalIgnoreCase))
            {
                return "Decor Type";
            }

            return "Specific Type";
        }

        private void UpdateSubcategoryDropdown(string preferredSubcategory = null)
        {
            if (_subcategoryDropdown == null)
            {
                return;
            }

            bool shouldShow = string.Equals(_currentCategory, AssetCategories.Decor, StringComparison.OrdinalIgnoreCase);
            var choices = shouldShow ? GetSubcategoryChoicesForSelectedDecorType() : new List<string>();
            string decorTypeStoredValue = GetCurrentDecorTypeStoredValue();
            if (!shouldShow || choices.Count == 0)
            {
                _subcategoryDropdown.style.display = DisplayStyle.None;
                return;
            }

            bool previousSyncState = _isSynchronizingUi;
            _isSynchronizingUi = true;
            try
            {
                _subcategoryDropdown.label = "Subcategory";
                _subcategoryDropdown.choices = choices;
                _subcategoryDropdown.style.display = DisplayStyle.Flex;

                string target = null;
                if (!string.IsNullOrWhiteSpace(preferredSubcategory) &&
                    choices.Any(choice => string.Equals(choice, preferredSubcategory, StringComparison.OrdinalIgnoreCase)))
                {
                    target = choices.First(choice => string.Equals(choice, preferredSubcategory, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    string persisted = GetPreferredSubcategoryForDecorType(decorTypeStoredValue);
                    if (!string.IsNullOrWhiteSpace(persisted) &&
                        choices.Any(choice => string.Equals(choice, persisted, StringComparison.OrdinalIgnoreCase)))
                    {
                        target = choices.First(choice => string.Equals(choice, persisted, StringComparison.OrdinalIgnoreCase));
                    }
                }

                if (target == null &&
                    !string.IsNullOrWhiteSpace(_subcategoryDropdown.value) &&
                    choices.Any(choice => string.Equals(choice, _subcategoryDropdown.value, StringComparison.OrdinalIgnoreCase)))
                {
                    target = choices.First(choice => string.Equals(choice, _subcategoryDropdown.value, StringComparison.OrdinalIgnoreCase));
                }

                string resolved = target ?? choices[0];
                _subcategoryDropdown.SetValueWithoutNotify(resolved);
                PersistSubcategorySelection(decorTypeStoredValue, resolved);
            }
            finally
            {
                _isSynchronizingUi = previousSyncState;
            }
        }

        private List<string> GetSubcategoryChoicesForSelectedDecorType()
        {
            var result = new List<string>();
            if (!string.Equals(_currentCategory, AssetCategories.Decor, StringComparison.OrdinalIgnoreCase) ||
                _config?.DecorConfig?.types == null ||
                _config.DecorConfig.types.Length == 0)
            {
                return result;
            }

            string selectedTypeValue = GetStoredValueForCategory(_currentCategory, _specificTypeDropdown.value);
            var decorType = _config.DecorConfig.types.FirstOrDefault(type =>
                type != null &&
                ((!string.IsNullOrWhiteSpace(type.type) &&
                  string.Equals(type.type, selectedTypeValue, StringComparison.OrdinalIgnoreCase)) ||
                 (!string.IsNullOrWhiteSpace(type.name) &&
                  string.Equals(type.name, _specificTypeDropdown.value, StringComparison.OrdinalIgnoreCase))));
            if (decorType?.subcategories == null || decorType.subcategories.Length == 0)
            {
                return result;
            }

            foreach (var subcategory in decorType.subcategories)
            {
                string name = subcategory?.name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (!result.Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(name);
                }
            }

            return result;
        }

        private string GetSelectedSubcategory()
        {
            if (_subcategoryDropdown == null ||
                _subcategoryDropdown.style.display == DisplayStyle.None ||
                string.IsNullOrWhiteSpace(_subcategoryDropdown.value))
            {
                return string.Empty;
            }

            return _subcategoryDropdown.value;
        }

        private string GetCurrentDecorTypeStoredValue()
        {
            if (!string.Equals(_currentCategory, AssetCategories.Decor, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return GetStoredValueForCategory(_currentCategory, _specificTypeDropdown?.value);
        }

        private static string GetPreferredSubcategoryForDecorType(string decorTypeStoredValue)
        {
            if (string.IsNullOrWhiteSpace(decorTypeStoredValue))
            {
                return string.Empty;
            }

            if (s_LastSubcategoryByDecorType.TryGetValue(decorTypeStoredValue, out string sessionValue) &&
                !string.IsNullOrWhiteSpace(sessionValue))
            {
                return sessionValue;
            }

            return EditorPrefs.GetString(GetSubcategoryPrefKey(decorTypeStoredValue), "");
        }

        private static void PersistSubcategorySelection(string decorTypeStoredValue, string subcategory)
        {
            if (string.IsNullOrWhiteSpace(decorTypeStoredValue) || string.IsNullOrWhiteSpace(subcategory))
            {
                return;
            }

            string normalized = subcategory.Trim();
            s_LastSubcategoryByDecorType[decorTypeStoredValue] = normalized;
            EditorPrefs.SetString(GetSubcategoryPrefKey(decorTypeStoredValue), normalized);
        }

        private static string GetSubcategoryPrefKey(string decorTypeStoredValue)
        {
            return $"{PrefKeyBackendSubcategory}_{NormalizeCategoryKey(decorTypeStoredValue)}";
        }

        private static string GetSpecificTypeValuePrefKey(string category)
        {
            return $"{PrefKeyBackendSpecificType}_{NormalizeCategoryKey(category)}";
        }

        private static string GetSpecificTypeDisplayNamePrefKey(string category)
        {
            return $"{PrefKeyBackendSpecificTypeDisplayName}_{NormalizeCategoryKey(category)}";
        }

        private static string NormalizeCategoryKey(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return "unknown";
            }

            var builder = new StringBuilder();
            foreach (char c in category.Trim())
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    builder.Append('_');
                }
            }

            return builder.Length > 0 ? builder.ToString() : "unknown";
        }

        public void SetLoading(bool loading, string message = null)
        {
            if (loading)
            {
                _loadingLabel.text = message ?? "Loading backend configuration...";
                _loadingLabel.style.color = StyleHelpers.Colors.TextSecondary;
                _loadingLabel.style.display = DisplayStyle.Flex;
                _categoryDropdown.style.display = DisplayStyle.None;
                _specificTypeDropdown.style.display = DisplayStyle.None;
                _subcategoryDropdown.style.display = DisplayStyle.None;
                _errorLabel.style.display = DisplayStyle.None;
                _retryButton.style.display = DisplayStyle.None;
            }
            else
            {
                _loadingLabel.style.display = DisplayStyle.None;
            }
        }

        public void SetError(string error)
        {
            _errorLabel.text = error;
            _errorLabel.style.display = DisplayStyle.Flex;
            _retryButton.style.display = DisplayStyle.Flex;
            _loadingLabel.style.display = DisplayStyle.None;
            _categoryDropdown.style.display = DisplayStyle.None;
            _specificTypeDropdown.style.display = DisplayStyle.None;
            _subcategoryDropdown.style.display = DisplayStyle.None;
        }

        public void ClearError()
        {
            _errorLabel.style.display = DisplayStyle.None;
            _retryButton.style.display = DisplayStyle.None;
        }
    }
}
