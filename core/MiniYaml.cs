using System.Collections.Generic;
using System.Text;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// 轻量级 YAML 解析器：基于缩进状态机，无外部依赖。
    /// 支持: object, array, string, number, bool, null。
    /// （改自 Inspiration-Core 的 MiniYaml：内联 [..]/{..} 不再走 JSON 子解析器，
    ///  保持为原始字符串；netstandard2.1 兼容。）
    /// </summary>
    public sealed class MiniYamlParser
    {
        private readonly string[] _lines;
        private int _lineIndex;

        public MiniYamlParser(string input)
        {
            _lines = (input ?? "").Split('\n');
        }

        public object? Parse()
        {
            _lineIndex = 0;
            SkipEmptyLines();
            if (_lineIndex >= _lines.Length) return null;
            return ParseValue(0);
        }

        public Dictionary<string, object?>? ParseObjectDocument()
        {
            return Parse() as Dictionary<string, object?>;
        }

        #region 核心解析

        private void SkipEmptyLines()
        {
            while (_lineIndex < _lines.Length)
            {
                var line = TrimEnd(CurrentLine());
                if (string.IsNullOrEmpty(line) || line.TrimStart().StartsWith("#", System.StringComparison.Ordinal))
                    _lineIndex++;
                else
                    break;
            }
        }

        private object? ParseValue(int parentIndent)
        {
            SkipEmptyLines();
            if (_lineIndex >= _lines.Length) return null;

            var line = CurrentLine();
            var indent = GetIndent(line);
            var content = line.TrimStart();

            if (indent < parentIndent && content.Length > 0)
                return null;

            if (content.StartsWith("- ", System.StringComparison.Ordinal))
                return ParseArray(indent);

            var colonIdx = content.IndexOf(':');
            if (colonIdx > 0 && !content.StartsWith("-", System.StringComparison.Ordinal))
            {
                var beforeColon = content.Substring(0, colonIdx);
                if (beforeColon.IndexOfAny(new[] { '{', '[' }) < 0)
                    return ParseObject(indent);
            }

            return ParseScalar(content);
        }

        #endregion

        #region 标量

        private object? ParseScalar(string content)
        {
            _lineIndex++;
            return ParseScalarValue(StripComment(content).Trim());
        }

        private static object? ParseScalarValue(string scalar)
        {
            if (scalar.Length == 0) return null;

            if ((scalar[0] == '"' && scalar[scalar.Length - 1] == '"') ||
                (scalar[0] == '\'' && scalar[scalar.Length - 1] == '\''))
            {
                var inner = scalar.Substring(1, scalar.Length - 2);
                return scalar[0] == '"' ? UnescapeString(inner) : inner.Replace("''", "'");
            }

            var lower = scalar.ToLowerInvariant();
            if (lower is "true" or "yes" or "on") return true;
            if (lower is "false" or "no" or "off") return false;
            if (lower is "null" or "nil" or "~") return null;

            if (long.TryParse(scalar, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var longVal)) return longVal;
            if (double.TryParse(scalar, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var dblVal)) return dblVal;

            return scalar;
        }

        #endregion

        #region 数组

        private List<object?> ParseArray(int parentIndent)
        {
            var list = new List<object?>();

            while (_lineIndex < _lines.Length)
            {
                SkipEmptyLines();
                if (_lineIndex >= _lines.Length) break;

                var line = CurrentLine();
                var indent = GetIndent(line);
                var content = line.TrimStart();

                if (indent < parentIndent) break;
                if (indent > parentIndent && !content.StartsWith("- ", System.StringComparison.Ordinal)) break;
                if (!content.StartsWith("- ", System.StringComparison.Ordinal)) break;

                var itemContent = content.Substring(2).Trim();
                _lineIndex++;

                if (itemContent.Length == 0 || itemContent[0] == '#')
                {
                    list.Add(ParseValue(indent + 2));
                }
                else if (itemContent.Contains(':') && itemContent[0] is not '"' and not '\'')
                {
                    var ci = itemContent.IndexOf(':');
                    if (ci > 0 && itemContent.Substring(0, ci).IndexOfAny(new[] { '{', '[' }) < 0)
                        list.Add(ParseInlineObject(itemContent));
                    else
                        list.Add(ParseScalarValue(itemContent));
                }
                else
                {
                    list.Add(ParseScalarValue(itemContent));
                }
            }

            return list;
        }

        #endregion

        #region 对象

        private Dictionary<string, object?> ParseObject(int parentIndent)
        {
            var obj = new Dictionary<string, object?>();

            while (_lineIndex < _lines.Length)
            {
                SkipEmptyLines();
                if (_lineIndex >= _lines.Length) break;

                var line = CurrentLine();
                var indent = GetIndent(line);
                var content = line.TrimStart();

                if (indent < parentIndent) break;
                if (indent > parentIndent) { _lineIndex++; continue; }

                var colonIdx = content.IndexOf(':');
                if (colonIdx < 0) break;

                var key = content.Substring(0, colonIdx).Trim();
                var valueContent = content.Substring(colonIdx + 1).Trim();

                _lineIndex++;

                if (valueContent.Length == 0 || valueContent[0] == '#')
                {
                    obj[key] = ParseValue(indent + 2);
                }
                else if (valueContent[0] is '|' or '>')
                {
                    obj[key] = ParseMultilineString(indent + 2, valueContent);
                }
                else if (valueContent.Contains(':') && valueContent[0] is not '"' and not '\'')
                {
                    obj[key] = ParseInlineObject(valueContent);
                }
                else
                {
                    obj[key] = ParseScalarValue(valueContent);
                }
            }

            return obj;
        }

        #endregion

        #region 多行文本

        private string ParseMultilineString(int minIndent, string indicator)
        {
            return indicator.Substring(1).Trim();
        }

        #endregion

        #region 内联对象

        private static Dictionary<string, object?> ParseInlineObject(string content)
        {
            var obj = new Dictionary<string, object?>();
            var pairs = content.Split(',');

            foreach (var pair in pairs)
            {
                var colonIdx = pair.IndexOf(':');
                if (colonIdx < 0) continue;
                var key = pair.Substring(0, colonIdx).Trim();
                var val = pair.Substring(colonIdx + 1).Trim();
                obj[key] = ParseScalarValue(val);
            }

            return obj;
        }

        #endregion

        #region 工具方法

        private string CurrentLine() => _lineIndex < _lines.Length ? _lines[_lineIndex] : string.Empty;

        private static int GetIndent(string line)
        {
            int count = 0;
            foreach (var ch in line)
            {
                if (ch == ' ') count++;
                else break;
            }
            return count;
        }

        private static string StripComment(string s)
        {
            if (s.Length == 0) return s;
            if (s[0] is '"' or '\'') return s;
            var idx = s.IndexOf('#');
            return idx < 0 ? s : s.Substring(0, idx).TrimEnd();
        }

        private static string TrimEnd(string s)
        {
            if (s == null) return string.Empty;
            return s.TrimEnd('\r');
        }

        private static string UnescapeString(string s)
        {
            return s
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\b", "\b")
                .Replace("\\f", "\f")
                .Replace("\\0", "\0");
        }

        #endregion
    }
}
