using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Genies.ArtForge.ContentAPI.Models;
using Genies.Convex.Exceptions;
using Genies.Convex.Values;
using UnityEngine;

namespace Genies.ArtForge.ContentAPI
{
    internal sealed class AssetsEndpoint : ContentEndpointBase
    {
        private const string AssetImagesEndpoint = "model/imageAsset:getAssetImagesForOwner";
        private const string AssetModelEndpoint = "model/modelAsset:getModelForAssetOwner";
        internal AssetsEndpoint(GeniesContentAPI api) : base(api) { }

        public async Task<List<AssetImage>> GetImagesAsync(string assetId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(assetId))
            {
                throw new ArgumentException("Asset ID cannot be null or empty.", nameof(assetId));
            }

            try
            {
                await EnsureConnectedAsync(cancellationToken);
                var args = new { assetId };
                var convexResult = await ExecuteAssetOwnerEndpointAsync(AssetImagesEndpoint, args, cancellationToken);

                return ConvertToAssetImageList(convexResult);
            }
            catch (ConvexError ex)
            {
                var errorCode = ExtractErrorCode(ex);
                throw new InvalidOperationException(FormatErrorMessage("Get asset images", ex.Message, errorCode), ex);
            }
            catch (ConvexExecutionException ex)
            {
                throw new InvalidOperationException(FormatErrorMessage("Get asset images", ex.Message), ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unexpected error getting asset images: {ex.Message}", ex);
            }
        }

        public async Task<AssetModel> GetModelAsync(string assetId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(assetId))
            {
                throw new ArgumentException("Asset ID cannot be null or empty.", nameof(assetId));
            }

            try
            {
                await EnsureConnectedAsync(cancellationToken);
                var args = new { assetId };
                var convexResult = await ExecuteAssetOwnerEndpointAsync(AssetModelEndpoint, args, cancellationToken);

                return ConvertToAssetModel(convexResult);
            }
            catch (ConvexError ex)
            {
                var errorCode = ExtractErrorCode(ex);
                throw new InvalidOperationException(FormatErrorMessage("Get asset model", ex.Message, errorCode), ex);
            }
            catch (ConvexExecutionException ex)
            {
                throw new InvalidOperationException(FormatErrorMessage("Get asset model", ex.Message), ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unexpected error getting asset model: {ex.Message}", ex);
            }
        }

        private List<AssetImage> ConvertToAssetImageList(ConvexValue convexValue)
        {
            var images = new List<AssetImage>();

            if (convexValue is ConvexValue.Object obj)
            {
                if (obj.TryGetValue("approvedImages", out var approvedImagesValue) &&
                    approvedImagesValue is ConvexValue.Array arrayValue)
                {
                    foreach (var item in arrayValue.Value)
                    {
                        if (item is ConvexValue.Object imageObj)
                        {
                            var image = new AssetImage
                            {
                                imageId = ConvexValueReader.GetStringValue(imageObj, "imageId"),
                                imageUrl = ConvexValueReader.GetStringValue(imageObj, "url"),
                                width = ConvexValueReader.GetIntValue(imageObj, "width"),
                                height = ConvexValueReader.GetIntValue(imageObj, "height")
                            };
                            images.Add(image);
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException("Response missing 'approvedImages' array");
                }
            }
            else
            {
                throw new InvalidOperationException("Expected Convex object response for asset images (with approvedImages and blockedImages)");
            }

            return images;
        }

        private AssetModel ConvertToAssetModel(ConvexValue convexValue)
        {
            if (convexValue is ConvexValue.Object obj)
            {
                var model = new AssetModel
                {
                    modelId = ConvexValueReader.GetStringValue(obj, "modelId"),
                    modelUrl = ConvexValueReader.GetStringValue(obj, "url"),
                    format = ConvexValueReader.GetStringValue(obj, "format")
                };

                Logger.Info($"[GeniesContentAPI] Asset model retrieved: modelId={model.modelId}, format={model.format}");

                return model;
            }

            throw new InvalidOperationException("Expected Convex object response for asset model");
        }

        private Task<ConvexValue> ExecuteAssetOwnerEndpointAsync(
            string endpointName,
            object args,
            CancellationToken cancellationToken)
        {
            if (Api.UseLegacyAssetOwnerQueries)
            {
                return Client.QueryAsync<ConvexValue>(endpointName, args, cancellationToken);
            }

            return Client.ActionAsync<ConvexValue>(endpointName, args, cancellationToken);
        }
    }
}
