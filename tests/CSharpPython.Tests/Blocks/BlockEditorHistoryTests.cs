using System;
using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // The host-independent editing rules (docs/node-view-design.md, step 5a): undo, redo and groups of edits.
    public class BlockEditorHistoryTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        private static BlockGraph Graph()
        {
            var main = new BlockFunction("main") { Start = "a" };
            main.Add(N("a", "call", "print").Literal("arg0", "1").Then("next", "b"));
            main.Add(N("b", "call", "print").Connect("arg0", "v"));
            main.Add(N("v", "add").Literal("a", "1").Literal("b", "2"));
            var graph = new BlockGraph().AddVariable("score", "0", "int").AddFunction(main);
            graph.Editor.Place("a", 1, 2);
            graph.Editor.Groups.Add(new BlockGroup("g", "a", "b"));
            return graph;
        }

        private static (BlockEditor editor, int[] changes) Editor()
        {
            var editor = new BlockEditor(Graph(), new BlockLibrary());
            var changes = new int[1];
            editor.Changed += () => changes[0]++;
            return (editor, changes);
        }

        private static string Json(BlockEditor e) => e.Graph.ToJson();

        [Test]
        public void UndoGoesBackOneEditAndRedoForwardAgain()
        {
            var (editor, changes) = Editor();
            string start = Json(editor);
            editor.AddNode("main", "set", "x");
            string added = Json(editor);
            editor.RemoveNode("main", "v");
            string removed = Json(editor);
            Assert.That(editor.CanUndo, Is.True);
            Assert.That(editor.CanRedo, Is.False);

            Assert.That(editor.Undo().Succeeded, Is.True);
            Assert.That(Json(editor), Is.EqualTo(added));
            Assert.That(editor.Undo().Succeeded, Is.True);
            Assert.That(Json(editor), Is.EqualTo(start));
            Assert.That(editor.CanUndo, Is.False);
            Assert.That(editor.CanRedo, Is.True);

            Assert.That(editor.Redo().Succeeded, Is.True);
            Assert.That(Json(editor), Is.EqualTo(added));
            Assert.That(editor.Redo().Succeeded, Is.True);
            Assert.That(Json(editor), Is.EqualTo(removed));
            Assert.That(changes[0], Is.EqualTo(6), "two edits, two undos and two redos");
        }

        [Test]
        public void EveryKindOfEditCanBeUndone()
        {
            var (editor, _) = Editor();
            string start = Json(editor);
            Func<EditResult>[] edits =
            {
                () => editor.AddNode("main", "set", "x", 4, 5),
                () => editor.MoveNode("main", "a", 50, 60),
                () => editor.SetName("main", "b", "print"),
                () => editor.Connect("main", "a", "arg0", "v"),
                () => editor.SetLiteral("main", "b", "arg0", "9"),
                () => editor.Disconnect("main", "b", "arg0"),
                () => editor.Link("main", "b", "next", "n1"),
                () => editor.Unlink("main", "a", "next"),
                () => editor.SetStart("main", "b"),
                () => editor.RemoveNode("main", "n1"),
                () => editor.AddFunction("other", "p"),
                () => editor.AddParameter("main", "q"),
                () => editor.RenameParameter("main", "q", "r"),
                () => editor.RemoveParameter("main", "r"),
                () => editor.RenameFunction("other", "second"),
                () => editor.RemoveFunction("second"),
                () => editor.AddVariable("health", "1", "int"),
                () => editor.RenameVariable("health", "hp"),
                () => editor.SetVariableType("hp", "float"),
                () => editor.SetVariableInitial("hp", "2.0"),
                () => editor.RemoveVariable("hp"),
            };
            var states = new System.Collections.Generic.List<string> { start };
            for (int i = 0; i < edits.Length; i++)
            {
                var made = edits[i]();
                Assert.That(made.Succeeded, Is.True, "edit " + i + ": " + made.Reason);
                states.Add(Json(editor));
            }
            for (int i = edits.Length - 1; i >= 0; i--)
            {
                Assert.That(editor.Undo().Succeeded, Is.True, "undo " + i);
                Assert.That(Json(editor), Is.EqualTo(states[i]), "after undoing edit " + i);
            }
            for (int i = 0; i < edits.Length; i++)
            {
                Assert.That(editor.Redo().Succeeded, Is.True, "redo " + i);
                Assert.That(Json(editor), Is.EqualTo(states[i + 1]), "after redoing edit " + i);
            }
        }

        [Test]
        public void ARefusedEditIsNotAStep()
        {
            var (editor, _) = Editor();
            editor.AddNode("main", "no-such-block");
            editor.RemoveNode("main", "nope");
            Assert.That(editor.CanUndo, Is.False);
            Assert.That(editor.Undo().Succeeded, Is.False);
            Assert.That(editor.Undo().Reason, Does.Contain("nothing to undo"));
            Assert.That(editor.Redo().Reason, Does.Contain("nothing to redo"));
        }

        [Test]
        public void AnEditAfterAnUndoClearsRedo()
        {
            var (editor, _) = Editor();
            editor.AddNode("main", "set", "x");
            editor.Undo();
            Assert.That(editor.CanRedo, Is.True);
            editor.AddNode("main", "set", "y");
            Assert.That(editor.CanRedo, Is.False);
            Assert.That(editor.Redo().Succeeded, Is.False);
        }

        [Test]
        public void AGroupOfEditsIsOneStep()
        {
            var (editor, changes) = Editor();
            string start = Json(editor);
            editor.BeginGroup();
            for (int i = 1; i <= 5; i++) editor.MoveNode("main", "a", i * 10, i * 10);
            Assert.That(editor.CanUndo, Is.False, "not until the group ends");
            editor.EndGroup();
            Assert.That(changes[0], Is.EqualTo(5), "the view was told about each move");
            Assert.That(editor.Graph.Editor.LayoutOf("a").X, Is.EqualTo(50f));

            Assert.That(editor.Undo().Succeeded, Is.True);
            Assert.That(Json(editor), Is.EqualTo(start));
            Assert.That(editor.CanUndo, Is.False, "the whole drag was one step");
        }

        [Test]
        public void GroupsNestAndTheOutermostCounts()
        {
            var (editor, _) = Editor();
            editor.BeginGroup();
            editor.AddNode("main", "set", "x");
            editor.BeginGroup();
            editor.AddNode("main", "set", "y");
            editor.EndGroup();
            Assert.That(editor.CanUndo, Is.False);
            editor.EndGroup();
            Assert.That(editor.Undo().Succeeded, Is.True);
            Assert.That(editor.CanUndo, Is.False);
            Assert.That(editor.Graph.Functions[0].Nodes.Count, Is.EqualTo(3));
        }

        [Test]
        public void AGroupWithNoEditsIsNotAStepAndAnExtraEndGroupDoesNothing()
        {
            var (editor, _) = Editor();
            editor.BeginGroup();
            editor.RemoveNode("main", "nope");
            editor.EndGroup();
            editor.EndGroup();
            Assert.That(editor.CanUndo, Is.False);
            editor.AddNode("main", "set", "x");
            Assert.That(editor.CanUndo, Is.True, "grouping still works afterwards");
        }

        [Test]
        public void UndoAndRedoAreRefusedWhileAGroupIsOpen()
        {
            var (editor, _) = Editor();
            editor.AddNode("main", "set", "x");
            editor.BeginGroup();
            Assert.That(editor.CanUndo, Is.False);
            Assert.That(editor.Undo().Succeeded, Is.False);
            Assert.That(editor.Redo().Succeeded, Is.False);
            editor.EndGroup();
            Assert.That(editor.Undo().Succeeded, Is.True);
        }

        [Test]
        public void TheOldestStepsAreDroppedPastTheMaximum()
        {
            var (editor, _) = Editor();
            editor.MaxUndoSteps = 3;
            for (int i = 0; i < 5; i++) editor.AddNode("main", "set", "x" + i);
            int undone = 0;
            while (editor.Undo().Succeeded) undone++;
            Assert.That(undone, Is.EqualTo(3));
            Assert.That(editor.Graph.Functions[0].Nodes.Count, Is.EqualTo(5), "two of the five adds can no longer be undone");
        }

        [Test]
        public void ZeroStepsTurnsUndoOff()
        {
            var (editor, _) = Editor();
            editor.MaxUndoSteps = 0;
            editor.AddNode("main", "set", "x");
            Assert.That(editor.CanUndo, Is.False);
            Assert.That(() => editor.MaxUndoSteps = -1, Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void LoweringTheMaximumTrimsWhatIsKept()
        {
            var (editor, _) = Editor();
            for (int i = 0; i < 4; i++) editor.AddNode("main", "set", "x" + i);
            editor.Undo();
            editor.MaxUndoSteps = 1;
            Assert.That(editor.Undo().Succeeded, Is.True);
            Assert.That(editor.Undo().Succeeded, Is.False);
            Assert.That(editor.Redo().Succeeded, Is.True);
            Assert.That(editor.Redo().Succeeded, Is.False, "only one step of redo was kept");
        }

        [Test]
        public void ClearHistoryForgetsEverythingButKeepsTheGraph()
        {
            var (editor, _) = Editor();
            editor.AddNode("main", "set", "x");
            editor.Undo();
            editor.AddNode("main", "set", "y");
            string kept = Json(editor);
            editor.ClearHistory();
            Assert.That(editor.CanUndo, Is.False);
            Assert.That(editor.CanRedo, Is.False);
            Assert.That(Json(editor), Is.EqualTo(kept));
            editor.AddNode("main", "set", "z");
            editor.Undo();
            Assert.That(Json(editor), Is.EqualTo(kept), "the first step after clearing goes back to the cleared state");
        }

        [Test]
        public void UndoKeepsTheGraphObjectAndItsLists()
        {
            var (editor, _) = Editor();
            var graph = editor.Graph;
            var functions = graph.Functions;
            var layouts = graph.Editor.Nodes;
            editor.AddNode("main", "set", "x");
            editor.Undo();
            Assert.That(editor.Graph, Is.SameAs(graph));
            Assert.That(graph.Functions, Is.SameAs(functions));
            Assert.That(graph.Editor.Nodes, Is.SameAs(layouts));
        }

        [Test]
        public void ARestoredGraphStillCompiles()
        {
            var (editor, _) = Editor();
            editor.AddNode("main", "set", "x");
            editor.RemoveNode("main", "b");
            editor.Undo();
            editor.Undo();
            var result = BlockCompiler.Compile(editor.Graph, editor.Library);
            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Diagnostics));
        }

        [Test]
        public void AnIdIsFreeAgainOnceItsBlockIsUndone()
        {
            var (editor, _) = Editor();
            string first = editor.AddNode("main", "set", "x").Node.Id;
            editor.Undo();
            string second = editor.AddNode("main", "set", "y").Node.Id;
            Assert.That(second, Is.EqualTo(first), "the id is free again once the block is gone");
            editor.Undo();
            Assert.That(editor.Graph.Functions[0].Nodes.Count(n => n.Id == first), Is.EqualTo(0));
        }
    }
}
