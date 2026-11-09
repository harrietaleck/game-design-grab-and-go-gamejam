using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Genies.Convex.Values
{
    /// <summary>
    /// Provides serialization and deserialization between ConvexValue types and JSON.
    /// This serializer handles special encodings for values that cannot be represented
    /// in standard JSON: Int64 (BigInt), special floats (NaN, Infinity, -0.0), and bytes (ArrayBuffer).
    /// </summary>
    /// <remarks>
    /// Special JSON encodings:
    /// <list type="bullet">
    /// <item><description>Int64 (BigInt): {"$integer": "base64_encoded_8_bytes"}</description></item>
    /// <item><description>Special floats: {"$float": "base64_encoded_8_bytes"}</description></item>
    /// <item><description>Bytes (ArrayBuffer): {"$bytes": "base64_encoded_data"}</description></item>
    /// </list>
    /// This implementation maintains compatibility with the Python Convex client and JavaScript runtime.
    /// </remarks>
    internal static class ValueSerializer
    {
        /// <summary>
        /// Serializes a ConvexValue to a JSON string.
        /// </summary>
        /// <param name="value">The ConvexValue to serialize.</param>
        /// <returns>A JSON string representation of the value.</returns>
        /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
        /// <exception cref="ConvexValidationException">Thrown when serialization fails due to validation errors.</exception>
        /// <example>
        /// <code>
        /// var value = new ConvexValue.String("hello");
        /// string json = ValueSerializer.Serialize(value);
        /// // Result: "\"hello\""
        ///
        /// var obj = new ConvexValue.Object(new Dictionary&lt;string, ConvexValue&gt;
        /// {
        ///     { "name", new ConvexValue.String("Alice") },
        ///     { "age", new ConvexValue.Float64(30.0) }
        /// });
        /// string json2 = ValueSerializer.Serialize(obj);
        /// // Result: "{\"name\":\"Alice\",\"age\":30.0}"
        /// </code>
        /// </example>
        public static string Serialize(ConvexValue value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            JToken token = ToJToken(value);
            return token.ToString(Formatting.None);
        }

        /// <summary>
        /// Converts a ConvexValue to a JToken (Newtonsoft.Json representation).
        /// </summary>
        /// <param name="value">The ConvexValue to convert.</param>
        /// <returns>A JToken representation of the value.</returns>
        /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
        /// <exception cref="ConvexValidationException">Thrown when conversion fails due to validation errors.</exception>
        public static JToken ToJToken(ConvexValue value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return SerializeValue(value);
        }

        /// <summary>
        /// Deserializes a JSON string to a ConvexValue.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>A ConvexValue representation of the JSON.</returns>
        /// <exception cref="ArgumentNullException">Thrown when json is null.</exception>
        /// <exception cref="ConvexValidationException">Thrown when deserialization fails due to invalid JSON or validation errors.</exception>
        /// <example>
        /// <code>
        /// string json = "{\"name\":\"Alice\",\"age\":30.0}";
        /// ConvexValue value = ValueSerializer.Deserialize(json);
        /// // Result: ConvexValue.Object with two fields
        ///
        /// string specialInt = "{\"$integer\":\"AQAAAAAAAAA=\"}";
        /// ConvexValue int64Value = ValueSerializer.Deserialize(specialInt);
        /// // Result: ConvexValue.Int64(1)
        /// </code>
        /// </example>
        public static ConvexValue Deserialize(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            try
            {
                JToken token = JToken.Parse(json);
                return FromJToken(token);
            }
            catch (JsonException ex)
            {
                throw new ConvexValidationException($"Invalid JSON: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Converts a JToken to a ConvexValue.
        /// </summary>
        /// <param name="token">The JToken to convert.</param>
        /// <returns>A ConvexValue representation of the token.</returns>
        /// <exception cref="ArgumentNullException">Thrown when token is null.</exception>
        /// <exception cref="ConvexValidationException">Thrown when conversion fails due to validation errors.</exception>
        public static ConvexValue FromJToken(JToken token)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            return DeserializeValue(token);
        }

        /// <summary>
        /// Serializes a ConvexValue to a JToken, handling all Convex value types.
        /// </summary>
        private static JToken SerializeValue(ConvexValue value)
        {
            // Handle each ConvexValue type
            switch (value)
            {
                case ConvexValue.Null _:
                    return JValue.CreateNull();

                case ConvexValue.Boolean boolValue:
                    return new JValue(boolValue.Value);

                case ConvexValue.Float64 floatValue:
                    // Check if this is a special float requiring encoding
                    if (ValueValidator.IsSpecialFloat(floatValue.Value))
                    {
                        return EncodeSpecialFloat(floatValue.Value);
                    }
                    return new JValue(floatValue.Value);

                case ConvexValue.Int64 int64Value:
                    // Int64 is always encoded as {"$integer": "base64"}
                    return EncodeInt64(int64Value.Value);

                case ConvexValue.String stringValue:
                    return new JValue(stringValue.Value);

                case ConvexValue.Bytes bytesValue:
                    // Bytes encoded as {"$bytes": "base64"}
                    return EncodeBytes(bytesValue.Value);

                case ConvexValue.Array arrayValue:
                    // Recursively serialize array elements
                    var jArray = new JArray();
                    foreach (var element in arrayValue.Value)
                    {
                        jArray.Add(SerializeValue(element));
                    }
                    return jArray;

                case ConvexValue.Object objectValue:
                    // Recursively serialize object fields
                    var jObject = new JObject();
                    foreach (var kvp in objectValue.Value)
                    {
                        // Skip null values to match JavaScript undefined behavior
                        // In JavaScript: {field: undefined} is omitted from JSON
                        // In C#: ConvexValue.Null should be omitted from JSON
                        // This ensures compatibility with Convex backend validators that expect optional fields
                        // to be omitted rather than explicitly null
                        if (kvp.Value is ConvexValue.Null)
                        {
                            continue; // Omit null fields
                        }

                        // Validate field name (should already be validated, but double-check)
                        ValueValidator.ValidateObjectFieldName(kvp.Key);
                        jObject.Add(kvp.Key, SerializeValue(kvp.Value));
                    }
                    return jObject;

                default:
                    throw new ConvexValidationException(
                        $"Unknown ConvexValue type: {value.GetType().Name}");
            }
        }

        /// <summary>
        /// Deserializes a JToken to a ConvexValue, handling all JSON types and special encodings.
        /// </summary>
        private static ConvexValue DeserializeValue(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Null:
                    return ConvexValue.Null.Instance;

                case JTokenType.Boolean:
                    return new ConvexValue.Boolean(token.Value<bool>());

                case JTokenType.Float:
                case JTokenType.Integer:
                    // Numbers are deserialized as Float64
                    return new ConvexValue.Float64(token.Value<double>());

                case JTokenType.String:
                    return new ConvexValue.String(token.Value<string>());

                case JTokenType.Array:
                    // Recursively deserialize array elements
                    var jArray = (JArray)token;
                    var elements = new List<ConvexValue>();
                    foreach (var element in jArray)
                    {
                        elements.Add(DeserializeValue(element));
                    }
                    return new ConvexValue.Array(elements);

                case JTokenType.Object:
                    var jObject = (JObject)token;

                    // Check for special encodings (objects with single "$" key)
                    if (jObject.Count == 1)
                    {
                        var property = jObject.Properties().First();
                        var key = property.Name;

                        if (key.StartsWith("$"))
                        {
                            return DecodeSpecialObject(key, property.Value);
                        }
                    }

                    // Regular object - recursively deserialize fields
                    var fields = new Dictionary<string, ConvexValue>();
                    foreach (var property in jObject.Properties())
                    {
                        // Validate field name
                        ValueValidator.ValidateObjectFieldName(property.Name);
                        fields[property.Name] = DeserializeValue(property.Value);
                    }
                    return new ConvexValue.Object(fields);

                default:
                    throw new ConvexValidationException(
                        $"Unsupported JSON token type: {token.Type}");
            }
        }

        /// <summary>
        /// Encodes a special float (NaN, Infinity, -0.0) as a JSON object.
        /// Format: {"$float": "base64_encoded_8_bytes"}
        /// </summary>
        private static JObject EncodeSpecialFloat(double value)
        {
            if (!ValueValidator.IsSpecialFloat(value))
            {
                throw new ConvexValidationException(
                    $"Value {value} is not a special float");
            }

            // Pack as little-endian 64-bit float
            byte[] bytes = BitConverter.GetBytes(value);

            // Ensure little-endian (Python uses "<d" format = little-endian double)
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            string base64 = Convert.ToBase64String(bytes);

            return new JObject
            {
                { "$float", base64 }
            };
        }

        /// <summary>
        /// Decodes a special float from base64 string.
        /// </summary>
        private static double DecodeSpecialFloat(string base64Value)
        {
            if (string.IsNullOrEmpty(base64Value))
            {
                throw new ConvexValidationException(
                    "Base64 value for $float cannot be null or empty");
            }

            try
            {
                byte[] bytes = Convert.FromBase64String(base64Value);

                if (bytes.Length != 8)
                {
                    throw new ConvexValidationException(
                        $"Invalid byte length {bytes.Length} for $float, expected 8 bytes");
                }

                // Ensure little-endian
                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }

                double value = BitConverter.ToDouble(bytes, 0);

                // Validate that it's actually a special float
                if (!ValueValidator.IsSpecialFloat(value))
                {
                    throw new ConvexValidationException(
                        $"Decoded value {value} is not a special float");
                }

                return value;
            }
            catch (FormatException ex)
            {
                throw new ConvexValidationException(
                    "Invalid base64 string in '$float' value", ex);
            }
        }

        /// <summary>
        /// Encodes an Int64 value as a JSON object.
        /// Format: {"$integer": "base64_encoded_8_bytes"}
        /// </summary>
        private static JObject EncodeInt64(long value)
        {
            // Pack as little-endian 64-bit signed integer
            byte[] bytes = BitConverter.GetBytes(value);

            // Ensure little-endian (Python uses "<q" format = little-endian signed long long)
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            string base64 = Convert.ToBase64String(bytes);

            return new JObject
            {
                { "$integer", base64 }
            };
        }

        /// <summary>
        /// Decodes an Int64 value from base64 string.
        /// </summary>
        private static long DecodeInt64(string base64Value)
        {
            if (string.IsNullOrEmpty(base64Value))
            {
                throw new ConvexValidationException(
                    "Base64 value for $integer cannot be null or empty");
            }

            try
            {
                byte[] bytes = Convert.FromBase64String(base64Value);

                if (bytes.Length != 8)
                {
                    throw new ConvexValidationException(
                        $"Invalid byte length {bytes.Length} for $integer, expected 8 bytes");
                }

                // Ensure little-endian
                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }

                return BitConverter.ToInt64(bytes, 0);
            }
            catch (FormatException ex)
            {
                throw new ConvexValidationException(
                    "Invalid base64 string in '$integer' value", ex);
            }
        }

        /// <summary>
        /// Encodes a byte array as a JSON object.
        /// Format: {"$bytes": "base64_encoded_data"}
        /// </summary>
        private static JObject EncodeBytes(byte[] value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            string base64 = Convert.ToBase64String(value);

            return new JObject
            {
                { "$bytes", base64 }
            };
        }

        /// <summary>
        /// Decodes a byte array from base64 string.
        /// </summary>
        private static byte[] DecodeBytes(string base64Value)
        {
            if (string.IsNullOrEmpty(base64Value))
            {
                throw new ConvexValidationException(
                    "Base64 value for $bytes cannot be null or empty");
            }

            try
            {
                return Convert.FromBase64String(base64Value);
            }
            catch (FormatException ex)
            {
                throw new ConvexValidationException(
                    "Invalid base64 string in '$bytes' value", ex);
            }
        }

        /// <summary>
        /// Decodes a special object (one with a single "$" key) to a ConvexValue.
        /// Handles $integer, $float, and $bytes encodings.
        /// </summary>
        private static ConvexValue DecodeSpecialObject(string key, JToken valueToken)
        {
            if (!key.StartsWith("$"))
            {
                throw new ConvexValidationException(
                    $"Expected key starting with '$', got '{key}'");
            }

            // Value must be a string for all special encodings
            if (valueToken.Type != JTokenType.String)
            {
                throw new ConvexValidationException(
                    $"Value for '{key}' must be a string, got {valueToken.Type}");
            }

            string base64Value = valueToken.Value<string>();

            switch (key)
            {
                case "$bytes":
                    byte[] bytes = DecodeBytes(base64Value);
                    return new ConvexValue.Bytes(bytes);

                case "$integer":
                    long int64Value = DecodeInt64(base64Value);
                    return new ConvexValue.Int64(int64Value);

                case "$float":
                    double floatValue = DecodeSpecialFloat(base64Value);
                    return new ConvexValue.Float64(floatValue);

                default:
                    throw new ConvexValidationException(
                        $"Unknown special encoding key: '{key}'. " +
                        "Valid keys are: $bytes, $integer, $float");
            }
        }

        /// <summary>
        /// Converts a C# object to a ConvexValue by serializing through JSON.
        /// </summary>
        /// <param name="obj">The object to convert.</param>
        /// <returns>A ConvexValue representation of the object.</returns>
        /// <exception cref="ConvexValidationException">Thrown when conversion fails.</exception>
        /// <remarks>
        /// Null values in objects are omitted from JSON to match JavaScript undefined behavior.
        /// This ensures compatibility with Convex backend validators that expect optional fields
        /// to be omitted rather than explicitly null.
        /// </remarks>
        public static ConvexValue FromObject(object obj)
        {
            if (obj == null)
            {
                return ConvexValue.Null.Instance;
            }

            try
            {
                // Configure serializer to ignore null values (JavaScript undefined behavior)
                // In JavaScript: {field: undefined} is omitted from JSON
                // In C#: null properties should be omitted from JSON
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                };

                // Serialize object to JSON using Newtonsoft.Json
                string json = JsonConvert.SerializeObject(obj, settings);

                // Deserialize JSON to ConvexValue
                return Deserialize(json);
            }
            catch (Exception ex)
            {
                throw new ConvexValidationException($"Failed to convert object to ConvexValue: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Converts a ConvexValue to a C# object of type T.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="value">The ConvexValue to convert.</param>
        /// <returns>An instance of type T.</returns>
        /// <exception cref="ConvexValidationException">Thrown when conversion fails.</exception>
        public static T ToObject<T>(ConvexValue value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            try
            {
                // Serialize ConvexValue to JSON
                string json = Serialize(value);

                // Deserialize JSON to target type using Newtonsoft.Json
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception ex)
            {
                throw new ConvexValidationException($"Failed to convert ConvexValue to {typeof(T).Name}: {ex.Message}", ex);
            }
        }
    }
}
