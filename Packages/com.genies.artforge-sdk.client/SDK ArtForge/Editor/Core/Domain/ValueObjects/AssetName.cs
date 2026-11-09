using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Common;

namespace Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects
{
    /// <summary>
    /// Value object representing a validated and sanitized asset name.
    /// Prevents invalid file system characters and directory traversal attacks.
    /// </summary>
    internal class AssetName : ValueObject
    {
        private const int MinLength = 2;
        private const int MaxLength = 30;

        public string Value { get; }

        // Internal constructor for JSON deserialization and factory method
        internal AssetName(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Creates a new AssetName after validation and sanitization.
        /// </summary>
        /// <param name="value">The raw asset name input</param>
        /// <returns>Result containing the validated AssetName or an error message</returns>
        public static Result<AssetName> Create(string value)
        {
            // Check if empty
            if (string.IsNullOrWhiteSpace(value))
            {
                return Result<AssetName>.Failure("Asset name cannot be empty");
            }

            // Sanitize the input
            var sanitized = Sanitize(value);

            // Validate length (after sanitization)
            if (sanitized.Length < MinLength)
            {
                return Result<AssetName>.Failure("Asset name must be at least 2 characters");
            }

            if (sanitized.Length > MaxLength)
            {
                return Result<AssetName>.Failure("Asset name must be at most 30 characters");
            }

            // Validate characters
            if (!IsValidFileName(sanitized))
            {
                return Result<AssetName>.Failure("Asset name contains invalid characters. Use only letters, numbers, spaces, hyphens, and underscores.");
            }

            return Result<AssetName>.Success(new AssetName(sanitized));
        }

        /// <summary>
        /// Removes invalid file system characters and potential security threats.
        /// </summary>
        private static string Sanitize(string value)
        {
            // Remove invalid file name characters
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(value.Where(c => !invalid.Contains(c)).ToArray());

            // Remove directory traversal attempts
            sanitized = sanitized.Replace("..", "")
                                 .Replace("/", "")
                                 .Replace("\\", "")
                                 .Trim();

            // Collapse multiple spaces into single space
            while (sanitized.Contains("  "))
            {
                sanitized = sanitized.Replace("  ", " ");
            }

            return sanitized;
        }

        /// <summary>
        /// Validates that the name only contains allowed characters.
        /// </summary>
        private static bool IsValidFileName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            // Must start with letter or number
            if (!char.IsLetterOrDigit(value[0]))
            {
                return false;
            }

            // Can only contain alphanumeric, spaces, hyphens, underscores
            return value.All(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_');
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            // Case-insensitive comparison (assets are case-insensitive on many file systems)
            yield return Value.ToLowerInvariant();
        }

        public static implicit operator string(AssetName assetName) => assetName?.Value;

        public override string ToString() => Value;
    }
}
