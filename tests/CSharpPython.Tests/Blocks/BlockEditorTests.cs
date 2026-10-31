using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // The host-independent editing rules (docs/node-view-design.md, step 5a): add, remove, name and move blocks.
    public class BlockEditorTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        private static BlockLibrary Library()
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m => m.Native("say", (c, a) => Value.None, 1, 1).Describe("say", "text: str"));
            return new BlockLibrary().AddAllModules(vm);
        }

        private static BlockGraph Graph()
        {
            var main = new BlockFunction("main") { Start = "a" };
            main.Add(N("a", "call", "print").Literal("arg0", "1").Then("b"));
            main.Add(N("b", "call", "print").Connect("arg0", "v").Then("c"));
            main.Add(N("v", "add").Literal("a", "1").Literal("b", "2"));
            main.Add(N("c", "call", "print").Connect("arg0", "v"));
            var other = new BlockFunction("other") { Start = "o" };
            other.Add(N("o", "comment", "hi"));
            return new BlockGraph().AddFunction(main).AddFunction(other);
        }

        private static (BlockEditor editor, int[] changes) Editor(BlockGraph g = null)
        {
            var editor = new BlockEditor(g ?? Graph(), Library());
            var changes = new int[1];
            editor.Changed += () => changes[0]++;
            return (editor, changes);
        }

        private static BlockNode Find(BlockGraph g, string id) => g.Functions.SelectMany(f => f.Nodes).FirstOrDefault(n => n.Id == id);

        // ---- Adding ----

        [Test]
        public void AddingABlockPutsItInTheFunctionAtAPlace()
        {
            var (editor, changes) = Editor();
            var result = editor.AddNode("main", "set", "score", 12.5f, -3f);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Reason, Is.Null);
            Assert.That(result.Node.Block, Is.EqualTo("set"));
            Assert.That(result.Node.Name, Is.EqualTo("score"));
            Assert.That(editor.Graph.Functions[0].Nodes, Does.Contain(result.Node));
            var place = editor.Graph.Editor.LayoutOf(result.Node.Id);
            Assert.That((place.X, place.Y), Is.EqualTo((12.5f, -3f)));
            Assert.That(changes[0], Is.EqualTo(1));
        }

        [Test]
        public void NewIdsAreUniqueAcrossTheWholeGraph()
        {
            var g = Graph();
            g.Functions[1].Add(N("n7", "comment", "x"));
            var (editor, _) = Editor(g);
            var first = editor.AddNode("main", "break").Node.Id;
            var second = editor.AddNode("other", "break").Node.Id;
            Assert.That(first, Is.EqualTo("n8"));
            Assert.That(second, Is.EqualTo("n9"));
            Assert.That(g.Functions.SelectMany(f => f.Nodes).Select(n => n.Id).Distinct().Count(), Is.EqualTo(g.Functions.Sum(f => f.Nodes.Count)));
        }

        [Test]
        public void AnIdOfAnotherShapeDoesNotConfuseTheNumbering()
        {
            var g = Graph();
            g.Functions[1].Add(N("nope", "comment", "x"));
            g.Functions[1].Add(N("n", "comment", "x"));
            g.Functions[1].Add(N("n-5", "comment", "x"));
            var (editor, _) = Editor(g);
            Assert.That(editor.AddNode("main", "break").Node.Id, Is.EqualTo("n1"));
        }

        [Test]
        public void ACallNeedsAFunctionTheLibraryOrTheGraphHas()
        {
            var (editor, _) = Editor();
            Assert.That(editor.AddNode("main", "call", "game.say").Succeeded, Is.True);
            Assert.That(editor.AddNode("main", "call", "len").Succeeded, Is.True);
            Assert.That(editor.AddNode("main", "call", "other").Succeeded, Is.True, "a function of the graph");
            var missing = editor.AddNode("main", "call", "nope");
            Assert.That(missing.Succeeded, Is.False);
            Assert.That(missing.Reason, Does.Contain("no function 'nope'"));
            Assert.That(editor.AddNode("main", "call").Reason, Does.Contain("needs the name"));
            Assert.That(editor.AddNode("main", "call", "").Succeeded, Is.False);
        }

        [Test]
        public void ABlockThatNeedsANameCanBeAddedBeforeItIsTyped()
        {
            var (editor, _) = Editor();
            var node = editor.AddNode("main", "get").Node;
            Assert.That(node.Name, Is.Null);
            Assert.That(editor.SetName("main", node.Id, "score").Succeeded, Is.True);
            Assert.That(node.Name, Is.EqualTo("score"));
        }

        [Test]
        public void ARefusedAddChangesNothingAndRaisesNothing()
        {
            var (editor, changes) = Editor();
            string before = editor.Graph.ToJson();
            Assert.That(editor.AddNode("nope", "set").Succeeded, Is.False);
            Assert.That(editor.AddNode(null, "set").Succeeded, Is.False);
            Assert.That(editor.AddNode("main", "frobnicate").Reason, Does.Contain("no block type 'frobnicate'"));
            Assert.That(editor.AddNode("main", null).Reason, Does.Contain("needs a type"));
            Assert.That(editor.AddNode("main", "call", "nope").Succeeded, Is.False);
            Assert.That(editor.Graph.ToJson(), Is.EqualTo(before));
            Assert.That(changes[0], Is.EqualTo(0));
        }

        [Test]
        public void TheLimitsStopTheGraphGrowing()
        {
            var (editor, _) = Editor();
            editor.Limits = new BlockLimits { MaxNodesPerFunction = 4 };
            var refused = editor.AddNode("main", "break");
            Assert.That(refused.Succeeded, Is.False);
            Assert.That(refused.Reason, Does.Contain("most blocks"));
            Assert.That(editor.AddNode("other", "break").Succeeded, Is.True, "the other function has room");
            editor.Limits = new BlockLimits { MaxNodes = 6 };
            Assert.That(editor.AddNode("other", "break").Reason, Does.Contain("The graph already has the most blocks"));
        }

        // ---- Removing ----

        [Test]
        public void RemovingABlockTakesItsWiresAndLinksWithIt()
        {
            var (editor, changes) = Editor();
            var result = editor.RemoveNode("main", "v");
            Assert.That(result.Succeeded, Is.True);
            Assert.That(Find(editor.Graph, "v"), Is.Null);
            Assert.That(Find(editor.Graph, "b").Inputs, Is.Empty, "the wire from v is gone");
            Assert.That(Find(editor.Graph, "c").Inputs, Is.Empty);
            Assert.That(Find(editor.Graph, "a").Inputs.Single().Literal, Is.EqualTo("1"), "typed-in values stay");
            Assert.That(changes[0], Is.EqualTo(1));

            editor.RemoveNode("main", "b");
            Assert.That(Find(editor.Graph, "a").Flow, Is.Empty, "the link to b is gone");
            Assert.That(Find(editor.Graph, "c"), Is.Not.Null, "what b linked to stays");
        }

        [Test]
        public void RemovingTheStartBlockClearsTheStart()
        {
            var (editor, _) = Editor();
            editor.RemoveNode("main", "a");
            Assert.That(editor.Graph.Functions[0].Start, Is.Null);
            editor.RemoveNode("main", "c");
            Assert.That(editor.Graph.Functions[0].Start, Is.Null);
        }

        [Test]
        public void RemovingABlockCleansTheEditorSection()
        {
            var g = Graph();
            g.Editor.Place("v", 1, 1);
            g.Editor.Place("b", 2, 2);
            var bends = new BlockWireBends("b", "arg0");
            bends.Points.Add(new BlockPoint(5, 5));
            g.Editor.Wires.Add(bends);
            g.Editor.Wires.Add(new BlockWireBends("c", "arg0"));
            g.Editor.Wires.Add(new BlockWireBends("a", "arg0"));
            g.Editor.Groups.Add(new BlockGroup("g", "v", "a"));
            g.Editor.Groups.Add(new BlockGroup("alone", "v"));
            var (editor, _) = Editor(g);
            editor.RemoveNode("main", "v");
            Assert.That(g.Editor.LayoutOf("v"), Is.Null);
            Assert.That(g.Editor.LayoutOf("b"), Is.Not.Null);
            Assert.That(g.Editor.BendsOf("b", "arg0"), Is.Null, "its wire went with it");
            Assert.That(g.Editor.BendsOf("c", "arg0"), Is.Null);
            Assert.That(g.Editor.BendsOf("a", "arg0"), Is.Not.Null, "a wire that did not touch v stays");
            Assert.That(g.Editor.Groups.Select(x => x.Title), Is.EqualTo(new[] { "g" }), "a group left empty goes");
            Assert.That(g.Editor.Groups[0].Nodes, Is.EqualTo(new[] { "a" }));
        }

        [Test]
        public void RemovingABlockWithItsOwnWiresDropsTheirBends()
        {
            var g = Graph();
            g.Editor.Wires.Add(new BlockWireBends("c", "arg0"));
            var (editor, _) = Editor(g);
            editor.RemoveNode("main", "c");
            Assert.That(g.Editor.BendsOf("c", "arg0"), Is.Null);
        }

        [Test]
        public void ARefusedRemoveChangesNothing()
        {
            var (editor, changes) = Editor();
            string before = editor.Graph.ToJson();
            Assert.That(editor.RemoveNode("nope", "a").Reason, Does.Contain("no function"));
            Assert.That(editor.RemoveNode("main", "zzz").Reason, Does.Contain("no block 'zzz'"));
            Assert.That(editor.RemoveNode("main", "o").Succeeded, Is.False, "o is in another function");
            Assert.That(editor.RemoveNode("main", null).Succeeded, Is.False);
            Assert.That(editor.Graph.ToJson(), Is.EqualTo(before));
            Assert.That(changes[0], Is.EqualTo(0));
        }

        [Test]
        public void TheGraphStillCompilesAfterABlockIsRemoved()
        {
            var (editor, _) = Editor();
            editor.RemoveNode("main", "v");
            var r = BlockCompiler.Compile(editor.Graph, editor.Library);
            Assert.That(r.Succeeded, Is.False, "b and c now have empty inputs");
            Assert.That(r.Errors.Select(e => e.NodeId), Is.EquivalentTo(new[] { "b", "c" }));
        }

        // ---- Names and places ----

        [Test]
        public void SetNameChecksACallAndFreesTheRest()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.SetName("main", "a", "game.say").Succeeded, Is.True);
            Assert.That(Find(editor.Graph, "a").Name, Is.EqualTo("game.say"));
            Assert.That(editor.SetName("main", "a", "nope").Succeeded, Is.False);
            Assert.That(Find(editor.Graph, "a").Name, Is.EqualTo("game.say"), "a refused name changes nothing");
            Assert.That(editor.SetName("other", "o", "a new comment").Succeeded, Is.True);
            Assert.That(Find(editor.Graph, "o").Name, Is.EqualTo("a new comment"));
            Assert.That(changes[0], Is.EqualTo(2));
        }

        [Test]
        public void ABlockWithNoNameCannotBeNamed()
        {
            var (editor, _) = Editor();
            Assert.That(editor.SetName("main", "v", "x").Reason, Does.Contain("no name to set"));
            Assert.That(editor.SetName("main", "zzz", "x").Succeeded, Is.False);
            Assert.That(editor.SetName("nope", "a", "x").Succeeded, Is.False);
            var repeat = editor.AddNode("main", "repeat").Node;
            Assert.That(editor.SetName("main", repeat.Id, "i").Succeeded, Is.True, "a repeat may name its counter");
        }

        [Test]
        public void MovingABlockRecordsItsPlace()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.MoveNode("main", "a", 40, 50).Succeeded, Is.True);
            Assert.That(editor.Graph.Editor.LayoutOf("a").X, Is.EqualTo(40f));
            Assert.That(editor.MoveNode("main", "a", 41, 51).Succeeded, Is.True);
            Assert.That(editor.Graph.Editor.Nodes.Count(n => n.Id == "a"), Is.EqualTo(1));
            Assert.That(changes[0], Is.EqualTo(2));
            Assert.That(editor.MoveNode("main", "zzz", 0, 0).Succeeded, Is.False);
            Assert.That(editor.MoveNode("nope", "a", 0, 0).Succeeded, Is.False);
        }

        [Test]
        public void MovingABlockNeverChangesTheProgram()
        {
            var (editor, _) = Editor();
            string before = BlockCompiler.Compile(editor.Graph, editor.Library).Source;
            editor.MoveNode("main", "a", 99, 99);
            Assert.That(BlockCompiler.Compile(editor.Graph, editor.Library).Source, Is.EqualTo(before));
        }

        [Test]
        public void TheConstructorChecksItsArguments()
        {
            Assert.Throws<System.ArgumentNullException>(() => new BlockEditor(null));
            Assert.That(new BlockEditor(new BlockGraph()).Library, Is.Not.Null, "the default library is used");
        }
    }
}
