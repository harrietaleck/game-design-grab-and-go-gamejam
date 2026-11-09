using System;
using System.Collections.Generic;
using Genies.Convex.Values;

namespace Genies.Convex.Transport
{
    /// <summary>
    /// Represents the result of a Convex function execution (Query, Mutation, or Action).
    /// </summary>
    /// <remarks>
    /// This is a discriminated union type representing either a successful result with a value,
    /// an error message, or a ConvexError with structured data.
    /// Use pattern matching or the IsSuccess/IsError properties to handle different cases.
    /// </remarks>
    internal abstract class FunctionResult
    {
        /// <summary>
        /// Gets the log lines from the function execution (debug mode only).
        /// </summary>
        /// <remarks>
        /// Log lines are only available when:
        /// 1. Debug mode is enabled on the client
        /// 2. The deployment is a development deployment (not production)
        /// </remarks>
        public List<string> LogLines { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether this is a successful result.
        /// </summary>
        public bool IsSuccess => this is Value;

        /// <summary>
        /// Gets a value indicating whether this is an error result.
        /// </summary>
        public bool IsError => !IsSuccess;

        /// <summary>
        /// Successful result with a value.
        /// </summary>
        public sealed class Value : FunctionResult
        {
            /// <summary>
            /// Gets the ConvexValue result.
            /// </summary>
            public ConvexValue Result { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="Value"/> class.
            /// </summary>
            /// <param name="result">The ConvexValue result.</param>
            /// <param name="logLines">Optional log lines from the function execution.</param>
            public Value(ConvexValue result, List<string> logLines = null)
            {
                Result = result ?? throw new ArgumentNullException(nameof(result));
                LogLines = logLines ?? new List<string>();
            }

            /// <summary>
            /// Returns a string representation of the successful result.
            /// </summary>
            public override string ToString()
            {
                return $"Success: {Result}";
            }
        }

        /// <summary>
        /// Error result with a message.
        /// </summary>
        public sealed class ErrorMessage : FunctionResult
        {
            /// <summary>
            /// Gets the error message.
            /// </summary>
            public string Message { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="ErrorMessage"/> class.
            /// </summary>
            /// <param name="message">The error message.</param>
            /// <param name="logLines">Optional log lines from the function execution.</param>
            public ErrorMessage(string message, List<string> logLines = null)
            {
                Message = message ?? throw new ArgumentNullException(nameof(message));
                LogLines = logLines ?? new List<string>();
            }

            /// <summary>
            /// Returns a string representation of the error.
            /// </summary>
            public override string ToString()
            {
                return $"Error: {Message}";
            }
        }

        /// <summary>
        /// Convex error result with structured error data.
        /// </summary>
        /// <remarks>
        /// This represents an error explicitly thrown by a Convex function using Genies.ConvexError.
        /// The error data is preserved as a ConvexValue for structured error handling.
        /// </remarks>
        public sealed class ConvexError : FunctionResult
        {
            /// <summary>
            /// Gets the error message.
            /// </summary>
            public string Message { get; }

            /// <summary>
            /// Gets the structured error data.
            /// </summary>
            public ConvexValue Data { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="ConvexError"/> class.
            /// </summary>
            /// <param name="message">The error message.</param>
            /// <param name="data">The structured error data.</param>
            /// <param name="logLines">Optional log lines from the function execution.</param>
            public ConvexError(string message, ConvexValue data, List<string> logLines = null)
            {
                Message = message ?? throw new ArgumentNullException(nameof(message));
                Data = data;
                LogLines = logLines ?? new List<string>();
            }

            /// <summary>
            /// Returns a string representation of the ConvexError.
            /// </summary>
            public override string ToString()
            {
                return $"ConvexError: {Message}, Data: {Data}";
            }
        }
    }
}
