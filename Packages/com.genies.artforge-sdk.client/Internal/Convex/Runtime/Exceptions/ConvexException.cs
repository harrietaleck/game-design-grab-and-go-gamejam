using System;

namespace Genies.Convex.Exceptions
{
    /// <summary>
    /// Base exception class for all Convex-related errors.
    /// </summary>
    /// <remarks>
    /// This is the base class for all exceptions thrown by the Convex client.
    /// Specific error types inherit from this class to provide more detailed
    /// error handling capabilities.
    /// </remarks>
    internal class ConvexException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexException"/> class.
        /// </summary>
        public ConvexException()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexException"/> class
        /// with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public ConvexException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexException"/> class
        /// with a specified error message and a reference to the inner exception that
        /// caused this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception, or null if no inner exception is specified.
        /// </param>
        public ConvexException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
