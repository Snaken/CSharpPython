using System;
using System.Collections.Generic;

namespace CSharpPython.Blocks
{
    /// <summary>One pin of a function, or of a built-in block, that a type search found.</summary>
    public sealed class PinMatch
    {
        /// <summary>The function the pin belongs to, or null when it belongs to a built-in block (see <see cref="Block"/>).</summary>
        public FunctionSpec Function { get; }

        /// <summary>The built-in block the pin belongs to (<c>choose</c>, <c>for_range</c>), or null when it belongs to a function.</summary>
        public BlockSpec Block { get; }

        /// <summary>The pin name: <c>arg0</c>, <c>arg1</c>, ... for a function input, the pin name of a block input, or the output's name (null for a single unnamed result).</summary>
        public string Pin { get; }

        /// <summary>The position of the pin among the function's inputs or outputs.</summary>
        public int Index { get; }

        public PinType Type { get; }

        internal int Order { get; }

        internal PinMatch(FunctionSpec function, string pin, int index, PinType type, int order)
            : this(function, null, pin, index, type, order)
        {
        }

        internal PinMatch(BlockSpec block, string pin, int index, PinType type, int order)
            : this(null, block, pin, index, type, order)
        {
        }

        private PinMatch(FunctionSpec function, BlockSpec block, string pin, int index, PinType type, int order)
        {
            Function = function;
            Block = block;
            Pin = pin;
            Index = index;
            Type = type;
            Order = order;
        }
    }

    /// <summary>
    /// Finds the functions that fit a pin by type, for an editor's search and sidebar. Given an output pin, it lists
    /// every function with an input that accepts it. Given an input pin, it lists every function with an output that
    /// fits it. A match is a wire <see cref="PinCompatibility"/> allows.
    /// </summary>
    /// <remarks>
    /// The index is built from the functions a <see cref="BlockLibrary"/> has when it is created; build a new one
    /// after adding functions. The built-in blocks (<c>choose</c>, <c>index</c>, <c>for_range</c>) are included only
    /// when <c>includeBlocks</c> is true, after the functions, so a search that expects functions only is not
    /// surprised. A block pin typed with a wildcard (<c>$T</c>) fits any type, as the compiler would allow. A loop
    /// block's output is its variable. The <c>literal</c> block's typed-in input is not a place to wire to. Results are
    /// in the order the functions and blocks were added.
    /// </remarks>
    public sealed class PinTypeIndex
    {
        // Pins grouped by the type name of the pin (int, list, Entity, any). Only pins in a few groups can fit a
        // given type, so a search looks at those groups and checks each pin with PinCompatibility.
        private readonly Dictionary<string, List<PinMatch>> _inputs = new Dictionary<string, List<PinMatch>>();
        private readonly Dictionary<string, List<PinMatch>> _outputs = new Dictionary<string, List<PinMatch>>();

        public PinTypeIndex(BlockLibrary library, bool includeBlocks = false)
        {
            if (library == null) throw new ArgumentNullException(nameof(library));
            int order = 0;
            foreach (var f in library.Functions)
            {
                for (int i = 0; i < f.Parameters.Count; i++)
                    Add(_inputs, new PinMatch(f, FunctionSpec.ArgPin(i), i, f.ParameterTypes[i], order++));
                for (int i = 0; i < f.Outputs.Count; i++)
                    Add(_outputs, new PinMatch(f, f.Outputs[i].Name, i, f.Outputs[i].Type, order++));
            }
            if (includeBlocks)
                foreach (var b in library.Blocks)
                    order = AddBlock(b, order);
        }

        private int AddBlock(BlockSpec block, int order)
        {
            if (block.Name != "literal")
                for (int i = 0; i < block.Inputs.Count; i++)
                    Add(_inputs, new PinMatch(block, block.Inputs[i], i, block.InputType(block.Inputs[i]), order++));
            var output = block.LoopVariable ? (block.Name == "for_each" ? PinType.Any : PinType.Int) : block.OutputType;
            if (output != null) Add(_outputs, new PinMatch(block, null, 0, output, order++));
            return order;
        }

        private static void Add(Dictionary<string, List<PinMatch>> groups, PinMatch match)
        {
            if (!groups.TryGetValue(match.Type.Name, out var list))
            {
                list = new List<PinMatch>();
                groups[match.Type.Name] = list;
            }
            list.Add(match);
        }

        /// <summary>Every input pin an output of type <paramref name="output"/> may be wired to.</summary>
        public IReadOnlyList<PinMatch> Accepting(PinType output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (output.Equals(PinType.None)) return new PinMatch[0];
            return Search(_inputs, output, m => PinCompatibility.CanConnect(output, m.Type));
        }

        /// <summary>Every output pin that may be wired to an input of type <paramref name="input"/>.</summary>
        public IReadOnlyList<PinMatch> Producing(PinType input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            return Search(_outputs, input, m => PinCompatibility.CanConnect(m.Type, input));
        }

        private static IReadOnlyList<PinMatch> Search(Dictionary<string, List<PinMatch>> groups, PinType type, Func<PinMatch, bool> fits)
        {
            var found = new List<PinMatch>();
            if (type.Equals(PinType.Any))
            {
                foreach (var list in groups.Values) found.AddRange(list);
            }
            else
            {
                // A pin of this type name, an any pin, and (since int fits float) the int pins for a float, and the wildcards.
                Collect(groups, type.Name, found);
                Collect(groups, PinType.Any.Name, found);
                Collect(groups, type.Equals(PinType.Float) ? PinType.Int.Name : null, found);
                Collect(groups, type.Equals(PinType.Int) ? PinType.Float.Name : null, found);
                // Wildcard pins (a block's $T) fit any type.
                foreach (var group in groups)
                    if (group.Key.Length > 0 && group.Key[0] == '$' && group.Key != type.Name) found.AddRange(group.Value);
            }
            found.RemoveAll(m => !fits(m));
            found.Sort((a, b) => a.Order.CompareTo(b.Order));
            return found;
        }

        private static void Collect(Dictionary<string, List<PinMatch>> groups, string name, List<PinMatch> into)
        {
            if (name != null && groups.TryGetValue(name, out var list)) into.AddRange(list);
        }
    }
}
