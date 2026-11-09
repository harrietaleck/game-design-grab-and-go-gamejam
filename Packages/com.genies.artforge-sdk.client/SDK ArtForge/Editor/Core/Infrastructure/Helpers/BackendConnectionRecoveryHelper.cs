using System;
using System.Threading.Tasks;
using Genies.Sdk.ArtForge.Editor.Hub;
using UnityEditor;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Helpers
{
    internal static class BackendConnectionRecoveryHelper
    {
        private const string DialogTitle = "Backend Connection";
        private const string FallbackPromptMessage = "Real-time backend connection is unavailable. Please reconnect in Settings.";
        private static readonly TimeSpan PromptCooldown = TimeSpan.FromSeconds(5);
        private static readonly object PromptLock = new object();
        private static DateTime _lastPromptUtc = DateTime.MinValue;
        private static bool _promptOpen;

        private static readonly string[] RecoverableConnectionErrorFragments =
        {
            "cannot send request",
            "websocket state is reconnecting",
            "websocket is reconnecting",
            "websocket reconnecting",
            "connection is reconnecting",
            "not connected",
            "connection not open",
            "timed out connecting to convex",
            "connection timeout waiting for transition response",
            "token identity expired",
            "identity expired",
            "a task was canceled",
            "task was canceled",
            "operation was canceled",
            "timed out while submitting backend job",
            "submitting backend job timed out"
        };

        public static bool IsRecoverableConnectionError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            foreach (string fragment in RecoverableConnectionErrorFragments)
            {
                if (message.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsRecoverableConnectionError(Exception exception)
        {
            if (exception == null)
            {
                return false;
            }

            if (IsRecoverableConnectionError(exception.Message))
            {
                return true;
            }

            Exception current = exception.InnerException;
            int depth = 0;
            while (current != null && depth < 6)
            {
                if (IsRecoverableConnectionError(current.Message))
                {
                    return true;
                }

                current = current.InnerException;
                depth++;
            }

            return false;
        }

        public static async Task<bool> EnsureConnectedOrPromptAsync(
            ILogger logger,
            string promptMessage = null,
            bool disconnectOnFailure = true,
            bool promptOnFailure = true)
        {
            string message = string.IsNullOrWhiteSpace(promptMessage) ? FallbackPromptMessage : promptMessage;

            var generationService = GeniesArtForgeService.Generation;
            if (generationService == null || !generationService.IsBackendAvailable)
            {
                if (promptOnFailure)
                {
                    PromptOpenSettings(message);
                }

                return false;
            }

            var contentClient = generationService.GetBackendClient();
            if (contentClient == null)
            {
                if (promptOnFailure)
                {
                    PromptOpenSettings(message);
                }

                return false;
            }

            if (contentClient.IsConnected)
            {
                return true;
            }

            bool reconnected = await contentClient.EnsureConnectedAsync();
            if (reconnected)
            {
                logger?.Info("[BackendConnection] Reconnected backend WebSocket automatically.");
                return true;
            }

            if (disconnectOnFailure)
            {
                try
                {
                    await generationService.DisconnectBackendAsync();
                    logger?.Warning("[BackendConnection] Auto-reconnect failed. Disconnected backend session.");
                }
                catch (Exception ex)
                {
                    logger?.Warning($"[BackendConnection] Failed to disconnect backend session after reconnect failure: {ex.Message}");
                }
            }

            if (promptOnFailure)
            {
                PromptOpenSettings(message);
            }

            return false;
        }

        public static void PromptOpenSettings(string message)
        {
            string dialogMessage = string.IsNullOrWhiteSpace(message) ? FallbackPromptMessage : message;
            if (ShouldSuppressPrompt())
            {
                return;
            }

            bool openSettings;
            lock (PromptLock)
            {
                _promptOpen = true;
            }

            try
            {
                openSettings = EditorUtility.DisplayDialog(DialogTitle, dialogMessage, "Open Settings", "Cancel");
            }
            finally
            {
                lock (PromptLock)
                {
                    _promptOpen = false;
                    _lastPromptUtc = DateTime.UtcNow;
                }
            }

            if (openSettings)
            {
                GeniesArtForgeHub.RestartAndOpen("Settings");
            }
        }

        private static bool ShouldSuppressPrompt()
        {
            lock (PromptLock)
            {
                if (_promptOpen)
                {
                    return true;
                }

                bool withinCooldown = DateTime.UtcNow - _lastPromptUtc < PromptCooldown;
                return withinCooldown;
            }
        }
    }
}
