using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CSharpPython.Blocks
{
    /// <summary>The answer to an edit made through <see cref="BlockEditor"/>. A refused edit changes nothing.</summary>
    public sealed class EditResult
    {
        public bool Succeeded { get; }

        /// <summary>Why the edit was refused, or null when it was made.</summary>
        public string Reason { get; }

        /// <summary>The block an edit added, or null.</summary>
        public BlockNode Node { get; }

        private EditResult(bool succeeded, string reason, BlockNode node)
        {
            Succeeded = succeeded;
            Reason = reason;
            Node = node;
        }

        internal static EditResult Done(BlockNode node = null) => new EditResult(true, null, node);

        internal static EditResult Refused(string reason) => new EditResult(false, reason, null);

        public override string ToString() => Succeeded ? "done" : "refused: " + Reason;
    }

    /// <summary>
    /// The editing rules of a block graph that do not depend on a host or on drawing (docs/node-view-design.md,
    /// step 5): an editor of any kind calls these instead of changing the graph itself, so a canvas cannot leave the
    /// graph in a state the compiler would not accept. Every method checks first and returns an
    /// <see cref="EditResult"/>; a refused edit changes nothing and raises nothing. <see cref="Changed"/> is raised
    /// once after each edit that was made.
    /// </summary>
    public sealed class BlockEditor
    {
        public BlockGraph Graph { get; }
        public BlockLibrary Library { get; }

        /// <summary>The limits a graph may not grow past; the defaults of <see cref="BlockLimits"/> unless the host sets its own.</summary>
        public BlockLimits Limits { get; set; } = new BlockLimits();

        /// <summary>Raised after every edit that was made.</summary>
        public event Action Changed;

        public BlockEditor(BlockGraph graph, BlockLibrary library = null)
        {
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            Library = library ?? new BlockLibrary();
        }

        // ---- Blocks ----

        /// <summary>
        /// Adds a block to a function at a place on the canvas, with a new id that no block of the graph has. For a
        /// <c>call</c> block, <paramref name="name"/> is the function and it must be one the library has or the graph
        /// defines. For a block that needs a name (a variable, a field) the name can be left out and set with
        /// <see cref="SetName"/> once it is typed. The new block is in <see cref="EditResult.Node"/>.
        /// </summary>
        public EditResult AddNode(string function, string block, string name = null, float x = 0, float y = 0)
        {
            var fn = FindFunction(function);
            if (fn == null) return EditResult.Refused(NoFunction(function));
            if (string.IsNullOrEmpty(block)) return EditResult.Refused("A block needs a type.");
            if (block == "call")
            {
                string problem = CheckCallName(name);
                if (problem != null) return EditResult.Refused(problem);
            }
            else if (Library.FindBlock(block) == null) return EditResult.Refused("There is no block type '" + block + "'.");

            if (fn.Nodes.Count >= Limits.MaxNodesPerFunction)
                return EditResult.Refused("The function '" + function + "' already has the most blocks it may have (" + Limits.MaxNodesPerFunction + ").");
            if (Graph.Functions.Where(f => f != null).Sum(f => f.Nodes.Count) >= Limits.MaxNodes)
                return EditResult.Refused("The graph already has the most blocks it may have (" + Limits.MaxNodes + ").");

            var node = new BlockNode(NewId(), block, name);
            fn.Add(node);
            Graph.Editor.Place(node.Id, x, y);
            return Made(EditResult.Done(node));
        }

        /// <summary>
        /// Removes a block and everything that pointed at it: wires from it to other blocks' inputs (the inputs are
        /// left empty), execution links that led to it, the function's start if it was the start, and its place,
        /// reroute points and group membership in the editor section. The blocks it was linked to stay.
        /// </summary>
        public EditResult RemoveNode(string function, string nodeId)
        {
            var fn = FindFunction(function);
            if (fn == null) return EditResult.Refused(NoFunction(function));
            var node = FindNode(fn, nodeId);
            if (node == null) return EditResult.Refused(NoNode(function, nodeId));

            fn.Nodes.Remove(node);
            foreach (var other in fn.Nodes.Where(n => n != null))
            {
                foreach (var gone in other.Inputs.Where(i => i != null && i.From == nodeId).ToList())
                {
                    other.Inputs.Remove(gone);
                    ForgetBends(other.Id, gone.Pin);
                }
                other.Flow.RemoveAll(l => l != null && l.To == nodeId);
            }
            if (fn.Start == nodeId) fn.Start = null;

            Graph.Editor.Nodes.RemoveAll(n => n != null && n.Id == nodeId);
            Graph.Editor.Wires.RemoveAll(w => w != null && w.Node == nodeId);
            foreach (var g in Graph.Editor.Groups.Where(g => g != null)) g.Nodes.Remove(nodeId);
            Graph.Editor.Groups.RemoveAll(g => g == null || g.Nodes.Count == 0);
            return Made(EditResult.Done());
        }

        /// <summary>
        /// Sets a block's name: the function of a <c>call</c> (which must exist), the variable of a variable block,
        /// the field of a field block, or the text of a comment. Whether a variable name is a valid identifier is
        /// reported by compiling, so a name can be typed a letter at a time.
        /// </summary>
        public EditResult SetName(string function, string nodeId, string name)
        {
            var fn = FindFunction(function);
            if (fn == null) return EditResult.Refused(NoFunction(function));
            var node = FindNode(fn, nodeId);
            if (node == null) return EditResult.Refused(NoNode(function, nodeId));
            if (node.Block == "call")
            {
                string problem = CheckCallName(name);
                if (problem != null) return EditResult.Refused(problem);
            }
            else if (!TakesName(node.Block)) return EditResult.Refused("A '" + node.Block + "' block has no name to set.");
            node.Name = name;
            return Made(EditResult.Done());
        }

        /// <summary>Moves a block on the canvas. This is not a change to the program.</summary>
        public EditResult MoveNode(string function, string nodeId, float x, float y)
        {
            var fn = FindFunction(function);
            if (fn == null) return EditResult.Refused(NoFunction(function));
            if (FindNode(fn, nodeId) == null) return EditResult.Refused(NoNode(function, nodeId));
            Graph.Editor.Place(nodeId, x, y);
            return Made(EditResult.Done());
        }

        // ---- Helpers ----

        private EditResult Made(EditResult result)
        {
            Changed?.Invoke();
            return result;
        }

        private BlockFunction FindFunction(string name) =>
            name == null ? null : Graph.Functions.FirstOrDefault(f => f != null && f.Name == name);

        private static BlockNode FindNode(BlockFunction fn, string id) =>
            id == null ? null : fn.Nodes.FirstOrDefault(n => n != null && n.Id == id);

        private static string NoFunction(string function) => "There is no function '" + function + "' in the graph.";

        private static string NoNode(string function, string id) => "There is no block '" + id + "' in the function '" + function + "'.";

        private string CheckCallName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "A call block needs the name of a function.";
            bool own = Graph.Functions.Any(f => f != null && f.Name == name);
            return own || Library.FindFunction(name) != null ? null : "There is no function '" + name + "'.";
        }

        // Blocks whose name is something the editor types: a variable or field name, or a comment's text.
        private bool TakesName(string block)
        {
            var spec = Library.FindBlock(block);
            return spec != null && (spec.NeedsName || block == "comment" || block == "repeat");
        }

        private void ForgetBends(string node, string pin) =>
            Graph.Editor.Wires.RemoveAll(w => w != null && w.Node == node && w.Pin == pin);

        // The next id of the form n1, n2, ... that no block in the graph has. Ids are unique across all functions.
        private string NewId()
        {
            long highest = 0;
            var used = new HashSet<string>();
            foreach (var f in Graph.Functions.Where(f => f != null))
                foreach (var n in f.Nodes.Where(n => n != null && n.Id != null))
                {
                    used.Add(n.Id);
                    if (n.Id.Length > 1 && n.Id[0] == 'n' && long.TryParse(n.Id.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out long number) && number > highest)
                        highest = number;
                }
            string id;
            do id = "n" + (++highest).ToString(CultureInfo.InvariantCulture);
            while (used.Contains(id));
            return id;
        }
    }
}
