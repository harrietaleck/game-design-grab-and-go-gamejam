using UnityEngine;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Processing
{
    /// <summary>
    /// Defines how to process a model based on its source format.
    /// Replaces boolean flags with explicit, readable configuration.
    /// </summary>
    internal class ModelFormatOptions
    {
        /// <summary>
        /// Model format type.
        /// </summary>
        public enum Format
        {
            FBX,
            GLB
        }

        public Format ModelFormat { get; set; }

        /// <summary>
        /// Whether to apply rotation fix (typically -90 X for FBX from Rodin/Hyper3D).
        /// </summary>
        public bool ApplyRotationFix { get; set; }

        /// <summary>
        /// Whether to adjust pivot to base of model.
        /// </summary>
        public bool AdjustPivotToBase { get; set; }

        /// <summary>
        /// Whether materials and textures need to be extracted from the model file.
        /// </summary>
        public bool ExtractMaterialsAndTextures { get; set; }

        /// <summary>
        /// Whether mesh assets need to be extracted and saved separately.
        /// </summary>
        public bool ExtractMeshAssets { get; set; }

        /// <summary>
        /// Whether generated prefabs should receive mesh colliders.
        /// </summary>
        public bool AddMeshColliders { get; set; }

        /// <summary>
        /// Creates options for FBX models (local generation).
        /// </summary>
        public static ModelFormatOptions ForFBX(bool pivotAtBase = true, bool addMeshColliders = false)
        {
            return new ModelFormatOptions
            {
                ModelFormat = Format.FBX,
                ApplyRotationFix = true,
                AdjustPivotToBase = pivotAtBase,
                ExtractMaterialsAndTextures = false,
                ExtractMeshAssets = false,
                AddMeshColliders = addMeshColliders
            };
        }

        /// <summary>
        /// Creates options for GLB models (backend generation).
        /// </summary>
        public static ModelFormatOptions ForGLB(bool addMeshColliders = false)
        {
            return new ModelFormatOptions
            {
                ModelFormat = Format.GLB,
                ApplyRotationFix = false,
                AdjustPivotToBase = false,
                ExtractMaterialsAndTextures = true,
                ExtractMeshAssets = true,
                AddMeshColliders = addMeshColliders
            };
        }

    }
}
