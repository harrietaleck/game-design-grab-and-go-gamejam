using System;
using System.Collections.Generic;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using UnityEditor;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Helpers
{
    internal static class InsufficientBalanceDialogHelper
    {
        private const string InsufficientBalanceErrorCode = "INSUFFICIENT_BALANCE";
        private const string InsufficientBalanceDialogTitle = "Insufficient Balance";
        private const string InsufficientBalanceDialogMessage =
            "Your generation could not complete because your token balance is too low.\n\n" +
            "Click 'Buy Tokens' to open the billing portal and top up.";
        private const string BuyTokensButtonLabel = "Buy Tokens";
        private const string OkButtonLabel = "OK";

        private static readonly object PendingDialogLock = new object();
        private static readonly HashSet<string> PendingDialogJobKeys = new HashSet<string>(StringComparer.Ordinal);

        internal static bool IsInsufficientBalanceError(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                return false;
            }

            return errorMessage.IndexOf(InsufficientBalanceErrorCode, StringComparison.OrdinalIgnoreCase) >= 0
                || errorMessage.IndexOf("\"statusCode\":402", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool IsInsufficientBalanceError(Job job)
        {
            return IsInsufficientBalanceError(job?.ErrorMessage);
        }

        internal static void ShowDialogForJobIfNeeded(Job job, ILogger logger, string logPrefix)
        {
            if (!IsInsufficientBalanceError(job))
            {
                return;
            }

            string jobKey = job?.Id?.ToString();
            if (string.IsNullOrWhiteSpace(jobKey))
            {
                jobKey = job?.AssetName?.Value ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(jobKey))
            {
                jobKey = Guid.NewGuid().ToString("N");
            }

            lock (PendingDialogLock)
            {
                if (!PendingDialogJobKeys.Add(jobKey))
                {
                    return;
                }
            }

            logger?.Warning($"{logPrefix} Insufficient balance detected. Showing top-up dialog.");

            try
            {
                bool shouldOpenPortal = EditorUtility.DisplayDialog(
                    InsufficientBalanceDialogTitle,
                    InsufficientBalanceDialogMessage,
                    BuyTokensButtonLabel,
                    OkButtonLabel);

                if (shouldOpenPortal)
                {
                    BillingPortalHelper.OpenBillingPortal(logger, logPrefix);
                }
            }
            catch
            {
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        bool shouldOpenPortal = EditorUtility.DisplayDialog(
                            InsufficientBalanceDialogTitle,
                            InsufficientBalanceDialogMessage,
                            BuyTokensButtonLabel,
                            OkButtonLabel);

                        if (shouldOpenPortal)
                        {
                            BillingPortalHelper.OpenBillingPortal(logger, logPrefix);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.Warning($"{logPrefix} Failed to show insufficient balance dialog: {ex.Message}");
                    }
                    finally
                    {
                        lock (PendingDialogLock)
                        {
                            PendingDialogJobKeys.Remove(jobKey);
                        }
                    }
                };

                return;
            }

            lock (PendingDialogLock)
            {
                PendingDialogJobKeys.Remove(jobKey);
            }
        }
    }
}
