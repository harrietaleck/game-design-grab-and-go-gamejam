using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;
using Genies.ArtForge.ContentAPI.Models;
using Genies.Sdk.ArtForge.Editor.Core.Application.Services;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Helpers;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Processing;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;
using UnityEngine;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.API
{
    /// <summary>
    /// Orchestrates asset generation workflows using the Genies Backend API.
    /// Integrates with the existing GenerationService and QueueService architecture.
    /// </summary>
    internal class AssetOrchestrator : IDisposable
    {
        private const float UploadStartProgress = 0.05f;
        private const float UploadCompleteProgress = 0.1f;
        private const float BackendSubmitAcceptedProgress = 0.11f;
        private const float ProgressPercentScale = 100f;
        private const int DownloadPollDelayMs = 100;
        private const string Decor2DType = "2d_image";
        private const string DefaultAspectRatio = "1:1";

        private readonly ContentClient _client;
        private readonly ILogger _logger;

        private bool _isDisposed;

        /// <summary>
        /// Creates a new BackendAssetOrchestrator
        /// </summary>
        /// <param name="client">Backend client for API communication</param>
        /// <param name="logger">Logger for diagnostics</param>
        public AssetOrchestrator(
            ContentClient client,
            ILogger logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Generates an asset using the backend API
        /// </summary>
        /// <param name="job">Job configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Generation result with file paths</returns>
        public async Task<GenerationResult> GenerateAssetAsync(
            Job job,
            CancellationToken cancellationToken = default)
        {
            if (!_client.IsAuthenticated)
            {
                _logger.Warning($"[BackendOrchestrator] Blocking generation: backend client is not authenticated (isConnected={_client.IsConnected}).");
                return new GenerationResult
                {
                    Success = false,
                    ErrorMessage = "Backend client is not authenticated. Please authenticate first in Settings."
                };
            }

            try
            {
                _logger.Info($"[BackendOrchestrator] Starting backend generation for: {job.AssetName.Value}");

                // Check if this is post-approval (image already confirmed, need to create model)
                if (job.ImageConfirmed && !string.IsNullOrEmpty(job.BackendAssetId))
                {
                    string approvedCategory;
                    string approvedSpecificType;
                    ParseBackendAssetType(job.AssetTypeId, out approvedCategory, out approvedSpecificType);
                    if (IsDecor2DType(approvedCategory, approvedSpecificType))
                    {
                        _logger.Info("[BackendOrchestrator] Decor type is 2D - skipping 3D model generation after approval.");
                        return BuildApprovalOnlyCompletionResult(job);
                    }

                    _logger.Info($"[BackendOrchestrator] Image already confirmed, creating 3D model...");
                    return await CreateModelAndTexturesAsync(job, cancellationToken);
                }

                // Step 1: Create asset based on type (image generation phase)
                string assetId;
                string jobId;

                // Parse backend asset type from job
                string assetCategory;
                string specificType;
                ParseBackendAssetType(job.AssetTypeId, out assetCategory, out specificType);

                _logger.Info($"[BackendOrchestrator] Category: {assetCategory}, Type: {specificType}");

                // Create asset based on category
                if (assetCategory == AssetCategories.Decor)
                {
                    string decorType = specificType;
                    _logger.Info(
                        $"[BackendOrchestrator] Creating decor: {job.Prompt} (Type: {(string.IsNullOrWhiteSpace(decorType) ? "unspecified" : decorType)})");
                    string[] referenceImageIds = null;
                    var quality = job?.QualitySettings;
                    int? polycount = quality != null && quality.Polycount > 0 ? quality.Polycount : null;
                    string meshMode = NormalizeMeshMode(quality?.TopologyType);
                    string aspectRatio = null;
                    string imageSize = null;
                    string decorSubcategory = string.IsNullOrWhiteSpace(job?.BackendSubcategory)
                        ? null
                        : job.BackendSubcategory.Trim();
                    if (IsDecor2DType(assetCategory, decorType))
                    {
                        aspectRatio = NormalizeAspectRatio(quality?.AspectRatio);
                        imageSize = BuildImageSize(quality?.TextureResolution, aspectRatio);
                    }
                    if (job.ReferenceImagePaths != null && job.ReferenceImagePaths.Count > 0)
                    {
                        job.UpdateProgress(UploadStartProgress, "Uploading reference images...", "upload");
                        _logger.Info($"[BackendOrchestrator] Uploading {job.ReferenceImagePaths.Count} decor reference image(s)...");
                        referenceImageIds = await _client.UploadWearableReferenceImagesAsync(
                            job.ReferenceImagePaths,
                            cancellationToken);
                        job.UpdateProgress(UploadCompleteProgress, "Reference images uploaded", "upload");
                    }

                    (assetId, jobId) = await _client.CreateDecorAsync(
                        job.Prompt,
                        referenceImageIds,
                        polycount,
                        meshMode,
                        decorType,
                        aspectRatio,
                        imageSize,
                        cancellationToken: cancellationToken,
                        subcategory: decorSubcategory);
                }
                else // Wearable (default)
                {
                    string wearableType = specificType ?? "Accessory";
                    _logger.Info($"[BackendOrchestrator] Creating wearable: {job.Prompt} (Type: {wearableType})");
                    string[] referenceImageIds = null;
                    if (job.ReferenceImagePaths != null && job.ReferenceImagePaths.Count > 0)
                    {
                        job.UpdateProgress(UploadStartProgress, "Uploading reference images...", "upload");
                        _logger.Info($"[BackendOrchestrator] Uploading {job.ReferenceImagePaths.Count} reference image(s)...");
                        referenceImageIds = await _client.UploadWearableReferenceImagesAsync(
                            job.ReferenceImagePaths,
                            cancellationToken);
                        job.UpdateProgress(UploadCompleteProgress, "Reference images uploaded", "upload");
                    }

                    (assetId, jobId) = await _client.CreateWearableAsync(
                        job.Prompt,
                        wearableType,
                        referenceImageIds,
                        job.DisplayName,
                        cancellationToken
                    );
                }

                _logger.Info($"[BackendOrchestrator] Asset created - ID: {assetId}, Job: {jobId}");
                job.UpdateProgress(BackendSubmitAcceptedProgress, "Backend job submitted. Waiting for processing...", "backend_submitted");

                // Step 2: Monitor job progress with real-time updates
                var finalStatus = await _client.SubscribeAndWaitForJobAsync(
                    jobId,
                    status =>
                    {
                        // Update job progress with step information
                        string stepMessage = JobStepFormatter.GetProgressMessage(status.step, status.progress);
                        job.UpdateProgress(status.progress, stepMessage, status.step);
                        _logger.Info($"[BackendOrchestrator] Progress: {status.progress * ProgressPercentScale:F1}% - {status.step}");
                    },
                    cancellationToken
                );

                if (finalStatus.IsFailed())
                {
                    // Use the actual error message from the server if available
                    string errorMessage = "Job failed";
                    if (!string.IsNullOrEmpty(finalStatus.error))
                    {
                        errorMessage = finalStatus.error;
                    }
                    else if (!string.IsNullOrEmpty(finalStatus.step))
                    {
                        errorMessage = $"Failed during {JobStepFormatter.GetErrorContextName(finalStatus.step)}";
                    }

                    return new GenerationResult
                    {
                        Success = false,
                        ErrorMessage = errorMessage
                    };
                }

                // Step 3: Retrieve generated images
                var images = await _client.GetAssetImagesAsync(assetId);

                if (images == null || images.Count == 0)
                {
                    return new GenerationResult
                    {
                        Success = false,
                        ErrorMessage = "No images generated by backend"
                    };
                }

                _logger.Info($"[BackendOrchestrator] Retrieved {images.Count} images from backend");
                bool isDecor2DJob = IsDecor2DType(assetCategory, specificType);
                bool shouldProcessDecor2DImages = isDecor2DJob && job != null && job.GenerateTransparent;
                if (isDecor2DJob)
                {
                    _logger.Info($"[BackendOrchestrator] Decor 2D transparency processing is {(shouldProcessDecor2DImages ? "enabled" : "disabled")} for this job.");
                }

                // Download ALL images (typically 4) and store them as versions for the approval dialog
                // This matches the behavior in the Runtime GeniesContentClient example
                string tempDir = Path.Combine(Path.GetTempPath(), "GeniesArtForge", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                // Create the History folder in the asset's RefImages directory
                string assetName = job.AssetName.Value;
                string historyFolder = BrandingConstants.AssetPaths.GetRefImagesHistoryFolder(assetName);
                string fullHistoryFolder = Path.Combine(UnityEngine.Application.dataPath, "..", historyFolder);
                Directory.CreateDirectory(fullHistoryFolder);
                _logger.Info($"[BackendOrchestrator] Created history folder: {fullHistoryFolder}");

                string firstImagePath = null;

                _logger.Info($"[BackendOrchestrator] Downloading {images.Count} images from backend...");

                for (int i = 0; i < images.Count; i++)
                {
                    var image = images[i];
                    string tempImagePath = Path.Combine(tempDir, $"ref_image_{i:D2}.png");
                    string tempOriginalImagePath = null;

                    _logger.Info($"[BackendOrchestrator] Downloading image {i + 1}/{images.Count} from: {image.imageUrl}");
                    bool downloadSuccess = await DownloadFileAsync(image.imageUrl, tempImagePath, cancellationToken);

                    if (!downloadSuccess || !File.Exists(tempImagePath))
                    {
                        _logger.Warning($"[BackendOrchestrator] Failed to download image {i + 1}/{images.Count}");
                        continue;
                    }

                    _logger.Info($"[BackendOrchestrator] ✓ Downloaded image {i + 1}/{images.Count} to: {tempImagePath}");
                    if (shouldProcessDecor2DImages)
                    {
                        if (ProcessDecor2DImageTransparency(tempImagePath, out tempOriginalImagePath))
                        {
                            _logger.Info($"[BackendOrchestrator] ✓ Removed magenta background for image {i + 1}/{images.Count}");
                        }
                        else
                        {
                            _logger.Warning($"[BackendOrchestrator] Could not remove magenta background for image {i + 1}/{images.Count}; using original download");
                        }
                    }

                    // Copy to History folder with correct naming convention
                    int iterationNumber = i; // 0, 1, 2, 3
                    string historyFileName = $"{assetName}_ref_{iterationNumber:D2}.png";
                    string historyImagePath = Path.Combine(historyFolder, historyFileName);
                    string fullHistoryImagePath = Path.Combine(fullHistoryFolder, historyFileName);

                    try
                    {
                        File.Copy(tempImagePath, fullHistoryImagePath, true);
                        _logger.Info($"[BackendOrchestrator] ✓ Copied image to history: {historyImagePath}");

                        if (shouldProcessDecor2DImages &&
                            !string.IsNullOrWhiteSpace(tempOriginalImagePath) &&
                            File.Exists(tempOriginalImagePath))
                        {
                            string historyOriginalFileName = $"{assetName}_ref_{iterationNumber:D2}_original.png";
                            string historyOriginalImagePath = Path.Combine(historyFolder, historyOriginalFileName);
                            string fullHistoryOriginalImagePath = Path.Combine(fullHistoryFolder, historyOriginalFileName);
                            File.Copy(tempOriginalImagePath, fullHistoryOriginalImagePath, true);
                            _logger.Info($"[BackendOrchestrator] ✓ Copied original image to history: {historyOriginalImagePath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[BackendOrchestrator] Failed to copy image to history: {ex.Message}");
                        continue;
                    }

                    // Use the first successfully downloaded image as the primary reference
                    if (firstImagePath == null)
                    {
                        firstImagePath = tempImagePath; // Keep temp path for immediate use

                        // Store backend metadata in the job for later use
                        _logger.Info($"[BackendOrchestrator] Storing backend metadata - AssetID: {assetId}, ImageID: {image.imageId}");
                        job.BackendAssetId = assetId;
                        job.BackendImageId = image.imageId; // Will be updated by ApprovalReviewDialog if user selects different version
                    }

                    // Add each image as a version to the job
                    // Use the history path (not temp path) so ApprovalReviewDialog can find them
                    var imageVersion = new ImageVersion
                    {
                        Iteration = iterationNumber,
                        ImagePath = historyImagePath, // Use project path, not temp path
                        Prompt = job.Prompt,
                        GeneratedAt = DateTime.Now,
                        BackendImageId = image.imageId // Store backend image ID for later model creation
                    };
                    job.ImageVersions.Add(imageVersion);
                    _logger.Info($"[BackendOrchestrator] Added image version {iterationNumber} at {historyImagePath} with BackendImageId: {image.imageId}");
                }

                if (firstImagePath == null)
                {
                    return new GenerationResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to download any images from backend"
                    };
                }

                _logger.Info($"[BackendOrchestrator] Successfully downloaded and stored {job.ImageVersions.Count} images in history (versions 0-{job.ImageVersions.Count - 1})");

                // Return partial result for image approval workflow
                // The QueueService will handle showing the ApprovalReviewDialog
                // After approval, the workflow continues in CreateModelAsync
                var result = new GenerationResult
                {
                    Success = true,
                    RequiresApproval = true,
                    ReferenceImagePath = firstImagePath, // Use first image as default

                    // Store backend-specific data in custom properties
                    // These will be needed after approval to create the 3D model
                    // Note: GenerationResult doesn't have BackendAssetId/BackendImageId properties
                    // so we'll need to track these separately or add them to the Job
                };

                _logger.Info("[BackendOrchestrator] ✓ Image generation complete, awaiting approval (user can navigate between versions)");

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Warning($"[BackendOrchestrator] Generation cancelled for: {job.AssetName.Value}");
                return new GenerationResult
                {
                    Success = false,
                    ErrorMessage = "Generation cancelled by user"
                };
            }
            catch (Exception ex)
            {
                _logger.Error($"[BackendOrchestrator] Generation failed: {ex.Message}");
                return new GenerationResult
                {
                    Success = false,
                    ErrorMessage = $"Backend generation error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Downloads a file from a URL using UnityWebRequest
        /// </summary>
        /// <param name="url">Source URL</param>
        /// <param name="destinationPath">Destination file path</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if download succeeded</returns>
        private async Task<bool> DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Info($"[BackendOrchestrator] Downloading file from: {url}");

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

                using (var request = UnityWebRequest.Get(url))
                {
                    // Send the request
                    var operation = request.SendWebRequest();

                    // Wait for completion with cancellation support
                    while (!operation.isDone)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            request.Abort();
                            _logger.Warning("[BackendOrchestrator] File download cancelled");
                            return false;
                        }
                        await Task.Delay(DownloadPollDelayMs, cancellationToken);
                    }

                    // Check for errors
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        _logger.Error($"[BackendOrchestrator] File download failed: {request.error}");
                        return false;
                    }

                    // Save the downloaded data
                    File.WriteAllBytes(destinationPath, request.downloadHandler.data);
                    _logger.Info($"[BackendOrchestrator] ✓ File downloaded successfully ({request.downloadHandler.data.Length} bytes)");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[BackendOrchestrator] File download failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Downloads an asset image from the backend (public API for external use)
        /// </summary>
        /// <param name="imageUrl">URL of the image</param>
        /// <param name="destinationPath">Local destination path</param>
        /// <returns>True if download succeeded</returns>
        public async Task<bool> DownloadImageAsync(string imageUrl, string destinationPath)
        {
            return await DownloadFileAsync(imageUrl, destinationPath);
        }

        private bool ProcessDecor2DImageTransparency(string imagePath, out string originalImagePath)
        {
            originalImagePath = null;
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return false;
            }

            Texture2D sourceTexture = null;
            Texture2D processedTexture = null;

            try
            {
                string directory = Path.GetDirectoryName(imagePath) ?? string.Empty;
                string fileName = Path.GetFileNameWithoutExtension(imagePath);
                string extension = Path.GetExtension(imagePath);
                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = ".png";
                }

                originalImagePath = Path.Combine(directory, $"{fileName}_original{extension}");
                File.Copy(imagePath, originalImagePath, true);

                byte[] imageData = File.ReadAllBytes(imagePath);
                sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!sourceTexture.LoadImage(imageData, false))
                {
                    return false;
                }

                var settings = new TransparencyProcessor.Settings
                {
                    manualBackgroundColor = Color.magenta
                };

                var result = TransparencyProcessor.Process(sourceTexture, settings);
                processedTexture = result?.texture;
                if (processedTexture == null)
                {
                    return false;
                }

                _logger.Info($"[BackendOrchestrator] Decor 2D auto-threshold applied tolerance: {result.appliedTolerance:F3}");

                byte[] processedBytes = processedTexture.EncodeToPNG();
                if (processedBytes == null || processedBytes.Length == 0)
                {
                    return false;
                }

                File.WriteAllBytes(imagePath, processedBytes);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Warning($"[BackendOrchestrator] Decor 2D transparency processing failed: {ex.Message}");
                return false;
            }
            finally
            {
                if (sourceTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(sourceTexture);
                }

                if (processedTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(processedTexture);
                }
            }
        }

        /// <summary>
        /// Parses the backend asset type from the job's AssetTypeId
        /// Formats:
        ///   - Wearable: "Wearable|Helmet"
        ///   - Decor: "Decor|" or "Decor|Chair"
        /// </summary>
        private void ParseBackendAssetType(string assetTypeId,
            out string category,
            out string specificType)
        {
            category = AssetCategories.Wearable; // Default
            specificType = null;

            if (string.IsNullOrEmpty(assetTypeId))
            {
                return;
            }

            // Check if it's in the format "Category|SpecificType|..."
            if (assetTypeId.Contains(AssetCategories.Delimiter))
            {
                var parts = assetTypeId.Split(AssetCategories.Delimiter[0]);
                category = parts[0];

                if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
                {
                    // Wearable or Decor format: "Wearable|Helmet", "Decor|", or "Decor|Chair"
                    specificType = parts[1];
                }
            }
            else
            {
                // Legacy format or simple type - try to infer
                string typeLower = assetTypeId.ToLowerInvariant();

                if (typeLower == "decor" || typeLower == "decoration" || typeLower == "prop")
                {
                    category = AssetCategories.Decor;
                }
                else
                {
                    // Assume it's a wearable type
                    category = AssetCategories.Wearable;
                    specificType = assetTypeId;
                }
            }
        }

        private static bool IsDecor2DType(string category, string specificType)
        {
            if (!string.Equals(category, AssetCategories.Decor, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(specificType))
            {
                return false;
            }

            string normalized = specificType.Trim()
                .Replace("_", "")
                .Replace("-", "")
                .Replace(" ", "")
                .ToLowerInvariant();

            return normalized == "2d" || normalized == "2dimage" ||
                   string.Equals(specificType.Trim(), Decor2DType, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAspectRatio(string aspectRatio)
        {
            return string.IsNullOrWhiteSpace(aspectRatio) ? DefaultAspectRatio : aspectRatio.Trim();
        }

        private static string BuildImageSize(string textureResolution, string aspectRatio)
        {
            if (!int.TryParse(textureResolution, out int maxSide) || maxSide <= 0)
            {
                return null;
            }

            if (!TryParseAspectRatio(aspectRatio, out int ratioWidth, out int ratioHeight))
            {
                ratioWidth = 1;
                ratioHeight = 1;
            }

            int width;
            int height;
            if (ratioWidth >= ratioHeight)
            {
                width = maxSide;
                height = Math.Max(1, (int)Math.Round(maxSide * (double)ratioHeight / ratioWidth));
            }
            else
            {
                height = maxSide;
                width = Math.Max(1, (int)Math.Round(maxSide * (double)ratioWidth / ratioHeight));
            }

            return $"{width}x{height}";
        }

        private static bool TryParseAspectRatio(string aspectRatio, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (string.IsNullOrWhiteSpace(aspectRatio))
            {
                return false;
            }

            string[] parts = aspectRatio.Split(':');
            if (parts.Length != 2)
            {
                return false;
            }

            return int.TryParse(parts[0].Trim(), out width) &&
                   int.TryParse(parts[1].Trim(), out height) &&
                   width > 0 &&
                   height > 0;
        }

        private GenerationResult BuildApprovalOnlyCompletionResult(Job job)
        {
            string referenceImagePath = BrandingConstants.AssetPaths.GetReferenceImagePath(job.AssetName.Value);
            string fullReferenceImagePath = Path.Combine(UnityEngine.Application.dataPath, "..", referenceImagePath);

            return new GenerationResult
            {
                Success = true,
                ReferenceImagePath = File.Exists(fullReferenceImagePath) ? fullReferenceImagePath : null
            };
        }

        /// <summary>
        /// Creates 3D model and downloads textures after image approval
        /// </summary>
        /// <param name="job">Job with confirmed image</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Complete generation result with model and texture paths</returns>
        private async Task<GenerationResult> CreateModelAndTexturesAsync(
            Job job,
            CancellationToken cancellationToken = default)
        {
            try
            {
                string assetId = job.BackendAssetId;
                string imageId = job.BackendImageId; // Use selected image ID (updated by ApprovalReviewDialog if user selected different version)
                string assetCategory;
                ParseBackendAssetType(job.AssetTypeId, out assetCategory, out _);

                _logger.Info($"[BackendOrchestrator] Creating 3D model - Asset: {assetId}, Image: {imageId}, Category: {assetCategory}");

                // Step 1: Create model
                string modelPath = await CreateModelAsync(job, assetId, imageId, assetCategory, cancellationToken);

                if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
                {
                    return new GenerationResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to create or download model"
                    };
                }

                _logger.Info($"[BackendOrchestrator] ✓ Model downloaded: {modelPath}");

                // Step 2: Get and download textures
                // Backend models include textures in the GLB, but we may also need separate texture files
                // For now, we'll extract them from the model or download separately if available
                var textures = await GetAndDownloadTexturesAsync(assetId, cancellationToken);

                _logger.Info($"[BackendOrchestrator] ✓ Downloaded {textures.Count} textures");

                // Step 3: Return complete result
                var result = new GenerationResult
                {
                    Success = true,
                    ModelPath = modelPath,
                    ReferenceImagePath = null, // Already imported during approval phase
                    AlbedoTexturePath = textures.ContainsKey("albedo") ? textures["albedo"] : null,
                    NormalTexturePath = textures.ContainsKey("normal") ? textures["normal"] : null,
                    MetallicTexturePath = textures.ContainsKey("metallic") ? textures["metallic"] : null,
                    RoughnessTexturePath = textures.ContainsKey("roughness") ? textures["roughness"] : null,
                    RequiresApproval = false
                };

                _logger.Info("[BackendOrchestrator] ✓ Model and textures ready for import");

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"[BackendOrchestrator] Model creation failed: {ex.Message}");
                return new GenerationResult
                {
                    Success = false,
                    ErrorMessage = $"Model creation failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Downloads texture files from backend (if available)
        /// </summary>
        private async Task<Dictionary<string, string>> GetAndDownloadTexturesAsync(
            string assetId,
            CancellationToken cancellationToken = default)
        {
            var textures = new Dictionary<string, string>();

            try
            {
                // Get model info which may include texture URLs
                var model = await _client.GetAssetModelAsync(assetId);

                // Backend may provide texture URLs in the model response
                // For now, textures are typically embedded in GLB, but we'll check for separate files
                // TODO: Check if backend API provides separate texture URLs
                // If yes, download them here

                _logger.Info($"[BackendOrchestrator] Model info retrieved, textures may be embedded in GLB");
            }
            catch (Exception ex)
            {
                _logger.Warning($"[BackendOrchestrator] Could not retrieve texture info: {ex.Message}");
            }

            return textures;
        }

        /// <summary>
        /// Creates a 3D model after image approval
        /// </summary>
        /// <param name="job">The job being processed (for progress updates)</param>
        /// <param name="assetId">Asset ID from image generation</param>
        /// <param name="imageId">Selected image ID</param>
        /// <param name="assetType">Type of asset (determines which model creation API to use)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Model file path</returns>
        public async Task<string> CreateModelAsync(
            Job job,
            string assetId,
            string imageId,
            string assetType,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Info($"[BackendOrchestrator] Creating 3D model - Asset: {assetId}, Image: {imageId}");

                var quality = job?.QualitySettings;
                int? polycount = quality != null && quality.Polycount > 0 ? quality.Polycount : null;
                string meshMode = NormalizeMeshMode(quality?.TopologyType);

                // If the client previously failed but the backend job is still alive,
                // reuse the original job instead of creating a duplicate.
                string modelJobId = job?.BackendModelGenerationJobId;
                JobStatus existingStatus = null;
                if (!string.IsNullOrEmpty(modelJobId))
                {
                    try
                    {
                        existingStatus = await _client.GetJobStatusAsync(modelJobId, cancellationToken);
                        bool canReuseExistingJob = existingStatus != null &&
                                                  (existingStatus.IsInProgress() ||
                                                   existingStatus.IsTimedOut() ||
                                                   existingStatus.IsCompleted());

                        if (!canReuseExistingJob)
                        {
                            _logger.Info($"[BackendOrchestrator] Previous model job {modelJobId} is terminal with status '{existingStatus?.status}', creating a new model job");
                            modelJobId = null;
                            existingStatus = null;
                        }
                        else
                        {
                            _logger.Info($"[BackendOrchestrator] Reusing existing model job {modelJobId} with status '{existingStatus.status}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning($"[BackendOrchestrator] Failed to query existing model job {modelJobId}: {ex.Message}. Creating a new model job.");
                        modelJobId = null;
                        existingStatus = null;
                    }
                }

                if (string.IsNullOrEmpty(modelJobId))
                {
                    modelJobId = await _client.CreateWearableModelAsync(
                        assetId,
                        imageId,
                        polycount,
                        meshMode);

                    if (job != null)
                    {
                        job.BackendModelGenerationJobId = modelJobId;

                        // Persist immediately so we can recover/resume after editor restart.
                        GeniesArtForgeService.Queue?.SaveJobState(job);
                    }
                }

                job?.UpdateProgress(
                    BackendSubmitAcceptedProgress,
                    "Backend job submitted. Waiting for processing...",
                    "backend_submitted");

                // Monitor model creation progress with job updates
                JobStatus finalStatus;
                if (existingStatus != null && existingStatus.IsCompleted())
                {
                    finalStatus = existingStatus;
                    string stepMessage = JobStepFormatter.GetProgressMessage(existingStatus.step, 1f);
                    job?.UpdateProgress(1f, stepMessage, existingStatus.step);
                }
                else
                {
                    finalStatus = await _client.SubscribeAndWaitForJobAsync(
                        modelJobId,
                        status =>
                        {
                            // Update job progress with step information for the queue UI
                            string stepMessage = JobStepFormatter.GetProgressMessage(status.step, status.progress);
                            job?.UpdateProgress(status.progress, stepMessage, status.step);
                            _logger.Info($"[BackendOrchestrator] Model creation: {status.progress * ProgressPercentScale:F1}% - {status.step}");
                        },
                        cancellationToken
                    );
                }

                if (finalStatus.IsFailed() || finalStatus.IsAborted())
                {
                    // Use actual error message from server if available
                    string errorMessage = "Model creation failed";
                    if (!string.IsNullOrEmpty(finalStatus.error))
                    {
                        errorMessage = finalStatus.error;
                    }
                    else if (!string.IsNullOrEmpty(finalStatus.step))
                    {
                        errorMessage = $"Model creation failed during {JobStepFormatter.GetErrorContextName(finalStatus.step)}";
                    }
                    throw new Exception(errorMessage);
                }

                return await DownloadGeneratedModelAsync(job, assetId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Error($"[BackendOrchestrator] Model creation failed: {ex.Message}");
                throw;
            }
        }

        private async Task<string> DownloadGeneratedModelAsync(
            Job job,
            string assetId,
            CancellationToken cancellationToken = default)
        {
            // Fetch model after backend progress pipeline reaches completion.
            var model = await _client.GetAssetModelAsync(assetId);

            if (model == null || string.IsNullOrEmpty(model.modelUrl))
            {
                throw new Exception("Model URL not available");
            }

            // Store model ID for GAP integration
            if (job != null && !string.IsNullOrEmpty(model.modelId))
            {
                job.BackendModelId = model.modelId;
            }

            _logger.Info($"[BackendOrchestrator] ✓ Model ready: {model.modelUrl}");

            // Download model file to temp location
            string tempModelPath = Path.Combine(Path.GetTempPath(), "GeniesArtForge", Guid.NewGuid().ToString(), "model.glb");
            Directory.CreateDirectory(Path.GetDirectoryName(tempModelPath));

            _logger.Info("[BackendOrchestrator] Downloading model file...");
            bool modelDownloadSuccess = await DownloadFileAsync(model.modelUrl, tempModelPath, cancellationToken);

            if (!modelDownloadSuccess || !File.Exists(tempModelPath))
            {
                throw new Exception("Failed to download model file from backend");
            }

            _logger.Info($"[BackendOrchestrator] Model downloaded to: {tempModelPath}");

            // Import model into Unity project
            // The AssetImporter will handle copying to the correct location
            // and setting up import settings
            _logger.Info("[BackendOrchestrator] Importing model into Unity project...");

            // Return the temp path - the caller (QueueService) will import it using AssetImporter
            return tempModelPath;
        }

        private static string NormalizeMeshMode(string topology)
        {
            if (string.IsNullOrWhiteSpace(topology))
            {
                return null;
            }

            if (string.Equals(topology, "Triangles", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(topology, "Triangle", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(topology, "Raw", StringComparison.OrdinalIgnoreCase))
            {
                return "Raw";
            }

            if (string.Equals(topology, "Quads", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(topology, "Quad", StringComparison.OrdinalIgnoreCase))
            {
                return "Quad";
            }

            return topology;
        }

        /// <summary>
        /// Disposes of resources
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _logger.Info("[BackendOrchestrator] Disposing resources...");
            _isDisposed = true;
        }
    }
}
