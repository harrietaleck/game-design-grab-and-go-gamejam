namespace Genies.Sdk.ArtForge.Editor.Core.Domain.Enums
{
    /// <summary>
    /// Additional queue-only state for jobs that are queued but temporarily blocked.
    /// </summary>
    internal enum JobQueueSubstate
    {
        None = 0,
        AwaitingLogin = 1,
        ReconnectingBackend = 2
    }
}
