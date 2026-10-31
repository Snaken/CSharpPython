using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CSharpPython.Blocks
{
    /// <summary>Which side of a block a pin is on, and whether it carries a value or the order of execution.</summary>
    public enum BlockPinKind
    {
        /// <summary>A value comes in (wire or typed-in literal).</summary>
        Input,

        /// <summary>A value goes out.</summary>
        Output,

        /// <summary>The block is a step: the execution flow arrives here (<see cref="BlockEditor.Link"/> targets the block itself).</summary>
        FlowIn,

        /// <summary>Execution leaves here: <c>next</c>, <c>then</c>, <c>else</c> or <c>body</c>.</summary>
        FlowOut,
    }

    /// <summary>One pin of a block as an editor draws it.</summary>
    public sealed class BlockPin
    {
        /// <summary>
        /// The pin name to pass to <see cref="BlockEditor.Connect"/>, <see cref="BlockEditor.Link"/> and the like. Null for
        /// the entry of a step and for the single unnamed output of a block.
        /// </summary>
        public string Name { get; }

        public BlockPinKind Kind { get; }

        /// <summary>
        /// The type of a value pin. An input of a block with a wildcard (<c>$T</c>) has a type that <see cref="PinType.IsVariable"/>
        /// says so for; an output is resolved from the wires. Null for execution pins.
        /// </summary>
        public PinType Type { get; }

        internal BlockPin(string name, BlockPinKind kind, PinType type)
        {
            Name = name;
            Kind = kind;
            Type = type;
        }

        public override string ToString() => Kind + " " + (Name ?? "-") + (Type != null ? ": " + Type : "");
    }

    public sealed partial class BlockEditor
    {
        /// <summary>
        /// The pins of a block in the order to draw them: the flow entry (for a step), the value inputs, the execution
        /// exits and the value outputs (none for a function that returns nothing). This is the list the editing rules use, so a canvas that draws it cannot disagree
        /// with <see cref="Connect"/> and <see cref="Link"/>. A <c>list</c> block has the item pins it uses plus one free
        /// pin. A call to a function that is not known has none. Empty when the block does not exist.
        /// </summary>
        public IReadOnlyList<BlockPin> PinsOf(string function, string nodeId)
        {
            var fn = FindFunction(function);
            var node = fn == null ? null : FindNode(fn, nodeId);
            var pins = new List<BlockPin>();
            if (node == null) return pins;

            if (IsStep(node)) pins.Add(new BlockPin(null, BlockPinKind.FlowIn, null));
            foreach (var pin in InputPinsOf(node).Concat(ItemPinsOf(node)))
                pins.Add(new BlockPin(pin, BlockPinKind.Input, InputTypeOf(node, pin)));
            foreach (var pin in FlowPinsOf(node))
                pins.Add(new BlockPin(pin, BlockPinKind.FlowOut, null));
            foreach (var name in OutputNamesOf(node))
            {
                var type = BlockTypeChecker.OutputType(Graph, function, nodeId, Library, name);
                if (type != null && !type.Equals(PinType.None)) pins.Add(new BlockPin(name, BlockPinKind.Output, type));
            }
            return pins;
        }

        // The numbered inputs of a list block: those in use, then the next one.
        private static IEnumerable<string> ItemPinsOf(BlockNode node)
        {
            if (node.Block != "list") return new string[0];
            int count = 0;
            foreach (var input in node.Inputs ?? new List<BlockInput>())
                if (input?.Pin != null && input.Pin.StartsWith("item", StringComparison.Ordinal)
                    && int.TryParse(input.Pin.Substring(4), NumberStyles.None, CultureInfo.InvariantCulture, out int n))
                    count = Math.Max(count, n + 1);
            return Enumerable.Range(0, count + 1).Select(i => "item" + i.ToString(CultureInfo.InvariantCulture));
        }

        // The names of the value outputs of a block: one null name for a single output, the result names for several.
        private IEnumerable<string> OutputNamesOf(BlockNode node)
        {
            if (node.Block != "call") return new string[] { null };
            if (Library.FindFunction(node.Name) == null && Graph.Functions.All(f => f == null || f.Name != node.Name)) return new string[0];
            var spec = Graph.Functions.Any(f => f != null && f.Name == node.Name) ? null : Library.FindFunction(node.Name);
            if (spec != null && spec.Outputs.Count > 1) return spec.Outputs.Select(o => o.Name);
            return new string[] { null };
        }
    }
}
