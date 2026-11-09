using System;
using System.Threading;
using System.Threading.Tasks;
using Genies.ArtForge.ContentAPI.Logging;
using Genies.Convex.Core;
using Genies.Convex.Exceptions;

namespace Genies.ArtForge.ContentAPI
{
    internal abstract class ContentEndpointBase
    {
        protected ContentEndpointBase(GeniesContentAPI api)
        {
            Api = api ?? throw new ArgumentNullException(nameof(api));
        }

        protected GeniesContentAPI Api { get; }

        protected ConvexClient Client => Api.Client;

        protected IContentApiLogger Logger => Api.Logger;

        protected Task EnsureConnectedAsync(CancellationToken cancellationToken)
        {
            return Api.EnsureConnectedInternalAsync(cancellationToken);
        }

        protected string FormatErrorMessage(string operation, string message, string errorCode = null)
        {
            return Api.FormatErrorMessage(operation, message, errorCode);
        }

        protected string ExtractErrorCode(ConvexError error)
        {
            return Api.ExtractErrorCode(error);
        }
    }
}
