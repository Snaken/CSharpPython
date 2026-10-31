using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CSharpPython.Blocks
{
    /// <summary>
    /// How large a block graph file may be. <see cref="BlockGraph.FromJson(string, BlockLimits)"/> rejects a file that
    /// goes over any of them, with the reason, so a damaged or hostile file cannot make an editor or the compiler do
    /// unbounded work (docs/node-view-design.md, decision 19). The defaults are generous for hand-made graphs; a host
    /// changes them by setting the properties. They bound the file, not what the graph does when it runs
    /// (<c>MaxSteps</c> does that).
    /// </summary>
    public sealed class BlockLimits
    {
        private int _text = 8_000_000, _functions = 500, _nodesPerFunction = 5_000, _nodes = 20_000, _variables = 1_000;
        private int _parameters = 32, _inputs = 256, _links = 8, _id = 64, _name = 128, _pin = 64, _comment = 10_000;
        private int _literal = 4_096, _type = 128, _editor = 20_000, _editorText = 10_000;

        /// <summary>The longest document, in characters.</summary>
        public int MaxTextLength { get => _text; set => _text = Positive(value); }

        public int MaxFunctions { get => _functions; set => _functions = Positive(value); }
        public int MaxNodesPerFunction { get => _nodesPerFunction; set => _nodesPerFunction = Positive(value); }

        /// <summary>The most blocks in the whole graph.</summary>
        public int MaxNodes { get => _nodes; set => _nodes = Positive(value); }

        public int MaxVariables { get => _variables; set => _variables = Positive(value); }
        public int MaxParameters { get => _parameters; set => _parameters = Positive(value); }

        /// <summary>The most input pins one block may have wired (a list block has one per item).</summary>
        public int MaxInputsPerNode { get => _inputs; set => _inputs = Positive(value); }

        public int MaxLinksPerNode { get => _links; set => _links = Positive(value); }

        /// <summary>The longest block id.</summary>
        public int MaxIdLength { get => _id; set => _id = Positive(value); }

        /// <summary>The longest function, variable, parameter or field name.</summary>
        public int MaxNameLength { get => _name; set => _name = Positive(value); }

        /// <summary>The longest pin name.</summary>
        public int MaxPinLength { get => _pin; set => _pin = Positive(value); }

        /// <summary>The longest comment block text (a comment block keeps its text in its name).</summary>
        public int MaxCommentLength { get => _comment; set => _comment = Positive(value); }

        /// <summary>The longest typed-in value or starting value.</summary>
        public int MaxLiteralLength { get => _literal; set => _literal = Positive(value); }

        /// <summary>The longest type name, such as <c>list[int]</c>.</summary>
        public int MaxTypeLength { get => _type; set => _type = Positive(value); }

        /// <summary>The most entries in each list of the editor section.</summary>
        public int MaxEditorEntries { get => _editor; set => _editor = Positive(value); }

        /// <summary>The longest comment box text in the editor section.</summary>
        public int MaxEditorTextLength { get => _editorText; set => _editorText = Positive(value); }

        private static int Positive(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "A limit cannot be negative.");
            return value;
        }

        /// <summary>The first limit the graph goes over, as a sentence, or null when it is within all of them.</summary>
        public string Problem(BlockGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (graph.Variables.Count > _variables) return Many("variables", graph.Variables.Count, _variables);
            if (graph.Functions.Count > _functions) return Many("functions", graph.Functions.Count, _functions);

            foreach (var v in graph.Variables.Where(v => v != null))
            {
                string p = TooLong("the name of a variable", v.Name, _name) ?? TooLong("the type of variable '" + Cut(v.Name) + "'", v.Type, _type)
                    ?? TooLong("the starting value of variable '" + Cut(v.Name) + "'", v.Initial, _literal);
                if (p != null) return p;
            }

            int nodes = 0;
            foreach (var f in graph.Functions.Where(f => f != null))
            {
                string at = "the function '" + Cut(f.Name) + "'";
                string p = TooLong("the name of a function", f.Name, _name) ?? TooLong("the start of " + at, f.Start, _id);
                if (p != null) return p;
                if (f.Parameters.Count > _parameters) return at + " has too many parameters (" + f.Parameters.Count + "; the limit is " + _parameters + ")";
                foreach (var param in f.Parameters)
                    if ((p = TooLong("a parameter name of " + at, param, _name)) != null) return p;
                if (f.Nodes.Count > _nodesPerFunction) return at + " has too many blocks (" + f.Nodes.Count + "; the limit is " + _nodesPerFunction + ")";
                nodes += f.Nodes.Count;
                if (nodes > _nodes) return Many("blocks", nodes, _nodes);
                foreach (var n in f.Nodes.Where(n => n != null))
                    if ((p = NodeProblem(n, at)) != null) return p;
            }
            return EditorProblem(graph.Editor);
        }

        private string NodeProblem(BlockNode n, string at)
        {
            string p = TooLong("a block id in " + at, n.Id, _id);
            string where = "the block '" + Cut(n.Id) + "' in " + at;
            if (p != null) return p;
            if ((p = TooLong("the type of " + where, n.Block, _name)) != null) return p;
            if ((p = TooLong(n.Block == "comment" ? "the text of " + where : "the name of " + where, n.Name, n.Block == "comment" ? _comment : _name)) != null) return p;
            if (n.Inputs.Count > _inputs) return where + " has too many inputs (" + n.Inputs.Count + "; the limit is " + _inputs + ")";
            if (n.Flow.Count > _links) return where + " has too many execution links (" + n.Flow.Count + "; the limit is " + _links + ")";
            foreach (var i in n.Inputs.Where(i => i != null))
                if ((p = TooLong("a pin name of " + where, i.Pin, _pin) ?? TooLong("a wire source in " + where, i.From, _id)
                    ?? TooLong("an output name in " + where, i.FromPin, _pin) ?? TooLong("a typed-in value of " + where, i.Literal, _literal)) != null) return p;
            foreach (var l in n.Flow.Where(l => l != null))
                if ((p = TooLong("an execution pin name of " + where, l.Pin, _pin) ?? TooLong("an execution link target in " + where, l.To, _id)) != null) return p;
            return null;
        }

        private string EditorProblem(BlockEditorData e)
        {
            if (e == null) return null;
            if (e.Nodes.Count > _editor) return Many("block layouts in the editor section", e.Nodes.Count, _editor);
            if (e.Comments.Count > _editor) return Many("comment boxes in the editor section", e.Comments.Count, _editor);
            if (e.Wires.Count > _editor) return Many("wires in the editor section", e.Wires.Count, _editor);
            if (e.Groups.Count > _editor) return Many("groups in the editor section", e.Groups.Count, _editor);
            string p;
            foreach (var n in e.Nodes.Where(n => n != null))
                if ((p = TooLong("a block id in the editor section", n.Id, _id)) != null) return p;
            foreach (var c in e.Comments.Where(c => c != null))
                if ((p = TooLong("a comment box text", c.Text, _editorText)) != null) return p;
            int points = 0;
            foreach (var w in e.Wires.Where(w => w != null))
            {
                if ((p = TooLong("a wire's block id in the editor section", w.Node, _id) ?? TooLong("a wire's pin name in the editor section", w.Pin, _pin)) != null) return p;
                points += w.Points.Count;
            }
            if (points > _editor) return Many("reroute points in the editor section", points, _editor);
            int members = 0;
            foreach (var g in e.Groups.Where(g => g != null))
            {
                if ((p = TooLong("a group title", g.Title, _name)) != null) return p;
                foreach (var id in g.Nodes)
                    if ((p = TooLong("a block id in a group", id, _id)) != null) return p;
                members += g.Nodes.Count;
            }
            return members > _editor ? Many("blocks in groups in the editor section", members, _editor) : null;
        }

        private static string Many(string what, int count, int limit) =>
            "the graph has too many " + what + " (" + count.ToString(CultureInfo.InvariantCulture) + "; the limit is " + limit.ToString(CultureInfo.InvariantCulture) + ")";

        private static string TooLong(string what, string text, int limit) =>
            text != null && text.Length > limit
                ? what + " is too long (" + text.Length.ToString(CultureInfo.InvariantCulture) + " characters; the limit is " + limit.ToString(CultureInfo.InvariantCulture) + ")"
                : null;

        // A name quoted in a message is cut short so a huge name cannot make a huge message.
        private static string Cut(string text) => text == null ? "" : text.Length <= 40 ? text : text.Substring(0, 40) + "...";
    }
}
