using System.Collections.Generic;
using System.Linq;

namespace CSharpPython.Blocks
{
    /// <summary>
    /// Where the variable of a loop block may be used: only by blocks inside its body. The compiler reports a wire
    /// that leaves the body; this answers the same question for an editor before the wire is drawn, from the shape
    /// of the graph alone.
    /// </summary>
    internal sealed class LoopScopes
    {
        private readonly BlockFunction _fn;
        private readonly BlockLibrary _lib;
        private readonly HashSet<string> _userFunctions;
        private readonly Dictionary<string, BlockNode> _nodes = new Dictionary<string, BlockNode>();
        private readonly Dictionary<string, HashSet<string>> _bodies = new Dictionary<string, HashSet<string>>();
        private HashSet<string> _placed;

        public LoopScopes(BlockFunction fn, BlockLibrary lib, HashSet<string> userFunctions)
        {
            _fn = fn;
            _lib = lib;
            _userFunctions = userFunctions;
            foreach (var n in fn.Nodes)
                if (n != null && !string.IsNullOrEmpty(n.Id) && !_nodes.ContainsKey(n.Id)) _nodes[n.Id] = n;
        }

        /// <summary>
        /// Why wiring the value of <paramref name="source"/> into <paramref name="target"/> would take a loop's variable
        /// out of its loop, or null when it would not. A block not yet joined to the flow is not judged: the wire that
        /// joins it is.
        /// </summary>
        public string Problem(BlockNode source, BlockNode target)
        {
            foreach (var loop in LoopsBehind(source))
            {
                if (Inside(target, loop, new HashSet<string>())) continue;
                return "The value of the loop '" + loop.Name + "' can only be used by blocks inside that loop.";
            }
            return null;
        }

        // The loop blocks whose variable the value of 'node' depends on, through any chain of wires.
        private IEnumerable<BlockNode> LoopsBehind(BlockNode node)
        {
            var loops = new List<BlockNode>();
            var seen = new HashSet<string>();
            var work = new Stack<BlockNode>();
            work.Push(node);
            while (work.Count > 0)
            {
                var n = work.Pop();
                if (!seen.Add(n.Id)) continue;
                if (n.Block != "call" && _lib.FindBlock(n.Block)?.LoopVariable == true)
                {
                    loops.Add(n);
                    continue;
                }
                foreach (var input in n.Inputs ?? new List<BlockInput>())
                    if (input != null && !string.IsNullOrEmpty(input.From) && _nodes.TryGetValue(input.From, out var from)) work.Push(from);
            }
            return loops;
        }

        // Whether a block is in the body of a loop. A value block is inside when every block that reads it is.
        private bool Inside(BlockNode node, BlockNode loop, HashSet<string> visiting)
        {
            if (!visiting.Add(node.Id)) return true;
            if (IsFlow(node)) return !Placed(node.Id) || Body(loop).Contains(node.Id);
            foreach (var reader in _nodes.Values)
                if ((reader.Inputs ?? new List<BlockInput>()).Any(i => i != null && i.From == node.Id) && !Inside(reader, loop, visiting))
                    return false;
            return true;
        }

        private bool IsFlow(BlockNode node)
        {
            if (node.Block == "call")
                return _userFunctions.Contains(node.Name ?? "") || !(_lib.FindFunction(node.Name ?? "")?.Pure ?? false);
            return _lib.FindBlock(node.Block)?.Kind != BlockKind.Value;
        }

        // The blocks of the flow reachable from the loop's body pin.
        private HashSet<string> Body(BlockNode loop)
        {
            if (_bodies.TryGetValue(loop.Id, out var known)) return known;
            var body = new HashSet<string>();
            string start = loop.Flow?.FirstOrDefault(l => l != null && l.Pin == "body")?.To;
            Walk(start, body);
            return _bodies[loop.Id] = body;
        }

        private bool Placed(string id)
        {
            if (_placed == null)
            {
                _placed = new HashSet<string>();
                Walk(_fn.Start, _placed);
            }
            return _placed.Contains(id);
        }

        private void Walk(string start, HashSet<string> into)
        {
            var work = new Stack<string>();
            if (!string.IsNullOrEmpty(start)) work.Push(start);
            while (work.Count > 0)
            {
                string id = work.Pop();
                if (!_nodes.TryGetValue(id, out var node) || !into.Add(id)) continue;
                foreach (var link in node.Flow ?? new List<BlockLink>())
                    if (link != null && !string.IsNullOrEmpty(link.To)) work.Push(link.To);
            }
        }
    }
}
