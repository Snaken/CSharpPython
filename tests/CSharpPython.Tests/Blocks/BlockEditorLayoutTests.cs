using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // The host-independent editing rules (docs/node-view-design.md, step 5a): collapse, comment boxes, reroute points and groups.
    public class BlockEditorLayoutTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        private static (BlockEditor editor, int[] changes) Editor()
        {
            var main = new BlockFunction("main") { Start = "a" };
            main.Add(N("a", "call", "print").Literal("arg0", "1").Then("next", "b"));
            main.Add(N("b", "call", "print").Connect("arg0", "v"));
            main.Add(N("v", "add").Literal("a", "1").Literal("b", "2"));
            var editor = new BlockEditor(new BlockGraph().AddFunction(main), new BlockLibrary());
            var changes = new int[1];
            editor.Changed += () => changes[0]++;
            return (editor, changes);
        }

        private static string Json(BlockEditor e) => e.Graph.ToJson();

        private static string Program(BlockEditor e) => e.Compile().Source;

        // ---- Collapse ----

        [Test]
        public void ABlockCanBeCollapsedAndOpened()
        {
            var (editor, changes) = Editor();
            editor.MoveNode("main", "a", 5, 6);
            Assert.That(editor.SetCollapsed("main", "a", true).Succeeded, Is.True);
            Assert.That(editor.Graph.Editor.LayoutOf("a").Collapsed, Is.True);
            Assert.That(editor.Graph.Editor.LayoutOf("a").X, Is.EqualTo(5f), "its place is kept");
            Assert.That(editor.SetCollapsed("main", "a", false).Succeeded, Is.True);
            Assert.That(editor.Graph.Editor.LayoutOf("a").Collapsed, Is.False);
            Assert.That(changes[0], Is.EqualTo(3));
        }

        [Test]
        public void CollapsingABlockWithNoPlaceGivesItOne()
        {
            var (editor, _) = Editor();
            Assert.That(editor.SetCollapsed("main", "b", true).Succeeded, Is.True);
            Assert.That(editor.Graph.Editor.LayoutOf("b").Collapsed, Is.True);
        }

        [Test]
        public void CollapseRefusesMissingFunctionsAndBlocks()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.SetCollapsed("nope", "a", true).Succeeded, Is.False);
            Assert.That(editor.SetCollapsed("main", "x", true).Succeeded, Is.False);
            Assert.That(changes[0], Is.EqualTo(0));
        }

        // ---- Comment boxes ----

        [Test]
        public void CommentBoxesCanBeAddedChangedMovedResizedAndRemoved()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.AddComment("note", 1, 2, 100, 50).Succeeded, Is.True);
            Assert.That(editor.AddComment(null, 0, 0, 10, 10).Succeeded, Is.True, "empty text is allowed");
            var comments = editor.Graph.Editor.Comments;
            Assert.That(comments.Select(c => c.Text), Is.EqualTo(new[] { "note", "" }));

            Assert.That(editor.SetCommentText(0, "better").Succeeded, Is.True);
            Assert.That(editor.MoveComment(0, 7, 8).Succeeded, Is.True);
            Assert.That(editor.ResizeComment(0, 200, 90).Succeeded, Is.True);
            Assert.That(comments[0].Text, Is.EqualTo("better"));
            Assert.That((comments[0].X, comments[0].Y, comments[0].Width, comments[0].Height), Is.EqualTo((7f, 8f, 200f, 90f)));

            Assert.That(editor.RemoveComment(0).Succeeded, Is.True);
            Assert.That(comments.Single().Text, Is.EqualTo(""), "the ones after move up");
            Assert.That(changes[0], Is.EqualTo(6));
        }

        [Test]
        public void CommentEditsRefuseBadRequestsAndChangeNothing()
        {
            var (editor, changes) = Editor();
            editor.AddComment("note", 1, 2, 100, 50);
            string before = Json(editor);
            int raised = changes[0];
            Assert.That(editor.SetCommentText(1, "x").Succeeded, Is.False);
            Assert.That(editor.SetCommentText(-1, "x").Succeeded, Is.False);
            Assert.That(editor.MoveComment(5, 0, 0).Succeeded, Is.False);
            Assert.That(editor.ResizeComment(5, 1, 1).Succeeded, Is.False);
            Assert.That(editor.RemoveComment(5).Succeeded, Is.False);
            Assert.That(editor.MoveComment(0, float.NaN, 0).Succeeded, Is.False);
            Assert.That(editor.ResizeComment(0, -1, 5).Succeeded, Is.False, "a negative size");
            Assert.That(editor.AddComment("x", float.PositiveInfinity, 0, 1, 1).Succeeded, Is.False);
            Assert.That(editor.AddComment("x", 0, 0, -1, 1).Succeeded, Is.False);
            editor.Limits = new BlockLimits { MaxEditorTextLength = 3 };
            Assert.That(editor.AddComment("long text", 0, 0, 1, 1).Succeeded, Is.False);
            Assert.That(editor.SetCommentText(0, "long text").Succeeded, Is.False);
            editor.Limits = new BlockLimits { MaxEditorEntries = 1 };
            Assert.That(editor.AddComment("one", 0, 0, 1, 1).Succeeded, Is.False, "the limit is reached");
            Assert.That(Json(editor), Is.EqualTo(before));
            Assert.That(changes[0], Is.EqualTo(raised));
        }

        // ---- Reroute points ----

        [Test]
        public void RerouteDotsAreAddedInOrderOnAWire()
        {
            var (editor, _) = Editor();
            Assert.That(editor.AddBend("main", "b", "arg0", 0, 10, 10).Succeeded, Is.True);
            Assert.That(editor.AddBend("main", "b", "arg0", 1, 30, 30).Succeeded, Is.True);
            Assert.That(editor.AddBend("main", "b", "arg0", 1, 20, 20).Succeeded, Is.True, "in the middle");
            Assert.That(editor.AddBend("main", "b", "arg0", 99, 40, 40).Succeeded, Is.True, "a large index adds at the end");
            var points = editor.Graph.Editor.BendsOf("b", "arg0").Points;
            Assert.That(points.Select(p => p.X), Is.EqualTo(new[] { 10f, 20f, 30f, 40f }));
        }

        [Test]
        public void ARerouteDotCanBeMovedAndRemovedAndTheLastRemovalForgetsTheWire()
        {
            var (editor, _) = Editor();
            editor.AddBend("main", "b", "arg0", 0, 10, 10);
            editor.AddBend("main", "b", "arg0", 1, 20, 20);
            Assert.That(editor.MoveBend("main", "b", "arg0", 1, 25, 26).Succeeded, Is.True);
            var points = editor.Graph.Editor.BendsOf("b", "arg0").Points;
            Assert.That((points[1].X, points[1].Y), Is.EqualTo((25f, 26f)));
            Assert.That(editor.RemoveBend("main", "b", "arg0", 0).Succeeded, Is.True);
            Assert.That(editor.Graph.Editor.BendsOf("b", "arg0").Points.Count, Is.EqualTo(1));
            Assert.That(editor.RemoveBend("main", "b", "arg0", 0).Succeeded, Is.True);
            Assert.That(editor.Graph.Editor.BendsOf("b", "arg0"), Is.Null);
            Assert.That(editor.Graph.Editor.Wires, Is.Empty);
        }

        [Test]
        public void RerouteEditsRefuseBadRequestsAndChangeNothing()
        {
            var (editor, changes) = Editor();
            editor.AddBend("main", "b", "arg0", 0, 1, 1);
            string before = Json(editor);
            int raised = changes[0];
            Assert.That(editor.AddBend("main", "a", "arg0", 0, 1, 1).Succeeded, Is.False, "a literal is not a wire");
            Assert.That(editor.AddBend("main", "b", "nope", 0, 1, 1).Succeeded, Is.False);
            Assert.That(editor.AddBend("main", "x", "arg0", 0, 1, 1).Succeeded, Is.False);
            Assert.That(editor.AddBend("nope", "b", "arg0", 0, 1, 1).Succeeded, Is.False);
            Assert.That(editor.AddBend("main", "b", "arg0", -1, 1, 1).Succeeded, Is.False);
            Assert.That(editor.AddBend("main", "b", "arg0", 0, float.NaN, 1).Succeeded, Is.False);
            Assert.That(editor.MoveBend("main", "b", "arg0", 3, 1, 1).Succeeded, Is.False);
            Assert.That(editor.MoveBend("main", "b", "arg0", 0, float.NaN, 1).Succeeded, Is.False);
            Assert.That(editor.MoveBend("main", "v", "a", 0, 1, 1).Succeeded, Is.False);
            Assert.That(editor.RemoveBend("main", "b", "arg0", -1).Succeeded, Is.False);
            Assert.That(editor.RemoveBend("main", "b", "arg0", 1).Succeeded, Is.False);
            editor.Limits = new BlockLimits { MaxEditorEntries = 1 };
            Assert.That(editor.AddBend("main", "b", "arg0", 0, 2, 2).Succeeded, Is.False, "the limit of points is reached");
            Assert.That(Json(editor), Is.EqualTo(before));
            Assert.That(changes[0], Is.EqualTo(raised));
        }

        [Test]
        public void RerouteDotsGoWhenTheirWireDoes()
        {
            var (editor, _) = Editor();
            editor.AddBend("main", "b", "arg0", 0, 1, 1);
            editor.Disconnect("main", "b", "arg0");
            Assert.That(editor.Graph.Editor.Wires, Is.Empty);
            editor.Connect("main", "b", "arg0", "v");
            editor.AddBend("main", "b", "arg0", 0, 1, 1);
            editor.RemoveNode("main", "v");
            Assert.That(editor.Graph.Editor.Wires, Is.Empty);
        }

        // ---- Groups ----

        [Test]
        public void ABlockCanBeGroupedRenamedCollapsedAndTheGroupRemoved()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.AddGroup("setup", "a", "b").Succeeded, Is.True);
            var group = editor.Graph.Editor.Groups.Single();
            Assert.That(group.Nodes, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(editor.RenameGroup(0, "start").Succeeded, Is.True);
            Assert.That(editor.SetGroupCollapsed(0, true).Succeeded, Is.True);
            Assert.That(group.Title, Is.EqualTo("start"));
            Assert.That(group.Collapsed, Is.True);
            Assert.That(editor.AddToGroup(0, "v").Succeeded, Is.True);
            Assert.That(group.Nodes, Is.EqualTo(new[] { "a", "b", "v" }));
            Assert.That(editor.RemoveFromGroup(0, "b").Succeeded, Is.True);
            Assert.That(group.Nodes, Is.EqualTo(new[] { "a", "v" }));
            Assert.That(editor.RemoveGroup(0).Succeeded, Is.True);
            Assert.That(editor.Graph.Editor.Groups, Is.Empty);
            Assert.That(editor.Graph.Functions[0].Nodes.Count, Is.EqualTo(3), "the blocks stay");
            Assert.That(changes[0], Is.EqualTo(6));
        }

        [Test]
        public void AGroupLeftEmptyIsRemoved()
        {
            var (editor, _) = Editor();
            editor.AddGroup("g", "a");
            Assert.That(editor.RemoveFromGroup(0, "a").Succeeded, Is.True);
            Assert.That(editor.Graph.Editor.Groups, Is.Empty);
        }

        [Test]
        public void ABlockIsInAtMostOneGroupAndDuplicatesAreMerged()
        {
            var (editor, _) = Editor();
            Assert.That(editor.AddGroup("g", "a", "a", "b").Succeeded, Is.True);
            Assert.That(editor.Graph.Editor.Groups[0].Nodes, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(editor.AddGroup("h", "b").Succeeded, Is.False);
            Assert.That(editor.AddGroup("h", "v").Succeeded, Is.True);
            Assert.That(editor.AddToGroup(1, "a").Succeeded, Is.False);
        }

        [Test]
        public void GroupEditsRefuseBadRequestsAndChangeNothing()
        {
            var (editor, changes) = Editor();
            editor.AddGroup("g", "a");
            string before = Json(editor);
            int raised = changes[0];
            Assert.That(editor.AddGroup("g").Succeeded, Is.False, "no blocks");
            Assert.That(editor.AddGroup("g", (string[])null).Succeeded, Is.False);
            Assert.That(editor.AddGroup("g", "x").Succeeded, Is.False);
            Assert.That(editor.RenameGroup(4, "x").Succeeded, Is.False);
            Assert.That(editor.SetGroupCollapsed(-1, true).Succeeded, Is.False);
            Assert.That(editor.AddToGroup(4, "b").Succeeded, Is.False);
            Assert.That(editor.AddToGroup(0, "x").Succeeded, Is.False);
            Assert.That(editor.RemoveFromGroup(0, "b").Succeeded, Is.False, "not a member");
            Assert.That(editor.RemoveFromGroup(4, "a").Succeeded, Is.False);
            Assert.That(editor.RemoveGroup(4).Succeeded, Is.False);
            editor.Limits = new BlockLimits { MaxNameLength = 2 };
            Assert.That(editor.AddGroup("long", "b").Succeeded, Is.False);
            Assert.That(editor.RenameGroup(0, "long").Succeeded, Is.False);
            editor.Limits = new BlockLimits { MaxEditorEntries = 1 };
            Assert.That(editor.AddGroup("h", "b").Succeeded, Is.False, "the limit of groups is reached");
            Assert.That(editor.AddToGroup(0, "b").Succeeded, Is.False, "the limit of members is reached");
            Assert.That(Json(editor), Is.EqualTo(before));
            Assert.That(changes[0], Is.EqualTo(raised));
        }

        [Test]
        public void GroupMembersGoWhenTheirBlockIsRemoved()
        {
            var (editor, _) = Editor();
            editor.AddGroup("g", "a", "b");
            editor.RemoveNode("main", "a");
            Assert.That(editor.Graph.Editor.Groups.Single().Nodes, Is.EqualTo(new[] { "b" }));
        }

        // ---- All of them ----

        [Test]
        public void NoneOfTheseEditsChangesTheProgram()
        {
            var (editor, _) = Editor();
            string before = Program(editor);
            editor.SetCollapsed("main", "a", true);
            editor.AddComment("hi", 1, 2, 3, 4);
            editor.AddBend("main", "b", "arg0", 0, 5, 5);
            editor.AddGroup("g", "a", "b");
            editor.SetGroupCollapsed(0, true);
            Assert.That(Program(editor), Is.EqualTo(before));
        }

        [Test]
        public void EveryOneCanBeUndoneAndRedoneAndSurvivesSaving()
        {
            var (editor, _) = Editor();
            string start = Json(editor);
            System.Func<EditResult>[] edits =
            {
                () => editor.SetCollapsed("main", "a", true),
                () => editor.AddComment("hi", 1, 2, 3, 4),
                () => editor.MoveComment(0, 9, 9),
                () => editor.AddBend("main", "b", "arg0", 0, 5, 5),
                () => editor.MoveBend("main", "b", "arg0", 0, 6, 6),
                () => editor.AddGroup("g", "a", "b"),
                () => editor.SetGroupCollapsed(0, true),
                () => editor.RemoveBend("main", "b", "arg0", 0),
                () => editor.RemoveGroup(0),
                () => editor.RemoveComment(0),
            };
            var states = new System.Collections.Generic.List<string> { start };
            foreach (var edit in edits)
            {
                Assert.That(edit().Succeeded, Is.True);
                states.Add(Json(editor));
            }
            for (int i = edits.Length - 1; i >= 0; i--)
            {
                Assert.That(editor.Undo().Succeeded, Is.True);
                Assert.That(Json(editor), Is.EqualTo(states[i]));
            }
            while (editor.Redo().Succeeded) { }
            Assert.That(Json(editor), Is.EqualTo(states[states.Count - 1]));

            var reloaded = BlockGraph.FromJson(Json(editor));
            Assert.That(reloaded.ToJson(), Is.EqualTo(Json(editor)));
        }

        [Test]
        public void ANonFinitePlaceIsRefusedWhenAddingOrMovingABlock()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.AddNode("main", "add", null, float.NaN, 0).Succeeded, Is.False);
            Assert.That(editor.MoveNode("main", "a", 0, float.PositiveInfinity).Succeeded, Is.False);
            Assert.That(editor.Graph.Functions[0].Nodes.Count, Is.EqualTo(3));
            Assert.That(editor.Graph.Editor.LayoutOf("a"), Is.Null);
            Assert.That(changes[0], Is.EqualTo(0));
        }
    }
}
