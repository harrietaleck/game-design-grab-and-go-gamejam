namespace Genies.Convex
{
    /// <summary>
    /// Central version management for the Convex Unity SDK.
    /// Update this file to change the protocol version across all transports.
    /// </summary>
    internal static class ConvexVersion
    {
        /// <summary>
        /// The Convex protocol version used by all transports (WebSocket and HTTP).
        /// This version is sent to the Convex backend for compatibility tracking.
        /// </summary>
        /// <remarks>
        /// Format: Major.Minor.Patch (e.g., "1.27.4")
        /// This should match the Convex protocol version you're targeting.
        ///
        /// Update this constant when:
        /// - Upgrading to a new Convex protocol version
        /// - Backend requires a specific minimum version
        /// - Breaking changes in the Convex API
        /// </remarks>
        public const string PROTOCOL_VERSION = "1.27.4";

        /// <summary>
        /// The full client identifier sent in requests.
        /// Format: "unity-{PROTOCOL_VERSION}"
        /// </summary>
        public static string ClientIdentifier => $"unity-{PROTOCOL_VERSION}";

        /// <summary>
        /// Gets the version info for logging and debugging.
        /// </summary>
        public static string GetVersionInfo()
        {
            return $"Convex Unity SDK - Protocol Version: {PROTOCOL_VERSION}";
        }
    }
}
