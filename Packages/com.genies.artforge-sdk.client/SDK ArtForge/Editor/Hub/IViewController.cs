using UnityEngine.UIElements;

namespace Genies.Sdk.ArtForge.Editor.Core.UI
{
    /// <summary>
    /// Interface for all component view controllers.
    /// Views are loaded into the Hub's content area.
    /// </summary>
    internal interface IViewController
    {
        /// <summary>
        /// Load the view's UI into the provided container.
        /// Called when the view is activated.
        /// </summary>
        /// <param name="container">The container element where the view should be loaded</param>
        void Load(VisualElement container);

        /// <summary>
        /// Unload and cleanup the view.
        /// Called when switching to another view or closing the hub.
        /// </summary>
        void Unload();

        /// <summary>
        /// Called every frame while the view is active.
        /// Optional - implement only if needed for animations, polling, etc.
        /// </summary>
        void Update() { }
    }
}

