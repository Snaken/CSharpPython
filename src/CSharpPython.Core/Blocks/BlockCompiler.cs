using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CSharpPython.Syntax;

namespace CSharpPython.Blocks
{
    /// <summary>
    /// Turns a <see cref="BlockGraph"/> into Python source plus a <see cref="SourceMap"/>. The source is checked as
    /// it is written, so a graph that compiles here always produces a script that <see cref="PyVm.Compile"/> accepts.
    /// </summary>
    public static class BlockCompiler
    {
        /// <summary>
        /// Compiles the graph. Nothing is thrown for a bad graph; problems come back as diagnostics and
        /// <see cref="BlockCompileResult.Source"/> is null when any of them is an error.
        /// </summary>
        public static BlockCompileResult Compile(BlockGraph graph, BlockLibrary library = null)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            return new Generator(graph, library ?? new BlockLibrary()).Run();
        }
    }

    /// <summary>Builds the text of Python literals for <see cref="BlockInput.Literal"/> and <see cref="BlockVariable.Initial"/> safely.</summary>
    public static class BlockLiterals
    {
        public const string None = "None";
        public static string Int(long value) => value.ToString(CultureInfo.InvariantCulture);
        public static string Bool(bool value) => value ? "True" : "False";

        public static string Float(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentException("NaN and infinity have no literal form.", nameof(value));
            string s = value.ToString("R", CultureInfo.InvariantCulture);
            return s.IndexOfAny(new[] { '.', 'E', 'e' }) >= 0 ? s : s + ".0";
        }

        /// <summary>A double-quoted string literal that reads back as exactly <paramref name="value"/>.</summary>
        public static string String(string value)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in value ?? "")
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\x").Append(((int)c).ToString("x2", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }

    internal sealed class Generator
    {
        private struct Ex
        {
            public readonly string Text;
            public readonly bool Composite;

            public Ex(string text, bool composite)
            {
                Text = text;
                Composite = composite;
            }

            public string Atom => Composite ? "(" + Text + ")" : Text;
        }

        private struct Line
        {
            public readonly string Text;
            public readonly string NodeId;

            public Line(string text, string nodeId)
            {
                Text = text;
                NodeId = nodeId;
            }
        }

        private sealed class Scope
        {
            public readonly Scope Parent;
            public readonly HashSet<string> Done = new HashSet<string>();

            public Scope(Scope parent)
            {
                Parent = parent;
            }

            public bool Has(string id)
            {
                for (var s = this; s != null; s = s.Parent)
                    if (s.Done.Contains(id)) return true;
                return false;
            }
        }

        private enum Callee { Unknown, User, Pure, Impure }

        private static readonly Dictionary<string, string> BinaryOps = new Dictionary<string, string>
        {
            { "add", "+" }, { "sub", "-" }, { "mul", "*" }, { "div", "/" }, { "floordiv", "//" }, { "mod", "%" }, { "pow", "**" },
            { "eq", "==" }, { "ne", "!=" }, { "lt", "<" }, { "le", "<=" }, { "gt", ">" }, { "ge", ">=" },
            { "and", "and" }, { "or", "or" }, { "contains", "in" },
        };

        private readonly BlockGraph _graph;
        private readonly BlockLibrary _lib;
        private readonly List<BlockDiagnostic> _diags = new List<BlockDiagnostic>();
        private readonly Dictionary<string, BlockVariable> _globals = new Dictionary<string, BlockVariable>();
        private readonly Dictionary<string, BlockFunction> _functions = new Dictionary<string, BlockFunction>();
        private readonly Dictionary<string, BlockFunction> _owner = new Dictionary<string, BlockFunction>();
        private readonly List<string> _imports = new List<string>();

        // State for the function being generated.
        private BlockFunction _fn;
        private Dictionary<string, BlockNode> _nodes;
        private HashSet<string> _known, _referenced, _flowSeen, _valueUsed, _pinsChecked, _evaluating;
        private Dictionary<string, string> _temps;
        private int _tempCount, _loopDepth;
        // True while a loop condition is read: it runs on every pass, so it cannot use a result stored before the loop.
        private bool _noResults;
        private Scope _scope;
        private List<Line> _out;

        public Generator(BlockGraph graph, BlockLibrary lib)
        {
            _graph = graph;
            _lib = lib;
        }

        private bool HasErrors => _diags.Any(d => d.Severity == BlockSeverity.Error);

        private void Error(string message, BlockNode node = null, string pin = null) =>
            _diags.Add(new BlockDiagnostic(BlockSeverity.Error, message, _fn?.Name, node?.Id, pin));

        private void Warn(string message, BlockNode node) =>
            _diags.Add(new BlockDiagnostic(BlockSeverity.Warning, message, _fn?.Name, node?.Id));

        public BlockCompileResult Run()
        {
            var header = new List<Line>();
            CheckGlobals(header);
            CheckFunctionNames();
            IndexNodes();

            var chunks = new List<List<Line>>();
            foreach (var fn in _graph.Functions)
            {
                if (fn == null) continue;
                _fn = fn;
                _out = new List<Line>();
                GenerateFunction();
                chunks.Add(_out);
            }
            _fn = null;

            if (HasErrors) return new BlockCompileResult(null, null, _diags);

            var all = new List<Line>();
            foreach (var module in _imports) all.Add(new Line("import " + module, null));
            all.AddRange(header);
            for (int i = 0; i < chunks.Count; i++)
            {
                if (all.Count > 0) all.Add(new Line("", null));
                all.AddRange(chunks[i]);
            }

            var text = new StringBuilder();
            foreach (var l in all) text.Append(l.Text).Append('\n');
            return new BlockCompileResult(text.ToString(), new SourceMap(all.Select(l => l.NodeId).ToArray()), _diags);
        }

        // ---- Names and literals ----

        private static List<Token> Significant(string text, out bool clean)
        {
            var tokens = Lexer.TokenizeTolerant(text, out var diagnostics);
            clean = diagnostics.Count == 0;
            while (tokens.Count > 0)
            {
                var k = tokens[tokens.Count - 1].Kind;
                if (k == TokenKind.EndOfFile || k == TokenKind.Newline || k == TokenKind.Dedent) tokens.RemoveAt(tokens.Count - 1);
                else break;
            }
            return tokens;
        }

        private static bool IsIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var tokens = Significant(name, out bool clean);
            if (!clean || tokens.Count != 1 || tokens[0].Kind != TokenKind.Name || tokens[0].Text != name) return false;
            // _b1, _b2, ... are the names the generator gives to intermediate results.
            if (name.Length > 2 && name.StartsWith("_b", StringComparison.Ordinal) && name.Skip(2).All(c => c >= '0' && c <= '9')) return false;
            return true;
        }

        private static string CheckLiteral(string literal)
        {
            string text = literal.Trim();
            if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0) return null;
            var t = Significant(text, out bool clean);
            if (!clean) return null;
            bool number(TokenKind k) => k == TokenKind.Int || k == TokenKind.Float;
            if (t.Count == 1 && (number(t[0].Kind) || t[0].Kind == TokenKind.String || t[0].Kind == TokenKind.KwTrue ||
                                 t[0].Kind == TokenKind.KwFalse || t[0].Kind == TokenKind.KwNone))
                return text;
            if (t.Count == 2 && t[0].Kind == TokenKind.Minus && number(t[1].Kind)) return text;
            return null;
        }

        private void CheckGlobals(List<Line> header)
        {
            foreach (var v in _graph.Variables)
            {
                if (v == null) continue;
                if (!IsIdentifier(v.Name))
                {
                    Error("'" + v.Name + "' is not a valid variable name");
                    continue;
                }
                if (_globals.ContainsKey(v.Name))
                {
                    Error("the variable '" + v.Name + "' is defined twice");
                    continue;
                }
                _globals[v.Name] = v;
                string literal = string.IsNullOrWhiteSpace(v.Initial) ? BlockLiterals.None : CheckLiteral(v.Initial);
                if (literal == null)
                {
                    Error("the starting value of '" + v.Name + "' must be a single number, string, True, False or None");
                    literal = BlockLiterals.None;
                }
                header.Add(new Line(v.Name + " = " + literal, null));
            }
        }

        private void CheckFunctionNames()
        {
            foreach (var fn in _graph.Functions)
            {
                if (fn == null) continue;
                if (!IsIdentifier(fn.Name))
                {
                    Error("'" + fn.Name + "' is not a valid function name");
                    continue;
                }
                if (_functions.ContainsKey(fn.Name)) Error("the function '" + fn.Name + "' is defined twice");
                else if (_globals.ContainsKey(fn.Name)) Error("'" + fn.Name + "' is both a variable and a function");
                else _functions[fn.Name] = fn;
            }
        }

        private void IndexNodes()
        {
            foreach (var fn in _graph.Functions)
            {
                if (fn == null) continue;
                _fn = fn;
                foreach (var node in fn.Nodes)
                {
                    if (node == null) continue;
                    if (string.IsNullOrEmpty(node.Id)) Error("a block has no id", node);
                    else if (_owner.ContainsKey(node.Id)) Error("the block id '" + node.Id + "' is used more than once", node);
                    else _owner[node.Id] = fn;
                }
            }
            _fn = null;
        }

        // ---- Functions ----

        private IEnumerable<BlockNode> NodesOf(BlockFunction fn) => fn.Nodes.Where(n => n != null && !string.IsNullOrEmpty(n.Id) && _owner.TryGetValue(n.Id, out var o) && o == fn);

        private void GenerateFunction()
        {
            _nodes = new Dictionary<string, BlockNode>();
            foreach (var n in NodesOf(_fn)) _nodes[n.Id] = n;
            _known = new HashSet<string>();
            _referenced = new HashSet<string>();
            _flowSeen = new HashSet<string>();
            _valueUsed = new HashSet<string>();
            _pinsChecked = new HashSet<string>();
            _evaluating = new HashSet<string>();
            _temps = new Dictionary<string, string>();
            _tempCount = 0;
            _loopDepth = 0;
            _scope = new Scope(null);

            var parameters = new List<string>();
            foreach (var p in _fn.Parameters)
            {
                if (!IsIdentifier(p)) Error("'" + p + "' is not a valid parameter name");
                else if (!_known.Add(p)) Error("the parameter '" + p + "' appears twice");
                else parameters.Add(p);
            }
            foreach (var g in _globals.Keys) _known.Add(g);

            var paramSet = new HashSet<string>(parameters);
            var globalsAssigned = new List<string>();
            foreach (var n in _nodes.Values)
            {
                foreach (var input in n.Inputs ?? new List<BlockInput>())
                    if (input != null && !string.IsNullOrEmpty(input.From)) _referenced.Add(input.From);

                bool assigns = n.Block == "set" || n.Block == "change" || n.Block == "for_each" || n.Block == "for_range" || (n.Block == "repeat" && !string.IsNullOrEmpty(n.Name));
                if (!assigns || !IsIdentifier(n.Name) || paramSet.Contains(n.Name)) continue;
                if (_globals.ContainsKey(n.Name))
                {
                    if (!globalsAssigned.Contains(n.Name)) globalsAssigned.Add(n.Name);
                }
                else if (n.Block != "change")
                {
                    if (_functions.ContainsKey(n.Name)) Error("'" + n.Name + "' is both a variable and a function", n);
                    else _known.Add(n.Name);
                }
            }

            _out.Add(new Line("def " + _fn.Name + "(" + string.Join(", ", parameters) + "):", null));
            int bodyStart = _out.Count;
            if (globalsAssigned.Count > 0) _out.Add(new Line(Indent(1) + "global " + string.Join(", ", globalsAssigned), null));
            EmitBody(_fn.Start, 1);
            if (!HasStatement(bodyStart)) _out.Add(new Line(Indent(1) + "pass", null));

            foreach (var n in _nodes.Values)
                if (!_flowSeen.Contains(n.Id) && !_valueUsed.Contains(n.Id))
                    Warn("this block is not connected to anything, so it never runs", n);
        }

        private static string Indent(int depth) => new string(' ', depth * 4);

        private void Emit(int depth, string text, BlockNode node) => _out.Add(new Line(Indent(depth) + text, node?.Id));

        // Emits a nested block of statements, writing `pass` if the branch is empty.
        private void EmitBody(string start, int depth)
        {
            var saved = _scope;
            _scope = new Scope(saved);
            int before = _out.Count;
            EmitChain(start, depth);
            if (!HasStatement(before)) _out.Add(new Line(Indent(depth) + "pass", null));
            _scope = saved;
        }

        // Whether the lines written since 'from' include code, not just comments (a body needs at least one statement).
        private bool HasStatement(int from)
        {
            for (int i = from; i < _out.Count; i++)
                if (!_out[i].Text.TrimStart().StartsWith("#", StringComparison.Ordinal)) return true;
            return false;
        }

        // A comment block's text, one '#' line per line of text. Control characters would end the line, so they become spaces.
        private void EmitComment(BlockNode node, int depth)
        {
            var text = new StringBuilder();
            foreach (char c in (node.Name ?? "").Replace("\r\n", "\n"))
                text.Append(c == '\n' ? '\n' : (c < ' ' && c != '\t') || c == '\u0085' || c == '\u2028' || c == '\u2029' ? ' ' : c);
            foreach (var line in text.ToString().Split('\n'))
                Emit(depth, line.Length == 0 ? "#" : "# " + line, node);
        }

        private string FlowTarget(BlockNode node, string pin)
        {
            foreach (var link in node.Flow ?? new List<BlockLink>())
                if (link != null && link.Pin == pin && !string.IsNullOrEmpty(link.To)) return link.To;
            return null;
        }

        private void EmitChain(string id, int depth)
        {
            while (!string.IsNullOrEmpty(id))
            {
                if (!_nodes.TryGetValue(id, out var node))
                {
                    Error("the flow leads to the block '" + id + "', which is not in this function");
                    return;
                }
                if (!_flowSeen.Add(id))
                {
                    Error("this block is reached more than once; flow cannot loop back or join up again (use a loop block)", node);
                    return;
                }

                BlockSpec spec = _lib.FindBlock(node.Block);
                bool isCall = node.Block == "call";
                if (!isCall && spec == null)
                {
                    Error("unknown block '" + node.Block + "'", node);
                    return;
                }
                if (!isCall && spec.Kind == BlockKind.Value)
                {
                    Error("'" + node.Block + "' produces a value; connect its output to an input instead of the execution flow", node);
                    return;
                }
                if (!CheckPins(node)) return;

                string next = FlowTarget(node, "next");
                switch (node.Block)
                {
                    case "set":
                        Emit(depth, RequireVariable(node, false) + " = " + Input(node, "value").Text, node);
                        break;
                    case "change":
                        Emit(depth, RequireVariable(node, true) + " += " + Input(node, "value").Text, node);
                        break;
                    case "set_index":
                        Emit(depth, Input(node, "list").Atom + "[" + Input(node, "index").Text + "] = " + Input(node, "value").Text, node);
                        break;
                    case "append":
                        Emit(depth, Input(node, "list").Atom + ".append(" + Input(node, "value").Text + ")", node);
                        break;
                    case "remove_at":
                        Emit(depth, "del " + Input(node, "list").Atom + "[" + Input(node, "index").Text + "]", node);
                        break;
                    case "set_attr":
                        Emit(depth, AttributeBase(Input(node, "object")) + "." + node.Name + " = " + Input(node, "value").Text, node);
                        break;
                    case "comment":
                        EmitComment(node, depth);
                        break;
                    case "call":
                        EmitCall(node, depth);
                        break;
                    case "if":
                        Emit(depth, "if " + Input(node, "cond").Text + ":", node);
                        EmitBody(FlowTarget(node, "then"), depth + 1);
                        string other = FlowTarget(node, "else");
                        if (other != null)
                        {
                            Emit(depth, "else:", node);
                            EmitBody(other, depth + 1);
                        }
                        break;
                    case "while":
                        _noResults = true;
                        string condition = Input(node, "cond").Text;
                        _noResults = false;
                        Emit(depth, "while " + condition + ":", node);
                        EmitLoopBody(node, depth);
                        break;
                    case "repeat":
                        string counter = string.IsNullOrEmpty(node.Name) ? NewTemp() : RequireVariable(node, false);
                        Emit(depth, "for " + counter + " in range(" + Input(node, "count").Text + "):", node);
                        EmitLoopBody(node, depth);
                        break;
                    case "for_range":
                    {
                        var stop = Input(node, "stop");
                        var start = Input(node, "start", optional: true);
                        var step = Input(node, "step", optional: true);
                        string range = step.Text != null ? (start.Text ?? "0") + ", " + stop.Text + ", " + step.Text
                            : start.Text != null ? start.Text + ", " + stop.Text : stop.Text;
                        Emit(depth, "for " + RequireVariable(node, false) + " in range(" + range + "):", node);
                        EmitLoopBody(node, depth);
                        break;
                    }
                    case "for_each":
                        Emit(depth, "for " + RequireVariable(node, false) + " in " + Input(node, "items").Text + ":", node);
                        EmitLoopBody(node, depth);
                        break;
                    case "break":
                    case "continue":
                        if (_loopDepth == 0) Error("'" + node.Block + "' only works inside a loop", node);
                        Emit(depth, node.Block, node);
                        break;
                    case "return":
                        var value = Input(node, "value", optional: true);
                        Emit(depth, value.Text == null ? "return" : "return " + value.Text, node);
                        break;
                }
                _scope.Done.Add(id);
                id = next;
            }
        }

        private void EmitLoopBody(BlockNode node, int depth)
        {
            _loopDepth++;
            EmitBody(FlowTarget(node, "body"), depth + 1);
            _loopDepth--;
        }

        private string NewTemp() => "_b" + (++_tempCount).ToString(CultureInfo.InvariantCulture);

        private void EmitCall(BlockNode node, int depth)
        {
            var kind = ClassifyCall(node, out _);
            if (kind == Callee.Pure)
            {
                Error("'" + node.Name + "' returns a value; connect its output to an input instead of the execution flow", node);
                return;
            }
            string text = Call(node).Text;
            if (_referenced.Contains(node.Id))
            {
                string temp = NewTemp();
                _temps[node.Id] = temp;
                Emit(depth, temp + " = " + text, node);
            }
            else Emit(depth, text, node);
        }

        // ---- Pins ----

        private bool CheckPins(BlockNode node)
        {
            if (!_pinsChecked.Add(node.Id)) return true;
            bool ok = true;
            IReadOnlyList<string> inputs;
            IReadOnlyList<string> flows;
            bool variadic = node.Block == "list";
            if (node.Block == "call")
            {
                var kind = ClassifyCall(node, out int arity);
                if (kind == Callee.Unknown)
                {
                    Error(string.IsNullOrEmpty(node.Name) ? "a call block needs a function name" : "unknown function '" + node.Name + "'", node);
                    return false;
                }
                inputs = Enumerable.Range(0, arity).Select(FunctionSpec.ArgPin).ToArray();
                flows = kind == Callee.Pure ? new string[0] : new[] { "next" };
            }
            else
            {
                var spec = _lib.FindBlock(node.Block);
                inputs = spec.Inputs;
                flows = spec.FlowPins;
                bool optionalName = node.Block == "repeat" && string.IsNullOrEmpty(node.Name);
                if ((spec.NeedsName || node.Block == "repeat") && !optionalName && !IsIdentifier(node.Name))
                {
                    Error("'" + node.Name + "' is not a valid " + (node.Block == "get_attr" || node.Block == "set_attr" ? "field" : "variable") + " name", node);
                    ok = false;
                }
            }

            var seen = new HashSet<string>();
            foreach (var input in node.Inputs ?? new List<BlockInput>())
            {
                if (input == null) continue;
                string pin = input.Pin ?? "";
                bool known = inputs.Contains(pin) || (variadic && IsItemPin(pin));
                if (!known)
                {
                    Error("'" + node.Block + "' has no input pin '" + pin + "'", node, pin);
                    ok = false;
                }
                else if (!seen.Add(pin))
                {
                    Error("the input pin '" + pin + "' is connected twice", node, pin);
                    ok = false;
                }
            }
            seen.Clear();
            foreach (var link in node.Flow ?? new List<BlockLink>())
            {
                if (link == null) continue;
                string pin = link.Pin ?? "";
                if (!flows.Contains(pin))
                {
                    Error("'" + node.Block + "' has no execution pin '" + pin + "'", node, pin);
                    ok = false;
                }
                else if (!seen.Add(pin))
                {
                    Error("the execution pin '" + pin + "' is linked twice", node, pin);
                    ok = false;
                }
            }
            return ok;
        }

        private static bool IsItemPin(string pin) =>
            pin.Length > 4 && pin.StartsWith("item", StringComparison.Ordinal) && pin.Skip(4).All(c => c >= '0' && c <= '9');

        private string RequireVariable(BlockNode node, bool mustExist)
        {
            string name = node.Name;
            if (mustExist && !_known.Contains(name))
                Error("the variable '" + name + "' does not exist; set it first or add it as a global variable", node);
            return name;
        }

        // ---- Values ----

        private static readonly Ex Missing = new Ex(null, false);

        // Reads an input pin. `optional` returns Missing (null text) instead of reporting an empty pin.
        private Ex Input(BlockNode node, string pin, bool optional = false)
        {
            BlockInput input = null;
            foreach (var i in node.Inputs ?? new List<BlockInput>())
                if (i != null && i.Pin == pin) input = i;

            if (input == null || (string.IsNullOrEmpty(input.From) && string.IsNullOrWhiteSpace(input.Literal)))
            {
                if (optional) return Missing;
                Error("the input '" + pin + "' has no value; connect a block or type one in", node, pin);
                return new Ex("None", false);
            }
            if (!string.IsNullOrEmpty(input.From)) return Reference(node, pin, input.From);

            string literal = CheckLiteral(input.Literal);
            if (literal == null)
            {
                Error("the input '" + pin + "' must be a single number, string, True, False or None", node, pin);
                return new Ex("None", false);
            }
            return new Ex(literal, literal[0] == '-');
        }

        private Ex Reference(BlockNode owner, string pin, string fromId)
        {
            if (!_nodes.TryGetValue(fromId, out var source))
            {
                Error("the input '" + pin + "' is connected to the block '" + fromId + "', which is not in this function", owner, pin);
                return new Ex("None", false);
            }

            bool isCall = source.Block == "call";
            var spec = _lib.FindBlock(source.Block);
            if (!isCall && spec == null)
            {
                Error("unknown block '" + source.Block + "'", source);
                return new Ex("None", false);
            }
            if (!isCall && spec.Kind == BlockKind.Flow)
            {
                Error("'" + source.Block + "' has no value to connect to the input '" + pin + "'", owner, pin);
                return new Ex("None", false);
            }

            var kind = isCall ? ClassifyCall(source, out _) : Callee.Pure;
            if (kind == Callee.Unknown)
            {
                Error(string.IsNullOrEmpty(source.Name) ? "a call block needs a function name" : "unknown function '" + source.Name + "'", source);
                return new Ex("None", false);
            }
            if (kind != Callee.Pure)
            {
                if (_noResults)
                {
                    Error("a loop condition cannot use the result of '" + source.Name + "'; compute it inside the loop and use if/break", owner, pin);
                    return new Ex("None", false);
                }
                if (!_temps.TryGetValue(fromId, out var temp) || !_scope.Has(fromId))
                {
                    Error("this uses the result of '" + source.Name + "' before it has run; put that block earlier in the flow, in the same or an enclosing branch", owner, pin);
                    return new Ex("None", false);
                }
                _valueUsed.Add(fromId);
                return new Ex(temp, false);
            }

            return Value(source);
        }

        private Ex Value(BlockNode node)
        {
            if (!_evaluating.Add(node.Id))
            {
                Error("this block's value depends on itself", node);
                return new Ex("None", false);
            }
            try
            {
                _valueUsed.Add(node.Id);
                if (!CheckPins(node)) return new Ex("None", false);

                switch (node.Block)
                {
                    case "literal":
                        return Input(node, "value");
                    case "get":
                        return new Ex(RequireVariable(node, true), false);
                    case "neg":
                        return new Ex("-" + Input(node, "a").Atom, true);
                    case "not":
                        return new Ex("not " + Input(node, "a").Atom, true);
                    case "is_none":
                        return new Ex(Input(node, "a").Atom + " is None", true);
                    case "choose":
                        return new Ex(Input(node, "a").Atom + " if " + Input(node, "cond").Atom + " else " + Input(node, "b").Atom, true);
                    case "get_attr":
                        return new Ex(AttributeBase(Input(node, "object")) + "." + node.Name, false);
                    case "index":
                        return new Ex(Input(node, "list").Atom + "[" + Input(node, "index").Text + "]", false);
                    case "list":
                        return ListOf(node);
                    case "call":
                        return Call(node);
                }
                // Every other value block is a binary operator.
                return new Ex(Input(node, "a").Atom + " " + BinaryOps[node.Block] + " " + Input(node, "b").Atom, true);
            }
            finally
            {
                _evaluating.Remove(node.Id);
            }
        }

        // The text an attribute is read from; a bare number needs brackets, since 5.real reads as a float.
        private static string AttributeBase(Ex value) =>
            value.Text.Length > 0 && char.IsDigit(value.Text[0]) ? "(" + value.Text + ")" : value.Atom;

        private Ex ListOf(BlockNode node)
        {
            int count = (node.Inputs ?? new List<BlockInput>()).Count(i => i != null);
            var parts = new List<string>();
            for (int i = 0; i < count; i++)
            {
                string pin = "item" + i.ToString(CultureInfo.InvariantCulture);
                bool present = node.Inputs.Any(x => x != null && x.Pin == pin);
                if (!present)
                {
                    Error("a list's items must be numbered item0, item1, ... without gaps", node, pin);
                    return new Ex("[]", false);
                }
                parts.Add(Input(node, pin).Text);
            }
            return new Ex("[" + string.Join(", ", parts) + "]", false);
        }

        // ---- Calls ----

        private Callee ClassifyCall(BlockNode node, out int arity)
        {
            arity = 0;
            if (string.IsNullOrEmpty(node.Name)) return Callee.Unknown;
            if (_functions.TryGetValue(node.Name, out var user))
            {
                arity = user.Parameters.Count;
                return Callee.User;
            }
            var spec = _lib.FindFunction(node.Name);
            if (spec == null) return Callee.Unknown;
            arity = spec.Parameters.Count;
            return spec.Pure ? Callee.Pure : Callee.Impure;
        }

        private Ex Call(BlockNode node)
        {
            // Callers have already checked that the function exists.
            var kind = ClassifyCall(node, out int arity);

            string target = node.Name;
            var spec = kind == Callee.User ? null : _lib.FindFunction(node.Name);
            if (spec != null && spec.Module != null)
            {
                target = spec.Module + "." + spec.Name;
                if (!_imports.Contains(spec.Module)) _imports.Add(spec.Module);
            }

            var args = new List<string>();
            for (int i = 0; i < arity; i++) args.Add(Input(node, FunctionSpec.ArgPin(i)).Text);
            return new Ex(target + "(" + string.Join(", ", args) + ")", false);
        }
    }
}
