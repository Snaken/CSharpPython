using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharpPython.Blocks
{
    /// <summary>Where an editor drew a block, and whether it is shown collapsed. Keyed by <see cref="BlockNode.Id"/>.</summary>
    [Serializable]
    public sealed class BlockNodeLayout
    {
        public string Id;
        public float X;
        public float Y;
        public bool Collapsed;

        public BlockNodeLayout() { }

        public BlockNodeLayout(string id, float x, float y, bool collapsed = false)
        {
            Id = id;
            X = x;
            Y = y;
            Collapsed = collapsed;
        }
    }

    /// <summary>A box of text on the canvas that belongs to no block.</summary>
    [Serializable]
    public sealed class BlockCommentBox
    {
        public string Text;
        public float X;
        public float Y;
        public float Width;
        public float Height;

        public BlockCommentBox() { }

        public BlockCommentBox(string text, float x, float y, float width, float height)
        {
            Text = text;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    [Serializable]
    public sealed class BlockPoint
    {
        public float X;
        public float Y;

        public BlockPoint() { }

        public BlockPoint(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>The reroute points of one wire, in order from its source to the input pin it ends at.</summary>
    [Serializable]
    public sealed class BlockWireBends
    {
        /// <summary>The block that has the input pin the wire ends at.</summary>
        public string Node;

        public string Pin;
        public List<BlockPoint> Points = new List<BlockPoint>();

        public BlockWireBends() { }

        public BlockWireBends(string node, string pin)
        {
            Node = node;
            Pin = pin;
        }
    }

    /// <summary>A named set of blocks that an editor draws together, and may collapse into one box.</summary>
    [Serializable]
    public sealed class BlockGroup
    {
        public string Title;
        public bool Collapsed;
        public List<string> Nodes = new List<string>();

        public BlockGroup() { }

        public BlockGroup(string title, params string[] nodes)
        {
            Title = title;
            Nodes.AddRange(nodes);
        }
    }

    /// <summary>
    /// What only an editor cares about: block positions, comment boxes, reroute points and groups, kept in the same
    /// file as the graph (docs/node-view-design.md, decision 17). The compiler never reads it, so changing it never
    /// changes the generated program, and a task saved from a script is still valid after a block is moved.
    /// </summary>
    [Serializable]
    public sealed class BlockEditorData
    {
        public List<BlockNodeLayout> Nodes = new List<BlockNodeLayout>();
        public List<BlockCommentBox> Comments = new List<BlockCommentBox>();
        public List<BlockWireBends> Wires = new List<BlockWireBends>();
        public List<BlockGroup> Groups = new List<BlockGroup>();

        /// <summary>True when nothing is stored, so the file has no editor section.</summary>
        public bool IsEmpty => Nodes.Count == 0 && Comments.Count == 0 && Wires.Count == 0 && Groups.Count == 0;

        /// <summary>The layout of a block, or null when the editor has not placed it.</summary>
        public BlockNodeLayout LayoutOf(string nodeId) => Nodes.FirstOrDefault(n => n != null && n.Id == nodeId);

        /// <summary>Records where a block is, adding its entry when it has none.</summary>
        public BlockNodeLayout Place(string nodeId, float x, float y)
        {
            var layout = LayoutOf(nodeId);
            if (layout == null)
            {
                layout = new BlockNodeLayout(nodeId, x, y);
                Nodes.Add(layout);
            }
            layout.X = x;
            layout.Y = y;
            return layout;
        }

        /// <summary>The reroute points of the wire into an input pin, or null when it has none.</summary>
        public BlockWireBends BendsOf(string node, string pin) => Wires.FirstOrDefault(w => w != null && w.Node == node && w.Pin == pin);

        /// <summary>
        /// Drops what refers to a block that is no longer in the graph: its layout, the reroute points of its wires, and
        /// its place in a group. Groups left empty are dropped too.
        /// </summary>
        public void Prune(BlockGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            var ids = new HashSet<string>(graph.Functions.Where(f => f != null).SelectMany(f => f.Nodes).Where(n => n != null && n.Id != null).Select(n => n.Id));
            Nodes.RemoveAll(n => n == null || !ids.Contains(n.Id ?? ""));
            Wires.RemoveAll(w => w == null || !ids.Contains(w.Node ?? ""));
            Comments.RemoveAll(c => c == null);
            foreach (var g in Groups.Where(g => g != null)) g.Nodes.RemoveAll(id => !ids.Contains(id ?? ""));
            Groups.RemoveAll(g => g == null || g.Nodes.Count == 0);
        }
    }
}
