using System;

namespace Genies.Convex.Values
{
    /// <summary>
    /// Provides validation methods for Convex value types to ensure compatibility
    /// with the Convex database system and JavaScript runtime environments.
    /// </summary>
    /// <remarks>
    /// Convex enforces specific constraints on field names and numeric ranges to maintain
    /// compatibility with JavaScript's number representation and database storage limits.
    /// </remarks>
    internal static class ValueValidator
    {
        /// <summary>
        /// Maximum allowed length for object field names.
        /// </summary>
        public const int MaxFieldNameLength = 1024;

        /// <summary>
        /// Minimum safe integer value representable in JavaScript's Float64 number type (-2^53).
        /// </summary>
        /// <remarks>
        /// JavaScript uses IEEE 754 double-precision floating-point format for numbers.
        /// This range ensures integers can be safely serialized without precision loss.
        /// </remarks>
        public const long MinSafeInteger = -(1L << 53);

        /// <summary>
        /// Maximum safe integer value representable in JavaScript's Float64 number type (2^53).
        /// </summary>
        public const long MaxSafeInteger = 1L << 53;

        /// <summary>
        /// Minimum value for Int64 type (-2^63).
        /// </summary>
        public const long MinInt64 = long.MinValue;

        /// <summary>
        /// Maximum value for Int64 type (2^63 - 1).
        /// </summary>
        public const long MaxInt64 = long.MaxValue;

        /// <summary>
        /// Validates that a field name meets Convex object field requirements.
        /// </summary>
        /// <param name="fieldName">The field name to validate.</param>
        /// <exception cref="ConvexValidationException">
        /// Thrown when the field name is null, empty, exceeds maximum length,
        /// starts with '$', or contains non-ASCII or control characters.
        /// </exception>
        /// <remarks>
        /// Convex field name requirements:
        /// <list type="bullet">
        /// <item><description>Must be non-null and non-empty</description></item>
        /// <item><description>Maximum length of 1024 characters</description></item>
        /// <item><description>Cannot start with '$' (reserved for system fields)</description></item>
        /// <item><description>Only ASCII characters in range 32-126 (printable characters)</description></item>
        /// </list>
        /// </remarks>
        public static void ValidateObjectFieldName(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
            {
                throw new ConvexValidationException(
                    "Field name cannot be null or empty.");
            }

            if (fieldName.Length > MaxFieldNameLength)
            {
                throw new ConvexValidationException(
                    $"Field name '{fieldName}' exceeds maximum field name length {MaxFieldNameLength}.");
            }

            if (fieldName[0] == '$')
            {
                throw new ConvexValidationException(
                    $"Field name '{fieldName}' starts with a '$', which is reserved.");
            }

            foreach (char c in fieldName)
            {
                // Non-control ASCII characters (32-126 inclusive)
                if (c < 32 || c >= 127)
                {
                    throw new ConvexValidationException(
                        $"Field name '{fieldName}' has invalid character '{c}': " +
                        "Field names can only contain non-control ASCII characters");
                }
            }
        }

        /// <summary>
        /// Validates that an integer value is within the safe range for JavaScript Float64 representation.
        /// </summary>
        /// <param name="value">The integer value to validate.</param>
        /// <exception cref="ConvexValidationException">
        /// Thrown when the value is outside the range [-2^53, 2^53].
        /// </exception>
        /// <remarks>
        /// JavaScript's number type is IEEE 754 double-precision float. Integers outside
        /// the safe range may lose precision when serialized. Use ConvexInt64 (BigInt in JavaScript)
        /// for values outside this range.
        /// </remarks>
        public static void ValidateIntegerInSafeRange(long value)
        {
            if (value < MinSafeInteger || value > MaxSafeInteger)
            {
                throw new ConvexValidationException(
                    $"Integer {value} is outside the range of a Convex `Float64` (-2^53 to 2^53). " +
                    "Consider using a `ConvexInt64`, which corresponds to a `BigInt` in " +
                    "JavaScript Convex functions.");
            }
        }

        /// <summary>
        /// Validates that a value is within the Int64 range.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <exception cref="ConvexValidationException">
        /// Thrown when the value is outside the range [-2^63, 2^63-1].
        /// </exception>
        /// <remarks>
        /// This validation is primarily for completeness; in C#, long is already
        /// constrained to this range by the type system.
        /// </remarks>
        public static void ValidateInt64Range(long value)
        {
            // In C#, long is already constrained to this range by the type system
            // This method exists for API completeness and explicit validation scenarios
            if (value < MinInt64 || value > MaxInt64)
            {
                throw new ConvexValidationException(
                    $"Value {value} is outside the valid Int64 range [{MinInt64}, {MaxInt64}].");
            }
        }

        /// <summary>
        /// Determines whether a floating-point value is a special float that cannot be
        /// serialized to standard JSON (NaN, Infinity, or negative zero).
        /// </summary>
        /// <param name="value">The floating-point value to check.</param>
        /// <returns>
        /// <c>true</c> if the value is NaN, positive/negative infinity, or negative zero;
        /// otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// Special floats require special handling during serialization as standard JSON
        /// does not support these IEEE 754 values. Convex serializes these values using
        /// base64-encoded binary representation.
        /// </remarks>
        public static bool IsSpecialFloat(double value)
        {
            return double.IsNaN(value) ||
                   double.IsInfinity(value) ||
                   IsNegativeZero(value);
        }

        /// <summary>
        /// Determines whether a floating-point value is negative zero (-0.0).
        /// </summary>
        /// <param name="value">The floating-point value to check.</param>
        /// <returns>
        /// <c>true</c> if the value is negative zero; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// IEEE 754 distinguishes between positive zero (+0.0) and negative zero (-0.0),
        /// though they compare as equal. This method detects negative zero by checking
        /// the sign of the result when dividing 1.0 by the value.
        /// </remarks>
        public static bool IsNegativeZero(double value)
        {
            // Negative zero: value equals zero AND 1/value is negative infinity
            return value == 0.0 && double.IsNegativeInfinity(1.0 / value);
        }
    }
}
