using System;
using System.Collections.Generic;

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

        internal BlockSpec(string name, string title, string category, BlockKind kind, string[] inputs, string[] flowPins, bool needsName)
        {
            Name = name;
            Title = title;
            Category = category;
            Kind = kind;
            Inputs = inputs;
            FlowPins = flowPins;
            NeedsName = needsName;
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

        internal FunctionSpec(string name, string module, string[] parameters, bool pure)
        {
            Name = name;
            Module = module;
            Parameters = parameters;
            Pure = pure;
        }

        /// <summary>The pin name of the argument at <paramref name="index"/>: <c>arg0</c>, <c>arg1</c>, ...</summary>
        public static string ArgPin(int index) => "arg" + index;
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

            AddBlock("index", "Item at", "Lists", BlockKind.Value, new[] { "list", "index" }, None, false);
            AddBlock("list", "List of", "Lists", BlockKind.Value, None, None, false);
            AddBlock("set_index", "Set item at", "Lists", BlockKind.Flow, new[] { "list", "index", "value" }, Next, false);
            AddBlock("append", "Append to list", "Lists", BlockKind.Flow, new[] { "list", "value" }, Next, false);

            AddBlock("if", "If", "Flow", BlockKind.Flow, new[] { "cond" }, new[] { "then", "else", "next" }, false);
            AddBlock("while", "While", "Flow", BlockKind.Flow, new[] { "cond" }, new[] { "body", "next" }, false);
            AddBlock("repeat", "Repeat", "Flow", BlockKind.Flow, new[] { "count" }, new[] { "body", "next" }, false);
            AddBlock("for_each", "For each", "Flow", BlockKind.Flow, new[] { "items" }, new[] { "body", "next" }, true);
            AddBlock("break", "Break", "Flow", BlockKind.Flow, None, None, false);
            AddBlock("continue", "Continue", "Flow", BlockKind.Flow, None, None, false);
            AddBlock("return", "Return", "Flow", BlockKind.Flow, new[] { "value" }, None, false);

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
        public BlockLibrary AddFunction(string name, string[] parameters, bool pure = false, string module = null)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A function needs a name.", nameof(name));
            var spec = new FunctionSpec(name, module, parameters ?? None, pure);
            string key = module == null ? name : module + "." + name;
            if (_functions.ContainsKey(key)) _functionOrder.Remove(_functions[key]);
            _functions[key] = spec;
            _functionOrder.Add(spec);
            return this;
        }

        private void AddBlock(string name, string title, string category, BlockKind kind, string[] inputs, string[] flowPins, bool needsName)
        {
            var spec = new BlockSpec(name, title, category, kind, inputs, flowPins, needsName);
            _blocks[name] = spec;
            _blockOrder.Add(spec);
        }
    }
}
