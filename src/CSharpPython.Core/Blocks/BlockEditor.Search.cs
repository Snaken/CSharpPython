using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharpPython.Blocks
{
    /// <summary>One block an editor can offer in its search box: pass <see cref="Block"/> and <see cref="Name"/> to <see cref="BlockEditor.AddNode"/>.</summary>
    public sealed class BlockChoice
    {
        /// <summary>The block kind: <c>call</c> for a function, otherwise the name of a built-in block.</summary>
        public string Block { get; }

        /// <summary>The function a <c>call</c> block calls (qualified with its module, like <c>game.say</c>), otherwise null.</summary>
        public string Name { get; }

        /// <summary>What to show: the title of a built-in block, or the function's name.</summary>
        public string Title { get; }

        /// <summary>A heading to group by: the block's category, the function's module, or <c>Functions</c> / <c>Graph</c>.</summary>
        public string Category { get; }

        /// <summary>
        /// For a search started from a pin, the pin of this block to wire to: an input pin when the search started from
        /// an output, an output pin (null for a single unnamed result) when it started from an input. Otherwise null.
        /// </summary>
        public string Pin { get; }

        internal BlockChoice(string block, string name, string title, string category, string pin = null)
        {
            Block = block;
            Name = name;
            Title = title;
            Category = category;
            Pin = pin;
        }

        public override string ToString() => Category + ": " + Title + (Pin != null ? " (" + Pin + ")" : "");
    }

    public sealed partial class BlockEditor
    {
        private PinTypeIndex _index;
        private int _indexedBlocks;
        private int _indexedFunctions;

        /// <summary>
        /// The blocks whose title, name or category contain <paramref name="text"/> (ignoring case), best match first:
        /// an exact title, then a title that starts with the text, then the rest in palette order. Functions of the
        /// library and of the graph are included. With no text, everything is returned.
        /// </summary>
        public IReadOnlyList<BlockChoice> Search(string text, int max = 50)
        {
            string needle = (text ?? "").Trim();
            var found = new List<KeyValuePair<int, BlockChoice>>();
            foreach (var choice in AllChoices())
            {
                int rank = Rank(choice, needle);
                if (rank >= 0) found.Add(new KeyValuePair<int, BlockChoice>(rank, choice));
            }
            return found.Select((p, i) => new { p.Key, p.Value, Order = i })
                .OrderBy(x => x.Key).ThenBy(x => x.Order)
                .Take(Math.Max(0, max)).Select(x => x.Value).ToList();
        }

        /// <summary>
        /// The blocks that could take the value of an output, for dragging a wire from it and dropping on empty
        /// canvas. Each has the input <see cref="BlockChoice.Pin"/> to wire to. Empty for a block with no value. Functions
        /// of the graph are offered for each of their parameters, since a parameter takes any value.
        /// </summary>
        public IReadOnlyList<BlockChoice> SuggestForOutput(string function, string nodeId, string outputPin = null)
        {
            var type = BlockTypeChecker.OutputType(Graph, function, nodeId, Library, outputPin);
            if (type == null) return new BlockChoice[0];
            var choices = Choices(Index().Accepting(type)).ToList();
            // Parameters of the graph's own functions have no declared type, so they take any value.
            foreach (var f in Graph.Functions.Where(f => f != null && !string.IsNullOrEmpty(f.Name)))
                for (int i = 0; i < f.Parameters.Count; i++)
                    choices.Add(new BlockChoice("call", f.Name, f.Name, "Graph", FunctionSpec.ArgPin(i)));
            return choices;
        }

        /// <summary>
        /// The blocks that could give a value to an input pin, for dragging a wire from the pin and dropping on empty
        /// canvas. Each has the output <see cref="BlockChoice.Pin"/> to wire from. Empty when the block has no such pin.
        /// Functions of the graph are offered, since one may return a value of any type.
        /// </summary>
        public IReadOnlyList<BlockChoice> SuggestForInput(string function, string nodeId, string pin)
        {
            var fn = FindFunction(function);
            var node = fn == null ? null : FindNode(fn, nodeId);
            if (node == null || CheckInputPin(node, pin) != null) return new BlockChoice[0];
            var choices = Choices(Index().Producing(InputTypeOf(node, pin))).ToList();
            // The graph's own functions may return a value of any type.
            foreach (var f in Graph.Functions.Where(f => f != null && !string.IsNullOrEmpty(f.Name)))
                choices.Add(new BlockChoice("call", f.Name, f.Name, "Graph"));
            return choices;
        }

        // ---- Search helpers ----

        private PinTypeIndex Index()
        {
            if (_index == null || _indexedBlocks != Library.Blocks.Count || _indexedFunctions != Library.Functions.Count)
            {
                _index = new PinTypeIndex(Library, includeBlocks: true);
                _indexedBlocks = Library.Blocks.Count;
                _indexedFunctions = Library.Functions.Count;
            }
            return _index;
        }

        private PinType InputTypeOf(BlockNode node, string pin)
        {
            if (node.Block == "call")
            {
                var spec = Library.FindFunction(node.Name);
                int at = spec == null ? -1 : InputPinsOf(node).IndexOf(pin);
                return Graph.Functions.Any(f => f != null && f.Name == node.Name) || at < 0 ? PinType.Any : spec.ParameterTypes[at];
            }
            return Library.FindBlock(node.Block)?.InputType(pin) ?? PinType.Any;
        }

        private static IReadOnlyList<BlockChoice> Choices(IReadOnlyList<PinMatch> matches) =>
            matches.Select(m => m.Function != null
                ? new BlockChoice("call", Qualified(m.Function), Qualified(m.Function), m.Function.Module ?? "Functions", m.Pin)
                : new BlockChoice(m.Block.Name, null, m.Block.Title, m.Block.Category, m.Pin)).ToList();

        private static string Qualified(FunctionSpec f) => f.Module != null ? f.Module + "." + f.Name : f.Name;

        private IEnumerable<BlockChoice> AllChoices()
        {
            foreach (var b in Library.Blocks)
                if (b.Name != "call") yield return new BlockChoice(b.Name, null, b.Title, b.Category);
            foreach (var f in Library.Functions)
                yield return new BlockChoice("call", Qualified(f), Qualified(f), f.Module ?? "Functions");
            foreach (var f in Graph.Functions.Where(f => f != null && !string.IsNullOrEmpty(f.Name)))
                yield return new BlockChoice("call", f.Name, f.Name, "Graph");
        }

        // -1: no match. 0: exact title. 1: title starts with the text. 2: the title, name or category contains it.
        private static int Rank(BlockChoice choice, string needle)
        {
            if (needle.Length == 0) return 2;
            if (string.Equals(choice.Title, needle, StringComparison.OrdinalIgnoreCase)) return 0;
            if (choice.Title.StartsWith(needle, StringComparison.OrdinalIgnoreCase)) return 1;
            bool contains = choice.Title.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                || (choice.Name != null && choice.Name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                || choice.Category.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
            return contains ? 2 : -1;
        }
    }
}
