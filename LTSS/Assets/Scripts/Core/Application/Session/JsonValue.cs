using System.Collections.Generic;
using System.Globalization;

namespace Game.Core.Application.Session
{
    public enum JsonValueKind
    {
        Null,
        Object,
        Array,
        String,
        Number,
        Boolean
    }

    public sealed class JsonValue
    {
        private static readonly IReadOnlyDictionary<string, JsonValue> EmptyObject =
            new Dictionary<string, JsonValue>();

        private static readonly IReadOnlyList<JsonValue> EmptyArray =
            new List<JsonValue>();

        public static JsonValue Null { get; } = new JsonValue(
            JsonValueKind.Null,
            string.Empty,
            0d,
            false,
            EmptyObject,
            EmptyArray);

        public JsonValueKind Kind { get; }
        public string StringValue { get; }
        public double NumberValue { get; }
        public bool BooleanValue { get; }
        public IReadOnlyDictionary<string, JsonValue> ObjectValue { get; }
        public IReadOnlyList<JsonValue> ArrayValue { get; }
        public int Count =>
            Kind == JsonValueKind.Object ? ObjectValue.Count :
            Kind == JsonValueKind.Array ? ArrayValue.Count :
            0;

        private JsonValue(
            JsonValueKind kind,
            string stringValue,
            double numberValue,
            bool booleanValue,
            IReadOnlyDictionary<string, JsonValue> objectValue,
            IReadOnlyList<JsonValue> arrayValue)
        {
            Kind = kind;
            StringValue = stringValue ?? string.Empty;
            NumberValue = numberValue;
            BooleanValue = booleanValue;
            ObjectValue = objectValue ?? EmptyObject;
            ArrayValue = arrayValue ?? EmptyArray;
        }

        public static JsonValue CreateObject(IDictionary<string, JsonValue> value)
        {
            return new JsonValue(
                JsonValueKind.Object,
                string.Empty,
                0d,
                false,
                new Dictionary<string, JsonValue>(value ?? new Dictionary<string, JsonValue>()),
                EmptyArray);
        }

        public static JsonValue CreateArray(IList<JsonValue> value)
        {
            return new JsonValue(
                JsonValueKind.Array,
                string.Empty,
                0d,
                false,
                EmptyObject,
                new List<JsonValue>(value ?? new List<JsonValue>()));
        }

        public static JsonValue CreateString(string value)
        {
            return new JsonValue(JsonValueKind.String, value, 0d, false, EmptyObject, EmptyArray);
        }

        public static JsonValue CreateNumber(double value)
        {
            return new JsonValue(JsonValueKind.Number, string.Empty, value, false, EmptyObject, EmptyArray);
        }

        public static JsonValue CreateBoolean(bool value)
        {
            return new JsonValue(JsonValueKind.Boolean, string.Empty, 0d, value, EmptyObject, EmptyArray);
        }

        public bool TryGetProperty(string key, out JsonValue value)
        {
            if (Kind == JsonValueKind.Object && !string.IsNullOrEmpty(key))
            {
                return ObjectValue.TryGetValue(key, out value);
            }

            value = Null;
            return false;
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case JsonValueKind.String:
                    return StringValue;
                case JsonValueKind.Number:
                    return NumberValue.ToString(CultureInfo.InvariantCulture);
                case JsonValueKind.Boolean:
                    return BooleanValue ? "true" : "false";
                case JsonValueKind.Object:
                    return $"Object({Count})";
                case JsonValueKind.Array:
                    return $"Array({Count})";
                default:
                    return "null";
            }
        }
    }
}
