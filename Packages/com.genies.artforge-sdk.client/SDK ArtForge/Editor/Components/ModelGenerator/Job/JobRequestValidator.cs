using System.IO;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Common;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;

namespace Genies.Sdk.ArtForge.Editor.Components.ModelGenerator.Job
{
    /// <summary>
    /// Validates job requests before creation.
    /// Centralizes all validation logic.
    /// </summary>
    internal sealed class JobRequestValidator
    {
        private const int MaxReferenceImages = 4;
        private const long MaxImageFileSizeBytes = 10 * 1024 * 1024; // 10MB
        private const int DescriptionMinLength = 5;
        private const int DescriptionMaxLength = 2000;
        private const string PngExtension = ".png";
        private const string JpgExtension = ".jpg";
        private const string JpegExtension = ".jpeg";

        /// <summary>
        /// Validate the current state for job creation
        /// </summary>
        public Result Validate(ModelGeneratorState state)
        {
            if (state == null)
            {
                return Result.Failure("State cannot be null");
            }

            // Validate asset name using domain value object
            var assetNameResult = AssetName.Create(state.AssetName);
            if (!assetNameResult.IsSuccess)
            {
                return Result.Failure(assetNameResult.Error);
            }

            // Validate description (only if required by feature availability)
            if (FeatureAvailabilityService.IsDescriptionFieldAvailable(state.BackendCategory))
            {
                var descResult = ValidateDescription(state.Description);
                if (!descResult.IsSuccess)
                {
                    return descResult;
                }
            }

            // Validate reference images (only if feature is available)
            if (FeatureAvailabilityService.AreReferenceImagesAvailable)
            {
                var imagesResult = ValidateReferenceImages(state.ReferenceImages);
                if (!imagesResult.IsSuccess)
                {
                    return imagesResult;
                }
            }

            var backendResult = ValidateBackendSelection(state);
            if (!backendResult.IsSuccess)
            {
                return backendResult;
            }

            return Result.Success();
        }

        private Result ValidateDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return Result.Failure("Description is required");
            }

            var trimmed = description.Trim();
            if (trimmed.Length < DescriptionMinLength)
            {
                return Result.Failure("Description must be at least 5 characters");
            }

            if (trimmed.Length > DescriptionMaxLength)
            {
                return Result.Failure("Description must be less than 2000 characters");
            }

            return Result.Success();
        }

        private Result ValidateReferenceImages(System.Collections.Generic.List<string> imagePaths)
        {
            if (imagePaths == null)
            {
                return Result.Success(); // Optional
            }

            if (imagePaths.Count > MaxReferenceImages)
            {
                return Result.Failure($"Maximum {MaxReferenceImages} reference images allowed");
            }

            // Validate each image file
            foreach (var path in imagePaths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return Result.Failure("Reference image path cannot be empty");
                }

                if (!File.Exists(path))
                {
                    return Result.Failure($"Reference image not found: {path}");
                }

                var fileInfo = new FileInfo(path);
                if (fileInfo.Length > MaxImageFileSizeBytes)
                {
                    return Result.Failure($"Reference image too large (max 10MB): {Path.GetFileName(path)}");
                }

                // Validate extension
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext != PngExtension && ext != JpgExtension && ext != JpegExtension)
                {
                    return Result.Failure($"Invalid image format (must be PNG/JPG): {Path.GetFileName(path)}");
                }
            }

            return Result.Success();
        }

        private Result ValidateBackendSelection(ModelGeneratorState state)
        {
            if (string.IsNullOrWhiteSpace(state.BackendCategory))
            {
                return Result.Failure("Backend category must be selected");
            }

            if (state.BackendCategory == AssetCategories.Wearable)
            {
                if (string.IsNullOrWhiteSpace(state.BackendSpecificType))
                {
                    return Result.Failure("Wearable type must be selected");
                }
            }
            // Decor doesn't require additional validation

            return Result.Success();
        }

    }
}
