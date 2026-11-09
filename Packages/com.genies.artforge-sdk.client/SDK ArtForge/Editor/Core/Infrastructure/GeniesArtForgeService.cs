using UnityEditor;
using UnityEngine;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using IAssetRepository = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.IAssetRepository;
using IProjectRepository = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.IProjectRepository;
using Genies.Sdk.ArtForge.Editor.Core.Application.Services;
using Genies.Sdk.ArtForge.Editor.Components.ModelGenerator.Config;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Persistence;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.API;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure
{
    /// <summary>
    /// Central service registry for GeniesArtForge.
    /// Provides global access to core services with support for custom implementations.
    /// Generation is exposed as concrete GenerationService to allow access to backend-specific methods.
    /// </summary>
    [InitializeOnLoad]
    internal static class GeniesArtForgeService
    {
        private static readonly ILogger BootstrapLogger = new UnityLogger();
        // Core services
        public static IAssetRepository Assets { get; private set; }
        public static IProjectRepository Projects { get; private set; }
        public static ILogger Logger { get; private set; }
        public static QueueService Queue { get; private set; }
        
        /// <summary>
        /// Generation service (backend-only).
        /// Handles all asset generation through the Genies backend.
        /// </summary>
        public static GenerationService Generation { get; private set; }

        /// <summary>
        /// Gets the backend ContentClient from the GenerationService.
        /// Used for direct backend API calls (e.g., GAP integration).
        /// </summary>
        public static ContentClient ContentClient => Generation?.GetBackendClient();

        /// <summary>
        /// Shared backend config provider with app-wide cached state.
        /// </summary>
        public static IModelGeneratorConfigProvider BackendConfigProvider { get; private set; }

        /// <summary>
        /// Static constructor - automatically initializes services when Unity editor loads.
        /// </summary>
        static GeniesArtForgeService()
        {
            try
            {
                // CRITICAL: Don't initialize in asset import worker processes
                // Asset import workers are lightweight separate processes that should NOT
                // initialize heavy services (file I/O, GPU resources, background threads, etc.)
                if (IsAssetImportWorkerProcess())
                {
                    BootstrapLogger.Info("[GeniesArtForge] Skipping service initialization in asset import worker");
                    return;
                }

                Initialize();
            }
            catch (System.Exception ex)
            {
                BootstrapLogger.Error($"[GeniesArtForge] CRITICAL: Failed to initialize services: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Checks if the current process is an asset import worker.
        /// Asset import workers run in separate processes and should not initialize heavy services.
        /// </summary>
        private static bool IsAssetImportWorkerProcess()
        {
            // Check for asset import worker indicators
            string[] args = System.Environment.GetCommandLineArgs();

            // Workers have specific command line arguments
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-batchMode" ||
                    args[i] == "-assetWorkerIndex" ||
                    args[i].Contains("AssetImportWorker"))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Initializes all services with default implementations.
        /// Can be called to reset services to defaults.
        /// </summary>
        public static void Initialize()
        {
            UnityLogger.RefreshLogsEnabledFromPrefs();
            var logger = new UnityLogger();
            Initialize(logger, null, null);
        }

        /// <summary>
        /// Initializes services with optional custom implementations.
        /// Pass null for any parameter to use the default implementation.
        /// </summary>
        /// <param name="logger">Custom logger implementation (optional)</param>
        /// <param name="assetRepository">Custom asset repository implementation (optional)</param>
        /// <param name="projectRepository">Custom project repository implementation (optional)</param>
        public static void Initialize(
            ILogger logger,
            IAssetRepository assetRepository = null,
            IProjectRepository projectRepository = null)
        {
            try
            {
                // Use provided implementations or fall back to defaults
                Logger = logger ?? new UnityLogger();
                Assets = assetRepository ?? new AssetRepository(Logger);
                Projects = projectRepository ?? new ProjectRepository(Logger);
                Generation = new GenerationService(Logger);
                BackendConfigProvider = new BackendConfigProvider(Generation, Logger);

                Queue = new QueueService(Logger, Generation);
                Queue.Initialize();

                Logger.Info("GeniesArtForge services initialized successfully");
            }
            catch (System.Exception ex)
            {
                BootstrapLogger.Error($"[GeniesArtForge] Failed to initialize services: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Checks if all core services are properly initialized.
        /// </summary>
        public static bool IsInitialized =>
            Logger != null &&
            Assets != null &&
            Projects != null &&
            Queue != null &&
            Generation != null &&
            BackendConfigProvider != null;
    }
}
