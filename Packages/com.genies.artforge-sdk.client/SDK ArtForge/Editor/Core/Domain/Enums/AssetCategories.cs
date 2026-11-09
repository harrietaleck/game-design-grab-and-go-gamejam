using System;

namespace Genies.Sdk.ArtForge.Editor.Core.Domain.Enums
{
    /// <summary>
    /// Constants for backend asset categories
    /// </summary>
    internal static class AssetCategories
    {
        public const string Wearable = "Wearable";
        public const string Decor = "Decor";

        /// <summary>
        /// Delimiter for AssetTypeId format: "Category|SpecificType"
        /// </summary>
        public const string Delimiter = "|";

        /// <summary>
        /// Gets all available categories (including hidden ones)
        /// </summary>
        public static string[] GetAll() => new[] { Wearable, Decor };

        public static bool IsDecorAssetTypeId(string assetTypeId)
        {
            if (string.IsNullOrWhiteSpace(assetTypeId))
            {
                return false;
            }

            string[] parts = assetTypeId.Split(new[] { Delimiter }, StringSplitOptions.None);
            if (parts.Length > 0)
            {
                return string.Equals(parts[0], Decor, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(assetTypeId, Decor, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets categories available for UI selection.
        /// </summary>
        public static string[] GetAvailableForUI() => new[] { Wearable, Decor };
    }
}
