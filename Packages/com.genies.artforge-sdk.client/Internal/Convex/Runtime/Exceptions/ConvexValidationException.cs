using System;

namespace Genies.Convex.Values
{
    /// <summary>
    /// Exception thrown when ConvexValue validation or serialization fails.
    /// </summary>
    internal class ConvexValidationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the ConvexValidationException class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the validation error.</param>
        public ConvexValidationException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ConvexValidationException class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ConvexValidationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
