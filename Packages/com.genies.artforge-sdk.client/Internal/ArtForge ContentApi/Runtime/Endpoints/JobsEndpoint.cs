using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Genies.ArtForge.ContentAPI.Models;
using Genies.Convex.Core;
using Genies.Convex.Exceptions;
using Genies.Convex.Values;

namespace Genies.ArtForge.ContentAPI
{
    internal sealed class JobsEndpoint : ContentEndpointBase
    {
        private const string JobStatusEndpoint = "model/job:getJobStatusPublic";
        internal JobsEndpoint(GeniesContentAPI api) : base(api) { }

        public async Task<JobStatus> GetStatusAsync(string jobId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                throw new ArgumentException("Job ID cannot be null or empty.", nameof(jobId));
            }

            try
            {
                await EnsureConnectedAsync(cancellationToken);
                var args = new { jobId };
                var convexResult = await Client.QueryAsync<ConvexValue>(JobStatusEndpoint, args, cancellationToken);

                return ConvertToJobStatus(convexResult);
            }
            catch (ConvexError ex)
            {
                var errorCode = ExtractErrorCode(ex);
                throw new InvalidOperationException(FormatErrorMessage("Get job status", ex.Message, errorCode), ex);
            }
            catch (ConvexExecutionException ex)
            {
                throw new InvalidOperationException(FormatErrorMessage("Get job status", ex.Message), ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unexpected error getting job status: {ex.Message}", ex);
            }
        }

        public IQuerySubscription SubscribeToStatus(string jobId, Action<JobStatus> onUpdate, Action<Exception> onError)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                throw new ArgumentException("Job ID cannot be null or empty.", nameof(jobId));
            }

            if (onUpdate == null)
            {
                throw new ArgumentNullException(nameof(onUpdate));
            }

            if (onError == null)
            {
                throw new ArgumentNullException(nameof(onError));
            }

            try
            {
                // Best-effort lazy connect for subscriptions (no await in sync method)
                // Fire-and-forget to avoid blocking caller; subscription will buffer until connected
                _ = EnsureConnectedAsync(CancellationToken.None);
                var args = new { jobId };
                var subscription = Client.Subscribe(JobStatusEndpoint, args);

                subscription.OnNext(
                    result =>
                    {
                        try
                        {
                            var status = ConvertToJobStatus(result);
                            onUpdate(status);
                        }
                        catch (Exception ex)
                        {
                            onError(new InvalidOperationException($"Failed to parse job status: {ex.Message}", ex));
                        }
                    },
                    onError
                );

                return subscription;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to subscribe to asset status: {ex.Message}", ex);
            }
        }

        private JobStatus ConvertToJobStatus(ConvexValue convexValue)
        {
            if (convexValue is ConvexValue.Object obj)
            {
                var status = new JobStatus
                {
                    id = ConvexValueReader.GetStringValue(obj, "id"),
                    name = ConvexValueReader.GetStringValue(obj, "name"),
                    assetId = ConvexValueReader.GetStringValue(obj, "assetId"),
                    status = ConvexValueReader.GetStringValue(obj, "status"),
                    step = ConvexValueReader.GetStringValue(obj, "step"),
                    stepIndex = ConvexValueReader.GetNullableIntValue(obj, "stepIndex"),
                    totalSteps = ConvexValueReader.GetIntValue(obj, "totalSteps"),
                    progress = ConvexValueReader.GetFloatValue(obj, "progress"),
                    totalEstimatedMs = ConvexValueReader.GetNullableLongValue(obj, "totalEstimatedMs"),
                    startedAt = ConvexValueReader.GetLongValue(obj, "startedAt"),
                    workflow = ConvexValueReader.GetStringValue(obj, "workflow"),
                    error = ConvexValueReader.GetStringValue(obj, "error"),
                    errorCode = ConvexValueReader.GetStringValue(obj, "errorCode"),
                    steps = ConvertToJobSteps(obj)
                };

                return status;
            }

            throw new InvalidOperationException("Expected Convex object response for job status");
        }

        private JobStep[] ConvertToJobSteps(ConvexValue.Object obj)
        {
            if (!obj.TryGetValue("steps", out var stepsValue) || !(stepsValue is ConvexValue.Array stepsArray))
            {
                return Array.Empty<JobStep>();
            }

            var steps = new List<JobStep>();
            foreach (var item in stepsArray.Value)
            {
                if (item is ConvexValue.Object stepObj)
                {
                    var step = new JobStep
                    {
                        name = ConvexValueReader.GetStringValue(stepObj, "name"),
                        status = ConvexValueReader.GetStringValue(stepObj, "status"),
                        expectedMs = ConvexValueReader.GetNullableLongValue(stepObj, "expectedMs"),
                        progress = ConvexValueReader.GetNullableFloatValue(stepObj, "progress")
                    };
                    steps.Add(step);
                }
            }
            return steps.ToArray();
        }
    }
}
