using System;
using System.Collections.Generic;

namespace CSharpPython.Blocks
{
    public sealed partial class BlockEditor
    {
        // Each step is the whole graph as JSON: simple, and it cannot miss a part of the graph an edit touched.
        private readonly List<string> _undo = new List<string>();
        private readonly List<string> _redo = new List<string>();
        private string _current;
        private int _groupDepth;
        private bool _groupDirty;
        private int _maxUndoSteps = 100;

        /// <summary>How many steps <see cref="Undo"/> can go back. The oldest steps are dropped past this. Zero turns undo off.</summary>
        public int MaxUndoSteps
        {
            get => _maxUndoSteps;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "The number of undo steps cannot be negative.");
                _maxUndoSteps = value;
                Trim(_undo);
                Trim(_redo);
            }
        }

        public bool CanUndo => _groupDepth == 0 && _undo.Count > 0;

        public bool CanRedo => _groupDepth == 0 && _redo.Count > 0;

        /// <summary>
        /// Starts a group: every edit made until the matching <see cref="EndGroup"/> is one step of undo. Use it for a
        /// drag (many <see cref="MoveNode"/> calls) or for an action that makes several edits. Groups may be nested; the
        /// outermost one counts. <see cref="Changed"/> is still raised for each edit, so the view can follow along.
        /// </summary>
        public void BeginGroup()
        {
            if (_groupDepth++ == 0) _groupDirty = false;
        }

        /// <summary>Ends a group. If any edit was made in it, the whole group becomes one step. A call with no group open does nothing.</summary>
        public void EndGroup()
        {
            if (_groupDepth == 0) return;
            if (--_groupDepth > 0) return;
            if (_groupDirty) Record();
            _groupDirty = false;
        }

        /// <summary>
        /// Goes back one step. The graph object is the same but its contents are replaced, so a block or function
        /// object read earlier is out of date: read it again. <see cref="Changed"/> is raised once. Refused when there
        /// is nothing to undo, or while a group is open.
        /// </summary>
        public EditResult Undo() => Step(_undo, _redo);

        /// <summary>Goes forward one step that <see cref="Undo"/> went back over. A new edit clears what could be redone.</summary>
        public EditResult Redo() => Step(_redo, _undo);

        /// <summary>Forgets all steps, for example after the graph is saved or loaded.</summary>
        public void ClearHistory()
        {
            _undo.Clear();
            _redo.Clear();
            _current = Snapshot();
            _groupDirty = false;
        }

        // ---- Helpers ----

        // Called by every edit that was made, before Changed is raised.
        private void RecordEdit()
        {
            if (_groupDepth > 0) _groupDirty = true;
            else Record();
        }

        private void Record()
        {
            if (_maxUndoSteps > 0)
            {
                _undo.Add(_current);
                Trim(_undo);
            }
            _redo.Clear();
            _current = Snapshot();
        }

        private EditResult Step(List<string> from, List<string> to)
        {
            if (_groupDepth > 0) return EditResult.Refused("Finish the group of edits first.");
            if (from.Count == 0) return EditResult.Refused(ReferenceEquals(from, _undo) ? "There is nothing to undo." : "There is nothing to redo.");

            string target = from[from.Count - 1];
            BlockGraph restored;
            try
            {
                restored = BlockGraph.FromJson(target, Unlimited());
            }
            catch (BlockGraphFormatException e)
            {
                return EditResult.Refused("The step could not be restored: " + e.Message);
            }

            from.RemoveAt(from.Count - 1);
            to.Add(_current);
            Trim(to);
            _current = target;
            Graph.Version = restored.Version;
            Replace(Graph.Variables, restored.Variables);
            Replace(Graph.Functions, restored.Functions);
            Replace(Graph.Editor.Nodes, restored.Editor.Nodes);
            Replace(Graph.Editor.Comments, restored.Editor.Comments);
            Replace(Graph.Editor.Wires, restored.Editor.Wires);
            Replace(Graph.Editor.Groups, restored.Editor.Groups);
            Changed?.Invoke();
            return EditResult.Done();
        }

        private string Snapshot() => Graph.ToJson(indent: false);

        private void Trim(List<string> steps)
        {
            if (steps.Count > _maxUndoSteps) steps.RemoveRange(0, steps.Count - _maxUndoSteps);
        }

        private static void Replace<T>(List<T> list, List<T> with)
        {
            list.Clear();
            list.AddRange(with);
        }

        // A step was written by this editor from a graph it already held, so it is not checked against limits again.
        private static BlockLimits Unlimited() => new BlockLimits
        {
            MaxTextLength = int.MaxValue, MaxFunctions = int.MaxValue, MaxNodesPerFunction = int.MaxValue, MaxNodes = int.MaxValue,
            MaxVariables = int.MaxValue, MaxParameters = int.MaxValue, MaxInputsPerNode = int.MaxValue, MaxLinksPerNode = int.MaxValue,
            MaxIdLength = int.MaxValue, MaxNameLength = int.MaxValue, MaxPinLength = int.MaxValue, MaxCommentLength = int.MaxValue,
            MaxLiteralLength = int.MaxValue, MaxTypeLength = int.MaxValue, MaxEditorEntries = int.MaxValue, MaxEditorTextLength = int.MaxValue,
        };
    }
}
