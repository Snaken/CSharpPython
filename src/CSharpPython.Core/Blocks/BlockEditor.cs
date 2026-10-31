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
    public sealed partial class BlockEditor
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

        // ---- Wires and links ----

        /// <summary>
        /// Wires the output of one block to an input pin of another, replacing what the pin had (a wire or a typed-in
        /// literal). The wire is refused when its type does not fit, when it would break the wires further along
        /// (<see cref="BlockTypeChecker.CheckWire"/>), when the pin is not one the block has, or when the value would
        /// depend on itself. For a block with several outputs, name the one being wired with <paramref name="fromPin"/>.
        /// </summary>
        public EditResult Connect(string function, string toNode, string pin, string fromNode, string fromPin = null)
        {
            var fn = FindFunction(function);
            if (fn == null) return EditResult.Refused(NoFunction(function));
            var target = FindNode(fn, toNode);
            if (target == null) return EditResult.Refused(NoNode(function, toNode));
            var source = FindNode(fn, fromNode);
            if (source == null) return EditResult.Refused(NoNode(function, fromNode));
            string problem = CheckInputPin(target, pin);
            if (problem != null) return EditResult.Refused(problem);
            if (DependsOn(fn, source, target)) return EditResult.Refused("A block cannot take its own value, even through other blocks.");

            var check = BlockTypeChecker.CheckWire(Graph, function, toNode, pin, fromNode, Library, fromPin);
            if (!check.Allowed) return EditResult.Refused(check.Reason);

            ClearInput(target, pin);
            target.Connect(pin, fromNode, fromPin);
            return Made(EditResult.Done());
        }

        /// <summary>Types a literal into an input pin, replacing what the pin had. Whether the literal fits the pin is reported by compiling.</summary>
        public EditResult SetLiteral(string function, string nodeId, string pin, string literal)
        {
            var fn = FindFunction(function);
            if (fn == null) return EditResult.Refused(NoFunction(function));
            var node = FindNode(fn, nodeId);
            if (node == null) return EditResult.Refused(NoNode(function, nodeId));
            string problem = CheckInputPin(node, pin);
            if (problem != null) return EditResult.Refused(problem);
            if (literal == null) return EditResult.Refused("A literal needs text; use Disconnect to empty the pin.");
            if (literal.Length > Limits.MaxLiteralLength)
                return EditResult.Refused("A literal may be at most " + Limits.MaxLiteralLength + " characters.");

            ClearInput(node, pin);
            node.Literal(pin, literal);
            return Made(EditResult.Done());
        }

        /// <summary>Empties an input pin: removes its wire or its literal, and the reroute points of the wire.</summary>
        public EditResult Disconnect(string function, string nodeId, string pin)
        {
            var fn = FindFunction(function);
            if (fn == null) return EditResult.Refused(NoFunction(function));
            var node = FindNode(fn, nodeId);
            if (node == null) return EditResult.Refused(NoNode(function, nodeId));
            if (!node.Inputs.Any(i => i != null && i.Pin == pin)) return EditResult.Refused("The pin '" + pin + "' has nothing connected.");
            ClearInput(node, pin);
            return Made(EditResult.Done());
        }

        /// <summary>
        /// Links an execution pin of a block to the block that runs next, replacing the link the pin had. Refused when
        /// the block does not have that pin, when the target is not a step in the flow (a value block has no
        /// execution pin), or when it would make the flow loop back on itself (use a loop block).
        /// </summary>
        public EditResult Link(string function, string fromNode, string pin, string toNode)
        {
            var fn = FindFunction(function);
            if (fn == null) return EditResult.Refused(NoFunction(function));
            var source = FindNode(fn, fromNode);
            if (source == null) return EditResult.Refused(NoNode(function, fromNode));
            var target = FindNode(fn, toNode);
            if (target == null) return EditResult.Refused(NoNode(function, toNode));
            if (!FlowPinsOf(source).Contains(pin ?? "")) return EditResult.Refused("A '" + source.Block + "' block has no execution pin '" + pin + "'.");
            if (!IsStep(target)) return EditResult.Refused("A '" + target.Block + "' block is a value, not a step in the flow, so nothing can run into it.");
            if (FlowReaches(fn, target, source)) return EditResult.Refused("That link would make the flow loop back on itself; use a loop block.");

            source.Flow.RemoveAll(l => l != null && l.Pin == pin);
            source.Then(pin, toNode);
            return Made(EditResult.Done());
        }

        /// <summary>Removes the link from an execution pin.</summary>
        public EditResult Unlink(string function, string fromNode, string pin)
        {
            var fn = FindFunction(function);
            if (fn == null) return EditResult.Refused(NoFunction(function));
            var source = FindNode(fn, fromNode);
            if (source == null) return EditResult.Refused(NoNode(function, fromNode));
            if (source.Flow.RemoveAll(l => l != null && l.Pin == pin) == 0) return EditResult.Refused("The pin '" + pin + "' is not linked.");
            return Made(EditResult.Done());
        }

        /// <summary>Sets the block a function starts at, or clears it when <paramref name="nodeId"/> is null or empty.</summary>
        public EditResult SetStart(string function, string nodeId)
        {
            var fn = FindFunction(function);
            if (fn == null) return EditResult.Refused(NoFunction(function));
            if (string.IsNullOrEmpty(nodeId)) fn.Start = null;
            else
            {
                var node = FindNode(fn, nodeId);
                if (node == null) return EditResult.Refused(NoNode(function, nodeId));
                if (!IsStep(node)) return EditResult.Refused("A '" + node.Block + "' block is a value, not a step in the flow, so a function cannot start at it.");
                fn.Start = nodeId;
            }
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

        // The input pins a block has: those of its kind, or (for a call) one per parameter of the function it calls.
        private List<string> InputPinsOf(BlockNode node)
        {
            if (node.Block == "call")
            {
                var own = Graph.Functions.FirstOrDefault(f => f != null && f.Name == node.Name);
                int count = own != null ? own.Parameters.Count : Library.FindFunction(node.Name)?.Parameters.Count ?? 0;
                return Enumerable.Range(0, count).Select(FunctionSpec.ArgPin).ToList();
            }
            return Library.FindBlock(node.Block)?.Inputs.ToList() ?? new List<string>();
        }

        private List<string> FlowPinsOf(BlockNode node)
        {
            if (node.Block != "call") return Library.FindBlock(node.Block)?.FlowPins.ToList() ?? new List<string>();
            bool own = Graph.Functions.Any(f => f != null && f.Name == node.Name);
            var spec = own ? null : Library.FindFunction(node.Name);
            bool known = own || spec != null;
            return known && (own || !spec.Pure) ? new List<string> { "next" } : new List<string>();
        }

        // True for a block that is a step in the execution flow (not a value block or a pure call).
        private bool IsStep(BlockNode node)
        {
            if (node.Block == "call") return FlowPinsOf(node).Count > 0;
            return Library.FindBlock(node.Block)?.Kind == BlockKind.Flow;
        }

        private string CheckInputPin(BlockNode node, string pin)
        {
            if (string.IsNullOrEmpty(pin)) return "A pin needs a name.";
            bool variadic = node.Block == "list" && pin.Length > 4 && pin.StartsWith("item", StringComparison.Ordinal) && pin.Skip(4).All(c => c >= '0' && c <= '9');
            if (variadic || InputPinsOf(node).Contains(pin)) return null;
            return "A '" + node.Block + "' block has no input pin '" + pin + "'.";
        }

        private void ClearInput(BlockNode node, string pin)
        {
            node.Inputs.RemoveAll(i => i != null && i.Pin == pin);
            ForgetBends(node.Id, pin);
        }

        // True when the value of 'block' reads the value of 'other' (or is it), through any chain of wires.
        private static bool DependsOn(BlockFunction fn, BlockNode block, BlockNode other)
        {
            var seen = new HashSet<string>();
            var pending = new Stack<BlockNode>();
            pending.Push(block);
            while (pending.Count > 0)
            {
                var n = pending.Pop();
                if (n == other) return true;
                if (!seen.Add(n.Id)) continue;
                foreach (var input in n.Inputs.Where(i => i != null && i.From != null))
                {
                    var next = FindNode(fn, input.From);
                    if (next != null) pending.Push(next);
                }
            }
            return false;
        }

        // True when the flow runs from 'from' to 'to' through any chain of links (or they are the same block).
        private static bool FlowReaches(BlockFunction fn, BlockNode from, BlockNode to)
        {
            var seen = new HashSet<string>();
            var pending = new Stack<BlockNode>();
            pending.Push(from);
            while (pending.Count > 0)
            {
                var n = pending.Pop();
                if (n == to) return true;
                if (!seen.Add(n.Id)) continue;
                foreach (var link in n.Flow.Where(l => l != null && l.To != null))
                {
                    var next = FindNode(fn, link.To);
                    if (next != null) pending.Push(next);
                }
            }
            return false;
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
