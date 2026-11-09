using System;
using System.Collections.Generic;
using System.Linq;

namespace Genies.Convex.Values
{
    /// <summary>
    /// Base abstract record representing a Convex value type.
    /// Convex supports 8 value types: Null, Boolean, Int64, Float64, String, Bytes, Array, and Object.
    /// This type system provides a discriminated union pattern for type-safe value handling.
    /// </summary>
    /// <remarks>
    /// Type mapping from JavaScript:
    /// - null → ConvexValue.Null
    /// - boolean → ConvexValue.Boolean
    /// - BigInt → ConvexValue.Int64
    /// - number → ConvexValue.Float64
    /// - string → ConvexValue.String
    /// - ArrayBuffer → ConvexValue.Bytes
    /// - Array → ConvexValue.Array
    /// - object → ConvexValue.Object
    /// </remarks>
    internal abstract record ConvexValue
    {
        /// <summary>
        /// Maximum length for object field identifiers in Convex.
        /// </summary>
        public const int MaxIdentifierLength = 1024;

        /// <summary>
        /// Minimum value for Int64 type.
        /// </summary>
        public const long MinInt64 = long.MinValue;

        /// <summary>
        /// Maximum value for Int64 type.
        /// </summary>
        public const long MaxInt64 = long.MaxValue;

        /// <summary>
        /// Minimum safe integer value that can be represented in Float64 without precision loss.
        /// </summary>
        public const long MinSafeInteger = -(1L << 53);

        /// <summary>
        /// Maximum safe integer value that can be represented in Float64 without precision loss.
        /// </summary>
        public const long MaxSafeInteger = 1L << 53;

        /// <summary>
        /// Validates an object field name according to Convex rules.
        /// Field names must:
        /// - Not exceed MaxIdentifierLength characters
        /// - Not start with '$' (reserved)
        /// - Contain only non-control ASCII characters (32-126)
        /// </summary>
        /// <param name="fieldName">The field name to validate.</param>
        /// <exception cref="ArgumentException">Thrown when the field name is invalid.</exception>
        protected static void ValidateObjectField(string fieldName)
        {
            if (fieldName == null)
            {
                throw new ArgumentNullException(nameof(fieldName), "Field name cannot be null.");
            }

            if (fieldName.Length > MaxIdentifierLength)
            {
                throw new ArgumentException(
                    $"Field name '{fieldName}' exceeds maximum field name length {MaxIdentifierLength}.",
                    nameof(fieldName));
            }

            if (fieldName.StartsWith("$"))
            {
                throw new ArgumentException(
                    $"Field name '{fieldName}' starts with a '$', which is reserved.",
                    nameof(fieldName));
            }

            foreach (char c in fieldName)
            {
                if (c < 32 || c >= 127)
                {
                    throw new ArgumentException(
                        $"Field name '{fieldName}' has invalid character '{c}': " +
                        "Field names can only contain non-control ASCII characters.",
                        nameof(fieldName));
                }
            }
        }

        /// <summary>
        /// Represents a Convex null value.
        /// </summary>
        public sealed record Null : ConvexValue
        {
            /// <summary>
            /// Singleton instance of the null value.
            /// </summary>
            public static readonly Null Instance = new Null();

            private Null() { }

            /// <summary>
            /// Returns a string representation of the null value.
            /// </summary>
            public override string ToString() => "null";
        }

        /// <summary>
        /// Represents a Convex boolean value.
        /// </summary>
        public sealed record Boolean : ConvexValue
        {
            /// <summary>
            /// The boolean value.
            /// </summary>
            public bool Value { get; init; }

            /// <summary>
            /// Initializes a new instance of the Boolean type.
            /// </summary>
            /// <param name="value">The boolean value.</param>
            public Boolean(bool value)
            {
                Value = value;
            }

            /// <summary>
            /// Returns a string representation of the boolean value.
            /// </summary>
            public override string ToString() => Value ? "true" : "false";

            /// <summary>
            /// Implicitly converts a bool to a ConvexValue.Boolean.
            /// </summary>
            public static implicit operator Boolean(bool value) => new Boolean(value);

            /// <summary>
            /// Implicitly converts a ConvexValue.Boolean to bool.
            /// </summary>
            public static implicit operator bool(Boolean value) => value.Value;
        }

        /// <summary>
        /// Represents a Convex Int64 value (corresponds to JavaScript BigInt).
        /// </summary>
        public sealed record Int64 : ConvexValue
        {
            /// <summary>
            /// The 64-bit signed integer value.
            /// </summary>
            public long Value { get; init; }

            /// <summary>
            /// Initializes a new instance of the Int64 type.
            /// </summary>
            /// <param name="value">The 64-bit signed integer value.</param>
            public Int64(long value)
            {
                Value = value;
            }

            /// <summary>
            /// Returns a string representation of the Int64 value.
            /// </summary>
            public override string ToString() => $"{Value}n";

            /// <summary>
            /// Implicitly converts a long to a ConvexValue.Int64.
            /// </summary>
            public static implicit operator Int64(long value) => new Int64(value);

            /// <summary>
            /// Implicitly converts a ConvexValue.Int64 to long.
            /// </summary>
            public static implicit operator long(Int64 value) => value.Value;
        }

        /// <summary>
        /// Represents a Convex Float64 value (IEEE 754 double-precision float, corresponds to JavaScript number).
        /// </summary>
        public sealed record Float64 : ConvexValue
        {
            /// <summary>
            /// The double-precision floating-point value.
            /// </summary>
            public double Value { get; init; }

            /// <summary>
            /// Initializes a new instance of the Float64 type.
            /// </summary>
            /// <param name="value">The double-precision floating-point value.</param>
            public Float64(double value)
            {
                Value = value;
            }

            /// <summary>
            /// Gets a value indicating whether this float is a special value (NaN, Infinity, or -0.0).
            /// Special floats require special JSON encoding.
            /// </summary>
            public bool IsSpecial =>
                double.IsNaN(Value) ||
                double.IsInfinity(Value) ||
                (Value == 0.0 && double.IsNegativeInfinity(1.0 / Value));

            /// <summary>
            /// Returns a string representation of the Float64 value.
            /// </summary>
            public override string ToString() => Value.ToString("G17");

            /// <summary>
            /// Implicitly converts a double to a ConvexValue.Float64.
            /// </summary>
            public static implicit operator Float64(double value) => new Float64(value);

            /// <summary>
            /// Implicitly converts a ConvexValue.Float64 to double.
            /// </summary>
            public static implicit operator double(Float64 value) => value.Value;
        }

        /// <summary>
        /// Represents a Convex string value.
        /// </summary>
        public sealed record String : ConvexValue
        {
            /// <summary>
            /// The string value.
            /// </summary>
            public string Value { get; init; }

            /// <summary>
            /// Initializes a new instance of the String type.
            /// </summary>
            /// <param name="value">The string value. Cannot be null.</param>
            /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
            public String(string value)
            {
                Value = value ?? throw new ArgumentNullException(nameof(value), "String value cannot be null.");
            }

            /// <summary>
            /// Returns a string representation of the String value.
            /// </summary>
            public override string ToString() => $"\"{Value}\"";

            /// <summary>
            /// Implicitly converts a string to a ConvexValue.String.
            /// </summary>
            public static implicit operator String(string value) => new String(value);

            /// <summary>
            /// Implicitly converts a ConvexValue.String to string.
            /// </summary>
            public static implicit operator string(String value) => value.Value;
        }

        /// <summary>
        /// Represents a Convex bytes value (corresponds to JavaScript ArrayBuffer).
        /// </summary>
        public sealed record Bytes : ConvexValue
        {
            /// <summary>
            /// The byte array value.
            /// </summary>
            public byte[] Value { get; init; }

            /// <summary>
            /// Initializes a new instance of the Bytes type.
            /// </summary>
            /// <param name="value">The byte array. Cannot be null. Creates a defensive copy.</param>
            /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
            public Bytes(byte[] value)
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value), "Bytes value cannot be null.");
                }
                // Create defensive copy to ensure immutability
                Value = new byte[value.Length];
                System.Array.Copy(value, Value, value.Length);
            }

            /// <summary>
            /// Gets a copy of the byte array to maintain immutability.
            /// </summary>
            /// <returns>A copy of the byte array.</returns>
            public byte[] ToArray()
            {
                byte[] copy = new byte[Value.Length];
                System.Array.Copy(Value, copy, Value.Length);
                return copy;
            }

            /// <summary>
            /// Returns a string representation of the Bytes value.
            /// </summary>
            public override string ToString() => $"Bytes[{Value.Length}]";

            /// <summary>
            /// Custom equality comparison for byte arrays.
            /// </summary>
            public bool Equals(Bytes other)
            {
                if (other is null)
                {
                    return false;
                }

                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                if (Value.Length != other.Value.Length)
                {
                    return false;
                }

                return Value.SequenceEqual(other.Value);
            }

            /// <summary>
            /// Custom hash code for byte arrays.
            /// </summary>
            public override int GetHashCode()
            {
                if (Value.Length == 0)
                {
                    return 0;
                }

                unchecked
                {
                    int hash = 17;
                    foreach (byte b in Value)
                    {
                        hash = hash * 31 + b;
                    }
                    return hash;
                }
            }
        }

        /// <summary>
        /// Represents a Convex array value (corresponds to JavaScript Array).
        /// </summary>
        public sealed record Array : ConvexValue
        {
            /// <summary>
            /// The list of ConvexValue elements.
            /// </summary>
            public List<ConvexValue> Value { get; init; }

            /// <summary>
            /// Initializes a new instance of the Array type.
            /// </summary>
            /// <param name="value">The list of ConvexValue elements. Cannot be null. Creates a defensive copy.</param>
            /// <exception cref="ArgumentNullException">Thrown when value is null or contains null elements.</exception>
            public Array(List<ConvexValue> value)
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value), "Array value cannot be null.");
                }

                if (value.Any(v => v == null))
                {
                    throw new ArgumentNullException(nameof(value), "Array cannot contain null elements.");
                }

                // Create defensive copy to ensure immutability
                Value = new List<ConvexValue>(value);
            }

            /// <summary>
            /// Initializes a new instance of the Array type from an enumerable.
            /// </summary>
            /// <param name="values">The enumerable of ConvexValue elements.</param>
            public Array(IEnumerable<ConvexValue> values)
                : this(values?.ToList() ?? throw new ArgumentNullException(nameof(values)))
            {
            }

            /// <summary>
            /// Initializes a new instance of the Array type from a params array.
            /// </summary>
            /// <param name="values">The array of ConvexValue elements.</param>
            public Array(params ConvexValue[] values)
                : this(values?.ToList() ?? throw new ArgumentNullException(nameof(values)))
            {
            }

            /// <summary>
            /// Gets the number of elements in the array.
            /// </summary>
            public int Count => Value.Count;

            /// <summary>
            /// Gets the element at the specified index.
            /// </summary>
            /// <param name="index">The zero-based index of the element to get.</param>
            public ConvexValue this[int index] => Value[index];

            /// <summary>
            /// Returns a string representation of the Array value.
            /// </summary>
            public override string ToString() => $"[{string.Join(", ", Value.Select(v => v.ToString()))}]";

            /// <summary>
            /// Custom equality comparison for arrays.
            /// </summary>
            public bool Equals(Array other)
            {
                if (other is null)
                {
                    return false;
                }

                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                if (Value.Count != other.Value.Count)
                {
                    return false;
                }

                return Value.SequenceEqual(other.Value);
            }

            /// <summary>
            /// Custom hash code for arrays.
            /// </summary>
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    foreach (var item in Value)
                    {
                        hash = hash * 31 + (item?.GetHashCode() ?? 0);
                    }
                    return hash;
                }
            }
        }

        /// <summary>
        /// Represents a Convex object value (corresponds to JavaScript object).
        /// </summary>
        public sealed record Object : ConvexValue
        {
            /// <summary>
            /// The dictionary of string keys to ConvexValue values.
            /// </summary>
            public Dictionary<string, ConvexValue> Value { get; init; }

            /// <summary>
            /// Initializes a new instance of the Object type.
            /// </summary>
            /// <param name="value">The dictionary of string keys to ConvexValue values. Cannot be null. Creates a defensive copy.</param>
            /// <exception cref="ArgumentNullException">Thrown when value is null or contains null values.</exception>
            /// <exception cref="ArgumentException">Thrown when keys violate Convex field naming rules.</exception>
            public Object(Dictionary<string, ConvexValue> value)
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value), "Object value cannot be null.");
                }

                // Validate all field names
                foreach (var key in value.Keys)
                {
                    ValidateObjectField(key);
                }

                if (value.Values.Any(v => v == null))
                {
                    throw new ArgumentNullException(nameof(value), "Object cannot contain null values.");
                }

                // Create defensive copy to ensure immutability
                Value = new Dictionary<string, ConvexValue>(value);
            }

            /// <summary>
            /// Initializes a new instance of the Object type from key-value pairs.
            /// </summary>
            /// <param name="pairs">The key-value pairs.</param>
            public Object(IEnumerable<KeyValuePair<string, ConvexValue>> pairs)
                : this(pairs?.ToDictionary(p => p.Key, p => p.Value)
                    ?? throw new ArgumentNullException(nameof(pairs)))
            {
            }

            /// <summary>
            /// Initializes an empty Object.
            /// </summary>
            public Object()
                : this(new Dictionary<string, ConvexValue>())
            {
            }

            /// <summary>
            /// Gets the number of fields in the object.
            /// </summary>
            public int Count => Value.Count;

            /// <summary>
            /// Gets the value associated with the specified field name.
            /// </summary>
            /// <param name="key">The field name.</param>
            public ConvexValue this[string key] => Value[key];

            /// <summary>
            /// Determines whether the object contains the specified field.
            /// </summary>
            /// <param name="key">The field name to check.</param>
            public bool ContainsKey(string key) => Value.ContainsKey(key);

            /// <summary>
            /// Tries to get the value associated with the specified field name.
            /// </summary>
            /// <param name="key">The field name.</param>
            /// <param name="value">When this method returns, contains the value associated with the key, if found.</param>
            public bool TryGetValue(string key, out ConvexValue value) => Value.TryGetValue(key, out value);

            /// <summary>
            /// Returns a string representation of the Object value.
            /// </summary>
            public override string ToString()
            {
                var fields = Value.Select(kvp => $"{kvp.Key}: {kvp.Value}");
                return $"{{{string.Join(", ", fields)}}}";
            }

            /// <summary>
            /// Custom equality comparison for objects.
            /// </summary>
            public bool Equals(Object other)
            {
                if (other is null)
                {
                    return false;
                }

                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                if (Value.Count != other.Value.Count)
                {
                    return false;
                }

                foreach (var kvp in Value)
                {
                    if (!other.Value.TryGetValue(kvp.Key, out var otherValue))
                    {
                        return false;
                    }

                    if (!Equals(kvp.Value, otherValue))
                    {
                        return false;
                    }
                }
                return true;
            }

            /// <summary>
            /// Custom hash code for objects.
            /// </summary>
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    // Sort keys to ensure consistent hash code regardless of insertion order
                    foreach (var key in Value.Keys.OrderBy(k => k))
                    {
                        hash = hash * 31 + key.GetHashCode();
                        hash = hash * 31 + (Value[key]?.GetHashCode() ?? 0);
                    }
                    return hash;
                }
            }
        }
    }

    /// <summary>
    /// Extension methods for ConvexValue type checking and conversion.
    /// </summary>
    internal static class ConvexValueExtensions
    {
        /// <summary>
        /// Checks if the value is a ConvexValue.Null.
        /// </summary>
        public static bool IsNull(this ConvexValue value) => value is ConvexValue.Null;

        /// <summary>
        /// Checks if the value is a ConvexValue.Boolean.
        /// </summary>
        public static bool IsBoolean(this ConvexValue value) => value is ConvexValue.Boolean;

        /// <summary>
        /// Checks if the value is a ConvexValue.Int64.
        /// </summary>
        public static bool IsInt64(this ConvexValue value) => value is ConvexValue.Int64;

        /// <summary>
        /// Checks if the value is a ConvexValue.Float64.
        /// </summary>
        public static bool IsFloat64(this ConvexValue value) => value is ConvexValue.Float64;

        /// <summary>
        /// Checks if the value is a ConvexValue.String.
        /// </summary>
        public static bool IsString(this ConvexValue value) => value is ConvexValue.String;

        /// <summary>
        /// Checks if the value is a ConvexValue.Bytes.
        /// </summary>
        public static bool IsBytes(this ConvexValue value) => value is ConvexValue.Bytes;

        /// <summary>
        /// Checks if the value is a ConvexValue.Array.
        /// </summary>
        public static bool IsArray(this ConvexValue value) => value is ConvexValue.Array;

        /// <summary>
        /// Checks if the value is a ConvexValue.Object.
        /// </summary>
        public static bool IsObject(this ConvexValue value) => value is ConvexValue.Object;

        /// <summary>
        /// Attempts to cast the value to a specific ConvexValue type.
        /// </summary>
        /// <typeparam name="T">The target ConvexValue type.</typeparam>
        /// <param name="value">The ConvexValue to cast.</param>
        /// <returns>The casted value, or null if the cast fails.</returns>
        public static T As<T>(this ConvexValue value) where T : ConvexValue => value as T;

        /// <summary>
        /// Attempts to get the value as a boolean.
        /// </summary>
        public static bool? AsBoolean(this ConvexValue value) =>
            value is ConvexValue.Boolean b ? (bool?)b.Value : null;

        /// <summary>
        /// Attempts to get the value as a long.
        /// </summary>
        public static long? AsInt64(this ConvexValue value) =>
            value is ConvexValue.Int64 i ? (long?)i.Value : null;

        /// <summary>
        /// Attempts to get the value as a double.
        /// </summary>
        public static double? AsFloat64(this ConvexValue value) =>
            value is ConvexValue.Float64 f ? (double?)f.Value : null;

        /// <summary>
        /// Attempts to get the value as a string.
        /// </summary>
        public static string AsString(this ConvexValue value) =>
            value is ConvexValue.String s ? s.Value : null;

        /// <summary>
        /// Attempts to get the value as a byte array.
        /// </summary>
        public static byte[] AsBytes(this ConvexValue value) =>
            value is ConvexValue.Bytes b ? b.ToArray() : null;

        /// <summary>
        /// Attempts to get the value as a List of ConvexValues.
        /// </summary>
        public static List<ConvexValue> AsArray(this ConvexValue value) =>
            value is ConvexValue.Array a ? new List<ConvexValue>(a.Value) : null;

        /// <summary>
        /// Attempts to get the value as a Dictionary of string to ConvexValue.
        /// </summary>
        public static Dictionary<string, ConvexValue> AsObject(this ConvexValue value) =>
            value is ConvexValue.Object o ? new Dictionary<string, ConvexValue>(o.Value) : null;
    }
}
