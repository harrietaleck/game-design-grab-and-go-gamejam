using System;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Common;
using Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects;

namespace Genies.Sdk.ArtForge.Editor.Core.Domain.Events
{
    /// <summary>
    /// Domain event raised when a new asset is created.
    /// </summary>
    internal class AssetCreatedEvent : IDomainEvent
    {
        public AssetId AssetId { get; }
        public AssetName AssetName { get; }
        public DateTime OccurredOn { get; }

        public AssetCreatedEvent(AssetId assetId, AssetName assetName)
        {
            AssetId = assetId;
            AssetName = assetName;
            OccurredOn = DateTime.UtcNow;
        }
    }
}

