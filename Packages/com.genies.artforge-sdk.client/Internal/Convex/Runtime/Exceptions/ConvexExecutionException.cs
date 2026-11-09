using System;

namespace Genies.Convex.Exceptions
{
    /// <summary>
    /// Exception thrown when a Convex function execution fails on the server.
    /// </summary>
    /// <remarks>
    /// This exception is thrown when the Convex backend returns an error response
    /// indicating that a function failed to execute. This includes:
    /// - Runtime errors in Convex functions
    /// - Validation errors
    /// - Database constraint violations
    /// - Function not found errors
    /// </remarks>
    internal class ConvexExecutionException : ConvexException
    {
        /// <summary>
        /// Gets the name of the Convex function that failed.
        /// </summary>
        public string FunctionName { get; }

        /// <summary>
        /// Gets the HTTP status code returned by the server, if applicable.
        /// </summary>
        public int? StatusCode { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexExecutionException"/> class.
        /// </summary>
        public ConvexExecutionException()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexExecutionException"/> class
        /// with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the execution error.</param>
        public ConvexExecutionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexExecutionException"/> class
        /// with a specified error message and function name.
        /// </summary>
        /// <param name="message">The message that describes the execution error.</param>
        /// <param name="functionName">The name of the Convex function that failed.</param>
        public ConvexExecutionException(string message, string functionName)
            : base(message)
        {
            FunctionName = functionName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexExecutionException"/> class
        /// with a specified error message, function name, and status code.
        /// </summary>
        /// <param name="message">The message that describes the execution error.</param>
        /// <param name="functionName">The name of the Convex function that failed.</param>
        /// <param name="statusCode">The HTTP status code returned by the server.</param>
        public ConvexExecutionException(string message, string functionName, int statusCode)
            : base(message)
        {
            FunctionName = functionName;
            StatusCode = statusCode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexExecutionException"/> class
        /// with a specified error message and a reference to the inner exception that
        /// caused this exception.
        /// </summary>
        /// <param name="message">The message that describes the execution error.</param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception, or null if no inner exception is specified.
        /// </param>
        public ConvexExecutionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexExecutionException"/> class
        /// with a specified error message, function name, and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the execution error.</param>
        /// <param name="functionName">The name of the Convex function that failed.</param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception, or null if no inner exception is specified.
        /// </param>
        public ConvexExecutionException(string message, string functionName, Exception innerException)
            : base(message, innerException)
        {
            FunctionName = functionName;
        }
    }
}
