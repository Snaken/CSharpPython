using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CSharpPython.Syntax;

namespace CSharpPython.Blocks
{
    /// <summary>
    /// Checks the types of the wires in a graph. <see cref="BlockCompiler"/> uses it, so a graph with a bad wire never
    /// compiles, and an editor calls <see cref="CheckWire"/> as the player draws a wire to refuse it with the reason.
    /// </summary>
    /// <remarks>
    /// Every block has a type for each input pin and for its value (see <see cref="BlockSpec.InputType"/> and
    /// <see cref="FunctionSpec.ParameterTypes"/>), and <see cref="PinCompatibility"/> decides each wire. A pin
    /// written <c>$T</c> is a wildcard: every pin of the block that mentions <c>$T</c> must agree, <c>$T</c> is the
    /// widest of the types wired to those pins (an <c>int</c> and a <c>float</c> make <c>float</c>), and the block's
    /// value is typed from it. A <c>list</c> block is the exception: its items need not match. Wires are read in pin order, so the answer does not depend on the order they were
    /// drawn in. A typed-in literal has the type of the literal; <c>None</c> and variables are <c>any</c>.
    /// The operators (<c>add</c>, <c>mul</c>, ...) give <c>int</c> or <c>float</c> for numbers,
    /// <c>str</c> or a list when both sides are, and <c>any</c> otherwise; two typed inputs Python cannot combine
    /// (text minus a number) are refused, while a pin of unknown type is not. Problems that are not about types
    /// (an unknown block, a missing input) are left to the compiler.
    /// </remarks>
    public static class BlockTypeChecker
    {
        /// <summary>Every wire in the graph whose type does not fit, as error diagnostics.</summary>
        public static IReadOnlyList<BlockDiagnostic> Check(BlockGraph graph, BlockLibrary library = null)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            var lib = library ?? new BlockLibrary();
            var userFunctions = UserFunctions(graph);
            var variables = BlockVariableTypes.Table(graph);
            var diagnostics = new List<BlockDiagnostic>();
            foreach (var fn in graph.Functions)
                if (fn != null) diagnostics.AddRange(new FunctionTypes(fn, lib, userFunctions, variables).Diagnostics());
            return diagnostics;
        }

        /// <summary>
        /// The type of a block's value in a function, with wildcards resolved from its wires, or null if there is no
        /// such block or it is a step in the flow with no value (a loop block has one: its item or count). An editor uses it to colour a pin. For a block with
        /// several outputs, name one with <paramref name="outputPin"/>; without it the answer is <c>any</c>.
        /// </summary>
        public static PinType OutputType(BlockGraph graph, string function, string nodeId, BlockLibrary library = null, string outputPin = null)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            var fn = graph.Functions.FirstOrDefault(f => f != null && f.Name == function);
            var node = fn?.Nodes.FirstOrDefault(n => n != null && n.Id == nodeId);
            if (node == null) return null;
            var lib = library ?? new BlockLibrary();
            if (node.Block != "call" && lib.FindBlock(node.Block) is BlockSpec spec && spec.Kind == BlockKind.Flow && !spec.LoopVariable) return null;
            return new FunctionTypes(fn, lib, UserFunctions(graph), BlockVariableTypes.Table(graph)).OutputOf(node, outputPin);
        }

        /// <summary>
        /// Whether the output of <paramref name="fromNode"/> may be wired to the input <paramref name="pin"/> of
        /// <paramref name="toNode"/>, replacing any wire already there. A wire is refused when it does not fit, and
        /// also when it would break the wires of blocks further along (for example changing a list's type under an
        /// "item at" block). For a block with several outputs, name the one being wired with <paramref name="fromPin"/>.
        /// The graph is not changed.
        /// </summary>
        public static PinCheck CheckWire(BlockGraph graph, string function, string toNode, string pin, string fromNode, BlockLibrary library = null, string fromPin = null)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            var fn = graph.Functions.FirstOrDefault(f => f != null && f.Name == function);
            if (fn == null) throw new ArgumentException("There is no function '" + function + "'.", nameof(function));
            var target = fn.Nodes.FirstOrDefault(n => n != null && n.Id == toNode);
            if (target == null) throw new ArgumentException("There is no block '" + toNode + "' in '" + function + "'.", nameof(toNode));
            var source = fn.Nodes.FirstOrDefault(n => n != null && n.Id == fromNode);
            if (source == null) return PinCheck.No("The block '" + fromNode + "' is not in this function.");

            var lib = library ?? new BlockLibrary();
            var userFunctions = UserFunctions(graph);
            var variables = BlockVariableTypes.Table(graph);
            if (source.Block != "call" && lib.FindBlock(source.Block) is BlockSpec sourceSpec && sourceSpec.Kind == BlockKind.Flow && !sourceSpec.LoopVariable)
                return PinCheck.No("'" + source.Block + "' is a step in the flow and has no value to wire.");
            var scope = new LoopScopes(fn, lib, userFunctions).Problem(source, target);
            if (scope != null) return PinCheck.No(scope);

            return Added(fn, WithWire(fn, target, pin, fromNode, fromPin), lib, userFunctions, variables);
        }

        /// <summary>
        /// Whether the text <paramref name="literal"/> may be typed into the input <paramref name="pin"/> of
        /// <paramref name="toNode"/>, replacing what the pin has. It is refused when it is not a single number,
        /// string, <c>True</c>, <c>False</c> or <c>None</c>, and when its type does not fit the pin or would break
        /// the wires of blocks further along. The graph is not changed.
        /// </summary>
        public static PinCheck CheckLiteral(BlockGraph graph, string function, string toNode, string pin, string literal, BlockLibrary library = null)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            var fn = graph.Functions.FirstOrDefault(f => f != null && f.Name == function);
            if (fn == null) throw new ArgumentException("There is no function '" + function + "'.", nameof(function));
            var target = fn.Nodes.FirstOrDefault(n => n != null && n.Id == toNode);
            if (target == null) throw new ArgumentException("There is no block '" + toNode + "' in '" + function + "'.", nameof(toNode));
            if (literal == null || Generator.CheckLiteral(literal) == null)
                return PinCheck.No("A typed-in value must be a single number, string, True, False or None.");

            var lib = library ?? new BlockLibrary();
            return Added(fn, WithInput(fn, target, pin, new BlockInput(pin, null, literal)), lib, UserFunctions(graph), BlockVariableTypes.Table(graph));
        }

        // Yes unless the changed function has a diagnostic that the function did not have before.
        private static PinCheck Added(BlockFunction fn, BlockFunction changed, BlockLibrary lib, HashSet<string> userFunctions, Dictionary<string, PinType> variables)
        {
            var before = new HashSet<string>(new FunctionTypes(fn, lib, userFunctions, variables).Diagnostics().Select(Key));
            var after = new FunctionTypes(changed, lib, userFunctions, variables).Diagnostics();
            var added = after.FirstOrDefault(d => !before.Contains(Key(d)));
            return added == null ? PinCheck.Yes : PinCheck.No(added.Message);
        }

        private static string Key(BlockDiagnostic d) => d.NodeId + "|" + d.Pin + "|" + d.Message;

        private static HashSet<string> UserFunctions(BlockGraph graph) =>
            new HashSet<string>(graph.Functions.Where(f => f != null && !string.IsNullOrEmpty(f.Name)).Select(f => f.Name));

        private static BlockFunction WithWire(BlockFunction fn, BlockNode target, string pin, string fromNode, string fromPin) =>
            WithInput(fn, target, pin, new BlockInput(pin, fromNode, null, fromPin));

        // A copy of the function in which one input pin has the given wire or literal.
        private static BlockFunction WithInput(BlockFunction fn, BlockNode target, string pin, BlockInput replacement)
        {
            var changed = new BlockNode(target.Id, target.Block, target.Name) { Flow = target.Flow };
            foreach (var input in target.Inputs ?? new List<BlockInput>())
                if (input != null && input.Pin != pin) changed.Inputs.Add(input);
            changed.Inputs.Add(replacement);

            var copy = new BlockFunction { Name = fn.Name, Start = fn.Start, Parameters = fn.Parameters };
            foreach (var n in fn.Nodes) copy.Nodes.Add(n == target ? changed : n);
            return copy;
        }
    }

    // The types of one function. Each block is solved once: its wires are matched to its pins, then its value gets a type.
    internal sealed class FunctionTypes
    {
        private sealed class Solution
        {
            public PinType Output = PinType.Any;

            // The types of the named outputs of a call that returns several values, otherwise null.
            public Dictionary<string, PinType> Named;
            public readonly List<BlockDiagnostic> Errors = new List<BlockDiagnostic>();
        }

        private struct Wire
        {
            public string Pin;
            public PinType Type;
            public int Order;
            public bool Failed;
        }

        private readonly BlockFunction _fn;
        private readonly BlockLibrary _lib;
        private readonly HashSet<string> _userFunctions;
        private readonly Dictionary<string, PinType> _variables;
        private readonly Dictionary<string, BlockNode> _nodes = new Dictionary<string, BlockNode>();
        private readonly Dictionary<string, Solution> _solved = new Dictionary<string, Solution>();
        private readonly HashSet<string> _busy = new HashSet<string>();

        public FunctionTypes(BlockFunction fn, BlockLibrary lib, HashSet<string> userFunctions, Dictionary<string, PinType> variables = null)
        {
            _fn = fn;
            _lib = lib;
            _userFunctions = userFunctions;
            _variables = variables ?? new Dictionary<string, PinType>();
            foreach (var n in fn.Nodes)
                if (n != null && !string.IsNullOrEmpty(n.Id) && !_nodes.ContainsKey(n.Id)) _nodes[n.Id] = n;
        }

        public List<BlockDiagnostic> Diagnostics()
        {
            var all = new List<BlockDiagnostic>();
            foreach (var n in _nodes.Values) all.AddRange(Solve(n).Errors);
            return all;
        }

        public PinType OutputOf(BlockNode node, string outputPin = null)
        {
            var solution = Solve(node);
            if (solution.Named == null) return solution.Output;
            return outputPin != null && solution.Named.TryGetValue(outputPin, out var named) ? named : PinType.Any;
        }

        private Solution Solve(BlockNode node)
        {
            if (_solved.TryGetValue(node.Id, out var done)) return done;
            // A block that depends on itself is reported by the compiler; here it is just untyped.
            if (!_busy.Add(node.Id)) return new Solution();
            try
            {
                var solution = new Solution();
                Patterns(node, out var pinType, out var outputPattern);
                var bindings = Match(node, pinType, solution);
                solution.Output = Refine(node, Substitute(outputPattern, bindings));
                solution.Named = NamedOutputs(node);
                CheckLoopTarget(node, solution);
                CheckOperator(node, solution);
                _solved[node.Id] = solution;
                return solution;
            }
            finally
            {
                _busy.Remove(node.Id);
            }
        }

        // ---- Operators ----

        private enum Kind { Unknown, Number, Text, List, Other }

        private static Kind KindOf(PinType t)
        {
            if (t.Equals(PinType.Any) || t.IsVariable) return Kind.Unknown;
            if (IsNumber(t) || t.Equals(PinType.Bool)) return Kind.Number;
            if (t.Equals(PinType.Str)) return Kind.Text;
            return t.IsList ? Kind.List : Kind.Other;
        }

        // An operator refuses a pair of known types Python cannot combine (text minus a number). A pin of unknown
        // type (any, an unwired pin, a variable) is never refused: it is checked when it is known.
        private void CheckOperator(BlockNode node, Solution solution)
        {
            string op = node.Block;
            bool unary = op == "neg";
            bool arithmetic = op == "add" || op == "sub" || op == "mul" || op == "div" || op == "floordiv" || op == "mod" || op == "pow";
            bool order = op == "lt" || op == "le" || op == "gt" || op == "ge";
            if (!unary && !arithmetic && !order && op != "contains") return;

            var a = InputType(node, "a");
            var b = unary ? PinType.Any : InputType(node, "b");
            if (Allows(op, a, b)) return;
            string title = _lib.FindBlock(op)?.Title ?? op;
            string pin = unary || KindOf(b) == Kind.Unknown ? "a" : "b";
            solution.Errors.Add(Error(node, pin, unary
                ? "'" + title + "' needs a number, not " + a
                : "'" + title + "' cannot take " + a + " and " + b + "."));
        }

        private static bool Allows(string op, PinType a, PinType b)
        {
            var ka = KindOf(a);
            var kb = KindOf(b);
            if (op == "neg") return ka == Kind.Unknown || ka == Kind.Number;
            if (op == "contains") return kb == Kind.Unknown || kb == Kind.List || (kb == Kind.Text && (ka == Kind.Unknown || ka == Kind.Text));
            if (ka == Kind.Unknown || kb == Kind.Unknown) return true;
            switch (op)
            {
                case "add": return ka == kb && ka != Kind.Other;
                case "mul":
                    return (ka == Kind.Number && kb == Kind.Number)
                        || (ka == Kind.Number && kb != Kind.Other && IsWhole(a)) || (kb == Kind.Number && ka != Kind.Other && IsWhole(b));
                case "mod": return ka == Kind.Text || (ka == Kind.Number && kb == Kind.Number);
                case "lt": case "le": case "gt": case "ge": return ka == kb && ka != Kind.Other;
                default: return ka == Kind.Number && kb == Kind.Number;
            }
        }

        // int or bool: what text or a list can be repeated by.
        private static bool IsWhole(PinType t) => t.Equals(PinType.Int) || t.Equals(PinType.Bool);

        // A loop that stores its item or count in a typed global has to give that type.
        private void CheckLoopTarget(BlockNode node, Solution solution)
        {
            if (node.Block != "for_each" && node.Block != "for_range" && node.Block != "repeat") return;
            var declared = DeclaredType(node);
            if (declared == null) return;
            var check = PinCompatibility.Check(solution.Output, declared);
            if (!check.Allowed)
                solution.Errors.Add(Error(node, null, "the loop stores its value in '" + node.Name + "', which is declared " + declared + ": " + check.Reason));
        }

        // ---- Pin types of a block ----

        private void Patterns(BlockNode node, out Func<string, PinType> pinType, out PinType output)
        {
            pinType = pin => PinType.Any;
            output = PinType.Any;
            if (node.Block == "call")
            {
                var spec = node.Name != null && !_userFunctions.Contains(node.Name) ? _lib.FindFunction(node.Name) : null;
                if (spec == null) return;
                pinType = pin => ArgumentType(spec, pin);
                output = spec.Outputs.Count == 1 ? spec.Outputs[0].Type : spec.Outputs.Count == 0 ? PinType.None : PinType.Any;
                return;
            }
            var block = _lib.FindBlock(node.Block);
            if (block == null) return;
            pinType = block.InputType;
            output = block.OutputType ?? PinType.Any;

            // A global variable declared with a type: reading it gives that type, and what is stored in it must fit.
            var declared = DeclaredType(node);
            if (declared == null) return;
            if (node.Block == "get") output = declared;
            else if (node.Block == "set" || node.Block == "change") pinType = pin => pin == "value" ? declared : block.InputType(pin);
        }

        // The declared type of the global variable a block reads or assigns, or null (a parameter of the function
        // hides a global of the same name).
        private PinType DeclaredType(BlockNode node)
        {
            bool uses = node.Block == "get" || node.Block == "set" || node.Block == "change" || node.Block == "for_each" || node.Block == "for_range" || node.Block == "repeat";
            if (!uses || string.IsNullOrEmpty(node.Name) || (_fn.Parameters ?? new List<string>()).Contains(node.Name)) return null;
            return _variables.TryGetValue(node.Name, out var type) ? type : null;
        }

        private Dictionary<string, PinType> NamedOutputs(BlockNode node)
        {
            if (node.Block != "call" || node.Name == null || _userFunctions.Contains(node.Name)) return null;
            var spec = _lib.FindFunction(node.Name);
            if (spec == null || spec.Outputs.Count < 2) return null;
            return spec.Outputs.ToDictionary(o => o.Name, o => o.Type);
        }

        private static PinType ArgumentType(FunctionSpec spec, string pin)
        {
            int index = Number(pin, "arg");
            return index >= 0 && index < spec.ParameterTypes.Count ? spec.ParameterTypes[index] : PinType.Any;
        }

        // The number at the end of a pin such as arg2 or item0, or -1 when the pin is not named like that.
        private static int Number(string pin, string stem)
        {
            if (pin == null || !pin.StartsWith(stem, StringComparison.Ordinal) || pin.Length == stem.Length) return -1;
            return int.TryParse(pin.Substring(stem.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n : -1;
        }

        // ---- Matching wires to pins ----

        // Reads the wires in pin order, works out each wildcard, and reports every wire that does not fit.
        private Dictionary<string, PinType> Match(BlockNode node, Func<string, PinType> pinType, Solution solution)
        {
            var bindings = new Dictionary<string, PinType>();
            var wires = Wires(node);
            for (int i = 0; i < wires.Count; i++)
            {
                var wire = wires[i];
                wire.Failed = !Bind(node, pinType(wire.Pin), wire, bindings, solution);
                wires[i] = wire;
            }
            foreach (var wire in wires)
            {
                if (wire.Failed) continue;
                var expected = Substitute(pinType(wire.Pin), bindings);
                var check = PinCompatibility.Check(wire.Type, expected);
                if (!check.Allowed) solution.Errors.Add(Error(node, wire.Pin, "the input '" + wire.Pin + "' cannot take this wire: " + check.Reason));
            }
            return bindings;
        }

        private List<Wire> Wires(BlockNode node)
        {
            var spec = node.Block == "call" ? null : _lib.FindBlock(node.Block);
            var wires = new List<Wire>();
            foreach (var input in node.Inputs ?? new List<BlockInput>())
            {
                if (input == null || input.Pin == null) continue;
                var type = WireType(input);
                if (type == null) continue;
                int order = spec == null ? -1 : spec.Inputs.ToList().IndexOf(input.Pin);
                if (order < 0) order = 1000 + Math.Max(Number(input.Pin, "arg"), Number(input.Pin, "item"));
                wires.Add(new Wire { Pin = input.Pin, Type = type, Order = order });
            }
            // A stable sort: wires on the same pin (a mistake the compiler reports) stay in their written order.
            return wires.Select((w, i) => (w, i)).OrderBy(x => x.w.Order).ThenBy(x => x.i).Select(x => x.w).ToList();
        }

        // Records what a wire says about the wildcards in its pin. False when it contradicts an earlier wire.
        private bool Bind(BlockNode node, PinType pattern, Wire wire, Dictionary<string, PinType> bindings, Solution solution)
        {
            var found = new List<KeyValuePair<string, PinType>>();
            Unify(pattern, wire.Type, found);
            foreach (var kv in found)
            {
                if (!bindings.TryGetValue(kv.Key, out var bound))
                {
                    bindings[kv.Key] = kv.Value;
                    continue;
                }
                var joined = Join(bound, kv.Value);
                if (joined == null)
                {
                    solution.Errors.Add(Error(node, wire.Pin, "the inputs of this block must have matching types: '" + wire.Pin +
                        "' has type " + kv.Value + ", but an earlier input has type " + bound));
                    return false;
                }
                bindings[kv.Key] = joined;
            }
            return true;
        }

        private static void Unify(PinType pattern, PinType actual, List<KeyValuePair<string, PinType>> found)
        {
            if (pattern.IsVariable) found.Add(new KeyValuePair<string, PinType>(pattern.Name, actual));
            else if (pattern.IsList && actual.IsList) Unify(pattern.Element, actual.Element, found);
        }

        // The type both a and b fit into, or null when there is none. int and float give float; lists must agree.
        private static PinType Join(PinType a, PinType b)
        {
            if (a.Equals(b)) return a;
            if (a.Equals(PinType.Any) || b.Equals(PinType.Any)) return PinType.Any;
            if (IsNumber(a) && IsNumber(b)) return PinType.Float;
            if (!a.IsList || !b.IsList) return null;
            if (a.Element.Equals(PinType.Any) || b.Element.Equals(PinType.Any)) return PinType.ListOf(PinType.Any);
            return null;
        }

        private static PinType Substitute(PinType pattern, Dictionary<string, PinType> bindings)
        {
            if (pattern.IsVariable) return bindings.TryGetValue(pattern.Name, out var bound) ? bound : PinType.Any;
            return pattern.IsList ? PinType.ListOf(Substitute(pattern.Element, bindings)) : pattern;
        }

        private BlockDiagnostic Error(BlockNode node, string pin, string message) =>
            new BlockDiagnostic(BlockSeverity.Error, message, _fn.Name, node.Id, pin);

        // ---- Types of values ----

        // The type a wire carries, or null when there is nothing to check (an empty pin, a block that is not here).
        private PinType WireType(BlockInput input)
        {
            if (!string.IsNullOrEmpty(input.From))
            {
                if (!_nodes.TryGetValue(input.From, out var source)) return null;
                var solved = Solve(source);
                if (solved.Named == null) return solved.Output;
                // A block with several outputs is typed by the one the wire names; the compiler reports a missing or wrong name.
                return input.FromPin != null && solved.Named.TryGetValue(input.FromPin, out var named) ? named : null;
            }
            return string.IsNullOrWhiteSpace(input.Literal) ? null : LiteralType(input.Literal);
        }

        private PinType InputType(BlockNode node, string pin)
        {
            var input = (node.Inputs ?? new List<BlockInput>()).LastOrDefault(i => i != null && i.Pin == pin);
            return (input == null ? null : WireType(input)) ?? PinType.Any;
        }

        // Blocks whose value depends on the types of their inputs, not only on the pin types.
        private PinType Refine(BlockNode node, PinType output)
        {
            switch (node.Block)
            {
                case "literal":
                    return InputType(node, "value");
                case "for_each":
                    var items = InputType(node, "items");
                    return items.IsList ? items.Element : PinType.Any;
                case "for_range":
                case "repeat":
                    return PinType.Int;
                case "list":
                    return ListOfItems(node);
                case "neg":
                    var operand = InputType(node, "a");
                    return IsNumber(operand) ? operand : PinType.Any;
                case "add": case "sub": case "mul": case "div": case "floordiv": case "mod": case "pow":
                    return Arithmetic(node.Block, InputType(node, "a"), InputType(node, "b"));
                default:
                    return output;
            }
        }

        // A list block is a list of its items' type when they agree (int and float give float), else list[any]. Python
        // allows mixed lists, so unlike append or choose the items are not required to match.
        private PinType ListOfItems(BlockNode node)
        {
            PinType element = null;
            foreach (var input in (node.Inputs ?? new List<BlockInput>()).Where(i => i != null && Number(i.Pin, "item") >= 0).OrderBy(i => Number(i.Pin, "item")))
            {
                var type = WireType(input);
                if (type == null) continue;
                element = element == null ? type : Join(element, type);
                if (element == null) return PinType.ListOf(PinType.Any);
            }
            return PinType.ListOf(element ?? PinType.Any);
        }

        private static PinType Arithmetic(string op, PinType a, PinType b)
        {
            if (IsNumber(a) && IsNumber(b))
            {
                if (op == "pow") return PinType.Any;
                if (op == "div") return PinType.Float;
                return a.Equals(PinType.Float) || b.Equals(PinType.Float) ? PinType.Float : PinType.Int;
            }
            if (op == "add" && a.Equals(b) && (a.Equals(PinType.Str) || a.IsList)) return a;
            if (op == "mul" && ((a.Equals(PinType.Str) && b.Equals(PinType.Int)) || (a.Equals(PinType.Int) && b.Equals(PinType.Str)))) return PinType.Str;
            return PinType.Any;
        }

        private static bool IsNumber(PinType t) => t.Equals(PinType.Int) || t.Equals(PinType.Float);

        // The type of a typed-in literal, or null when it is not a single literal (the compiler reports that).
        internal static PinType LiteralType(string text)
        {
            var tokens = Lexer.TokenizeTolerant(text.Trim(), out var problems);
            if (problems.Count > 0) return null;
            while (tokens.Count > 0 && IsLayout(tokens[tokens.Count - 1].Kind)) tokens.RemoveAt(tokens.Count - 1);
            if (tokens.Count == 2 && tokens[0].Kind == TokenKind.Minus) tokens.RemoveAt(0);
            if (tokens.Count != 1) return null;
            switch (tokens[0].Kind)
            {
                case TokenKind.Int: return PinType.Int;
                case TokenKind.Float: return PinType.Float;
                case TokenKind.String: return PinType.Str;
                case TokenKind.KwTrue:
                case TokenKind.KwFalse: return PinType.Bool;
                case TokenKind.KwNone: return PinType.Any;
                default: return null;
            }
        }

        private static bool IsLayout(TokenKind k) => k == TokenKind.EndOfFile || k == TokenKind.Newline || k == TokenKind.Dedent;
    }
}
