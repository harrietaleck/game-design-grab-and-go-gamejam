using System;

namespace Genies.Convex.Exceptions
{
    /// <summary>
    /// Exception thrown when a connection to the Convex backend fails or is lost.
    /// </summary>
    /// <remarks>
    /// This exception is thrown for network-level errors such as connection timeouts,
    /// DNS resolution failures, SSL/TLS errors, or network unavailability.
    /// It does not include HTTP status code errors (4xx, 5xx) which are handled
    /// by other exception types.
    /// </remarks>
    internal class ConvexConnectionException : ConvexException
    {
        /// <summary>
        /// Gets the deployment URL that failed to connect.
        /// </summary>
        public string DeploymentUrl { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexConnectionException"/> class.
        /// </summary>
        public ConvexConnectionException()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexConnectionException"/> class
        /// with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the connection error.</param>
        public ConvexConnectionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexConnectionException"/> class
        /// with a specified error message and deployment URL.
        /// </summary>
        /// <param name="message">The message that describes the connection error.</param>
        /// <param name="deploymentUrl">The deployment URL that failed to connect.</param>
        public ConvexConnectionException(string message, string deploymentUrl)
            : base(message)
        {
            DeploymentUrl = deploymentUrl;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexConnectionException"/> class
        /// with a specified error message and a reference to the inner exception that
        /// caused this exception.
        /// </summary>
        /// <param name="message">The message that describes the connection error.</param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception, or null if no inner exception is specified.
        /// </param>
        public ConvexConnectionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexConnectionException"/> class
        /// with a specified error message, deployment URL, and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the connection error.</param>
        /// <param name="deploymentUrl">The deployment URL that failed to connect.</param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception, or null if no inner exception is specified.
        /// </param>
        public ConvexConnectionException(string message, string deploymentUrl, Exception innerException)
            : base(message, innerException)
        {
            DeploymentUrl = deploymentUrl;
        }
    }
}
