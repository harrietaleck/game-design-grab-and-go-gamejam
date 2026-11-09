using System;
using System.Collections.Generic;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Common;

namespace Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects
{
    /// <summary>
    /// Strongly-typed identifier for jobs.
    /// </summary>
    internal class JobId : ValueObject
    {
        public Guid Value { get; }

        // Internal constructor for JSON deserialization and factory methods
        internal JobId(Guid value)
        {
            Value = value;
        }

        public static JobId NewId() => new JobId(Guid.NewGuid());

        public static JobId Parse(string value)
        {
            if (Guid.TryParse(value, out var guid))
            {
                return new JobId(guid);
            }

            throw new ArgumentException($"Invalid JobId: {value}", nameof(value));
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }

        public override string ToString() => Value.ToString();
    }
}


