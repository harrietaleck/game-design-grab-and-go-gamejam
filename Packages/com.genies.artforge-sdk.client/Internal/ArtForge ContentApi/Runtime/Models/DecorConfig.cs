using System;

namespace Genies.ArtForge.ContentAPI.Models
{
    /// <summary>
    /// Configuration data for decor creation, including available decor types.
    /// Retrieved from the backend via GetDecorConfigAsync() before creating decor.
    /// </summary>
    [Serializable]
    internal class DecorConfig
    {
        /// <summary>
        /// Supported aspect ratios for decor 2D generation.
        /// Example: ["1:1", "16:9"]
        /// </summary>
        public string[] supportedAspectRatios;

        /// <summary>
        /// Array of available decor types.
        /// Example: [{ type: "CHAIR", name: "Chair" }, { type: "TABLE", name: "Table" }]
        /// </summary>
        public DecorType[] types;

        /// <summary>
        /// Initializes a new instance of the DecorConfig class.
        /// </summary>
        public DecorConfig()
        {
            supportedAspectRatios = new string[0];
            types = new DecorType[0];
        }

        /// <summary>
        /// Initializes a new instance of the DecorConfig class with types.
        /// </summary>
        /// <param name="types">Array of available decor types.</param>
        /// <param name="supportedAspectRatios">Array of supported aspect ratios for 2D decor generation.</param>
        public DecorConfig(DecorType[] types, string[] supportedAspectRatios = null)
        {
            this.supportedAspectRatios = supportedAspectRatios ?? new string[0];
            this.types = types ?? new DecorType[0];
        }
    }

    /// <summary>
    /// Represents a single decor type option.
    /// </summary>
    [Serializable]
    internal class DecorType
    {
        /// <summary>
        /// The type identifier.
        /// </summary>
        public string type;

        /// <summary>
        /// The display name for the type.
        /// </summary>
        public string name;

        /// <summary>
        /// Optional default polycount for this decor type.
        /// </summary>
        public int? polycount;

        /// <summary>
        /// Optional subcategories for this decor type.
        /// </summary>
        public DecorSubcategory[] subcategories;

        /// <summary>
        /// Initializes a new instance of the DecorType class.
        /// </summary>
        public DecorType()
        {
            subcategories = new DecorSubcategory[0];
        }

        /// <summary>
        /// Initializes a new instance of the DecorType class with parameters.
        /// </summary>
        /// <param name="type">The type identifier.</param>
        /// <param name="name">The display name.</param>
        /// <param name="polycount">Optional default polycount for this decor type.</param>
        /// <param name="subcategories">Optional subcategories for this decor type.</param>
        public DecorType(
            string type,
            string name,
            int? polycount = null,
            DecorSubcategory[] subcategories = null)
        {
            this.type = type;
            this.name = name;
            this.polycount = polycount;
            this.subcategories = subcategories ?? new DecorSubcategory[0];
        }
    }

    /// <summary>
    /// Represents a single decor subcategory option.
    /// </summary>
    [Serializable]
    internal class DecorSubcategory
    {
        /// <summary>
        /// The display name for the subcategory.
        /// </summary>
        public string name;

        /// <summary>
        /// Optional default polycount for this subcategory (typically for 3D decor).
        /// </summary>
        public int? polycount;

        /// <summary>
        /// Optional default aspect ratio for this subcategory (typically for 2D decor).
        /// </summary>
        public string defaultAspectRatio;

        /// <summary>
        /// Optional default transparency behavior for this subcategory (typically for 2D decor).
        /// </summary>
        public bool? transparentBackground;

        /// <summary>
        /// Initializes a new instance of the DecorSubcategory class.
        /// </summary>
        public DecorSubcategory()
        {
        }

        /// <summary>
        /// Initializes a new instance of the DecorSubcategory class with parameters.
        /// </summary>
        /// <param name="name">The display name.</param>
        /// <param name="polycount">Optional default polycount.</param>
        /// <param name="defaultAspectRatio">Optional default aspect ratio.</param>
        /// <param name="transparentBackground">Optional transparency default.</param>
        public DecorSubcategory(
            string name,
            int? polycount = null,
            string defaultAspectRatio = null,
            bool? transparentBackground = null)
        {
            this.name = name;
            this.polycount = polycount;
            this.defaultAspectRatio = defaultAspectRatio;
            this.transparentBackground = transparentBackground;
        }
    }
}
