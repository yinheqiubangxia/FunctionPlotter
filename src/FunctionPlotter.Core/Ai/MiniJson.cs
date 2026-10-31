using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FunctionPlotter.Core.Ai
{
    /// <summary>
    /// 极简 JSON 读写器：只为调用 OpenAI 兼容的 chat/completions 接口服务，
    /// 不引入 Newtonsoft / System.Text.Json 依赖（netstandard2.0 + UWP 老工具链友好）。
    /// 支持对象、数组、字符串、数字、布尔、null，足以覆盖请求构造与响应解析。
    /// </summary>
    public static class MiniJson
    {
        public static string WriteObject(Dictionary<string, object> obj)
        {
            var sb = new StringBuilder();
            WriteValue(obj, sb);
            return sb.ToString();
        }

        private static void WriteValue(object value, StringBuilder sb)
        {
            if (value == null) { sb.Append("null"); return; }
            if (value is string s) { WriteString(s, sb); return; }
            if (value is bool b) { sb.Append(b ? "true" : "false"); return; }
            if (value is int || value is long || value is double || value is float || value is decimal)
            {
                sb.Append(Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (value is Dictionary<string, object> dict)
            {
                sb.Append('{');
                bool first = true;
                foreach (var kv in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(kv.Key, sb);
                    sb.Append(':');
                    WriteValue(kv.Value, sb);
                }
                sb.Append('}');
                return;
            }
            if (value is List<object> list)
            {
                sb.Append('[');
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    WriteValue(list[i], sb);
                }
                sb.Append(']');
                return;
            }
            throw new ArgumentException("MiniJson 不支持类型: " + value.GetType().FullName);
        }

        private static void WriteString(string s, StringBuilder sb)
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

        public static object Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("空的 JSON 内容");
            int pos = 0;
            object result = ParseValue(json, ref pos);
            SkipWs(json, ref pos);
            return result;
        }

        private static void SkipWs(string s, ref int pos)
        {
            while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
        }

        private static object ParseValue(string s, ref int pos)
        {
            SkipWs(s, ref pos);
            if (pos >= s.Length) throw new FormatException("JSON 意外结束");
            char c = s[pos];
            switch (c)
            {
                case '{':
                    {
                        var dict = new Dictionary<string, object>(StringComparer.Ordinal);
                        pos++;
                        SkipWs(s, ref pos);
                        if (pos < s.Length && s[pos] == '}') { pos++; return dict; }
                        while (true)
                        {
                            SkipWs(s, ref pos);
                            string key = ParseString(s, ref pos);
                            SkipWs(s, ref pos);
                            if (pos >= s.Length || s[pos] != ':') throw new FormatException("JSON 对象缺少冒号");
                            pos++;
                            dict[key] = ParseValue(s, ref pos);
                            SkipWs(s, ref pos);
                            if (pos < s.Length && s[pos] == ',') { pos++; continue; }
                            if (pos < s.Length && s[pos] == '}') { pos++; return dict; }
                            throw new FormatException("JSON 对象缺少逗号或结束括号");
                        }
                    }
                case '[':
                    {
                        var list = new List<object>();
                        pos++;
                        SkipWs(s, ref pos);
                        if (pos < s.Length && s[pos] == ']') { pos++; return list; }
                        while (true)
                        {
                            list.Add(ParseValue(s, ref pos));
                            SkipWs(s, ref pos);
                            if (pos < s.Length && s[pos] == ',') { pos++; continue; }
                            if (pos < s.Length && s[pos] == ']') { pos++; return list; }
                            throw new FormatException("JSON 数组缺少逗号或结束括号");
                        }
                    }
                case '"':
                    return ParseString(s, ref pos);
                case 't':
                    Expect(s, ref pos, "true"); return true;
                case 'f':
                    Expect(s, ref pos, "false"); return false;
                case 'n':
                    Expect(s, ref pos, "null"); return null;
                default:
                    {
                        int start = pos;
                        while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '-' || s[pos] == '+' || s[pos] == '.' || s[pos] == 'e' || s[pos] == 'E')) pos++;
                        string num = s.Substring(start, pos - start);
                        if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
                        throw new FormatException("无法解析 JSON 数字: " + num);
                    }
            }
        }

        private static string ParseString(string s, ref int pos)
        {
            if (pos >= s.Length || s[pos] != '"') throw new FormatException("JSON 期望字符串");
            pos++;
            var sb = new StringBuilder();
            while (pos < s.Length)
            {
                char c = s[pos++];
                if (c == '"') return sb.ToString();
                if (c == '\\' && pos < s.Length)
                {
                    char esc = s[pos++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (pos + 4 <= s.Length)
                            {
                                sb.Append((char)int.Parse(s.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                                pos += 4;
                            }
                            break;
                        default: sb.Append(esc); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            throw new FormatException("JSON 字符串未闭合");
        }

        private static void Expect(string s, ref int pos, string literal)
        {
            if (pos + literal.Length > s.Length || string.CompareOrdinal(s, pos, literal, 0, literal.Length) != 0)
                throw new FormatException("JSON 期望 " + literal);
            pos += literal.Length;
        }

        // —— 便捷取值：针对 Dictionary<string, object> 结构的链式访问 ——

        public static Dictionary<string, object> AsDict(object value)
        {
            return value as Dictionary<string, object>;
        }

        public static List<object> AsList(object value)
        {
            return value as List<object>;
        }

        public static string GetString(Dictionary<string, object> dict, string key)
        {
            if (dict != null && dict.TryGetValue(key, out object v)) return v as string;
            return null;
        }

        public static double? GetNumber(Dictionary<string, object> dict, string key)
        {
            if (dict != null && dict.TryGetValue(key, out object v) && v is double d) return d;
            return null;
        }
    }
}
