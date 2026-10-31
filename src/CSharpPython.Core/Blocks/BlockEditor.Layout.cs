using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharpPython.Blocks
{
    // Edits to the editor section of the graph (graph.Editor): collapse, comment boxes, reroute points and groups.
    // None of them changes the generated program. Comment boxes and groups have no ids, so they are named by their
    // position in graph.Editor.Comments and graph.Editor.Groups; removing one moves the ones after it up.
    public sealed partial class BlockEditor
    {
        // ---- Collapse ----

        /// <summary>Collapses a block to its title bar, or opens it again. A block with no place yet is placed at the origin.</summary>
        public EditResult SetCollapsed(string function, string nodeId, bool collapsed)
        {
            var fn = FindFunction(function);
            if (fn == null) return EditResult.Refused(NoFunction(function));
            if (FindNode(fn, nodeId) == null) return EditResult.Refused(NoNode(function, nodeId));
            var layout = Graph.Editor.LayoutOf(nodeId) ?? Graph.Editor.Place(nodeId, 0, 0);
            layout.Collapsed = collapsed;
            return Made(EditResult.Done());
        }

        // ---- Comment boxes ----

        /// <summary>Adds a comment box on the canvas. It is the last in <c>Graph.Editor.Comments</c>.</summary>
        public EditResult AddComment(string text, float x, float y, float width, float height)
        {
            string problem = CheckFinite(x, y, width, height) ?? CheckCommentText(text);
            if (problem == null && (width < 0 || height < 0)) problem = "A comment box cannot have a negative size.";
            if (problem == null && Graph.Editor.Comments.Count >= Limits.MaxEditorEntries) problem = TooManyEditorEntries("comment boxes");
            if (problem != null) return EditResult.Refused(problem);
            Graph.Editor.Comments.Add(new BlockCommentBox(text ?? "", x, y, width, height));
            return Made(EditResult.Done());
        }

        public EditResult SetCommentText(int index, string text)
        {
            var box = CommentAt(index, out string problem);
            problem = problem ?? CheckCommentText(text);
            if (problem != null) return EditResult.Refused(problem);
            box.Text = text ?? "";
            return Made(EditResult.Done());
        }

        public EditResult MoveComment(int index, float x, float y)
        {
            var box = CommentAt(index, out string problem);
            problem = problem ?? CheckFinite(x, y);
            if (problem != null) return EditResult.Refused(problem);
            box.X = x;
            box.Y = y;
            return Made(EditResult.Done());
        }

        public EditResult ResizeComment(int index, float width, float height)
        {
            var box = CommentAt(index, out string problem);
            problem = problem ?? CheckFinite(width, height);
            if (problem == null && (width < 0 || height < 0)) problem = "A comment box cannot have a negative size.";
            if (problem != null) return EditResult.Refused(problem);
            box.Width = width;
            box.Height = height;
            return Made(EditResult.Done());
        }

        public EditResult RemoveComment(int index)
        {
            CommentAt(index, out string problem);
            if (problem != null) return EditResult.Refused(problem);
            Graph.Editor.Comments.RemoveAt(index);
            return Made(EditResult.Done());
        }

        // ---- Reroute points ----

        /// <summary>
        /// Adds a reroute point to the wire that ends at an input pin, at <paramref name="index"/> among its points
        /// (0 is next to the source block; the count of points, or any larger number, adds it at the end). The pin
        /// must have a wire from another block.
        /// </summary>
        public EditResult AddBend(string function, string nodeId, string pin, int index, float x, float y)
        {
            string problem = CheckWire(function, nodeId, pin) ?? CheckFinite(x, y);
            if (problem == null && index < 0) problem = "A reroute point cannot be placed before the first one.";
            var bends = Graph.Editor.BendsOf(nodeId, pin);
            if (problem == null && CountBends() >= Limits.MaxEditorEntries) problem = TooManyEditorEntries("reroute points");
            if (problem == null && bends == null && Graph.Editor.Wires.Count >= Limits.MaxEditorEntries) problem = TooManyEditorEntries("wires");
            if (problem != null) return EditResult.Refused(problem);

            if (bends == null)
            {
                bends = new BlockWireBends(nodeId, pin);
                Graph.Editor.Wires.Add(bends);
            }
            bends.Points.Insert(Math.Min(index, bends.Points.Count), new BlockPoint(x, y));
            return Made(EditResult.Done());
        }

        public EditResult MoveBend(string function, string nodeId, string pin, int index, float x, float y)
        {
            var bends = BendsOf(function, nodeId, pin, index, out string problem);
            problem = problem ?? CheckFinite(x, y);
            if (problem != null) return EditResult.Refused(problem);
            bends.Points[index] = new BlockPoint(x, y);
            return Made(EditResult.Done());
        }

        /// <summary>Removes a reroute point; the wire stays and goes straight between its neighbours.</summary>
        public EditResult RemoveBend(string function, string nodeId, string pin, int index)
        {
            var bends = BendsOf(function, nodeId, pin, index, out string problem);
            if (problem != null) return EditResult.Refused(problem);
            bends.Points.RemoveAt(index);
            if (bends.Points.Count == 0) Graph.Editor.Wires.Remove(bends);
            return Made(EditResult.Done());
        }

        // ---- Groups ----

        /// <summary>
        /// Groups blocks under a title, to draw them together or collapse them. A block is in at most one group. The
        /// new group is the last in <c>Graph.Editor.Groups</c>.
        /// </summary>
        public EditResult AddGroup(string title, params string[] nodeIds)
        {
            var ids = (nodeIds ?? new string[0]).Distinct().ToList();
            string problem = CheckGroupTitle(title);
            if (problem == null && ids.Count == 0) problem = "A group needs at least one block.";
            if (problem == null) problem = ids.Select(CheckGroupable).FirstOrDefault(p => p != null);
            if (problem == null && Graph.Editor.Groups.Count >= Limits.MaxEditorEntries) problem = TooManyEditorEntries("groups");
            if (problem == null && CountMembers() + ids.Count > Limits.MaxEditorEntries) problem = TooManyEditorEntries("blocks in groups");
            if (problem != null) return EditResult.Refused(problem);

            Graph.Editor.Groups.Add(new BlockGroup(title ?? "", ids.ToArray()));
            return Made(EditResult.Done());
        }

        public EditResult RenameGroup(int index, string title)
        {
            var group = GroupAt(index, out string problem);
            problem = problem ?? CheckGroupTitle(title);
            if (problem != null) return EditResult.Refused(problem);
            group.Title = title ?? "";
            return Made(EditResult.Done());
        }

        public EditResult SetGroupCollapsed(int index, bool collapsed)
        {
            var group = GroupAt(index, out string problem);
            if (problem != null) return EditResult.Refused(problem);
            group.Collapsed = collapsed;
            return Made(EditResult.Done());
        }

        /// <summary>Adds a block to a group. Refused when the block is already in a group.</summary>
        public EditResult AddToGroup(int index, string nodeId)
        {
            var group = GroupAt(index, out string problem);
            problem = problem ?? CheckGroupable(nodeId);
            if (problem == null && CountMembers() >= Limits.MaxEditorEntries) problem = TooManyEditorEntries("blocks in groups");
            if (problem != null) return EditResult.Refused(problem);
            group.Nodes.Add(nodeId);
            return Made(EditResult.Done());
        }

        /// <summary>Takes a block out of a group. A group left with no blocks is removed.</summary>
        public EditResult RemoveFromGroup(int index, string nodeId)
        {
            var group = GroupAt(index, out string problem);
            if (problem == null && !group.Nodes.Contains(nodeId)) problem = "The block '" + nodeId + "' is not in that group.";
            if (problem != null) return EditResult.Refused(problem);
            group.Nodes.Remove(nodeId);
            if (group.Nodes.Count == 0) Graph.Editor.Groups.RemoveAt(index);
            return Made(EditResult.Done());
        }

        /// <summary>Removes a group. Its blocks stay.</summary>
        public EditResult RemoveGroup(int index)
        {
            GroupAt(index, out string problem);
            if (problem != null) return EditResult.Refused(problem);
            Graph.Editor.Groups.RemoveAt(index);
            return Made(EditResult.Done());
        }

        // ---- Helpers ----

        private static string CheckFinite(params float[] values) =>
            values.Any(v => float.IsNaN(v) || float.IsInfinity(v)) ? "A position or size must be a finite number." : null;

        private string CheckCommentText(string text) =>
            text != null && text.Length > Limits.MaxEditorTextLength ? "A comment may be at most " + Limits.MaxEditorTextLength + " characters." : null;

        private string CheckGroupTitle(string title) =>
            title != null && title.Length > Limits.MaxNameLength ? "A group title may be at most " + Limits.MaxNameLength + " characters." : null;

        private string TooManyEditorEntries(string what) =>
            "The graph already has the most " + what + " it may have (" + Limits.MaxEditorEntries + ").";

        private int CountBends() => Graph.Editor.Wires.Where(w => w != null).Sum(w => w.Points.Count);

        private int CountMembers() => Graph.Editor.Groups.Where(g => g != null).Sum(g => g.Nodes.Count);

        private BlockCommentBox CommentAt(int index, out string problem)
        {
            var list = Graph.Editor.Comments;
            problem = index < 0 || index >= list.Count || list[index] == null ? "There is no comment box " + index + "." : null;
            return problem == null ? list[index] : null;
        }

        private BlockGroup GroupAt(int index, out string problem)
        {
            var list = Graph.Editor.Groups;
            problem = index < 0 || index >= list.Count || list[index] == null ? "There is no group " + index + "." : null;
            return problem == null ? list[index] : null;
        }

        // A block may be grouped when it is in the graph and not already in a group.
        private string CheckGroupable(string nodeId)
        {
            bool exists = Graph.Functions.Where(f => f != null).Any(f => FindNode(f, nodeId) != null);
            if (!exists) return "There is no block '" + nodeId + "' in the graph.";
            return Graph.Editor.Groups.Any(g => g != null && g.Nodes.Contains(nodeId)) ? "The block '" + nodeId + "' is in a group already." : null;
        }

        // The wire into an input pin must exist to have reroute points.
        private string CheckWire(string function, string nodeId, string pin)
        {
            var fn = FindFunction(function);
            if (fn == null) return NoFunction(function);
            var node = FindNode(fn, nodeId);
            if (node == null) return NoNode(function, nodeId);
            bool wired = node.Inputs.Any(i => i != null && i.Pin == pin && i.From != null);
            return wired ? null : "The pin '" + pin + "' has no wire from another block.";
        }

        private BlockWireBends BendsOf(string function, string nodeId, string pin, int index, out string problem)
        {
            problem = CheckWire(function, nodeId, pin);
            var bends = problem == null ? Graph.Editor.BendsOf(nodeId, pin) : null;
            if (problem == null && (bends == null || index < 0 || index >= bends.Points.Count))
                problem = "The wire has no reroute point " + index + ".";
            return problem == null ? bends : null;
        }
    }
}
