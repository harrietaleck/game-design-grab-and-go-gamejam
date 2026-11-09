using System;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Helpers
{
    internal static class BillingPortalHelper
    {
        internal const string BalanceTopUpUrl = "https://hub.genies.com/";

        internal static void OpenBillingPortal(ILogger logger, string logPrefix)
        {
            try
            {
                UnityEngine.Application.OpenURL(BalanceTopUpUrl);
            }
            catch (Exception ex)
            {
                logger?.Warning($"{logPrefix} Failed to open billing portal: {ex.Message}");
            }
        }
    }
}
