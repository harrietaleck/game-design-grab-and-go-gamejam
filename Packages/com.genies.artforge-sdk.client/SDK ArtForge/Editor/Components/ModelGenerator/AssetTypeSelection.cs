using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;

namespace Genies.Sdk.ArtForge.Editor.Components.ModelGenerator
{
    /// <summary>
    /// Base class for asset type selection (Backend only)
    /// </summary>
    internal abstract class AssetTypeSelection
    {
        public string Category { get; set; } // "Wearable", "Decor"
        
        /// <summary>
        /// Encode to string format for Job.AssetTypeId
        /// </summary>
        public abstract string Encode();
    }

    /// <summary>
    /// Wearable asset type selection
    /// </summary>
    internal sealed class WearableTypeSelection : AssetTypeSelection
    {
        public string SpecificType { get; set; } // e.g., "Helmet", "Shirt"

        public WearableTypeSelection(string specificType)
        {
            Category = AssetCategories.Wearable;
            SpecificType = specificType;
        }

        public override string Encode()
        {
            return $"{Category}{AssetCategories.Delimiter}{SpecificType}";
        }
    }

    /// <summary>
    /// Decor asset type selection
    /// </summary>
    internal sealed class DecorTypeSelection : AssetTypeSelection
    {
        public string SpecificType { get; set; }

        public DecorTypeSelection(string specificType = "")
        {
            Category = AssetCategories.Decor;
            SpecificType = specificType ?? string.Empty;
        }

        public override string Encode()
        {
            return $"{Category}{AssetCategories.Delimiter}{SpecificType}";
        }
    }

    /// <summary>
    /// Generic asset type selection for categories beyond wearable/decor.
    /// </summary>
    internal sealed class GenericTypeSelection : AssetTypeSelection
    {
        public string SpecificType { get; set; }

        public GenericTypeSelection(string category, string specificType)
        {
            Category = category ?? string.Empty;
            SpecificType = specificType ?? string.Empty;
        }

        public override string Encode()
        {
            return $"{Category}{AssetCategories.Delimiter}{SpecificType}";
        }
    }

}
