using System;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Common;
using Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects;

namespace Genies.Sdk.ArtForge.Editor.Core.Domain.Entities
{
    /// <summary>
    /// Represents project-level settings for GeniesArtForge.
    /// Contains scene description, art style, and style reference information.
    /// </summary>
    internal class Project : Entity<ProjectId>
    {
        private const int SceneDescriptionMinLength = 10;
        private const int SceneDescriptionMaxLength = 2000;
        private const int ArtStyleMaxLength = 1000;

        public string SceneDescription { get; private set; }
        public string ArtStyle { get; private set; }
        public string StyleReferencePath { get; private set; }

        // Private constructor for EF/serialization
        private Project() { }

        /// <summary>
        /// Factory method to create a new Project.
        /// </summary>
        public static Project Create()
        {
            return new Project
            {
                Id = ProjectId.NewId(),
                SceneDescription = string.Empty,
                ArtStyle = string.Empty,
                StyleReferencePath = string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Updates the project settings with validation.
        /// Use this for manual saves where user explicitly clicks "Save".
        /// </summary>
        public Result UpdateSettings(string sceneDescription, string artStyle)
        {
            if (string.IsNullOrWhiteSpace(sceneDescription))
            {
                return Result.Failure("Scene description is required");
            }

            if (sceneDescription.Length < SceneDescriptionMinLength)
            {
                return Result.Failure("Scene description must be at least 20 characters");
            }

            if (sceneDescription.Length > SceneDescriptionMaxLength)
            {
                return Result.Failure("Scene description must be less than 2000 characters");
            }

            SceneDescription = sceneDescription.Trim();
            ArtStyle = artStyle?.Trim() ?? string.Empty;
            UpdatedAt = DateTime.UtcNow;

            return Result.Success();
        }

        /// <summary>
        /// Updates the project settings WITHOUT validation.
        /// Use this for auto-save to allow partial/in-progress data.
        /// </summary>
        public void UpdateSettingsAutoSave(string sceneDescription, string artStyle)
        {
            if (sceneDescription != null && sceneDescription.Length <= SceneDescriptionMaxLength)
            {
                SceneDescription = sceneDescription.Trim();
            }

            if (artStyle != null && artStyle.Length <= ArtStyleMaxLength)
            {
                ArtStyle = artStyle.Trim();
            }

            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Sets the style reference image path.
        /// </summary>
        public Result SetStyleReference(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                StyleReferencePath = string.Empty;
            }
            else
            {
                // Basic validation - file should exist
                if (!System.IO.File.Exists(imagePath))
                {
                    return Result.Failure("Style reference file does not exist");
                }

                StyleReferencePath = imagePath;
            }

            UpdatedAt = DateTime.UtcNow;
            return Result.Success();
        }
    }
}
