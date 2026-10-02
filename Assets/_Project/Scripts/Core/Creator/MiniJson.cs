using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FS27.Core
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
    /// A tiny JSON value. Core cannot depend on a JSON library (Unity ships none for plain C#), and the Creator Engine data is
    /// small and flat, so a ~200 line reader/writer is enough. Objects keep their keys sorted when written, so the same data
    /// always produces the same text (stable diffs, stable size).
    /// </summary>
    public sealed class JsonValue
    {
        public JsonKind Kind;
        public bool Bool;
        public double Number;
        public string String;
        public List<JsonValue> Items;
        public SortedDictionary<string, JsonValue> Members;

        public static JsonValue Null() { return new JsonValue { Kind = JsonKind.Null }; }
        public static JsonValue Of(bool b) { return new JsonValue { Kind = JsonKind.Bool, Bool = b }; }
        public static JsonValue Of(double d) { return new JsonValue { Kind = JsonKind.Number, Number = d }; }
        public static JsonValue Of(string s) { return s == null ? Null() : new JsonValue { Kind = JsonKind.String, String = s }; }
        public static JsonValue NewArray() { return new JsonValue { Kind = JsonKind.Array, Items = new List<JsonValue>() }; }
        public static JsonValue NewObject() { return new JsonValue { Kind = JsonKind.Object, Members = new SortedDictionary<string, JsonValue>(StringComparer.Ordinal) }; }

        public JsonValue Set(string key, JsonValue value) { Members[key] = value; return this; }
        public JsonValue Add(JsonValue value) { Items.Add(value); return this; }

        public bool TryGet(string key, out JsonValue value)
        {
            value = null;
            return Kind == JsonKind.Object && Members.TryGetValue(key, out value);
        }

        public string GetString(string key, string fallback = null)
        {
            return TryGet(key, out JsonValue v) && v.Kind == JsonKind.String ? v.String : fallback;
        }

        public double GetNumber(string key, double fallback = 0.0)
        {
            return TryGet(key, out JsonValue v) && v.Kind == JsonKind.Number ? v.Number : fallback;
        }
    }

    public sealed class JsonException : Exception
    {
        public int Position { get; }

        public JsonException(string message, int position) : base(message + " (at character " + position + ")")
        {
            Position = position;
        }
    }

    public static class Json
    {
        // ------------------------------------------------------------------ parse

        public static JsonValue Parse(string text)
        {
            if (text == null) throw new JsonException("Text is null", 0);
            int i = 0;
            JsonValue v = ParseValue(text, ref i, 0);
            SkipWhite(text, ref i);
            if (i != text.Length) throw new JsonException("Unexpected text after the JSON value", i);
            return v;
        }

        /// <summary>Parses without throwing: false (with the message) on bad input.</summary>
        public static bool TryParse(string text, out JsonValue value, out string error)
        {
            try
            {
                value = Parse(text);
                error = null;
                return true;
            }
            catch (JsonException e)
            {
                value = null;
                error = e.Message;
                return false;
            }
        }

        private const int MaxDepth = 32;

        private static void SkipWhite(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        private static JsonValue ParseValue(string s, ref int i, int depth)
        {
            if (depth > MaxDepth) throw new JsonException("Nesting is too deep", i);
            SkipWhite(s, ref i);
            if (i >= s.Length) throw new JsonException("Unexpected end of text", i);
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i, depth);
            if (c == '[') return ParseArray(s, ref i, depth);
            if (c == '"') return JsonValue.Of(ParseString(s, ref i));
            if (c == 't') { Expect(s, ref i, "true"); return JsonValue.Of(true); }
            if (c == 'f') { Expect(s, ref i, "false"); return JsonValue.Of(false); }
            if (c == 'n') { Expect(s, ref i, "null"); return JsonValue.Null(); }
            return ParseNumber(s, ref i);
        }

        private static void Expect(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw new JsonException("Expected '" + word + "'", i);
            i += word.Length;
        }

        private static JsonValue ParseObject(string s, ref int i, int depth)
        {
            var obj = JsonValue.NewObject();
            i++;
            SkipWhite(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return obj; }
            while (true)
            {
                SkipWhite(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new JsonException("Expected a quoted key", i);
                string key = ParseString(s, ref i);
                SkipWhite(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new JsonException("Expected ':'", i);
                i++;
                if (obj.Members.ContainsKey(key)) throw new JsonException("Duplicate key '" + key + "'", i);
                obj.Members[key] = ParseValue(s, ref i, depth + 1);
                SkipWhite(s, ref i);
                if (i >= s.Length) throw new JsonException("Unterminated object", i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return obj; }
                throw new JsonException("Expected ',' or '}'", i);
            }
        }

        private static JsonValue ParseArray(string s, ref int i, int depth)
        {
            var arr = JsonValue.NewArray();
            i++;
            SkipWhite(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return arr; }
            while (true)
            {
                arr.Items.Add(ParseValue(s, ref i, depth + 1));
                SkipWhite(s, ref i);
                if (i >= s.Length) throw new JsonException("Unterminated array", i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return arr; }
                throw new JsonException("Expected ',' or ']'", i);
            }
        }

        private static string ParseString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++;
            while (true)
            {
                if (i >= s.Length) throw new JsonException("Unterminated string", i);
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) throw new JsonException("Unterminated escape", i);
                char e = s[i++];
                switch (e)
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
                        if (i + 4 > s.Length) throw new JsonException("Bad \\u escape", i);
                        if (!int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code)) throw new JsonException("Bad \\u escape", i);
                        sb.Append((char)code);
                        i += 4;
                        break;
                    default: throw new JsonException("Unknown escape '\\" + e + "'", i);
                }
            }
        }

        private static JsonValue ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start) throw new JsonException("Unexpected character '" + s[start] + "'", start);
            if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double d) || double.IsNaN(d) || double.IsInfinity(d))
                throw new JsonException("Bad number", start);
            return JsonValue.Of(d);
        }

        // ------------------------------------------------------------------ write

        public static string Write(JsonValue value, bool indented = false)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, indented, 0);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, JsonValue v, bool indented, int level)
        {
            switch (v.Kind)
            {
                case JsonKind.Null: sb.Append("null"); break;
                case JsonKind.Bool: sb.Append(v.Bool ? "true" : "false"); break;
                case JsonKind.Number: sb.Append(FormatNumber(v.Number)); break;
                case JsonKind.String: WriteString(sb, v.String); break;
                case JsonKind.Array:
                    sb.Append('[');
                    for (int i = 0; i < v.Items.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        NewLine(sb, indented, level + 1);
                        WriteValue(sb, v.Items[i], indented, level + 1);
                    }
                    if (v.Items.Count > 0) NewLine(sb, indented, level);
                    sb.Append(']');
                    break;
                default:
                    sb.Append('{');
                    bool first = true;
                    foreach (KeyValuePair<string, JsonValue> kv in v.Members)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        NewLine(sb, indented, level + 1);
                        WriteString(sb, kv.Key);
                        sb.Append(indented ? ": " : ":");
                        WriteValue(sb, kv.Value, indented, level + 1);
                    }
                    if (v.Members.Count > 0) NewLine(sb, indented, level);
                    sb.Append('}');
                    break;
            }
        }

        private static void NewLine(StringBuilder sb, bool indented, int level)
        {
            if (!indented) return;
            sb.Append('\n');
            sb.Append(' ', level * 2);
        }

        /// <summary>Numbers are written with at most 4 decimals (data precision never needs more) and no trailing zeros.</summary>
        private static string FormatNumber(double d)
        {
            double r = Math.Round(d, 4);
            return r.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
