using System;
using Genies.Sdk.ArtForge.Editor.Components.ModelGenerator.Config;
using UnityEngine.UIElements;

namespace Genies.Sdk.ArtForge.Editor.Components.ModelGenerator.UI
{
    /// <summary>
    /// Interface for type selection UI components.
    /// Polymorphic handling of local vs backend type selection.
    /// </summary>
    internal interface ITypeSelectionSection
    {
        /// <summary>
        /// Root visual element
        /// </summary>
        VisualElement Root { get; }

        /// <summary>
        /// Bind to state and config
        /// </summary>
        void Bind(ModelGeneratorState state, BackendConfigResult config);

        /// <summary>
        /// Update state from UI
        /// </summary>
        void UpdateState(ModelGeneratorState state);

        /// <summary>
        /// Get current selection
        /// </summary>
        AssetTypeSelection GetSelection();

        /// <summary>
        /// Raised when selection changes
        /// </summary>
        event Action OnSelectionChanged;

        /// <summary>
        /// Show loading state
        /// </summary>
        void SetLoading(bool loading, string message = null);

        /// <summary>
        /// Show error state
        /// </summary>
        void SetError(string error);

        /// <summary>
        /// Clear error state
        /// </summary>
        void ClearError();
    }
}

