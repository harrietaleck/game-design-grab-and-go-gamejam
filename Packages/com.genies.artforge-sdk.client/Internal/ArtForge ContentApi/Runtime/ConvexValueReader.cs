using Genies.Convex.Values;

namespace Genies.ArtForge.ContentAPI
{
    internal static class ConvexValueReader
    {
        internal static string GetStringValue(ConvexValue.Object obj, string key)
        {
            if (obj.TryGetValue(key, out var value))
            {
                return value.AsString();
            }
            return null;
        }

        internal static float GetFloatValue(ConvexValue.Object obj, string key)
        {
            if (obj.TryGetValue(key, out var value))
            {
                var doubleValue = value.AsFloat64();
                return doubleValue.HasValue ? (float)doubleValue.Value : 0f;
            }
            return 0f;
        }

        internal static long GetLongValue(ConvexValue.Object obj, string key)
        {
            if (obj.TryGetValue(key, out var value))
            {
                var int64Value = value.AsInt64();
                if (int64Value.HasValue)
                {
                    return int64Value.Value;
                }

                var floatValue = value.AsFloat64();
                if (floatValue.HasValue)
                {
                    return (long)floatValue.Value;
                }
            }
            return 0L;
        }

        internal static int GetIntValue(ConvexValue.Object obj, string key)
        {
            if (obj.TryGetValue(key, out var value))
            {
                var int64Value = value.AsInt64();
                if (int64Value.HasValue)
                {
                    return (int)int64Value.Value;
                }

                var floatValue = value.AsFloat64();
                if (floatValue.HasValue)
                {
                    return (int)floatValue.Value;
                }
            }
            return 0;
        }

        internal static int? GetNullableIntValue(ConvexValue.Object obj, string key)
        {
            if (obj.TryGetValue(key, out var value))
            {
                var int64Value = value.AsInt64();
                if (int64Value.HasValue)
                {
                    return (int)int64Value.Value;
                }

                var floatValue = value.AsFloat64();
                if (floatValue.HasValue)
                {
                    return (int)floatValue.Value;
                }
            }
            return null;
        }

        internal static float? GetNullableFloatValue(ConvexValue.Object obj, string key)
        {
            if (obj.TryGetValue(key, out var value))
            {
                var floatValue = value.AsFloat64();
                if (floatValue.HasValue)
                {
                    return (float)floatValue.Value;
                }
            }
            return null;
        }

        internal static long? GetNullableLongValue(ConvexValue.Object obj, string key)
        {
            if (obj.TryGetValue(key, out var value))
            {
                var int64Value = value.AsInt64();
                if (int64Value.HasValue)
                {
                    return int64Value.Value;
                }

                var floatValue = value.AsFloat64();
                if (floatValue.HasValue)
                {
                    return (long)floatValue.Value;
                }
            }
            return null;
        }
    }
}
