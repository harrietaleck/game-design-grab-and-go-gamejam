using System;
using System.Threading;
using System.Threading.Tasks;

namespace Genies.Convex.Transport
{
    /// <summary>
    /// Defines the interface for Convex transport implementations.
    /// </summary>
    /// <remarks>
    /// Transport implementations handle the low-level communication with Convex backends.
    /// Implementations may use HTTP, WebSocket, or other protocols to communicate with the server.
    /// </remarks>
    internal interface ITransport : IDisposable
    {
        /// <summary>
        /// Connects to the Convex backend.
        /// </summary>
        /// <param name="url">The deployment URL to connect to.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous connect operation.</returns>
        /// <remarks>
        /// For connectionless transports (like HTTP), this method may simply validate the URL format.
        /// For stateful transports (like WebSocket), this establishes the connection.
        /// </remarks>
        Task ConnectAsync(string url, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a request to the Convex backend and returns the result.
        /// </summary>
        /// <typeparam name="TRequest">The type of request message.</typeparam>
        /// <param name="request">The request message to send.</param>
        /// <param name="timeout">The maximum time to wait for a response.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous request operation and contains the function result.</returns>
        Task<FunctionResult> SendRequestAsync<TRequest>(
            TRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
            where TRequest : TransportRequest;

        /// <summary>
        /// Sets the authentication token for subsequent requests.
        /// </summary>
        /// <param name="token">The authentication token (typically a JWT).</param>
        /// <remarks>
        /// The token will be sent in the Authorization header as: Bearer {token}
        /// </remarks>
        void SetAuth(string token);

        /// <summary>
        /// Sets the admin authentication key for subsequent requests.
        /// </summary>
        /// <param name="adminKey">The admin key for the deployment.</param>
        /// <remarks>
        /// The admin key will be sent in the Authorization header as: Convex {adminKey}
        /// Admin keys provide full access to the deployment and should be kept secure.
        /// </remarks>
        void SetAdminAuth(string adminKey);

        /// <summary>
        /// Clears any previously set authentication.
        /// </summary>
        void ClearAuth();

        /// <summary>
        /// Sets whether debug mode is enabled.
        /// </summary>
        /// <param name="enabled">True to enable debug mode, false to disable.</param>
        /// <remarks>
        /// When debug mode is enabled, log lines from Convex functions will be included
        /// in responses (development deployments only).
        /// </remarks>
        void SetDebug(bool enabled);

        /// <summary>
        /// Gets a value indicating whether the transport is connected.
        /// </summary>
        bool IsConnected { get; }
    }
}
