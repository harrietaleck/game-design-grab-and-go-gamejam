using System.Collections.Generic;
using System.Linq;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Common;

namespace Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects
{
    /// <summary>
    /// Value object representing a validated and sanitized AI prompt.
    /// Removes control characters and enforces length limits.
    /// </summary>
    internal class Prompt : ValueObject
    {
        public string Value { get; }

        private const int MinLength = 5;
        private const int MaxLength = 2000;

        private Prompt(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Creates a new Prompt after validation and sanitization.
        /// </summary>
        /// <param name="value">The raw prompt text</param>
        /// <returns>Result containing the validated Prompt or an error message</returns>
        public static Result<Prompt> Create(string value)
        {
            // Check if empty
            if (string.IsNullOrWhiteSpace(value))
            {
                return Result<Prompt>.Failure("Prompt cannot be empty");
            }

            // Sanitize (remove control chars except whitespace)
            var sanitized = Sanitize(value);

            // Validate minimum length
            if (sanitized.Length < MinLength)
            {
                return Result<Prompt>.Failure($"Prompt must be at least {MinLength} characters");
            }

            // Truncate if too long
            if (sanitized.Length > MaxLength)
            {
                sanitized = sanitized.Substring(0, MaxLength);
            }

            return Result<Prompt>.Success(new Prompt(sanitized));
        }

        /// <summary>
        /// Removes control characters (except whitespace) that could cause issues.
        /// </summary>
        private static string Sanitize(string value)
        {
            // Remove control characters but keep normal whitespace (space, tab, newline)
            var sanitized = new string(value
                .Where(c => !char.IsControl(c) || char.IsWhiteSpace(c))
                .ToArray());

            // Trim leading/trailing whitespace
            return sanitized.Trim();
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }

        public static implicit operator string(Prompt prompt) => prompt?.Value;

        public override string ToString() => Value;
    }
}

