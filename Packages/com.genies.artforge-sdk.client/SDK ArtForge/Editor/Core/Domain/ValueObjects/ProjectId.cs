using System;
using System.Collections.Generic;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Common;

namespace Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects
{
    /// <summary>
    /// Strongly-typed identifier for projects.
    /// </summary>
    internal class ProjectId : ValueObject
    {
        public Guid Value { get; }

        private ProjectId(Guid value)
        {
            Value = value;
        }

        public static ProjectId NewId() => new ProjectId(Guid.NewGuid());

        public static ProjectId Parse(string value)
        {
            if (Guid.TryParse(value, out var guid))
            {
                return new ProjectId(guid);
            }

            throw new ArgumentException($"Invalid ProjectId: {value}", nameof(value));
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }

        public override string ToString() => Value.ToString();
    }
}

