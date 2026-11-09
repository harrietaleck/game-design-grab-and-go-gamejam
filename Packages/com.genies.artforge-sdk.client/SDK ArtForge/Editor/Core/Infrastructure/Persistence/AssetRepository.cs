using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using IAssetRepository = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.IAssetRepository;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Persistence
{
    /// <summary>
    /// File-based repository implementation for Asset persistence.
    /// Stores assets as JSON metadata with folder structure.
    /// </summary>
    internal class AssetRepository : IAssetRepository
    {
        private readonly ILogger _logger;
        private const string MetadataFileName = "metadata.json";

        public AssetRepository(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Asset> GetByIdAsync(AssetId id, CancellationToken cancellationToken = default)
        {
            var assets = await GetAllAsync(cancellationToken);
            return assets.FirstOrDefault(a => a.Id == id);
        }

        public async Task<Asset> GetByNameAsync(AssetName name, CancellationToken cancellationToken = default)
        {
            var assetPath = GetAssetDirectory(name.Value);
            var metadataPath = Path.Combine(assetPath, MetadataFileName);

            if (!File.Exists(metadataPath))
            {
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(metadataPath, cancellationToken);
                var asset = JsonConvert.DeserializeObject<Asset>(json);
                return asset;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to read asset metadata: {name}", ex);
                return null;
            }
        }

        public async Task<IEnumerable<Asset>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var assetsRoot = GetAssetsRootDirectory();

            if (!Directory.Exists(assetsRoot))
            {
                return Enumerable.Empty<Asset>();
            }

            var assets = new List<Asset>();
            var assetDirs = Directory.GetDirectories(assetsRoot);

            foreach (var dir in assetDirs)
            {
                var metadataPath = Path.Combine(dir, MetadataFileName);
                if (!File.Exists(metadataPath))
                {
                    continue;
                }

                try
                {
                    var json = await File.ReadAllTextAsync(metadataPath, cancellationToken);
                    var asset = JsonConvert.DeserializeObject<Asset>(json);
                    if (asset != null)
                    {
                        assets.Add(asset);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Failed to read asset metadata from {dir}: {ex.Message}");
                }
            }

            return assets;
        }

        public async Task AddAsync(Asset asset, CancellationToken cancellationToken = default)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            var assetDir = GetAssetDirectory(asset.Name.Value);

            // Create directory structure
            Directory.CreateDirectory(assetDir);
            Directory.CreateDirectory(Path.Combine(assetDir, "Models"));
            Directory.CreateDirectory(Path.Combine(assetDir, "Textures"));
            Directory.CreateDirectory(Path.Combine(assetDir, "RefImages"));

            // Save metadata
            await SaveMetadataAsync(asset, cancellationToken);

            _logger.Info($"Asset created: {asset.Name} (ID: {asset.Id})");
        }

        public async Task UpdateAsync(Asset asset, CancellationToken cancellationToken = default)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            await SaveMetadataAsync(asset, cancellationToken);

            _logger.Info($"Asset updated: {asset.Name} (ID: {asset.Id})");
        }

        public Task DeleteAsync(AssetId id, CancellationToken cancellationToken = default)
        {
            // Note: This is a soft delete by archiving the asset
            // Physical deletion should be done through Archive status
            _logger.Warning($"DeleteAsync called for asset {id}. Consider using Archive status instead.");
            return Task.CompletedTask;
        }

        public async Task<bool> ExistsAsync(AssetName name, CancellationToken cancellationToken = default)
        {
            var asset = await GetByNameAsync(name, cancellationToken);
            return asset != null;
        }

        private async Task SaveMetadataAsync(Asset asset, CancellationToken cancellationToken)
        {
            var assetDir = GetAssetDirectory(asset.Name.Value);
            var metadataPath = Path.Combine(assetDir, MetadataFileName);

            var json = JsonConvert.SerializeObject(asset, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            });

            await File.WriteAllTextAsync(metadataPath, json, cancellationToken);
        }

        private string GetAssetsRootDirectory()
        {
            // Store using BrandingConstants asset type folder
            var projectPath = UnityEngine.Application.dataPath;
            return Path.Combine(projectPath, "Genies", BrandingConstants.ShortProductName, BrandingConstants.AssetPaths.DefaultAssetType);
        }

        private string GetAssetDirectory(string assetName)
        {
            return Path.Combine(GetAssetsRootDirectory(), assetName);
        }
    }
}
