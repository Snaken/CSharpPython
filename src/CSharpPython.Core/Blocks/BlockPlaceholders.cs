using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharpPython.Blocks
{
    /// <summary>
    /// A block that names a function or block type this host does not have, for instance after the host removed a
    /// function or the graph came from another project. The graph still loads and keeps the block and its wires;
    /// an editor shows it as a placeholder, and compiling reports it (docs/node-view-design.md, decision 18).
    /// </summary>
    public sealed class BlockPlaceholder
    {
        public string Function { get; }
        public string NodeId { get; }

        /// <summary>The block type: <c>call</c> for a missing function.</summary>
        public string Block { get; }

        /// <summary>The missing function's name, or null for a missing block type.</summary>
        public string Name { get; }

        public string Reason { get; }

        internal BlockPlaceholder(string function, BlockNode node, string reason)
        {
            Function = function;
            NodeId = node.Id;
            Block = node.Block;
            Name = node.Block == "call" ? node.Name : null;
            Reason = reason;
        }
    }

    public static class BlockPlaceholders
    {
        /// <summary>The blocks of the graph that name something <paramref name="library"/> does not have, in graph order.</summary>
        public static IReadOnlyList<BlockPlaceholder> Find(BlockGraph graph, BlockLibrary library = null)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            var lib = library ?? new BlockLibrary();
            var own = new HashSet<string>(graph.Functions.Where(f => f != null && !string.IsNullOrEmpty(f.Name)).Select(f => f.Name));
            var found = new List<BlockPlaceholder>();
            foreach (var fn in graph.Functions)
            {
                if (fn == null) continue;
                foreach (var node in fn.Nodes)
                {
                    if (node == null) continue;
                    if (node.Block == "call")
                    {
                        if (!string.IsNullOrEmpty(node.Name) && !own.Contains(node.Name) && lib.FindFunction(node.Name) == null)
                            found.Add(new BlockPlaceholder(fn.Name, node, "the function '" + node.Name + "' is not available"));
                    }
                    else if (!string.IsNullOrEmpty(node.Block) && lib.FindBlock(node.Block) == null)
                        found.Add(new BlockPlaceholder(fn.Name, node, "the block type '" + node.Block + "' is not available"));
                }
            }
            return found;
        }
    }
}
