using System.Collections.Generic;
using Genies.Convex.Values;

namespace Genies.Convex.Transport
{
    /// <summary>
    /// Base class for transport request messages.
    /// </summary>
    internal abstract class TransportRequest
    {
        /// <summary>
        /// Gets the path to the Convex function (e.g., "messages:list").
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the arguments to pass to the Convex function.
        /// </summary>
        public ConvexValue.Object Args { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransportRequest"/> class.
        /// </summary>
        /// <param name="path">The path to the Convex function.</param>
        /// <param name="args">The arguments to pass to the function.</param>
        protected TransportRequest(string path, ConvexValue.Object args)
        {
            Path = path ?? throw new System.ArgumentNullException(nameof(path));
            Args = args ?? new ConvexValue.Object();
        }
    }

    /// <summary>
    /// Represents a query request to a Convex backend.
    /// </summary>
    /// <remarks>
    /// Queries are read-only operations that do not modify the database.
    /// They are automatically cached and can be subscribed to for real-time updates
    /// (when using WebSocket transport).
    /// </remarks>
    internal class QueryRequest : TransportRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QueryRequest"/> class.
        /// </summary>
        /// <param name="path">The path to the Convex query function.</param>
        /// <param name="args">The arguments to pass to the query.</param>
        public QueryRequest(string path, ConvexValue.Object args = null)
            : base(path, args)
        {
        }
    }

    /// <summary>
    /// Represents a mutation request to a Convex backend.
    /// </summary>
    /// <remarks>
    /// Mutations can modify the database and are executed transactionally.
    /// They are not cached and cannot be subscribed to.
    /// </remarks>
    internal class MutationRequest : TransportRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MutationRequest"/> class.
        /// </summary>
        /// <param name="path">The path to the Convex mutation function.</param>
        /// <param name="args">The arguments to pass to the mutation.</param>
        public MutationRequest(string path, ConvexValue.Object args = null)
            : base(path, args)
        {
        }
    }

    /// <summary>
    /// Represents an action request to a Convex backend.
    /// </summary>
    /// <remarks>
    /// Actions can perform arbitrary operations including external API calls
    /// and database modifications. They are not cached and cannot be subscribed to.
    /// Unlike mutations, actions do not run in a transaction and can have side effects.
    /// </remarks>
    internal class ActionRequest : TransportRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActionRequest"/> class.
        /// </summary>
        /// <param name="path">The path to the Convex action function.</param>
        /// <param name="args">The arguments to pass to the action.</param>
        public ActionRequest(string path, ConvexValue.Object args = null)
            : base(path, args)
        {
        }
    }

    /// <summary>
    /// Represents the response status from a Convex backend.
    /// </summary>
    internal enum ResponseStatus
    {
        /// <summary>
        /// The request completed successfully.
        /// </summary>
        Success,

        /// <summary>
        /// The request failed with an error.
        /// </summary>
        Error
    }

    /// <summary>
    /// Represents a response from a Convex backend.
    /// </summary>
    internal class TransportResponse
    {
        /// <summary>
        /// Gets the response status.
        /// </summary>
        public ResponseStatus Status { get; }

        /// <summary>
        /// Gets the value returned by the function (if successful).
        /// </summary>
        public ConvexValue Value { get; }

        /// <summary>
        /// Gets the error message (if failed).
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Gets the error data (if failed with ConvexError).
        /// </summary>
        public ConvexValue ErrorData { get; }

        /// <summary>
        /// Gets the log lines from the function execution (debug mode only).
        /// </summary>
        public List<string> LogLines { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransportResponse"/> class for a successful response.
        /// </summary>
        /// <param name="value">The value returned by the function.</param>
        /// <param name="logLines">Optional log lines from the function execution.</param>
        public TransportResponse(ConvexValue value, List<string> logLines = null)
        {
            Status = ResponseStatus.Success;
            Value = value;
            LogLines = logLines ?? new List<string>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransportResponse"/> class for an error response.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <param name="errorData">Optional error data (for ConvexError).</param>
        /// <param name="logLines">Optional log lines from the function execution.</param>
        public TransportResponse(string errorMessage, ConvexValue errorData = null, List<string> logLines = null)
        {
            Status = ResponseStatus.Error;
            ErrorMessage = errorMessage;
            ErrorData = errorData;
            LogLines = logLines ?? new List<string>();
        }
    }
}
