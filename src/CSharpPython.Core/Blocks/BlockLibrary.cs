using System;
using System.Collections.Generic;
using CSharpPython.Editor;

namespace CSharpPython.Blocks
{
    /// <summary>Whether a block runs as a step in the execution flow or produces a value for other blocks' inputs.</summary>
    public enum BlockKind
    {
        /// <summary>A step in the execution flow (has a <c>next</c> pin; may have branches).</summary>
        Flow,

        /// <summary>A value expression with no execution pins, evaluated wherever its output is used.</summary>
        Value,
    }

    /// <summary>Describes one kind of block: its pins and whether it needs a name. Editors use this to build a palette.</summary>
    public sealed class BlockSpec
    {
        public string Name { get; }
        public string Title { get; }
        public string Category { get; }
        public BlockKind Kind { get; }

        /// <summary>The input pins a node of this kind may have, in display order.</summary>
        public IReadOnlyList<string> Inputs { get; }

        /// <summary>The execution pins a node of this kind may link, in display order.</summary>
        public IReadOnlyList<string> FlowPins { get; }

        /// <summary>True when the node's <see cref="BlockNode.Name"/> is required (a variable or function name).</summary>
        public bool NeedsName { get; }

        /// <summary>The typed description of the pins, or null when every pin is <c>any</c>.</summary>
        public HostSignature Typed { get; }

        internal BlockSpec(string name, string title, string category, BlockKind kind, string[] inputs, string[] flowPins, bool needsName, HostSignature typed)
        {
            Name = name;
            Title = title;
            Category = category;
            Kind = kind;
            Inputs = inputs;
            FlowPins = flowPins;
            NeedsName = needsName;
            Typed = typed;
        }

        /// <summary>
        /// The type of an input pin, which may be a type variable such as <c>$T</c> (a wildcard that takes its type
        /// from the node's wires). A pin with no declared type is <see cref="PinType.Any"/>.
        /// </summary>
        public PinType InputType(string pin)
        {
            if (Typed == null || pin == null) return PinType.Any;
            foreach (var p in Typed.Parameters)
            {
                if (p.Kind == ParameterKind.Normal && p.Name == pin) return p.Type;
                if (p.Kind == ParameterKind.VarArgs && IsNumbered(pin, p.Name)) return p.Type;
            }
            return PinType.Any;
        }

        /// <summary>The type of the block's value, which may mention a type variable, or null for a step in the flow.</summary>
        public PinType OutputType
        {
            get
            {
                if (Kind == BlockKind.Flow) return null;
                if (Typed != null && Typed.HasResult && Typed.Outputs.Count == 1) return Typed.Outputs[0].Type;
                return PinType.Any;
            }
        }

        private static bool IsNumbered(string pin, string stem)
        {
            if (pin.Length <= stem.Length || !pin.StartsWith(stem, StringComparison.Ordinal)) return false;
            for (int i = stem.Length; i < pin.Length; i++)
                if (pin[i] < '0' || pin[i] > '9') return false;
            return true;
        }
    }

    /// <summary>A function the blocks can call: a built-in, a host function, or (inside the graph) a user-made block.</summary>
    public sealed class FunctionSpec
    {
        public string Name { get; }

        /// <summary>The module to import and call through (<c>game.spawn(...)</c>), or null for a built-in.</summary>
        public string Module { get; }

        public IReadOnlyList<string> Parameters { get; }

        /// <summary>
        /// True for a function with no side effects whose result is used as a value (<c>len</c>, <c>abs</c>). Such a
        /// call is written wherever its output is used. Other functions run as a step in the execution flow.
        /// </summary>
        public bool Pure { get; }

        /// <summary>
        /// How many leading parameters must be connected. The rest are optional: leave their pins empty and the
        /// compiler leaves the argument out. An optional pin may only be used when the ones before it are.
        /// </summary>
        public int RequiredParameters { get; }

        /// <summary>The parameter list as an editor should show it, such as <c>"x, base=10"</c>.</summary>
        public string Signature { get; }

        /// <summary>The typed description this function was added with, or null when its pins are all <c>any</c>.</summary>
        public HostSignature Typed { get; }

        /// <summary>The type of each pin in <see cref="Parameters"/>. A pin with no declared type is <see cref="PinType.Any"/>.</summary>
        public IReadOnlyList<PinType> ParameterTypes { get; }

        /// <summary>
        /// The value outputs, in order. A function with no declared result has one <c>any</c> output, and one that
        /// returns <c>none</c> has none. Several named outputs come from a result such as <c>(hit: bool, point: Vec)</c>.
        /// </summary>
        public IReadOnlyList<HostOutput> Outputs { get; }

        internal FunctionSpec(string name, string module, string[] parameters, bool pure, int required, string signature, HostSignature typed)
        {
            Name = name;
            Module = module;
            Parameters = parameters;
            Pure = pure;
            RequiredParameters = required < 0 || required > parameters.Length ? parameters.Length : required;
            Signature = signature ?? string.Join(", ", parameters);
            Typed = typed;
            ParameterTypes = PinTypesOf(parameters, typed);
            Outputs = OutputsOf(typed);
        }

        // A pin takes the type of the parameter with its name. A built-in registered with its own pin names (print's
        // "value") takes the type of the positional parameter in the same place, and a pin with no such parameter is any.
        private static PinType[] PinTypesOf(string[] pins, HostSignature typed)
        {
            var types = new PinType[pins.Length];
            for (int i = 0; i < pins.Length; i++) types[i] = PinType.Any;
            if (typed == null) return types;

            var byName = new Dictionary<string, PinType>();
            var positional = new List<HostParameter>();
            bool keywordOnly = false;
            foreach (var p in typed.Parameters)
            {
                if (p.Kind == ParameterKind.VarArgs || (p.Kind == ParameterKind.Marker && p.Name == "*")) keywordOnly = true;
                if (p.Kind != ParameterKind.Normal) continue;
                byName[p.Name] = p.Type;
                if (!keywordOnly) positional.Add(p);
            }
            for (int i = 0; i < pins.Length; i++)
            {
                if (byName.TryGetValue(pins[i], out var named)) types[i] = named;
                else if (i < positional.Count && !Array.Exists(pins, pin => pin == positional[i].Name)) types[i] = positional[i].Type;
            }
            return types;
        }

        private static IReadOnlyList<HostOutput> OutputsOf(HostSignature typed)
        {
            if (typed == null || !typed.HasResult) return new[] { new HostOutput(null, PinType.Any) };
            var outputs = new List<HostOutput>();
            foreach (var o in typed.Outputs)
                if (!o.Type.Equals(PinType.None)) outputs.Add(o);
            return outputs;
        }

        /// <summary>The pin name of the argument at <paramref name="index"/>: <c>arg0</c>, <c>arg1</c>, ...</summary>
        public static string ArgPin(int index) => "arg" + index;
    }

    /// <summary>One section of the palette: a named list of blocks and/or functions.</summary>
    public sealed class PaletteGroup
    {
        internal readonly List<BlockSpec> BlockList = new List<BlockSpec>();
        internal readonly List<FunctionSpec> FunctionList = new List<FunctionSpec>();

        public string Name { get; }
        public IReadOnlyList<BlockSpec> Blocks => BlockList;
        public IReadOnlyList<FunctionSpec> Functions => FunctionList;

        internal PaletteGroup(string name) { Name = name; }
    }

    /// <summary>
    /// The catalogue of blocks a graph may use: the built-in blocks plus any functions the host adds. Pass the same
    /// library to <see cref="BlockCompiler"/> and to the editor that draws the palette.
    /// </summary>
    public sealed class BlockLibrary
    {
        private static readonly string[] None = new string[0];
        private static readonly string[] A = { "a" };
        private static readonly string[] AB = { "a", "b" };
        private static readonly string[] Next = { "next" };

        private readonly Dictionary<string, BlockSpec> _blocks = new Dictionary<string, BlockSpec>();
        private readonly List<BlockSpec> _blockOrder = new List<BlockSpec>();
        private readonly Dictionary<string, FunctionSpec> _functions = new Dictionary<string, FunctionSpec>();
        private readonly List<FunctionSpec> _functionOrder = new List<FunctionSpec>();

        /// <summary>Creates a library holding the built-in blocks and the always-available built-in functions.</summary>
        public BlockLibrary()
        {
            AddBlock("literal", "Value", "Values", BlockKind.Value, new[] { "value" }, None, false);
            AddBlock("get", "Get variable", "Variables", BlockKind.Value, None, None, true);
            AddBlock("set", "Set variable", "Variables", BlockKind.Flow, new[] { "value" }, Next, true);
            AddBlock("change", "Change variable by", "Variables", BlockKind.Flow, new[] { "value" }, Next, true);

            foreach (var op in new[]
            {
                new[] { "add", "Add" }, new[] { "sub", "Subtract" }, new[] { "mul", "Multiply" }, new[] { "div", "Divide" },
                new[] { "floordiv", "Divide (whole)" }, new[] { "mod", "Remainder" }, new[] { "pow", "Power" },
            })
                AddBlock(op[0], op[1], "Math", BlockKind.Value, AB, None, false);
            AddBlock("neg", "Negate", "Math", BlockKind.Value, A, None, false);

            foreach (var op in new[]
            {
                new[] { "eq", "Equal" }, new[] { "ne", "Not equal" }, new[] { "lt", "Less than" },
                new[] { "le", "Less or equal" }, new[] { "gt", "Greater than" }, new[] { "ge", "Greater or equal" },
            })
                AddBlock(op[0], op[1], "Logic", BlockKind.Value, AB, None, false);
            AddBlock("and", "And", "Logic", BlockKind.Value, AB, None, false);
            AddBlock("or", "Or", "Logic", BlockKind.Value, AB, None, false);
            AddBlock("not", "Not", "Logic", BlockKind.Value, A, None, false);
            AddBlock("contains", "Is in", "Logic", BlockKind.Value, AB, None, false);
            AddBlock("is_none", "Is nothing", "Logic", BlockKind.Value, A, None, false);
            AddBlock("choose", "Choose", "Logic", BlockKind.Value, new[] { "cond", "a", "b" }, None, false);

            AddBlock("index", "Item at", "Lists", BlockKind.Value, new[] { "list", "index" }, None, false);
            AddBlock("list", "List of", "Lists", BlockKind.Value, None, None, false);
            AddBlock("set_index", "Set item at", "Lists", BlockKind.Flow, new[] { "list", "index", "value" }, Next, false);
            AddBlock("append", "Append to list", "Lists", BlockKind.Flow, new[] { "list", "value" }, Next, false);
            AddBlock("remove_at", "Remove item at", "Lists", BlockKind.Flow, new[] { "list", "index" }, Next, false);
            AddBlock("get_attr", "Get field", "Objects", BlockKind.Value, new[] { "object" }, None, true);
            AddBlock("set_attr", "Set field", "Objects", BlockKind.Flow, new[] { "object", "value" }, Next, true);

            AddBlock("if", "If", "Flow", BlockKind.Flow, new[] { "cond" }, new[] { "then", "else", "next" }, false);
            AddBlock("while", "While", "Flow", BlockKind.Flow, new[] { "cond" }, new[] { "body", "next" }, false);
            AddBlock("repeat", "Repeat", "Flow", BlockKind.Flow, new[] { "count" }, new[] { "body", "next" }, false);
            AddBlock("for_each", "For each", "Flow", BlockKind.Flow, new[] { "items" }, new[] { "body", "next" }, true);
            AddBlock("for_range", "Count from", "Flow", BlockKind.Flow, new[] { "start", "stop", "step" }, new[] { "body", "next" }, true);
            AddBlock("break", "Break", "Flow", BlockKind.Flow, None, None, false);
            AddBlock("continue", "Continue", "Flow", BlockKind.Flow, None, None, false);
            AddBlock("return", "Return", "Flow", BlockKind.Flow, new[] { "value" }, None, false);

            AddBlock("comment", "Comment", "Notes", BlockKind.Flow, None, Next, false);

            AddFunction("print", new[] { "value" });
            foreach (var name in new[] { "len", "abs", "str", "int", "float", "round" })
                AddFunction(name, new[] { "value" }, pure: true);
            AddFunction("min", AB, pure: true);
            AddFunction("max", AB, pure: true);
        }

        /// <summary>All block kinds, in the order they were added. The <c>call</c> block is described by <see cref="Functions"/>.</summary>
        public IReadOnlyList<BlockSpec> Blocks => _blockOrder;

        /// <summary>All functions the <c>call</c> block can reach, in the order they were added.</summary>
        public IReadOnlyList<FunctionSpec> Functions => _functionOrder;

        /// <summary>Finds a block kind by name, or null.</summary>
        public BlockSpec FindBlock(string name) => name != null && _blocks.TryGetValue(name, out var spec) ? spec : null;

        /// <summary>Finds a function by the name a <c>call</c> block uses: <c>len</c>, or <c>module.name</c> for a module function. Null if unknown.</summary>
        public FunctionSpec FindFunction(string name) => name != null && _functions.TryGetValue(name, out var spec) ? spec : null;

        /// <summary>
        /// Makes a built-in function (when <paramref name="module"/> is null) or a host module function callable from
        /// blocks. The compiler adds <c>import module</c> automatically. Registering a name again replaces it.
        /// </summary>
        public BlockLibrary AddFunction(string name, string[] parameters, bool pure = false, string module = null, int required = -1, string signature = null, HostSignature typed = null)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A function needs a name.", nameof(name));
            typed = typed ?? TypedSignatures.Find(module == null ? name : module + "." + name);
            var spec = new FunctionSpec(name, module, parameters ?? None, pure, required, signature, typed);
            string key = module == null ? name : module + "." + name;
            if (_functions.ContainsKey(key)) _functionOrder.Remove(_functions[key]);
            _functions[key] = spec;
            _functionOrder.Add(spec);
            return this;
        }

        /// <summary>
        /// Adds every function of a host module registered on <paramref name="vm"/>, so the palette follows the host
        /// API. Parameter names come from the function's signature; the ones with defaults become optional pins, and
        /// <c>*args</c>/<c>**kwargs</c> are left out. Set <paramref name="pure"/> for a module whose functions only
        /// compute a value (such as <c>math</c>). Returns false if the module does not exist.
        /// </summary>
        public bool AddModule(PyVm vm, string module, bool pure = false)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));
            if (module == null || !vm.Modules.TryGetValue(module, out var m)) return false;
            var names = new List<string>(m.Members.Keys);
            names.Sort(StringComparer.Ordinal);
            foreach (var name in names)
            {
                var v = m.Members[name];
                if (v.Kind != ValueKind.Native || name.StartsWith("_", StringComparison.Ordinal) || name.StartsWith(".", StringComparison.Ordinal)) continue;
                var native = (NativeFunction)v.RawObject;
                string signature = Signatures.For(native, module + "." + name, 0);
                var parameters = new List<string>();
                int required = -1;
                foreach (var part in signature.Split(','))
                {
                    string p = part.Trim();
                    int eq = p.IndexOf('=');
                    string pname = eq < 0 ? p : p.Substring(0, eq).Trim();
                    if (!IsParameterName(pname)) continue;
                    if (eq >= 0 && required < 0) required = parameters.Count;
                    parameters.Add(pname);
                }
                AddFunction(name, parameters.ToArray(), pure, module, required, signature, TypedSignatures.For(native, module + "." + name, 0));
            }
            return true;
        }

        /// <summary>Adds every module registered on <paramref name="vm"/> (see <see cref="AddModule"/>). The <c>math</c> module is treated as pure.</summary>
        public BlockLibrary AddAllModules(PyVm vm)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));
            var names = new List<string>(vm.Modules.Keys);
            names.Sort(StringComparer.Ordinal);
            foreach (var name in names) AddModule(vm, name, name == "math");
            return this;
        }

        private static bool IsParameterName(string text)
        {
            if (text.Length == 0 || !(char.IsLetter(text[0]) || text[0] == '_')) return false;
            foreach (char c in text)
                if (!(char.IsLetterOrDigit(c) || c == '_')) return false;
            return true;
        }

        /// <summary>
        /// The palette an editor shows, grouped: each block category, then the built-in functions, then one group per
        /// module. Each group lists its blocks or functions in the order they were added.
        /// </summary>
        public IReadOnlyList<PaletteGroup> Palette()
        {
            var groups = new List<PaletteGroup>();
            var byName = new Dictionary<string, PaletteGroup>();
            PaletteGroup Group(string name)
            {
                if (!byName.TryGetValue(name, out var g))
                {
                    g = new PaletteGroup(name);
                    byName[name] = g;
                    groups.Add(g);
                }
                return g;
            }
            foreach (var b in _blockOrder) Group(b.Category).BlockList.Add(b);
            foreach (var f in _functionOrder)
                if (f.Module == null) Group("Functions").FunctionList.Add(f);
            foreach (var f in _functionOrder)
                if (f.Module != null) Group(f.Module).FunctionList.Add(f);
            return groups;
        }

        private void AddBlock(string name, string title, string category, BlockKind kind, string[] inputs, string[] flowPins, bool needsName)
        {
            var spec = new BlockSpec(name, title, category, kind, inputs, flowPins, needsName, BlockTypes.Find(name));
            _blocks[name] = spec;
            _blockOrder.Add(spec);
        }
    }
}
