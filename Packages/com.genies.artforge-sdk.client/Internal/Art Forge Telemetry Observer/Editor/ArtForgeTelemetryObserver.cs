using System;
using System.Collections.Generic;
using System.IO;
using Genies.Sdk.ArtForge.Editor.Auth;
using Genies.Sdk.ArtForge.Editor.Components.Settings;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using Genies.Sdk.ArtForge.Editor.Hub;
using Genies.Sdk.ArtForge.Editor.Hub.UI;
using UnityEditor;
using UnityEngine;
using Genies.Sdk.ArtForge.Editor.Components.ModelGenerator;

namespace Genies.ArtForge.Telemetry.Observer.Editor
{
    [InitializeOnLoad]
    internal static class AreForgeTelemetryObserver
    {
        private static bool _subscribed;

        private static bool _queueSubscribed;
        private static Genies.Sdk.ArtForge.Editor.Core.Application.Services.QueueService _queueSource;

        private const string TelemetryConsentAskedKey = "Genies.Telemetry.Consent.Asked";

        private const string VersionCacheResourcesFolder = "Assets/Genies/Resources";
        private static readonly string VersionCacheAssetPath =
            $"{VersionCacheResourcesFolder}/{GeniesArtForgeTelemetryInstaller.CacheName}.asset";
        
        private static string sdkVersion = "";

        static AreForgeTelemetryObserver()
        {
            EditorApplication.delayCall -= EnsureVersionCacheWritten;
            EditorApplication.delayCall += EnsureVersionCacheWritten;

            EditorApplication.delayCall -= EnsureTelemetryConsentPrompted;
            EditorApplication.delayCall += EnsureTelemetryConsentPrompted;

            EditorApplication.delayCall -= InitializePreAuthTelemetry;
            EditorApplication.delayCall += InitializePreAuthTelemetry;

            EditorApplication.delayCall -= EnsureSubscribed;
            EditorApplication.delayCall += EnsureSubscribed;

            EnsureSubscribed();
        }

        private static async void InitializePreAuthTelemetry()
        {
            if (GeniesArtForgeTelemetry.IsInitialized())
            {
                return;
            }

            GeniesArtForgeTelemetryInstaller installer = new GeniesArtForgeTelemetryInstaller();
            await installer.Initialize();
        }

        private static void EnsureSubscribed()
        {
            if (_subscribed)
            {
                EnsureQueueSubscribed();
                return;
            }

            _subscribed = true;

            GeniesArtForgeHub.HubOpened -= OnTelemetry;
            GeniesArtForgeHub.HubOpened += OnTelemetry;

            GeniesArtForgeHub.HubClosed -= OnTelemetry;
            GeniesArtForgeHub.HubClosed += OnTelemetry;

            GeniesArtForgeHub.HubTabViewed -= OnTelemetry;
            GeniesArtForgeHub.HubTabViewed += OnTelemetry;

            GeniesArtForgeHub.BalanceClicked -= OnTelemetry;
            GeniesArtForgeHub.BalanceClicked += OnTelemetry;

            GeniesArtForgeHub.PackageFirstInstalled -= OnTelemetry;
            GeniesArtForgeHub.PackageFirstInstalled += OnTelemetry;

            BackendSettingsSection.ArtForgeAuthTelemetry -= OnTelemetry;
            BackendSettingsSection.ArtForgeAuthTelemetry += OnTelemetry;

            // 2D preview dialog UX events
            ApprovalReviewDialog.Preview2DJobStarted -= OnTelemetry;
            ApprovalReviewDialog.Preview2DJobStarted += OnTelemetry;

            ApprovalReviewDialog.Preview2DJobSucceeded -= OnTelemetry;
            ApprovalReviewDialog.Preview2DJobSucceeded += OnTelemetry;

            ApprovalReviewDialog.Preview2DJobFailed -= OnTelemetry;
            ApprovalReviewDialog.Preview2DJobFailed += OnTelemetry;

            ApprovalReviewDialog.Preview2DImageViewed -= OnTelemetry;
            ApprovalReviewDialog.Preview2DImageViewed += OnTelemetry;

            ApprovalReviewDialog.Preview2DApproved -= OnTelemetry;
            ApprovalReviewDialog.Preview2DApproved += OnTelemetry;

            ApprovalReviewDialog.Preview2DCancelled -= OnTelemetry;
            ApprovalReviewDialog.Preview2DCancelled += OnTelemetry;

            ApprovalReviewDialog.Preview2DEditDescription -= OnTelemetry;
            ApprovalReviewDialog.Preview2DEditDescription += OnTelemetry;

            ApprovalReviewDialog.Preview2DRetryClicked -= OnTelemetry;
            ApprovalReviewDialog.Preview2DRetryClicked += OnTelemetry;

            // Queue panel UX + inventory + errors (static events)
            QueuePanelController.QueueCancelJob -= OnTelemetry;
            QueuePanelController.QueueCancelJob += OnTelemetry;

            QueuePanelController.QueueOpen2DPreview -= OnTelemetry;
            QueuePanelController.QueueOpen2DPreview += OnTelemetry;

            QueuePanelController.QueueOpenFolder -= OnTelemetry;
            QueuePanelController.QueueOpenFolder += OnTelemetry;

            QueuePanelController.Model3DRetryClicked -= OnTelemetry;
            QueuePanelController.Model3DRetryClicked += OnTelemetry;

            QueuePanelController.Model3DCancelClicked -= OnTelemetry;
            QueuePanelController.Model3DCancelClicked += OnTelemetry;

            QueuePanelController.InventorySendClicked -= OnTelemetry;
            QueuePanelController.InventorySendClicked += OnTelemetry;

            QueuePanelController.InventorySendStarted -= OnTelemetry;
            QueuePanelController.InventorySendStarted += OnTelemetry;

            QueuePanelController.InventorySendSucceeded -= OnTelemetry;
            QueuePanelController.InventorySendSucceeded += OnTelemetry;

            QueuePanelController.InventorySendFailed -= OnTelemetry;
            QueuePanelController.InventorySendFailed += OnTelemetry;

            QueuePanelController.InventoryResendClicked -= OnTelemetry;
            QueuePanelController.InventoryResendClicked += OnTelemetry;

            QueuePanelController.UiErrorShown -= OnTelemetry;
            QueuePanelController.UiErrorShown += OnTelemetry;

            QueuePanelController.ApiRequestFailed -= OnTelemetry;
            QueuePanelController.ApiRequestFailed += OnTelemetry;

            // ------------------------------------------------------------
            // Model Gen telemetry (static events emitted by Model Gen UI)
            // ------------------------------------------------------------
            ModelGeneratorPresenter.ModelGenTelemetry -= OnTelemetry;
            ModelGeneratorPresenter.ModelGenTelemetry += OnTelemetry;

            ModelGeneratorView.ModelGenTelemetry -= OnTelemetry;
            ModelGeneratorView.ModelGenTelemetry += OnTelemetry;

            EnsureQueueSubscribed();

            EditorApplication.delayCall -= EnsureQueueSubscribed;
            EditorApplication.delayCall += EnsureQueueSubscribed;
        }

        private static void EnsureQueueSubscribed()
        {
            var queue = GeniesArtForgeService.Queue;
            if (queue == null)
            {
                return;
            }

            if (_queueSubscribed && ReferenceEquals(_queueSource, queue))
            {
                return;
            }

            if (_queueSource != null)
            {
                _queueSource.Preview2DJobStarted -= OnTelemetry;
                _queueSource.Preview2DJobSucceeded -= OnTelemetry;
                _queueSource.Preview2DJobFailed -= OnTelemetry;

                _queueSource.Model3DJobStarted -= OnTelemetry;
                _queueSource.Model3DJobSucceeded -= OnTelemetry;
                _queueSource.Model3DJobFailed -= OnTelemetry;

                _queueSource.HistoryRemoveAll -= OnTelemetry;
                _queueSource.HistoryJobRemoved -= OnTelemetry;

                _queueSource.ApiRequestFailed -= OnTelemetry;
            }

            _queueSource = queue;
            _queueSubscribed = true;

            _queueSource.Preview2DJobStarted -= OnTelemetry;
            _queueSource.Preview2DJobStarted += OnTelemetry;

            _queueSource.Preview2DJobSucceeded -= OnTelemetry;
            _queueSource.Preview2DJobSucceeded += OnTelemetry;

            _queueSource.Preview2DJobFailed -= OnTelemetry;
            _queueSource.Preview2DJobFailed += OnTelemetry;

            _queueSource.Model3DJobStarted -= OnTelemetry;
            _queueSource.Model3DJobStarted += OnTelemetry;

            _queueSource.Model3DJobSucceeded -= OnTelemetry;
            _queueSource.Model3DJobSucceeded += OnTelemetry;

            _queueSource.Model3DJobFailed -= OnTelemetry;
            _queueSource.Model3DJobFailed += OnTelemetry;

            _queueSource.HistoryRemoveAll -= OnTelemetry;
            _queueSource.HistoryRemoveAll += OnTelemetry;

            _queueSource.HistoryJobRemoved -= OnTelemetry;
            _queueSource.HistoryJobRemoved += OnTelemetry;

            _queueSource.ApiRequestFailed -= OnTelemetry;
            _queueSource.ApiRequestFailed += OnTelemetry;
        }

        private static void OnTelemetry(string eventName, Dictionary<string, object> properties)
        {
            properties ??= new Dictionary<string, object>();

            var userId = WorkOSAuthService.GetStoredUserId();

            if (string.IsNullOrWhiteSpace(sdkVersion))
            {
                sdkVersion = GeniesArtForgeVersionChecker.GetInstalledSdkVersion();
            }

            properties["sdkVersion"] = string.IsNullOrWhiteSpace(sdkVersion) ? "unknown" : sdkVersion;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                properties["client_user_id"] = userId;
            }

            GeniesArtForgeTelemetry.RecordPreauthEvent(
                GeniesArtForgeTelemetryEvent.Create(
                    name: eventName,
                    properties: properties
                )
            );
        }

        private static void EnsureVersionCacheWritten()
        {
            try
            {
                EnsureFolder(VersionCacheResourcesFolder);

                string currentVersion = GeniesArtForgeVersionChecker.GetInstalledSdkVersion();
                var existing = AssetDatabase.LoadAssetAtPath<GeniesArtForgeSdkVersionCache>(VersionCacheAssetPath);

                if (!string.IsNullOrWhiteSpace(currentVersion))
                {
                    sdkVersion = currentVersion;

                    if (existing == null || existing.Version != currentVersion)
                    {
                        WriteVersionCache(currentVersion, notes: null);
                    }

                    return;
                }

                sdkVersion = "";

                if (existing == null)
                {
                    string notes = GeniesArtForgeVersionChecker.IsPackageInstalled()
                        ? "package.json missing/unreadable or version field not found"
                        : "Art Forge not installed";

                    WriteVersionCache("unknown", notes);
                }
            }
            catch (Exception e)
            {
                sdkVersion = "";
                Debug.LogWarning($"[Genies Art Forge] Failed to ensure version cache: {e.Message}");
            }
        }

        private static void EnsureTelemetryConsentPrompted()
        {
            if (EditorUserSettings.GetConfigValue(TelemetryConsentAskedKey) == "true")
            {
                return;
            }

            EditorUserSettings.SetConfigValue(TelemetryConsentAskedKey, "true");

            const string title = "Genies Art Forge Telemetry";
            const string message =
                "Genies gathers information about how you use our products (such as configuration and feature usage). " +
                "We collect this data to help us improve Art Forge and other products for future updates. " +
                "You can change this setting in \n\n" + "Project Settings → Genies → Telemetry Settings.";

            EditorUtility.DisplayDialog(title, message, "OK");

            PlayerPrefs.SetInt(GeniesArtForgeTelemetry.TelemetryEnabledKey, 1);
            PlayerPrefs.Save();
        }

        private static void WriteVersionCache(string version, string notes)
        {
            EnsureFolder(VersionCacheResourcesFolder);

            var cache = AssetDatabase.LoadAssetAtPath<GeniesArtForgeSdkVersionCache>(VersionCacheAssetPath);
            if (cache == null)
            {
                cache = ScriptableObject.CreateInstance<GeniesArtForgeSdkVersionCache>();
                AssetDatabase.CreateAsset(cache, VersionCacheAssetPath);
            }

            cache.Version = string.IsNullOrWhiteSpace(version) ? "" : version;
            cache.LastUpdatedUtc = DateTime.UtcNow.ToString("o");
            cache.Notes = notes ?? "";

            EditorUtility.SetDirty(cache);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath);
            string name = Path.GetFileName(folderPath);

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
