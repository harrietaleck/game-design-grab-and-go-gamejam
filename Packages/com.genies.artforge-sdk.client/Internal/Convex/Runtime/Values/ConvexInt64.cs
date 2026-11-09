using System;
using System.Collections.Generic;

namespace Genies.Convex.Values
{
    /// <summary>
    /// A wrapper around a long (64-bit signed integer) to represent a Convex Int64 value.
    /// This type is used to explicitly represent JavaScript BigInt values that fit within the int64 range.
    /// Range: -2^63 to 2^63-1 (long.MinValue to long.MaxValue)
    /// </summary>
    internal readonly struct ConvexInt64 : IEquatable<ConvexInt64>, IComparable<ConvexInt64>
    {
        /// <summary>
        /// Minimum allowed value for ConvexInt64 (-9223372036854775808)
        /// </summary>
        public const long MinValue = long.MinValue;

        /// <summary>
        /// Maximum allowed value for ConvexInt64 (9223372036854775807)
        /// </summary>
        public const long MaxValue = long.MaxValue;

        private readonly long _value;

        /// <summary>
        /// Gets the underlying long value.
        /// </summary>
        public long Value => _value;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvexInt64"/> struct.
        /// </summary>
        /// <param name="value">The 64-bit signed integer value to wrap.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when value is outside the valid range (this should never occur for long type,
        /// but the validation is documented for API consistency).
        /// </exception>
        public ConvexInt64(long value)
        {
            // While this check is technically redundant for long type,
            // it's included for API consistency and documentation purposes
            if (value < MinValue || value > MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"Value must be between {MinValue} and {MaxValue}");
            }
            _value = value;
        }

        /// <summary>
        /// Converts this ConvexInt64 to its JSON representation.
        /// The value is encoded as a base64 string of the little-endian 64-bit representation.
        /// </summary>
        /// <returns>A dictionary containing the "$integer" key with base64-encoded value.</returns>
        public Dictionary<string, string> ToJson()
        {
            // Pack the long value as little-endian 64-bit integer
            byte[] bytes = BitConverter.GetBytes(_value);

            // Ensure little-endian byte order (BitConverter uses system endianness)
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            // Convert to base64
            string base64Value = Convert.ToBase64String(bytes);

            return new Dictionary<string, string>
            {
                { "$integer", base64Value }
            };
        }

        /// <summary>
        /// Creates a ConvexInt64 from its JSON representation.
        /// </summary>
        /// <param name="json">A dictionary containing the "$integer" key with base64-encoded value.</param>
        /// <returns>A new ConvexInt64 instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when json is null.</exception>
        /// <exception cref="ArgumentException">Thrown when json doesn't contain the "$integer" key or the value is invalid.</exception>
        public static ConvexInt64 FromJson(Dictionary<string, string> json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            if (!json.TryGetValue("$integer", out string base64Value))
            {
                throw new ArgumentException("JSON dictionary must contain '$integer' key", nameof(json));
            }

            if (string.IsNullOrEmpty(base64Value))
            {
                throw new ArgumentException("Base64 value cannot be null or empty", nameof(json));
            }

            try
            {
                // Decode base64 to bytes
                byte[] bytes = Convert.FromBase64String(base64Value);

                if (bytes.Length != 8)
                {
                    throw new ArgumentException(
                        $"Invalid byte length {bytes.Length}, expected 8 bytes for int64",
                        nameof(json));
                }

                // Ensure little-endian byte order
                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }

                // Convert bytes to long
                long value = BitConverter.ToInt64(bytes, 0);

                return new ConvexInt64(value);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("Invalid base64 string in '$integer' value", nameof(json), ex);
            }
        }

        /// <summary>
        /// Implicitly converts a long to a ConvexInt64.
        /// </summary>
        /// <param name="value">The long value to convert.</param>
        public static implicit operator ConvexInt64(long value)
        {
            return new ConvexInt64(value);
        }

        /// <summary>
        /// Explicitly converts a ConvexInt64 to a long.
        /// </summary>
        /// <param name="convexInt64">The ConvexInt64 to convert.</param>
        public static explicit operator long(ConvexInt64 convexInt64)
        {
            return convexInt64._value;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current ConvexInt64.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>true if the specified object is equal to the current instance; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is ConvexInt64 other)
            {
                return Equals(other);
            }

            if (obj is long longValue)
            {
                return _value == longValue;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the specified ConvexInt64 is equal to the current ConvexInt64.
        /// </summary>
        /// <param name="other">The ConvexInt64 to compare with the current instance.</param>
        /// <returns>true if the specified ConvexInt64 is equal to the current instance; otherwise, false.</returns>
        public bool Equals(ConvexInt64 other)
        {
            return _value == other._value;
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        /// <summary>
        /// Compares the current instance with another ConvexInt64.
        /// </summary>
        /// <param name="other">The ConvexInt64 to compare with this instance.</param>
        /// <returns>
        /// A value that indicates the relative order of the objects being compared.
        /// Less than zero: This instance precedes other in the sort order.
        /// Zero: This instance occurs in the same position in the sort order as other.
        /// Greater than zero: This instance follows other in the sort order.
        /// </returns>
        public int CompareTo(ConvexInt64 other)
        {
            return _value.CompareTo(other._value);
        }

        /// <summary>
        /// Returns the string representation of this ConvexInt64.
        /// </summary>
        /// <returns>A string in the format "ConvexInt64(value)".</returns>
        public override string ToString()
        {
            return $"ConvexInt64({_value})";
        }

        /// <summary>
        /// Determines whether two ConvexInt64 instances are equal.
        /// </summary>
        public static bool operator ==(ConvexInt64 left, ConvexInt64 right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two ConvexInt64 instances are not equal.
        /// </summary>
        public static bool operator !=(ConvexInt64 left, ConvexInt64 right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Determines whether one ConvexInt64 is less than another.
        /// </summary>
        public static bool operator <(ConvexInt64 left, ConvexInt64 right)
        {
            return left._value < right._value;
        }

        /// <summary>
        /// Determines whether one ConvexInt64 is less than or equal to another.
        /// </summary>
        public static bool operator <=(ConvexInt64 left, ConvexInt64 right)
        {
            return left._value <= right._value;
        }

        /// <summary>
        /// Determines whether one ConvexInt64 is greater than another.
        /// </summary>
        public static bool operator >(ConvexInt64 left, ConvexInt64 right)
        {
            return left._value > right._value;
        }

        /// <summary>
        /// Determines whether one ConvexInt64 is greater than or equal to another.
        /// </summary>
        public static bool operator >=(ConvexInt64 left, ConvexInt64 right)
        {
            return left._value >= right._value;
        }
    }
}
