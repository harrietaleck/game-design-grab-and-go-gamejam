using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure
{
    /// <summary>
    /// Centralized service for determining feature availability.
    /// Backend-only mode is always enabled.
    /// </summary>
    internal static class FeatureAvailabilityService
    {
        // ========================================
        // NAVIGATION FEATURES
        // ========================================

        /// <summary>
        /// Project tab visibility - Always hidden in backend-only mode
        /// </summary>
        public static bool IsProjectTabAvailable => false;

        /// <summary>
        /// Edit tab visibility - Always hidden in backend-only mode
        /// </summary>
        public static bool IsEditTabAvailable => false;

        /// <summary>
        /// Services tab visibility - Always hidden in backend-only mode
        /// </summary>
        public static bool IsServicesTabAvailable => false;

        /// <summary>
        /// Get default view to open on startup - Always ModelGenerator
        /// </summary>
        public static string GetDefaultView()
        {
            return "ModelGenerator";
        }

        /// <summary>
        /// Get default expanded navigation section - No longer applicable (flat navigation)
        /// </summary>
        public static string GetDefaultExpandedSection()
        {
            return null;
        }

        // ========================================
        // APPROVAL DIALOG FEATURES
        // ========================================

        /// <summary>
        /// Description/prompt field visibility
        /// </summary>
        public static bool IsApprovalDescriptionFieldAvailable => true;

        /// <summary>
        /// Retry button visibility
        /// </summary>
        public static bool IsApprovalRetryButtonAvailable => true;

        // ========================================
        // MODEL GENERATOR FEATURES
        // ========================================

        /// <summary>
        /// Reference images section visibility
        /// </summary>
        public static bool AreReferenceImagesAvailable => true;

        /// <summary>
        /// Check if description field should be visible in Model Generator
        /// Only visible for Wearable and Decor
        /// </summary>
        /// <param name="selectedCategory">Currently selected asset category</param>
        public static bool IsDescriptionFieldAvailable(string selectedCategory)
        {
            // Only show for Wearable and Decor
            return selectedCategory == AssetCategories.Wearable || 
                   selectedCategory == AssetCategories.Decor;
        }

        // ========================================
        // NAVIGATION CONFIG INTEGRATION
        // ========================================

        /// <summary>
        /// Get feature availability list for NavigationConfig
        /// Returns features that should be disabled (all local-only features)
        /// </summary>
        public static System.Collections.Generic.List<Hub.FeatureAvailability> GetNavigationFeatureAvailability()
        {
            var features = new System.Collections.Generic.List<Hub.FeatureAvailability>();

            // All local-only features are disabled
            features.Add(new Hub.FeatureAvailability { FeatureId = "project-setup", IsAvailable = false });
            features.Add(new Hub.FeatureAvailability { FeatureId = "model-editor", IsAvailable = false });

            return features;
        }
    }
}
