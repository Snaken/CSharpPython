using System;
using System.Collections.Generic;

namespace CSharpPython.Blocks
{
    /// <summary>One pin of a function that a type search found.</summary>
    public sealed class PinMatch
    {
        public FunctionSpec Function { get; }

        /// <summary>The pin name: <c>arg0</c>, <c>arg1</c>, ... for an input, or the output's name (null for a single unnamed result).</summary>
        public string Pin { get; }

        /// <summary>The position of the pin among the function's inputs or outputs.</summary>
        public int Index { get; }

        public PinType Type { get; }

        internal int Order { get; }

        internal PinMatch(FunctionSpec function, string pin, int index, PinType type, int order)
        {
            Function = function;
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
    /// after adding functions. Built-in blocks (<c>add</c>, <c>if</c>) have no pin types yet and are not included.
    /// Results are in the order the functions were added.
    /// </remarks>
    public sealed class PinTypeIndex
    {
        // Pins grouped by the type name of the pin (int, list, Entity, any). Only pins in a few groups can fit a
        // given type, so a search looks at those groups and checks each pin with PinCompatibility.
        private readonly Dictionary<string, List<PinMatch>> _inputs = new Dictionary<string, List<PinMatch>>();
        private readonly Dictionary<string, List<PinMatch>> _outputs = new Dictionary<string, List<PinMatch>>();

        public PinTypeIndex(BlockLibrary library)
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
                // A pin of this type name, an any pin, and (since int fits float) the int pins for a float.
                Collect(groups, type.Name, found);
                Collect(groups, PinType.Any.Name, found);
                Collect(groups, type.Equals(PinType.Float) ? PinType.Int.Name : null, found);
                Collect(groups, type.Equals(PinType.Int) ? PinType.Float.Name : null, found);
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
