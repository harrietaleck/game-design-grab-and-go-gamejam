using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Common;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;

namespace Genies.Sdk.ArtForge.Editor.Core.Application.Services
{
    /// <summary>
    /// Service for managing assets.
    /// Provides simple, direct methods for asset operations.
    /// </summary>
    internal class AssetService
    {
        /// <summary>
        /// Creates a new asset with validation.
        /// </summary>
        public async Task<Result<Asset>> CreateAssetAsync(
            string name,
            AssetType type,
            CancellationToken cancellationToken = default)
        {
            // Validate name
            var nameResult = AssetName.Create(name);
            if (!nameResult.IsSuccess)
            {
                return Result<Asset>.Failure(nameResult.Error);
            }

            // Check if already exists
            var exists = await GeniesArtForgeService.Assets.ExistsAsync(nameResult.Value, cancellationToken);
            if (exists)
            {
                return Result<Asset>.Failure($"Asset '{name}' already exists");
            }

            // Create entity
            var assetResult = Asset.Create(nameResult.Value, type);
            if (!assetResult.IsSuccess)
            {
                return Result<Asset>.Failure(assetResult.Error);
            }

            // Save
            await GeniesArtForgeService.Assets.AddAsync(assetResult.Value, cancellationToken);

            GeniesArtForgeService.Logger.Info($"Asset created: {name}");

            return Result<Asset>.Success(assetResult.Value);
        }

        /// <summary>
        /// Gets an asset by name.
        /// </summary>
        public async Task<Result<Asset>> GetAssetAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            var nameResult = AssetName.Create(name);
            if (!nameResult.IsSuccess)
            {
                return Result<Asset>.Failure(nameResult.Error);
            }

            var asset = await GeniesArtForgeService.Assets.GetByNameAsync(nameResult.Value, cancellationToken);
            if (asset == null)
            {
                return Result<Asset>.Failure($"Asset '{name}' not found");
            }

            return Result<Asset>.Success(asset);
        }

        /// <summary>
        /// Gets all assets.
        /// </summary>
        public async Task<IEnumerable<Asset>> GetAllAssetsAsync(CancellationToken cancellationToken = default)
        {
            return await GeniesArtForgeService.Assets.GetAllAsync(cancellationToken);
        }

        /// <summary>
        /// Updates an asset's status.
        /// </summary>
        public async Task<Result> UpdateAssetStatusAsync(
            AssetId assetId,
            AssetStatus newStatus,
            CancellationToken cancellationToken = default)
        {
            var asset = await GeniesArtForgeService.Assets.GetByIdAsync(assetId, cancellationToken);
            if (asset == null)
            {
                return Result.Failure("Asset not found");
            }

            var result = asset.UpdateStatus(newStatus);
            if (!result.IsSuccess)
            {
                return result;
            }

            await GeniesArtForgeService.Assets.UpdateAsync(asset, cancellationToken);

            GeniesArtForgeService.Logger.Info($"Asset status updated: {asset.Name} -> {newStatus}");

            return Result.Success();
        }
    }
}

