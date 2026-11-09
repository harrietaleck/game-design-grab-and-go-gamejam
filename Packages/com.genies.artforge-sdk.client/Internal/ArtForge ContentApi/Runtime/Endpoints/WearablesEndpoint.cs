using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Genies.ArtForge.ContentAPI.Models;
using Genies.Convex.Exceptions;
using Genies.Convex.Values;
using UnityEngine;
using UnityEngine.Networking;

namespace Genies.ArtForge.ContentAPI
{
    internal sealed class WearablesEndpoint : ContentEndpointBase
    {
        private const string WearableEndpoint = "content/wearable_gaf";
        private const int UploadRequestTimeoutSeconds = 30;
        internal WearablesEndpoint(GeniesContentAPI api) : base(api) { }

        public async Task<WearableConfig> GetConfigAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureConnectedAsync(cancellationToken);
                var args = new { };
                var convexResult = await Client.QueryAsync<ConvexValue>($"{WearableEndpoint}:getConfig", args, cancellationToken);

                return ConvertToWearableConfig(convexResult);
            }
            catch (ConvexError ex)
            {
                var errorCode = ExtractErrorCode(ex);
                throw new InvalidOperationException(FormatErrorMessage("Get wearable config", ex.Message, errorCode), ex);
            }
            catch (ConvexExecutionException ex)
            {
                throw new InvalidOperationException(FormatErrorMessage("Get wearable config", ex.Message), ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unexpected error getting wearable config: {ex.Message}", ex);
            }
        }

        public async Task<WearableResult> CreateAsync(WearableData data, CancellationToken cancellationToken = default)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            try
            {
                await EnsureConnectedAsync(cancellationToken);
                var args = data;
                var convexResult = await Client.MutationAsync<ConvexValue>($"{WearableEndpoint}:createImages", args, cancellationToken);
                return ConvertToWearableResult(convexResult);
            }
            catch (ConvexError ex)
            {
                var errorCode = ExtractErrorCode(ex);
                throw new InvalidOperationException(FormatErrorMessage("Wearable creation", ex.Message, errorCode), ex);
            }
            catch (ConvexExecutionException ex)
            {
                throw new InvalidOperationException(FormatErrorMessage("Wearable creation", ex.Message), ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unexpected error creating wearable: {ex.Message}", ex);
            }
        }

        public async Task<string> GenerateUploadUrlAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureConnectedAsync(cancellationToken);
                var args = new { };
                var convexResult = await Client.MutationAsync<ConvexValue>($"{WearableEndpoint}:generateUploadUrl", args, cancellationToken);
                var uploadUrl = convexResult.AsString();
                if (string.IsNullOrWhiteSpace(uploadUrl))
                {
                    throw new InvalidOperationException("Expected upload URL string response for wearable upload");
                }
                return uploadUrl;
            }
            catch (ConvexError ex)
            {
                var errorCode = ExtractErrorCode(ex);
                throw new InvalidOperationException(FormatErrorMessage("Generate wearable upload URL", ex.Message, errorCode), ex);
            }
            catch (ConvexExecutionException ex)
            {
                throw new InvalidOperationException(FormatErrorMessage("Generate wearable upload URL", ex.Message), ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unexpected error generating wearable upload URL: {ex.Message}", ex);
            }
        }

        public async Task ConfirmUploadAsync(string storageId, string fileName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(storageId))
            {
                throw new ArgumentException("Storage ID cannot be null or empty.", nameof(storageId));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));
            }

            try
            {
                await EnsureConnectedAsync(cancellationToken);
                var args = new { storageId, fileName };
                await Client.MutationAsync<ConvexValue>($"{WearableEndpoint}:confirmUpload", args, cancellationToken);
            }
            catch (ConvexError ex)
            {
                var errorCode = ExtractErrorCode(ex);
                throw new InvalidOperationException(FormatErrorMessage("Confirm wearable upload", ex.Message, errorCode), ex);
            }
            catch (ConvexExecutionException ex)
            {
                throw new InvalidOperationException(FormatErrorMessage("Confirm wearable upload", ex.Message), ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unexpected error confirming wearable upload: {ex.Message}", ex);
            }
        }

        public async Task<string> UploadWearableReferenceAsync(
            byte[] bytes,
            string contentType,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new ArgumentException("Image bytes cannot be null or empty.", nameof(bytes));
            }

            if (string.IsNullOrWhiteSpace(contentType))
            {
                throw new ArgumentException("Content type cannot be null or empty.", nameof(contentType));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));
            }

            try
            {
                var uploadUrl = await GenerateUploadUrlAsync(cancellationToken);

                using var req = new UnityWebRequest(uploadUrl, "POST");
                req.uploadHandler = new UploadHandlerRaw(bytes);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = UploadRequestTimeoutSeconds;
                req.SetRequestHeader("Content-Type", contentType);

                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        req.Abort();
                        throw new OperationCanceledException(cancellationToken);
                    }
                    await Task.Yield();
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    throw new InvalidOperationException($"Upload failed: {req.responseCode} {req.error} {req.downloadHandler.text}");
                }

                var uploadResponse = JsonUtility.FromJson<UploadResponse>(req.downloadHandler.text);
                if (string.IsNullOrWhiteSpace(uploadResponse?.storageId))
                {
                    throw new InvalidOperationException("Upload response missing storageId");
                }

                await ConfirmUploadAsync(uploadResponse.storageId, fileName, cancellationToken);

                return uploadResponse.storageId;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unexpected error uploading wearable reference image: {ex.Message}", ex);
            }
        }

        public async Task<WearableModelResult> CreateModelAsync(
            string assetId,
            string imageId,
            int? polycount = null,
            string meshMode = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(assetId))
            {
                throw new ArgumentException("Asset ID cannot be null or empty.", nameof(assetId));
            }

            if (string.IsNullOrWhiteSpace(imageId))
            {
                throw new ArgumentException("Image ID cannot be null or empty.", nameof(imageId));
            }

            try
            {
                await EnsureConnectedAsync(cancellationToken);
                var args = new Dictionary<string, object>
                {
                    { "assetId", assetId },
                    { "imageId", imageId }
                };
                if (polycount.HasValue)
                {
                    args["polycount"] = polycount.Value;
                }
                if (!string.IsNullOrWhiteSpace(meshMode))
                {
                    args["meshMode"] = meshMode;
                }
                var convexResult = await Client.MutationAsync<ConvexValue>($"{WearableEndpoint}:createModel", args, cancellationToken);

                return ConvertToWearableModelResult(convexResult);
            }
            catch (ConvexError ex)
            {
                var errorCode = ExtractErrorCode(ex);
                throw new InvalidOperationException(FormatErrorMessage("Wearable model creation", ex.Message, errorCode), ex);
            }
            catch (ConvexExecutionException ex)
            {
                throw new InvalidOperationException(FormatErrorMessage("Wearable model creation", ex.Message), ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unexpected error creating wearable model: {ex.Message}", ex);
            }
        }

        public async Task<WearableModelResult> CreateGameAssetAsync(
            string assetId,
            string modelId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(assetId))
            {
                throw new ArgumentException("Asset ID cannot be null or empty.", nameof(assetId));
            }

            if (string.IsNullOrWhiteSpace(modelId))
            {
                throw new ArgumentException("Model ID cannot be null or empty.", nameof(modelId));
            }

            try
            {
                await EnsureConnectedAsync(cancellationToken);
                var userId = "user";
                var args = new { userId, assetId, modelId };
                var convexResult = await Client.MutationAsync<ConvexValue>($"{WearableEndpoint}:createGameAsset", args, cancellationToken);

                return ConvertToWearableModelResult(convexResult);
            }
            catch (ConvexError ex)
            {
                var errorCode = ExtractErrorCode(ex);
                throw new InvalidOperationException(FormatErrorMessage("Wearable game asset creation", ex.Message, errorCode), ex);
            }
            catch (ConvexExecutionException ex)
            {
                throw new InvalidOperationException(FormatErrorMessage("Wearable game asset creation", ex.Message), ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unexpected error creating wearable game asset: {ex.Message}", ex);
            }
        }

        public async Task<GameAssetResult> CreateInventoryGameAssetAsync(
            string assetId,
            string modelId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(assetId))
            {
                throw new ArgumentException("Asset ID cannot be null or empty.", nameof(assetId));
            }

            if (string.IsNullOrWhiteSpace(modelId))
            {
                throw new ArgumentException("Model ID cannot be null or empty.", nameof(modelId));
            }

            try
            {
                await EnsureConnectedAsync(cancellationToken);
                var args = new { assetId, modelId };
                var convexResult = await Client.MutationAsync<ConvexValue>($"{WearableEndpoint}:createGameAsset", args, cancellationToken);

                return ConvertToGameAssetResult(convexResult);
            }
            catch (ConvexError ex)
            {
                var errorCode = ExtractErrorCode(ex);
                throw new InvalidOperationException(FormatErrorMessage("Game asset creation", ex.Message, errorCode), ex);
            }
            catch (ConvexExecutionException ex)
            {
                throw new InvalidOperationException(FormatErrorMessage("Game asset creation", ex.Message), ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unexpected error creating game asset: {ex.Message}", ex);
            }
        }

        private WearableResult ConvertToWearableResult(ConvexValue convexValue)
        {
            if (convexValue is ConvexValue.Object obj)
            {
                var result = new WearableResult
                {
                    assetId = ConvexValueReader.GetStringValue(obj, "assetId"),
                    jobId = ConvexValueReader.GetStringValue(obj, "jobId")
                };

                Logger.Info($"[GeniesContentAPI] Wearable creation result: assetId={result.assetId}, jobId={result.jobId}");

                return result;
            }

            throw new InvalidOperationException("Expected Convex object response for wearable creation");
        }

        private WearableModelResult ConvertToWearableModelResult(ConvexValue convexValue)
        {
            if (convexValue is ConvexValue.Object obj)
            {
                var result = new WearableModelResult
                {
                    assetId = ConvexValueReader.GetStringValue(obj, "assetId"),
                    jobId = ConvexValueReader.GetStringValue(obj, "jobId")
                };

                Logger.Info($"[GeniesContentAPI] Wearable model creation result: assetId={result.assetId}, jobId={result.jobId}");

                return result;
            }

            throw new InvalidOperationException("Expected Convex object response for wearable model creation");
        }

        private GameAssetResult ConvertToGameAssetResult(ConvexValue convexValue)
        {
            if (convexValue is ConvexValue.Object obj)
            {
                var result = new GameAssetResult
                {
                    jobId = ConvexValueReader.GetStringValue(obj, "jobId"),
                    modelId = ConvexValueReader.GetStringValue(obj, "modelId")
                };

                Logger.Info($"[GeniesContentAPI] Game asset creation result: jobId={result.jobId}, modelId={result.modelId}");

                return result;
            }

            throw new InvalidOperationException("Expected Convex object response for game asset creation");
        }

        private WearableConfig ConvertToWearableConfig(ConvexValue convexValue)
        {
            if (convexValue is ConvexValue.Object obj)
            {
                var config = new WearableConfig();

                if (obj.TryGetValue("types", out var typesValue) && typesValue is ConvexValue.Array typesArray)
                {
                    var typesList = new List<WearableType>();
                    foreach (var item in typesArray.Value)
                    {
                        if (item is ConvexValue.Object typeObj)
                        {
                            var wearableType = new WearableType
                            {
                                type = ConvexValueReader.GetStringValue(typeObj, "type"),
                                name = ConvexValueReader.GetStringValue(typeObj, "name")
                            };
                            typesList.Add(wearableType);
                        }
                    }
                    config.types = typesList.ToArray();
                }

                Logger.Info($"[GeniesContentAPI] Wearable config loaded: {config.types.Length} wearable types");

                return config;
            }

            throw new InvalidOperationException("Expected Convex object response for wearable config");
        }

        [Serializable]
        private sealed class UploadResponse
        {
            public string storageId;
        }
    }
}
