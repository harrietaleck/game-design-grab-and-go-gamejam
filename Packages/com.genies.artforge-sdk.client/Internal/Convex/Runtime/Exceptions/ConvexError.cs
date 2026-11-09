using System;
using Genies.Convex.Values;

namespace Genies.Convex.Exceptions
{
    /// <summary>
    /// Exception thrown when a Convex function explicitly throws a ConvexError with custom data.
    /// </summary>
    /// <remarks>
    /// This exception is thrown when a Convex function uses the ConvexError mechanism
    /// to return structured error information. The error data is preserved as a ConvexValue
    /// for client-side error handling logic.
    /// </remarks>
    /// <example>
    /// <code>
    /// // In Convex function:
    /// // throw new ConvexError("User not found", { userId: args.userId });
    ///
    /// // In C# client:
    /// try {
    ///     await client.QueryAsync("users:get", args);
    /// } catch (ConvexError e) {
    ///     var errorData = e.ErrorData.As&lt;ConvexValue.Object&gt;();
    ///     var userId = errorData["userId"].AsString();
    ///     Console.WriteLine($"User {userId} not found");
    /// }
    /// </code>
    /// </example>
    internal class ConvexError : ConvexException
    {
        /// <summary>
        /// Gets the structured error data provided by the Convex function.
        /// </summary>
        /// <remarks>
        /// This contains the custom error data passed to the ConvexError constructor
        /// in the Convex function. The data is preserved as a ConvexValue for
        /// type-safe access in the client.
        /// </remarks>
        public ConvexValue ErrorData { get; }

        /// <summary>
        /// Gets the name of the Convex function that threw the error.
        /// </summary>
        public string FunctionName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexError"/> class
        /// with a specified error message.
        /// </summary>
        /// <param name="message">The error message from the Convex function.</param>
        public ConvexError(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexError"/> class
        /// with a specified error message and error data.
        /// </summary>
        /// <param name="message">The error message from the Convex function.</param>
        /// <param name="errorData">The structured error data from the Convex function.</param>
        public ConvexError(string message, ConvexValue errorData)
            : base(message)
        {
            ErrorData = errorData;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexError"/> class
        /// with a specified error message, error data, and function name.
        /// </summary>
        /// <param name="message">The error message from the Convex function.</param>
        /// <param name="errorData">The structured error data from the Convex function.</param>
        /// <param name="functionName">The name of the Convex function that threw the error.</param>
        public ConvexError(string message, ConvexValue errorData, string functionName)
            : base(message)
        {
            ErrorData = errorData;
            FunctionName = functionName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexError"/> class
        /// with a specified error message, error data, and inner exception.
        /// </summary>
        /// <param name="message">The error message from the Convex function.</param>
        /// <param name="errorData">The structured error data from the Convex function.</param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception, or null if no inner exception is specified.
        /// </param>
        public ConvexError(string message, ConvexValue errorData, Exception innerException)
            : base(message, innerException)
        {
            ErrorData = errorData;
        }

        /// <summary>
        /// Returns a string representation of the ConvexError including error data.
        /// </summary>
        /// <returns>A string that represents the current exception.</returns>
        public override string ToString()
        {
            var baseString = base.ToString();
            if (ErrorData != null)
            {
                return $"{baseString}\nError Data: {ErrorData}";
            }
            return baseString;
        }
    }
}
