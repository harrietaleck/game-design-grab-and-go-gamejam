using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects;

namespace Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Asset aggregate persistence.
    /// Provides data access abstraction following repository pattern.
    /// </summary>
    internal interface IAssetRepository
    {
        /// <summary>
        /// Retrieves an asset by its unique identifier.
        /// </summary>
        /// <param name="id">The asset ID to search for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The asset if found, null otherwise</returns>
        Task<Asset> GetByIdAsync(AssetId id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves an asset by its name.
        /// </summary>
        /// <param name="name">The asset name to search for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The asset if found, null otherwise</returns>
        Task<Asset> GetByNameAsync(AssetName name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all assets.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of all assets</returns>
        Task<IEnumerable<Asset>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a new asset to the repository.
        /// </summary>
        /// <param name="asset">The asset to add</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task AddAsync(Asset asset, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing asset.
        /// </summary>
        /// <param name="asset">The asset to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task UpdateAsync(Asset asset, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an asset by its ID.
        /// </summary>
        /// <param name="id">The asset ID to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task DeleteAsync(AssetId id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if an asset with the given name exists.
        /// </summary>
        /// <param name="name">The asset name to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if exists, false otherwise</returns>
        Task<bool> ExistsAsync(AssetName name, CancellationToken cancellationToken = default);
    }
}

