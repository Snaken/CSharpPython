using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // Editor-only data lives in the same file and never reaches the compiler (docs/node-view-design.md, decision 17).
    public class BlockEditorDataTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        private static BlockGraph Graph()
        {
            var f = new BlockFunction("main") { Start = "a" };
            f.Add(N("a", "call", "print").Literal("arg0", "1").Then("b"));
            f.Add(N("b", "call", "print").Connect("arg0", "v"));
            f.Add(N("v", "add").Literal("a", "1").Literal("b", "2"));
            return new BlockGraph().AddFunction(f);
        }

        private static BlockGraph Decorated()
        {
            var g = Graph();
            g.Editor.Place("a", 10.5f, -20f);
            g.Editor.Place("b", 300f, 40.25f).Collapsed = true;
            g.Editor.Comments.Add(new BlockCommentBox("start here\n\"quoted\"", 0, -50, 200, 80));
            var bends = new BlockWireBends("b", "arg0");
            bends.Points.Add(new BlockPoint(150, 10));
            bends.Points.Add(new BlockPoint(160, 20));
            g.Editor.Wires.Add(bends);
            g.Editor.Groups.Add(new BlockGroup("setup", "a", "b") { Collapsed = true });
            return g;
        }

        // ---- Saving ----

        [Test]
        public void AGraphWithoutEditorDataHasNoEditorSection()
        {
            Assert.That(Graph().Editor.IsEmpty, Is.True);
            Assert.That(Graph().ToJson(), Does.Not.Contain("Editor"));
            Assert.That(BlockGraph.FromJson(Graph().ToJson()).Editor.IsEmpty, Is.True);
        }

        [Test]
        public void EditorDataSurvivesASaveAndLoad()
        {
            var back = BlockGraph.FromJson(Decorated().ToJson()).Editor;
            Assert.That(back.LayoutOf("a").X, Is.EqualTo(10.5f));
            Assert.That(back.LayoutOf("a").Y, Is.EqualTo(-20f));
            Assert.That(back.LayoutOf("a").Collapsed, Is.False);
            Assert.That(back.LayoutOf("b").Y, Is.EqualTo(40.25f));
            Assert.That(back.LayoutOf("b").Collapsed, Is.True);
            Assert.That(back.Comments.Single().Text, Is.EqualTo("start here\n\"quoted\""));
            Assert.That(back.Comments.Single().Width, Is.EqualTo(200f));
            Assert.That(back.BendsOf("b", "arg0").Points.Select(p => p.X), Is.EqualTo(new[] { 150f, 160f }));
            Assert.That(back.Groups.Single().Title, Is.EqualTo("setup"));
            Assert.That(back.Groups.Single().Nodes, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(back.Groups.Single().Collapsed, Is.True);
        }

        [Test]
        public void ASavedFileIsStableAndCompact()
        {
            var json = Decorated().ToJson(indent: false);
            Assert.That(BlockGraph.FromJson(json).ToJson(indent: false), Is.EqualTo(json));
        }

        [Test]
        public void LoadingReadsIntsFloatsAndLeavesOutMissingFields()
        {
            var g = BlockGraph.FromJson("{\"Editor\":{\"Nodes\":[{\"Id\":\"a\",\"X\":3,\"Y\":1e2}],\"Extra\":1}}");
            Assert.That(g.Editor.LayoutOf("a").X, Is.EqualTo(3f));
            Assert.That(g.Editor.LayoutOf("a").Y, Is.EqualTo(100f));
            Assert.That(g.Editor.LayoutOf("a").Collapsed, Is.False);
            Assert.That(g.Editor.Comments, Is.Empty);
        }

        [Test]
        public void ANumberThatIsNotFiniteIsWrittenAsZero()
        {
            var g = Graph();
            g.Editor.Place("a", float.NaN, float.PositiveInfinity);
            var back = BlockGraph.FromJson(g.ToJson()).Editor.LayoutOf("a");
            Assert.That(back.X, Is.EqualTo(0f));
            Assert.That(back.Y, Is.EqualTo(0f));
            Assert.That(BlockGraph.FromJson("{\"Editor\":{\"Nodes\":[{\"Id\":\"a\",\"X\":1e999}]}}").Editor.LayoutOf("a").X, Is.EqualTo(0f));
        }

        [TestCase("{\"Editor\":3}")]
        [TestCase("{\"Editor\":{\"Nodes\":[1]}}")]
        [TestCase("{\"Editor\":{\"Nodes\":[{\"X\":\"a\"}]}}")]
        [TestCase("{\"Editor\":{\"Nodes\":[{\"Collapsed\":1}]}}")]
        [TestCase("{\"Editor\":{\"Groups\":[{\"Nodes\":[1]}]}}")]
        [TestCase("{\"Editor\":{\"Wires\":[{\"Points\":[2]}]}}")]
        public void ABadEditorSectionIsRejected(string json)
        {
            Assert.Throws<BlockGraphFormatException>(() => BlockGraph.FromJson(json));
        }

        // ---- The compiler ignores it ----

        [Test]
        public void EditorDataNeverChangesTheCompiledProgram()
        {
            var plain = BlockCompiler.Compile(Graph());
            var moved = BlockCompiler.Compile(Decorated());
            Assert.That(moved.Succeeded, Is.True);
            Assert.That(moved.Source, Is.EqualTo(plain.Source));
            Assert.That(Enumerable.Range(1, plain.Map.LineCount).Select(plain.Map.NodeAt),
                Is.EqualTo(Enumerable.Range(1, moved.Map.LineCount).Select(moved.Map.NodeAt)));
            var again = Decorated();
            again.Editor.Place("a", 999, 999);
            Assert.That(BlockCompiler.Compile(again).Source, Is.EqualTo(plain.Source), "moving a block");
        }

        [Test]
        public void EditorDataAboutMissingBlocksDoesNotStopACompile()
        {
            var g = Graph();
            g.Editor.Place("ghost", 1, 1);
            g.Editor.Groups.Add(new BlockGroup("g", "ghost"));
            Assert.That(BlockCompiler.Compile(g).Succeeded, Is.True);
        }

        // ---- Keeping it tidy ----

        [Test]
        public void PlaceMovesAnExistingEntryInsteadOfAddingOne()
        {
            var e = Graph().Editor;
            var first = e.Place("a", 1, 2);
            Assert.That(e.Place("a", 5, 6), Is.SameAs(first));
            Assert.That(e.Nodes, Has.Count.EqualTo(1));
            Assert.That(e.LayoutOf("a").X, Is.EqualTo(5f));
            Assert.That(e.LayoutOf("zzz"), Is.Null);
            Assert.That(e.BendsOf("a", "x"), Is.Null);
        }

        [Test]
        public void PruneDropsWhatPointsAtBlocksThatAreGone()
        {
            var g = Decorated();
            g.Editor.Place("gone", 0, 0);
            g.Editor.Wires.Add(new BlockWireBends("gone", "arg0"));
            g.Editor.Groups.Add(new BlockGroup("lost", "gone"));
            g.Editor.Groups[0].Nodes.Add("gone");
            g.Editor.Prune(g);
            Assert.That(g.Editor.Nodes.Select(n => n.Id), Is.EquivalentTo(new[] { "a", "b" }));
            Assert.That(g.Editor.Wires.Select(w => w.Node), Is.EqualTo(new[] { "b" }));
            Assert.That(g.Editor.Groups.Select(x => x.Title), Is.EqualTo(new[] { "setup" }));
            Assert.That(g.Editor.Groups[0].Nodes, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(g.Editor.Comments, Has.Count.EqualTo(1), "comment boxes belong to no block");
            Assert.Throws<System.ArgumentNullException>(() => g.Editor.Prune(null));
        }
    }
}
