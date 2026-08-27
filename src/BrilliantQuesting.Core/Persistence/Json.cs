using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BrilliantQuesting.Persistence
{
    public enum JsonKind
    {
        Null,
        Bool,
        Number,
        String,
        Array,
        Object
    }

    /// <summary>
    /// A very small JSON tree.
    ///
    /// Written by hand rather than taken from a package on purpose: this assembly is loaded into
    /// Unity/Mono next to whatever else the player has installed, and a serializer dependency is
    /// exactly the kind of thing that turns into an assembly-resolution bug report. The save
    /// format is small and flat, so this is enough.
    /// </summary>
    public sealed class JsonValue
    {
        private readonly List<JsonValue> _array;
        private readonly List<KeyValuePair<string, JsonValue>> _members;

        private JsonValue(JsonKind kind)
        {
            Kind = kind;
            if (kind == JsonKind.Array)
            {
                _array = new List<JsonValue>();
            }
            else if (kind == JsonKind.Object)
            {
                _members = new List<KeyValuePair<string, JsonValue>>();
            }
        }

        public JsonKind Kind { get; }

        public bool BoolValue { get; private set; }

        public double NumberValue { get; private set; }

        public string StringValue { get; private set; }

        public static JsonValue Null() => new JsonValue(JsonKind.Null);

        public static JsonValue Bool(bool value) => new JsonValue(JsonKind.Bool) { BoolValue = value };

        public static JsonValue Number(double value) => new JsonValue(JsonKind.Number) { NumberValue = value };

        public static JsonValue String(string value)
        {
            return value == null ? Null() : new JsonValue(JsonKind.String) { StringValue = value };
        }

        public static JsonValue Array() => new JsonValue(JsonKind.Array);

        public static JsonValue Object() => new JsonValue(JsonKind.Object);

        public IReadOnlyList<JsonValue> Items => (IReadOnlyList<JsonValue>)_array ?? EmptyArray;

        public IReadOnlyList<KeyValuePair<string, JsonValue>> Members => (IReadOnlyList<KeyValuePair<string, JsonValue>>)_members ?? EmptyMembers;

        public int Count => _array?.Count ?? _members?.Count ?? 0;

        public JsonValue Add(JsonValue value)
        {
            if (_array == null)
            {
                throw new InvalidOperationException("Not an array.");
            }

            _array.Add(value ?? Null());
            return this;
        }

        /// <summary>Adds or replaces a member, keeping its original position when replacing.</summary>
        public JsonValue Set(string name, JsonValue value)
        {
            if (_members == null)
            {
                throw new InvalidOperationException("Not an object.");
            }

            KeyValuePair<string, JsonValue> member = new KeyValuePair<string, JsonValue>(name, value ?? Null());
            for (int i = 0; i < _members.Count; i++)
            {
                if (string.Equals(_members[i].Key, name, StringComparison.Ordinal))
                {
                    _members[i] = member;
                    return this;
                }
            }

            _members.Add(member);
            return this;
        }

        public JsonValue Set(string name, string value) => Set(name, String(value));

        public JsonValue Set(string name, double value) => Set(name, Number(value));

        public JsonValue Set(string name, bool value) => Set(name, Bool(value));

        public JsonValue this[string name]
        {
            get
            {
                if (_members != null)
                {
                    for (int i = 0; i < _members.Count; i++)
                    {
                        if (string.Equals(_members[i].Key, name, StringComparison.Ordinal))
                        {
                            return _members[i].Value;
                        }
                    }
                }

                return null;
            }
        }

        public string GetString(string name, string fallback = "")
        {
            JsonValue member = this[name];
            return member != null && member.Kind == JsonKind.String ? member.StringValue : fallback;
        }

        public double GetNumber(string name, double fallback = 0.0)
        {
            JsonValue member = this[name];
            return member != null && member.Kind == JsonKind.Number ? member.NumberValue : fallback;
        }

        public int GetInt(string name, int fallback = 0) => (int)GetNumber(name, fallback);

        public long GetLong(string name, long fallback = 0) => (long)GetNumber(name, fallback);

        public bool GetBool(string name, bool fallback = false)
        {
            JsonValue member = this[name];
            return member != null && member.Kind == JsonKind.Bool ? member.BoolValue : fallback;
        }

        public IReadOnlyList<JsonValue> GetArray(string name)
        {
            JsonValue member = this[name];
            return member != null && member.Kind == JsonKind.Array ? member.Items : EmptyArray;
        }

        public string ToJson(bool indented = false)
        {
            StringBuilder sb = new StringBuilder();
            Write(sb, this, indented, 0);
            return sb.ToString();
        }

        public static JsonValue Parse(string text)
        {
            int index = 0;
            JsonValue value = ParseValue(text, ref index);
            SkipWhitespace(text, ref index);
            if (index != text.Length)
            {
                throw new FormatException("Trailing content at offset " + index + ".");
            }

            return value;
        }

        private static readonly JsonValue[] EmptyArray = new JsonValue[0];
        private static readonly KeyValuePair<string, JsonValue>[] EmptyMembers = new KeyValuePair<string, JsonValue>[0];

        private static void Write(StringBuilder sb, JsonValue value, bool indented, int depth)
        {
            switch (value.Kind)
            {
                case JsonKind.Null:
                    sb.Append("null");
                    break;
                case JsonKind.Bool:
                    sb.Append(value.BoolValue ? "true" : "false");
                    break;
                case JsonKind.Number:
                    sb.Append(value.NumberValue.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case JsonKind.String:
                    WriteString(sb, value.StringValue);
                    break;
                case JsonKind.Array:
                    WriteArray(sb, value, indented, depth);
                    break;
                default:
                    WriteObject(sb, value, indented, depth);
                    break;
            }
        }

        private static void WriteArray(StringBuilder sb, JsonValue value, bool indented, int depth)
        {
            if (value.Count == 0)
            {
                sb.Append("[]");
                return;
            }

            sb.Append('[');
            for (int i = 0; i < value.Items.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                NewLine(sb, indented, depth + 1);
                Write(sb, value.Items[i], indented, depth + 1);
            }

            NewLine(sb, indented, depth);
            sb.Append(']');
        }

        private static void WriteObject(StringBuilder sb, JsonValue value, bool indented, int depth)
        {
            if (value.Count == 0)
            {
                sb.Append("{}");
                return;
            }

            sb.Append('{');
            for (int i = 0; i < value.Members.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                NewLine(sb, indented, depth + 1);
                WriteString(sb, value.Members[i].Key);
                sb.Append(':');
                if (indented)
                {
                    sb.Append(' ');
                }

                Write(sb, value.Members[i].Value, indented, depth + 1);
            }

            NewLine(sb, indented, depth);
            sb.Append('}');
        }

        private static void NewLine(StringBuilder sb, bool indented, int depth)
        {
            if (!indented)
            {
                return;
            }

            sb.Append('\n');
            sb.Append(' ', depth * 2);
        }

        private static void WriteString(StringBuilder sb, string text)
        {
            sb.Append('"');
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            sb.Append('"');
        }

        private static JsonValue ParseValue(string text, ref int index)
        {
            SkipWhitespace(text, ref index);
            if (index >= text.Length)
            {
                throw new FormatException("Unexpected end of input.");
            }

            char c = text[index];
            switch (c)
            {
                case '{': return ParseObject(text, ref index);
                case '[': return ParseArray(text, ref index);
                case '"': return String(ParseString(text, ref index));
                case 't': Expect(text, ref index, "true"); return Bool(true);
                case 'f': Expect(text, ref index, "false"); return Bool(false);
                case 'n': Expect(text, ref index, "null"); return Null();
                default: return Number(ParseNumber(text, ref index));
            }
        }

        private static JsonValue ParseObject(string text, ref int index)
        {
            JsonValue result = Object();
            index++;
            SkipWhitespace(text, ref index);
            if (index < text.Length && text[index] == '}')
            {
                index++;
                return result;
            }

            while (true)
            {
                SkipWhitespace(text, ref index);
                string name = ParseString(text, ref index);
                SkipWhitespace(text, ref index);
                if (index >= text.Length || text[index] != ':')
                {
                    throw new FormatException("Expected ':' at offset " + index + ".");
                }

                index++;
                result.Set(name, ParseValue(text, ref index));
                SkipWhitespace(text, ref index);
                if (index >= text.Length)
                {
                    throw new FormatException("Unterminated object.");
                }

                if (text[index] == ',')
                {
                    index++;
                    continue;
                }

                if (text[index] == '}')
                {
                    index++;
                    return result;
                }

                throw new FormatException("Expected ',' or '}' at offset " + index + ".");
            }
        }

        private static JsonValue ParseArray(string text, ref int index)
        {
            JsonValue result = Array();
            index++;
            SkipWhitespace(text, ref index);
            if (index < text.Length && text[index] == ']')
            {
                index++;
                return result;
            }

            while (true)
            {
                result.Add(ParseValue(text, ref index));
                SkipWhitespace(text, ref index);
                if (index >= text.Length)
                {
                    throw new FormatException("Unterminated array.");
                }

                if (text[index] == ',')
                {
                    index++;
                    continue;
                }

                if (text[index] == ']')
                {
                    index++;
                    return result;
                }

                throw new FormatException("Expected ',' or ']' at offset " + index + ".");
            }
        }

        private static string ParseString(string text, ref int index)
        {
            if (index >= text.Length || text[index] != '"')
            {
                throw new FormatException("Expected string at offset " + index + ".");
            }

            index++;
            StringBuilder sb = new StringBuilder();
            while (index < text.Length)
            {
                char c = text[index++];
                if (c == '"')
                {
                    return sb.ToString();
                }

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                char escape = text[index++];
                switch (escape)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        sb.Append((char)int.Parse(text.Substring(index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        index += 4;
                        break;
                    default:
                        throw new FormatException("Bad escape '\\" + escape + "'.");
                }
            }

            throw new FormatException("Unterminated string.");
        }

        private static double ParseNumber(string text, ref int index)
        {
            int start = index;
            while (index < text.Length && "+-.eE0123456789".IndexOf(text[index]) >= 0)
            {
                index++;
            }

            if (start == index)
            {
                throw new FormatException("Expected a value at offset " + index + ".");
            }

            return double.Parse(text.Substring(start, index - start), CultureInfo.InvariantCulture);
        }

        private static void Expect(string text, ref int index, string literal)
        {
            if (index + literal.Length > text.Length || string.CompareOrdinal(text, index, literal, 0, literal.Length) != 0)
            {
                throw new FormatException("Expected '" + literal + "' at offset " + index + ".");
            }

            index += literal.Length;
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }
        }
    }
}
