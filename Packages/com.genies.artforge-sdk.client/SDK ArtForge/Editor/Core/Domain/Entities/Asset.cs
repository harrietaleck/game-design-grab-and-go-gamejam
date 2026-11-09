using System;
using System.Collections.Generic;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Common;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Events;
using Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects;

namespace Genies.Sdk.ArtForge.Editor.Core.Domain.Entities
{
    /// <summary>
    /// Snapshot of quality/post-process settings used to import and process an asset.
    /// Stored in metadata.json so import processors can apply settings deterministically.
    /// </summary>
    public class AssetPostProcessSettings
    {
        public string Pivot { get; set; }
        public string TopologyType { get; set; }
        public int Polycount { get; set; }
        public string TextureResolution { get; set; }
        public string AspectRatio { get; set; }
        public bool GenerateLODs { get; set; }
        public bool GenerateCollider { get; set; }
        public bool GenerateAO { get; set; }
        public bool GenerateTransparent { get; set; }
    }

    /// <summary>
    /// Represents a game asset (3D model, texture, or material).
    /// Aggregate root for the Asset aggregate.
    /// </summary>
    internal class Asset : Entity<AssetId>
    {
        private readonly List<IDomainEvent> _domainEvents = new List<IDomainEvent>();

        // Value Objects (validated)
        public AssetName Name { get; private set; }
        public AssetType Type { get; private set; }

        // Properties
        public AssetStatus Status { get; private set; }
        public string FilePath { get; private set; }
        public string BackendAssetTypeId { get; private set; }
        public AssetPostProcessSettings PostProcessSettings { get; private set; }

        // Collections (read-only)
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        // Private constructor for EF/serialization
        private Asset() { }

        /// <summary>
        /// Factory method to create a new Asset with validation.
        /// </summary>
        public static Result<Asset> Create(AssetName name, AssetType type)
        {
            // Validation
            if (name == null)
            {
                return Result<Asset>.Failure("Asset name is required");
            }

            var asset = new Asset
            {
                Id = AssetId.NewId(),
                Name = name,
                Type = type,
                Status = AssetStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Raise domain event
            asset.RaiseDomainEvent(new AssetCreatedEvent(asset.Id, name));

            return Result<Asset>.Success(asset);
        }

        /// <summary>
        /// Updates the asset status.
        /// </summary>
        public Result UpdateStatus(AssetStatus newStatus)
        {
            // Business rule: Cannot go back to Draft once generating
            if (Status == AssetStatus.Generating && newStatus == AssetStatus.Draft)
            {
                return Result.Failure("Cannot revert to Draft status while generating");
            }

            // Business rule: Cannot change status of archived assets
            if (Status == AssetStatus.Archived)
            {
                return Result.Failure("Cannot change status of archived assets");
            }

            Status = newStatus;
            UpdatedAt = DateTime.UtcNow;

            return Result.Success();
        }

        /// <summary>
        /// Sets the file path where the asset is stored.
        /// </summary>
        public void SetFilePath(string filePath)
        {
            FilePath = filePath;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Stores backend asset classification (e.g. "wearable|hat", "decor").
        /// Persisted to metadata.json for downstream tooling.
        /// </summary>
        public void SetBackendAssetTypeId(string backendAssetTypeId)
        {
            BackendAssetTypeId = backendAssetTypeId?.Trim();
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Stores the quality settings used when generating/processing this asset.
        /// </summary>
        public void SetPostProcessSettings(JobQualitySettings qualitySettings)
        {
            if (qualitySettings == null)
            {
                PostProcessSettings = null;
                UpdatedAt = DateTime.UtcNow;
                return;
            }

            PostProcessSettings = new AssetPostProcessSettings
            {
                Pivot = qualitySettings.Pivot,
                TopologyType = qualitySettings.TopologyType,
                Polycount = qualitySettings.Polycount,
                TextureResolution = qualitySettings.TextureResolution,
                AspectRatio = qualitySettings.AspectRatio,
                GenerateLODs = qualitySettings.GenerateLODs,
                GenerateCollider = qualitySettings.GenerateCollider,
                GenerateAO = qualitySettings.GenerateAO,
                GenerateTransparent = qualitySettings.GenerateTransparent
            };

            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Archives the asset (soft delete).
        /// </summary>
        public Result Archive()
        {
            // Business rule: Cannot archive while generating
            if (Status == AssetStatus.Generating)
            {
                return Result.Failure("Cannot archive asset while it is being generated");
            }

            Status = AssetStatus.Archived;
            UpdatedAt = DateTime.UtcNow;

            return Result.Success();
        }

        private void RaiseDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }

        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }
    }
}
