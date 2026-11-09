namespace Genies.Convex.Transport
{
    /// <summary>
    /// Represents the connection state of a transport.
    /// </summary>
    internal enum ConnectionState
    {
        /// <summary>
        /// The transport is disconnected and not attempting to connect.
        /// </summary>
        Disconnected,

        /// <summary>
        /// The transport is attempting to establish a connection.
        /// </summary>
        Connecting,

        /// <summary>
        /// The transport is connected and ready for communication.
        /// </summary>
        Connected,

        /// <summary>
        /// The transport is attempting to reconnect after a connection loss.
        /// </summary>
        Reconnecting,

        /// <summary>
        /// The transport connection has been closed gracefully.
        /// </summary>
        Closed
    }
}
