namespace Genies.Sdk.ArtForge.Editor.Core.Domain.Enums
{
    /// <summary>
    /// Type of generation job
    /// </summary>
    internal enum JobType
    {
        /// <summary>
        /// Generate a new 3D model from description + images
        /// </summary>
        ModelGeneration,

        /// <summary>
        /// Edit an existing 3D model
        /// </summary>
        ModelEdit,

        /// <summary>
        /// Generate texture from description
        /// </summary>
        TextureGeneration,

        /// <summary>
        /// Generate material maps (normal, metallic, etc)
        /// </summary>
        MaterialGeneration,

        /// <summary>
        /// Generate style reference images for project
        /// </summary>
        StyleReferenceGeneration
    }
}


