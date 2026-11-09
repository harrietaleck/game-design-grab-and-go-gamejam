using System;
using System.Collections.Generic;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Common;

namespace Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects
{
    /// <summary>
    /// Strongly-typed identifier for assets.
    /// </summary>
    internal class AssetId : ValueObject
    {
        public Guid Value { get; }

        private AssetId(Guid value)
        {
            Value = value;
        }

        public static AssetId NewId() => new AssetId(Guid.NewGuid());

        public static AssetId Parse(string value)
        {
            if (Guid.TryParse(value, out var guid))
            {
                return new AssetId(guid);
            }

            throw new ArgumentException($"Invalid AssetId: {value}", nameof(value));
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }

        public override string ToString() => Value.ToString();
    }
}

