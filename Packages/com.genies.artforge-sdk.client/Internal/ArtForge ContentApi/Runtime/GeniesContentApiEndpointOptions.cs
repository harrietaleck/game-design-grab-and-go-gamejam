namespace Genies.ArtForge.ContentAPI
{
    /// <summary>
    /// Controls which endpoint objects are created by GeniesContentAPI.
    /// </summary>
    internal sealed class GeniesContentApiEndpointOptions
    {
        public bool EnableWearables { get; set; } = true;
        public bool EnableDecor { get; set; } = true;
        public bool EnableJobs { get; set; } = true;
        public bool EnableAssets { get; set; } = true;
        public bool UseLegacyAssetOwnerQueries { get; set; } = false;
    }
}
