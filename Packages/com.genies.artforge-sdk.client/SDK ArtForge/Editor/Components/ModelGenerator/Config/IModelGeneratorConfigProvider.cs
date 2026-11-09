using System;
using System.Threading.Tasks;
using Genies.ArtForge.ContentAPI.Models;

namespace Genies.Sdk.ArtForge.Editor.Components.ModelGenerator.Config
{
    /// <summary>
    /// Result of backend configuration loading
    /// </summary>
    internal sealed class BackendConfigResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public WearableConfig WearableConfig { get; set; }
        public DecorConfig DecorConfig { get; set; }

        public static BackendConfigResult Success(WearableConfig wearable, DecorConfig decor = null)
        {
            return new BackendConfigResult
            {
                IsSuccess = true,
                WearableConfig = wearable,
                DecorConfig = decor
            };
        }

        public static BackendConfigResult Failure(string error)
        {
            return new BackendConfigResult
            {
                IsSuccess = false,
                ErrorMessage = error
            };
        }
    }

    /// <summary>
    /// Status of configuration loading
    /// </summary>
    internal enum ConfigLoadStatus
    {
        NotStarted,
        Loading,
        Loaded,
        Failed
    }

    /// <summary>
    /// Provides backend configuration for Model Generator.
    /// Handles authentication, loading, caching, and retries.
    /// </summary>
    internal interface IModelGeneratorConfigProvider
    {
        /// <summary>
        /// Current loading status
        /// </summary>
        ConfigLoadStatus Status { get; }

        /// <summary>
        /// Last error message (if Status == Failed)
        /// </summary>
        string LastError { get; }

        /// <summary>
        /// Cached configuration (if Status == Loaded)
        /// </summary>
        BackendConfigResult CachedConfig { get; }

        /// <summary>
        /// Event raised when status changes
        /// </summary>
        event Action<ConfigLoadStatus> OnStatusChanged;

        /// <summary>
        /// Get backend configuration (loads if not cached, returns cached otherwise)
        /// </summary>
        Task<BackendConfigResult> GetAsync();

        /// <summary>
        /// Force reload configuration (clears cache and reloads)
        /// </summary>
        Task<BackendConfigResult> ReloadAsync();

        /// <summary>
        /// Clear cached configuration
        /// </summary>
        void ClearCache();
    }
}
