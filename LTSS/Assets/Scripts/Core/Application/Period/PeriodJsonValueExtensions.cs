using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Core.Application.Session;

namespace Game.Core.Application.Periods
{
    internal static class PeriodJsonValueExtensions
    {
        public static bool TryGetPropertyIgnoreCase(this JsonValue node, string propertyName, out JsonValue value)
        {
            value = JsonValue.Null;

            if (node == null || node.Kind != JsonValueKind.Object || string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            if (node.TryGetProperty(propertyName, out value))
            {
                return true;
            }

            foreach (var pair in node.ObjectValue)
            {
                if (string.Equals(pair.Key, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value ?? JsonValue.Null;
                    return true;
                }
            }

            return false;
        }

        public static JsonValue FindFirstProperty(this JsonValue node, params string[] propertyNames)
        {
            if (propertyNames == null)
            {
                return JsonValue.Null;
            }

            foreach (var propertyName in propertyNames)
            {
                if (node.TryGetPropertyIgnoreCase(propertyName, out var value))
                {
                    return value;
                }
            }

            return JsonValue.Null;
        }

        public static JsonValue FindFirstDescendantProperty(this JsonValue node, params string[] propertyNames)
        {
            var direct = node.FindFirstProperty(propertyNames);

            if (direct.Kind != JsonValueKind.Null)
            {
                return direct;
            }

            if (node == null)
            {
                return JsonValue.Null;
            }

            if (node.Kind == JsonValueKind.Object)
            {
                foreach (var pair in node.ObjectValue)
                {
                    var value = FindFirstDescendantProperty(pair.Value, propertyNames);

                    if (value.Kind != JsonValueKind.Null)
                    {
                        return value;
                    }
                }
            }

            if (node.Kind == JsonValueKind.Array)
            {
                foreach (var item in node.ArrayValue)
                {
                    var value = FindFirstDescendantProperty(item, propertyNames);

                    if (value.Kind != JsonValueKind.Null)
                    {
                        return value;
                    }
                }
            }

            return JsonValue.Null;
        }

        public static IReadOnlyList<JsonValue> AsArrayOrEmpty(this JsonValue node)
        {
            return node != null && node.Kind == JsonValueKind.Array
                ? node.ArrayValue
                : Array.Empty<JsonValue>();
        }

        public static bool TryGetStringValue(this JsonValue node, out string value)
        {
            value = string.Empty;

            if (node == null)
            {
                return false;
            }

            switch (node.Kind)
            {
                case JsonValueKind.String:
                    value = node.StringValue ?? string.Empty;
                    return true;
                case JsonValueKind.Number:
                    value = node.NumberValue.ToString("0.##", CultureInfo.InvariantCulture);
                    return true;
                case JsonValueKind.Boolean:
                    value = node.BooleanValue ? "true" : "false";
                    return true;
                default:
                    return false;
            }
        }

        public static string GetStringOrDefault(this JsonValue node, string fallback = "")
        {
            return node.TryGetStringValue(out var value)
                ? value
                : fallback ?? string.Empty;
        }

        public static double? AsNullableNumber(this JsonValue node)
        {
            if (node == null)
            {
                return null;
            }

            if (node.Kind == JsonValueKind.Number)
            {
                return node.NumberValue;
            }

            if (node.Kind == JsonValueKind.String
                && double.TryParse(
                    node.StringValue,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                return parsed;
            }

            return null;
        }

        public static bool? AsNullableBoolean(this JsonValue node)
        {
            if (node == null)
            {
                return null;
            }

            if (node.Kind == JsonValueKind.Boolean)
            {
                return node.BooleanValue;
            }

            if (node.Kind == JsonValueKind.String
                && bool.TryParse(node.StringValue, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        public static JsonValue GetArrayCandidate(this JsonValue node, params string[] names)
        {
            var candidate = node.FindFirstProperty(names);
            return candidate.Kind == JsonValueKind.Array
                ? candidate
                : JsonValue.Null;
        }

        public static JsonValue GetObjectCandidate(this JsonValue node, params string[] names)
        {
            var candidate = node.FindFirstProperty(names);
            return candidate.Kind == JsonValueKind.Object
                ? candidate
                : JsonValue.Null;
        }

        public static IReadOnlyList<JsonValue> FindArrayDescendant(this JsonValue node, params string[] names)
        {
            var candidate = node.FindFirstDescendantProperty(names);
            return candidate.Kind == JsonValueKind.Array
                ? candidate.ArrayValue
                : Array.Empty<JsonValue>();
        }
    }
}
