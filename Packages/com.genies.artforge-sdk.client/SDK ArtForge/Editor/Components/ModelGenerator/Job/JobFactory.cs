using System;
using System.IO;
using System.Linq;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Common;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;

namespace Genies.Sdk.ArtForge.Editor.Components.ModelGenerator.Job
{
    /// <summary>
    /// Factory for creating Job instances from ModelGeneratorState.
    /// Separates job creation logic from UI concerns.
    /// </summary>
    internal sealed class JobFactory
    {
        private const int MaxAssetNameLength = 30;
        private const int MaxSuffixAttempts = 9999;

        /// <summary>
        /// Create a Job from the current state
        /// </summary>
        public Result<Core.Domain.Entities.Job> Create(ModelGeneratorState state)
        {
            if (state == null)
            {
                return Result<Core.Domain.Entities.Job>.Failure("State cannot be null");
            }

            // Validate and sanitize display name first.
            var displayNameResult = AssetName.Create(state.AssetName);
            if (!displayNameResult.IsSuccess)
            {
                return Result<Core.Domain.Entities.Job>.Failure(displayNameResult.Error);
            }

            var uniqueAssetNameResult = ResolveUniqueAssetName(displayNameResult.Value);
            if (!uniqueAssetNameResult.IsSuccess)
            {
                return Result<Core.Domain.Entities.Job>.Failure(uniqueAssetNameResult.Error);
            }

            // Build asset type selection
            var selection = BuildSelection(state);
            string assetTypeId = selection.Encode();
            string backendSubcategory = state.BackendCategory == AssetCategories.Decor
                ? state.BackendSubcategory
                : null;

            // Create job
            var job = new Core.Domain.Entities.Job(
                JobId.NewId(),
                uniqueAssetNameResult.Value,
                JobType.ModelGeneration,
                state.Description,
                state.Description, // Use description as prompt
                assetTypeId,
                state.ReferenceImages,
                state.Quality,
                null, // No target asset for new generation
                displayNameResult.Value.Value,
                backendSubcategory
            );

            return Result<Core.Domain.Entities.Job>.Success(job);
        }

        private Result<AssetName> ResolveUniqueAssetName(AssetName baseAssetName)
        {
            string baseValue = baseAssetName.Value;

            for (int suffix = 0; suffix <= MaxSuffixAttempts; suffix++)
            {
                string candidate = suffix == 0
                    ? baseValue
                    : BuildSuffixedName(baseValue, suffix);

                var candidateResult = AssetName.Create(candidate);
                if (!candidateResult.IsSuccess)
                {
                    continue;
                }

                if (!IsAssetNameTaken(candidateResult.Value.Value))
                {
                    return Result<AssetName>.Success(candidateResult.Value);
                }
            }

            return Result<AssetName>.Failure("Unable to find a unique folder name for this asset.");
        }

        private static string BuildSuffixedName(string baseName, int suffix)
        {
            string suffixToken = $"_{suffix}";
            int maxBaseLength = MaxAssetNameLength - suffixToken.Length;
            string trimmedBase = baseName.Length <= maxBaseLength
                ? baseName
                : baseName.Substring(0, maxBaseLength);

            return $"{trimmedBase}{suffixToken}";
        }

        private static bool IsAssetNameTaken(string assetName)
        {
            var queue = GeniesArtForgeService.Queue;
            if (queue != null)
            {
                var allJobs = queue.GetAllJobs();
                if (allJobs.Any(j =>
                        j?.AssetName != null &&
                        j.AssetName.Value.Equals(assetName, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            string assetFolderPath = BrandingConstants.AssetPaths.GetAssetRoot(assetName).TrimEnd('/');
            return Directory.Exists(assetFolderPath);
        }

        private AssetTypeSelection BuildSelection(ModelGeneratorState state)
        {
            // Backend mode only
            if (state.BackendCategory == AssetCategories.Wearable)
            {
                return new WearableTypeSelection(state.BackendSpecificType);
            }
            else if (state.BackendCategory == AssetCategories.Decor)
            {
                return new DecorTypeSelection(state.BackendSpecificType);
            }
            else
            {
                return new GenericTypeSelection(state.BackendCategory, state.BackendSpecificType);
            }
        }
    }
}
