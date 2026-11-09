using System;

namespace Genies.Sdk.ArtForge.Editor.Core.Domain.Common
{
    /// <summary>
    /// Interface for domain events.
    /// Domain events represent something meaningful that happened in the domain.
    /// </summary>
    internal interface IDomainEvent
    {
        DateTime OccurredOn { get; }
    }
}

