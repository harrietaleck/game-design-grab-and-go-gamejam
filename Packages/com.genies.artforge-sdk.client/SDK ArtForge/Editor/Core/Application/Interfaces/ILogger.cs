using System;

namespace Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces
{
    /// <summary>
    /// Interface for logging operations.
    /// Provides abstraction over different logging implementations.
    /// </summary>
    internal interface ILogger
    {
        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log</param>
        void Info(string message);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The warning message to log</param>
        void Warning(string message);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The error message to log</param>
        void Error(string message);

        /// <summary>
        /// Logs an error message with exception details.
        /// </summary>
        /// <param name="message">The error message to log</param>
        /// <param name="exception">The exception that occurred</param>
        void Error(string message, Exception exception);

        /// <summary>
        /// Logs a debug message (only in development).
        /// </summary>
        /// <param name="message">The debug message to log</param>
        void Debug(string message);
    }
}

