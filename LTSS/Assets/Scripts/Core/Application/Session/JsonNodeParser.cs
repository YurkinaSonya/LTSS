using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.Core.Application.Session
{
    public sealed class JsonNodeParser : IJsonNodeParser
    {
        public bool TryParse(string json, out JsonValue value, out string error)
        {
            value = JsonValue.Null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON is empty.";
                return false;
            }

            try
            {
                var parser = new Parser(json);
                value = parser.Parse();
                parser.ExpectEnd();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                value = JsonValue.Null;
                return false;
            }
        }

        private sealed class Parser
        {
            private readonly string _json;
            private int _index;

            public Parser(string json)
            {
                _json = json ?? string.Empty;
            }

            public JsonValue Parse()
            {
                SkipWhitespace();
                return ParseValue();
            }

            public void ExpectEnd()
            {
                SkipWhitespace();

                if (_index < _json.Length)
                {
                    throw new FormatException($"Unexpected trailing JSON content at index {_index}.");
                }
            }

            private JsonValue ParseValue()
            {
                SkipWhitespace();

                if (_index >= _json.Length)
                {
                    throw new FormatException("Unexpected end of JSON.");
                }

                switch (_json[_index])
                {
                    case '{':
                        return ParseObject();
                    case '[':
                        return ParseArray();
                    case '"':
                        return JsonValue.CreateString(ParseString());
                    case 't':
                        ReadLiteral("true");
                        return JsonValue.CreateBoolean(true);
                    case 'f':
                        ReadLiteral("false");
                        return JsonValue.CreateBoolean(false);
                    case 'n':
                        ReadLiteral("null");
                        return JsonValue.Null;
                    default:
                        return ParseNumber();
                }
            }

            private JsonValue ParseObject()
            {
                var result = new Dictionary<string, JsonValue>();
                Consume('{');
                SkipWhitespace();

                if (TryConsume('}'))
                {
                    return JsonValue.CreateObject(result);
                }

                while (true)
                {
                    SkipWhitespace();
                    var propertyName = ParseString();
                    SkipWhitespace();
                    Consume(':');
                    var propertyValue = ParseValue();
                    result[propertyName] = propertyValue;
                    SkipWhitespace();

                    if (TryConsume('}'))
                    {
                        break;
                    }

                    Consume(',');
                }

                return JsonValue.CreateObject(result);
            }

            private JsonValue ParseArray()
            {
                var result = new List<JsonValue>();
                Consume('[');
                SkipWhitespace();

                if (TryConsume(']'))
                {
                    return JsonValue.CreateArray(result);
                }

                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();

                    if (TryConsume(']'))
                    {
                        break;
                    }

                    Consume(',');
                }

                return JsonValue.CreateArray(result);
            }

            private string ParseString()
            {
                Consume('"');
                var builder = new StringBuilder();

                while (_index < _json.Length)
                {
                    var symbol = _json[_index++];

                    if (symbol == '"')
                    {
                        return builder.ToString();
                    }

                    if (symbol != '\\')
                    {
                        builder.Append(symbol);
                        continue;
                    }

                    if (_index >= _json.Length)
                    {
                        throw new FormatException("Unexpected end of JSON string.");
                    }

                    var escaped = _json[_index++];

                    switch (escaped)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            builder.Append(escaped);
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            builder.Append(ParseUnicodeSymbol());
                            break;
                        default:
                            throw new FormatException($"Unsupported string escape '\\{escaped}' at index {_index - 1}.");
                    }
                }

                throw new FormatException("Unexpected end of JSON string.");
            }

            private char ParseUnicodeSymbol()
            {
                if (_index + 4 > _json.Length)
                {
                    throw new FormatException("Unexpected end of unicode escape sequence.");
                }

                var rawHex = _json.Substring(_index, 4);
                _index += 4;

                if (!ushort.TryParse(rawHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                {
                    throw new FormatException($"Invalid unicode escape '\\u{rawHex}'.");
                }

                return (char)code;
            }

            private JsonValue ParseNumber()
            {
                var startIndex = _index;

                if (Peek('-'))
                {
                    _index++;
                }

                ReadDigits();

                if (Peek('.'))
                {
                    _index++;
                    ReadDigits();
                }

                if (Peek('e') || Peek('E'))
                {
                    _index++;

                    if (Peek('+') || Peek('-'))
                    {
                        _index++;
                    }

                    ReadDigits();
                }

                var rawNumber = _json.Substring(startIndex, _index - startIndex);

                if (!double.TryParse(rawNumber, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    throw new FormatException($"Invalid JSON number '{rawNumber}' at index {startIndex}.");
                }

                return JsonValue.CreateNumber(value);
            }

            private void ReadDigits()
            {
                var startIndex = _index;

                while (_index < _json.Length && char.IsDigit(_json[_index]))
                {
                    _index++;
                }

                if (startIndex == _index)
                {
                    throw new FormatException($"Expected digit at index {_index}.");
                }
            }

            private void ReadLiteral(string literal)
            {
                for (var i = 0; i < literal.Length; i++)
                {
                    if (_index + i >= _json.Length || _json[_index + i] != literal[i])
                    {
                        throw new FormatException($"Expected literal '{literal}' at index {_index}.");
                    }
                }

                _index += literal.Length;
            }

            private bool Peek(char symbol)
            {
                return _index < _json.Length && _json[_index] == symbol;
            }

            private bool TryConsume(char symbol)
            {
                if (!Peek(symbol))
                {
                    return false;
                }

                _index++;
                return true;
            }

            private void Consume(char symbol)
            {
                if (!TryConsume(symbol))
                {
                    throw new FormatException($"Expected '{symbol}' at index {_index}.");
                }
            }

            private void SkipWhitespace()
            {
                while (_index < _json.Length && char.IsWhiteSpace(_json[_index]))
                {
                    _index++;
                }
            }
        }
    }
}
