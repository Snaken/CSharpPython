using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CSharpPython.Blocks
{
    /// <summary>Thrown by <see cref="BlockGraph.FromJson"/> when the text is not a valid block graph document.</summary>
    public sealed class BlockGraphFormatException : Exception
    {
        public BlockGraphFormatException(string message) : base(message) { }
    }

    // A small hand-written JSON writer and reader (no reflection, so it is safe under IL2CPP and trimming). Field
    // names are the C# field names, which is what Unity's JsonUtility produces and expects.
    internal static class BlockJson
    {
        private const int MaxDepth = 64;

        // ---- writing ----

        public static string Write(BlockGraph graph, bool indent)
        {
            var w = new Writer(indent);
            w.Begin('{');
            w.Name("Version"); w.Raw(graph.Version.ToString(CultureInfo.InvariantCulture));
            w.Name("Variables");
            w.Array(graph.Variables, v => { w.Begin('{'); w.Str("Name", v.Name); w.Str("Initial", v.Initial); w.End('}'); });
            w.Name("Functions");
            w.Array(graph.Functions, f =>
            {
                w.Begin('{');
                w.Str("Name", f.Name);
                w.Name("Parameters");
                w.Array(f.Parameters, p => w.Value(p));
                w.Str("Start", f.Start);
                w.Name("Nodes");
                w.Array(f.Nodes, n =>
                {
                    w.Begin('{');
                    w.Str("Id", n.Id); w.Str("Block", n.Block); w.Str("Name", n.Name);
                    w.Name("Inputs");
                    w.Array(n.Inputs, i => { w.Begin('{'); w.Str("Pin", i.Pin); w.Str("From", i.From); w.Str("Literal", i.Literal); w.End('}'); });
                    w.Name("Flow");
                    w.Array(n.Flow, l => { w.Begin('{'); w.Str("Pin", l.Pin); w.Str("To", l.To); w.End('}'); });
                    w.End('}');
                });
                w.End('}');
            });
            w.End('}');
            return w.ToString();
        }

        private sealed class Writer
        {
            private readonly StringBuilder _sb = new StringBuilder();
            private readonly bool _indent;
            private int _depth;
            private bool _first = true;

            public Writer(bool indent) { _indent = indent; }

            public override string ToString() => _sb.ToString();

            private void Separator()
            {
                if (!_first) _sb.Append(',');
                _first = false;
                if (_indent) { _sb.Append('\n'); _sb.Append(' ', _depth * 2); }
            }

            public void Begin(char c)
            {
                _sb.Append(c);
                _depth++;
                _first = true;
            }

            public void End(char c)
            {
                _depth--;
                if (_indent && !_first) { _sb.Append('\n'); _sb.Append(' ', _depth * 2); }
                _sb.Append(c);
                _first = false;
            }

            public void Name(string name)
            {
                Separator();
                Quote(name);
                _sb.Append(_indent ? ": " : ":");
                _first = true;
            }

            public void Raw(string text)
            {
                _sb.Append(text);
                _first = false;
            }

            public void Value(string text)
            {
                Quote(text ?? "");
                _first = false;
            }

            public void Str(string name, string value)
            {
                if (value == null) return;
                Name(name);
                Quote(value);
                _first = false;
            }

            public void Array<T>(List<T> items, Action<T> each)
            {
                Begin('[');
                if (items != null)
                    foreach (var item in items)
                    {
                        if (item == null) continue;
                        Separator();
                        _first = true;
                        each(item);
                    }
                End(']');
            }

            private void Quote(string text)
            {
                _sb.Append('"');
                foreach (char c in text)
                {
                    switch (c)
                    {
                        case '"': _sb.Append("\\\""); break;
                        case '\\': _sb.Append("\\\\"); break;
                        case '\n': _sb.Append("\\n"); break;
                        case '\r': _sb.Append("\\r"); break;
                        case '\t': _sb.Append("\\t"); break;
                        default:
                            if (c < ' ' || c == '\u2028' || c == '\u2029' || c == '\u0085')
                                _sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            else _sb.Append(c);
                            break;
                    }
                }
                _sb.Append('"');
            }
        }

        // ---- reading ----

        public static BlockGraph Read(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var root = new Parser(json).ParseDocument();
            var obj = AsObject(root, "the document");
            var graph = new BlockGraph();
            if (obj.TryGetValue("Version", out var version) && version != null)
            {
                if (!(version is double d) || d != Math.Floor(d) || d < 0 || d > int.MaxValue)
                    throw new BlockGraphFormatException("'Version' must be a whole number");
                if (d > BlockGraph.CurrentVersion)
                    throw new BlockGraphFormatException("the document is version " + d.ToString(CultureInfo.InvariantCulture) + ", newer than this library understands (" + BlockGraph.CurrentVersion + ")");
                graph.Version = (int)d;
            }
            foreach (var v in Items(obj, "Variables"))
            {
                var o = AsObject(v, "a variable");
                graph.Variables.Add(new BlockVariable(Text(o, "Name"), Text(o, "Initial")));
            }
            foreach (var f in Items(obj, "Functions"))
            {
                var fo = AsObject(f, "a function");
                var fn = new BlockFunction { Name = Text(fo, "Name"), Start = Text(fo, "Start") };
                foreach (var p in Items(fo, "Parameters"))
                    fn.Parameters.Add(p as string ?? throw new BlockGraphFormatException("'Parameters' must hold strings"));
                foreach (var n in Items(fo, "Nodes"))
                {
                    var no = AsObject(n, "a block");
                    var node = new BlockNode { Id = Text(no, "Id"), Block = Text(no, "Block"), Name = Text(no, "Name") };
                    foreach (var i in Items(no, "Inputs"))
                    {
                        var io = AsObject(i, "an input");
                        node.Inputs.Add(new BlockInput(Text(io, "Pin"), Text(io, "From"), Text(io, "Literal")));
                    }
                    foreach (var l in Items(no, "Flow"))
                    {
                        var lo = AsObject(l, "a link");
                        node.Flow.Add(new BlockLink(Text(lo, "Pin"), Text(lo, "To")));
                    }
                    fn.Nodes.Add(node);
                }
                graph.Functions.Add(fn);
            }
            return graph;
        }

        private static Dictionary<string, object> AsObject(object value, string what)
        {
            return value as Dictionary<string, object> ?? throw new BlockGraphFormatException(what + " must be a JSON object");
        }

        private static string Text(Dictionary<string, object> o, string key)
        {
            if (!o.TryGetValue(key, out var v) || v == null) return null;
            return v as string ?? throw new BlockGraphFormatException("'" + key + "' must be a string");
        }

        private static List<object> Items(Dictionary<string, object> o, string key)
        {
            if (!o.TryGetValue(key, out var v) || v == null) return new List<object>();
            return v as List<object> ?? throw new BlockGraphFormatException("'" + key + "' must be an array");
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;

            public Parser(string s) { _s = s; }

            public object ParseDocument()
            {
                var v = ParseValue(0);
                SkipSpace();
                if (_i < _s.Length) Fail("unexpected text after the document");
                return v;
            }

            private void Fail(string message)
            {
                throw new BlockGraphFormatException("invalid JSON at position " + _i + ": " + message);
            }

            private void SkipSpace()
            {
                while (_i < _s.Length && (_s[_i] == ' ' || _s[_i] == '\t' || _s[_i] == '\n' || _s[_i] == '\r')) _i++;
            }

            private object ParseValue(int depth)
            {
                if (depth > MaxDepth) Fail("nested too deeply");
                SkipSpace();
                if (_i >= _s.Length) Fail("the text ends early");
                char c = _s[_i];
                if (c == '{') return ParseObject(depth);
                if (c == '[') return ParseArray(depth);
                if (c == '"') return ParseString();
                if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                if (Word("true")) return true;
                if (Word("false")) return false;
                if (Word("null")) return null;
                Fail("unexpected character '" + c + "'");
                return null;
            }

            private bool Word(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) return false;
                _i += word.Length;
                return true;
            }

            private Dictionary<string, object> ParseObject(int depth)
            {
                var result = new Dictionary<string, object>();
                _i++;
                SkipSpace();
                if (_i < _s.Length && _s[_i] == '}') { _i++; return result; }
                while (true)
                {
                    SkipSpace();
                    if (_i >= _s.Length || _s[_i] != '"') Fail("expected a field name");
                    string key = ParseString();
                    SkipSpace();
                    if (_i >= _s.Length || _s[_i] != ':') Fail("expected ':'");
                    _i++;
                    result[key] = ParseValue(depth + 1);
                    SkipSpace();
                    if (_i >= _s.Length) Fail("the text ends early");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == '}') { _i++; return result; }
                    Fail("expected ',' or '}'");
                }
            }

            private List<object> ParseArray(int depth)
            {
                var result = new List<object>();
                _i++;
                SkipSpace();
                if (_i < _s.Length && _s[_i] == ']') { _i++; return result; }
                while (true)
                {
                    result.Add(ParseValue(depth + 1));
                    SkipSpace();
                    if (_i >= _s.Length) Fail("the text ends early");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == ']') { _i++; return result; }
                    Fail("expected ',' or ']'");
                }
            }

            private string ParseString()
            {
                var sb = new StringBuilder();
                _i++;
                while (true)
                {
                    if (_i >= _s.Length) Fail("a string is not closed");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c < ' ') { _i--; Fail("a control character inside a string"); }
                    if (c != '\\') { sb.Append(c); continue; }
                    if (_i >= _s.Length) Fail("a string is not closed");
                    char e = _s[_i++];
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
                            if (_i + 4 > _s.Length || !int.TryParse(_s.Substring(_i, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code))
                                Fail("a bad \\u escape");
                            else { sb.Append((char)code); _i += 4; }
                            break;
                        default: _i--; Fail("a bad escape '\\" + e + "'"); break;
                    }
                }
            }

            private object ParseNumber()
            {
                int start = _i;
                while (_i < _s.Length && "+-0123456789.eE".IndexOf(_s[_i]) >= 0) _i++;
                if (!double.TryParse(_s.Substring(start, _i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                {
                    _i = start;
                    Fail("a bad number");
                }
                return d;
            }
        }
    }
}
