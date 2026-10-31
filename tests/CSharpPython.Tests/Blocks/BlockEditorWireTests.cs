using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // The host-independent editing rules (docs/node-view-design.md, step 5a): wires, literals, execution links and the start.
    public class BlockEditorWireTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        private static BlockLibrary Library()
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m => m.Native("say", (c, a) => Value.None, 1, 1).Describe("say", "text: str"));
            return new BlockLibrary().AddAllModules(vm);
        }

        // a -> b -> c are print calls; v is 1 + 2; w is 3 + 4; s is game.say.
        private static BlockGraph Graph()
        {
            var main = new BlockFunction("main") { Start = "a" };
            main.Add(N("a", "call", "print").Literal("arg0", "1").Then("next", "b"));
            main.Add(N("b", "call", "print").Connect("arg0", "v").Then("next", "c"));
            main.Add(N("c", "call", "print"));
            main.Add(N("v", "add").Literal("a", "1").Literal("b", "2"));
            main.Add(N("w", "add").Connect("a", "v").Literal("b", "4"));
            main.Add(N("s", "call", "game.say"));
            main.Add(N("t", "comment", "text"));
            return new BlockGraph().AddFunction(main);
        }

        private static (BlockEditor editor, int[] changes) Editor()
        {
            var editor = new BlockEditor(Graph(), Library());
            var changes = new int[1];
            editor.Changed += () => changes[0]++;
            return (editor, changes);
        }

        private static BlockNode Find(BlockEditor e, string id) => e.Graph.Functions[0].Nodes.First(n => n.Id == id);

        private static BlockInput Input(BlockEditor e, string id, string pin) => Find(e, id).Inputs.FirstOrDefault(i => i.Pin == pin);

        private static string Json(BlockEditor e) => e.Graph.ToJson();

        // ---- Wires ----

        [Test]
        public void ConnectWiresAnOutputToAnInputAndTheGraphCompiles()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.Connect("main", "c", "arg0", "w").Succeeded, Is.True);
            Assert.That(Input(editor, "c", "arg0").From, Is.EqualTo("w"));
            Assert.That(changes[0], Is.EqualTo(1));
            Assert.That(BlockCompiler.Compile(editor.Graph, editor.Library).Succeeded, Is.True);
        }

        [Test]
        public void ConnectReplacesALiteralOrAWireOnThePin()
        {
            var (editor, _) = Editor();
            Assert.That(editor.Connect("main", "a", "arg0", "v").Succeeded, Is.True);
            Assert.That(editor.Graph.Functions[0].Nodes.First(n => n.Id == "a").Inputs.Count(i => i.Pin == "arg0"), Is.EqualTo(1));
            Assert.That(Input(editor, "a", "arg0").Literal, Is.Null);

            Assert.That(editor.Connect("main", "a", "arg0", "w").Succeeded, Is.True);
            Assert.That(Find(editor, "a").Inputs.Count(i => i.Pin == "arg0"), Is.EqualTo(1));
            Assert.That(Input(editor, "a", "arg0").From, Is.EqualTo("w"));
        }

        [Test]
        public void ConnectForgetsTheRerouteOfTheWireItReplaces()
        {
            var (editor, _) = Editor();
            editor.Graph.Editor.Wires.Add(new BlockWireBends { Node = "b", Pin = "arg0" });
            Assert.That(editor.Connect("main", "b", "arg0", "w").Succeeded, Is.True);
            Assert.That(editor.Graph.Editor.Wires, Is.Empty);
        }

        [Test]
        public void ConnectRefusesAPinTheBlockDoesNotHave()
        {
            var (editor, changes) = Editor();
            string before = Json(editor);
            var result = editor.Connect("main", "c", "nope", "w");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Does.Contain("nope"));
            Assert.That(editor.Connect("main", "c", "arg3", "w").Succeeded, Is.False, "print takes one argument here");
            Assert.That(Json(editor), Is.EqualTo(before));
            Assert.That(changes[0], Is.EqualTo(0));
        }

        [Test]
        public void ConnectRefusesAWireWhoseTypeDoesNotFit()
        {
            var (editor, changes) = Editor();
            string before = Json(editor);
            var result = editor.Connect("main", "s", "arg0", "v");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Is.Not.Empty);
            Assert.That(Json(editor), Is.EqualTo(before));
            Assert.That(changes[0], Is.EqualTo(0));
        }

        [Test]
        public void ConnectRefusesAValueThatWouldDependOnItself()
        {
            var (editor, _) = Editor();
            string before = Json(editor);
            Assert.That(editor.Connect("main", "v", "a", "v").Succeeded, Is.False, "itself");
            Assert.That(editor.Connect("main", "v", "a", "w").Succeeded, Is.False, "w reads v");
            Assert.That(Json(editor), Is.EqualTo(before));
        }

        [Test]
        public void ConnectRefusesAStepInTheFlowAsASource()
        {
            var (editor, _) = Editor();
            var result = editor.Connect("main", "c", "arg0", "a");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Does.Contain("no value"));
        }

        [Test]
        public void ConnectRefusesMissingFunctionsAndBlocks()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.Connect("nope", "c", "arg0", "w").Succeeded, Is.False);
            Assert.That(editor.Connect("main", "x", "arg0", "w").Succeeded, Is.False);
            Assert.That(editor.Connect("main", "c", "arg0", "x").Succeeded, Is.False);
            Assert.That(changes[0], Is.EqualTo(0));
        }

        [Test]
        public void ConnectAcceptsAnItemPinOnAListBlock()
        {
            var (editor, _) = Editor();
            editor.AddNode("main", "list");
            string list = editor.Graph.Functions[0].Nodes.Last().Id;
            Assert.That(editor.Connect("main", list, "item0", "v").Succeeded, Is.True);
            Assert.That(editor.Connect("main", list, "item7", "w").Succeeded, Is.True);
            Assert.That(editor.Connect("main", list, "itemx", "w").Succeeded, Is.False);
        }

        [Test]
        public void DisconnectEmptiesAPinAndForgetsItsReroute()
        {
            var (editor, changes) = Editor();
            editor.Graph.Editor.Wires.Add(new BlockWireBends { Node = "b", Pin = "arg0" });
            Assert.That(editor.Disconnect("main", "b", "arg0").Succeeded, Is.True);
            Assert.That(Input(editor, "b", "arg0"), Is.Null);
            Assert.That(editor.Graph.Editor.Wires, Is.Empty);
            Assert.That(changes[0], Is.EqualTo(1));

            Assert.That(editor.Disconnect("main", "a", "arg0").Succeeded, Is.True, "a literal goes too");
            Assert.That(editor.Disconnect("main", "a", "arg0").Succeeded, Is.False, "nothing left to remove");
            Assert.That(editor.Disconnect("main", "x", "arg0").Succeeded, Is.False);
            Assert.That(editor.Disconnect("nope", "a", "arg0").Succeeded, Is.False);
            Assert.That(changes[0], Is.EqualTo(2));
        }

        // ---- Literals ----

        [Test]
        public void SetLiteralReplacesAWireOrAnotherLiteral()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.SetLiteral("main", "b", "arg0", "\"hi\"").Succeeded, Is.True);
            Assert.That(Input(editor, "b", "arg0").From, Is.Null);
            Assert.That(Input(editor, "b", "arg0").Literal, Is.EqualTo("\"hi\""));
            Assert.That(editor.SetLiteral("main", "b", "arg0", "5").Succeeded, Is.True);
            Assert.That(Find(editor, "b").Inputs.Count(i => i.Pin == "arg0"), Is.EqualTo(1));
            Assert.That(changes[0], Is.EqualTo(2));
        }

        [Test]
        public void SetLiteralRefusesBadRequestsAndChangesNothing()
        {
            var (editor, changes) = Editor();
            string before = Json(editor);
            Assert.That(editor.SetLiteral("main", "b", "nope", "1").Succeeded, Is.False);
            Assert.That(editor.SetLiteral("main", "b", "arg0", null).Succeeded, Is.False);
            Assert.That(editor.SetLiteral("main", "x", "arg0", "1").Succeeded, Is.False);
            Assert.That(editor.SetLiteral("nope", "b", "arg0", "1").Succeeded, Is.False);
            editor.Limits = new BlockLimits { MaxLiteralLength = 3 };
            Assert.That(editor.SetLiteral("main", "b", "arg0", "12345").Succeeded, Is.False);
            Assert.That(Json(editor), Is.EqualTo(before));
            Assert.That(changes[0], Is.EqualTo(0));
        }

        // ---- Links ----

        [Test]
        public void LinkRunsOneBlockIntoAnotherAndReplacesTheOldLink()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.Link("main", "c", "next", "s").Succeeded, Is.True);
            Assert.That(Find(editor, "c").Flow.Single().To, Is.EqualTo("s"));
            Assert.That(editor.Link("main", "a", "next", "c").Succeeded, Is.True);
            Assert.That(Find(editor, "a").Flow.Single().To, Is.EqualTo("c"), "the old link a -> b is replaced");
            Assert.That(changes[0], Is.EqualTo(2));
        }

        [Test]
        public void LinkRefusesAPinTheBlockDoesNotHave()
        {
            var (editor, changes) = Editor();
            var result = editor.Link("main", "c", "then", "s");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Does.Contain("then"));
            Assert.That(changes[0], Is.EqualTo(0));
        }

        [Test]
        public void LinkRefusesAValueBlockAsTheTargetAndAsTheSource()
        {
            var (editor, _) = Editor();
            Assert.That(editor.Link("main", "c", "next", "v").Succeeded, Is.False, "v is a value");
            Assert.That(editor.Link("main", "v", "next", "c").Succeeded, Is.False, "a value has no execution pin");
        }

        [Test]
        public void LinkRefusesAFlowThatWouldLoopBack()
        {
            var (editor, changes) = Editor();
            string before = Json(editor);
            Assert.That(editor.Link("main", "c", "next", "a").Succeeded, Is.False, "a -> b -> c -> a");
            Assert.That(editor.Link("main", "b", "next", "b").Succeeded, Is.False, "itself");
            Assert.That(editor.Link("main", "b", "next", "a").Succeeded, Is.False, "a -> b -> a");
            Assert.That(Json(editor), Is.EqualTo(before));
            Assert.That(changes[0], Is.EqualTo(0));
        }

        [Test]
        public void LinkAllowsTwoPathsToMeet()
        {
            var (editor, _) = Editor();
            editor.AddNode("main", "if");
            string branch = editor.Graph.Functions[0].Nodes.Last().Id;
            Assert.That(editor.Link("main", branch, "then", "c").Succeeded, Is.True);
            Assert.That(editor.Link("main", branch, "else", "c").Succeeded, Is.True);
            Assert.That(editor.Link("main", "b", "next", "c").Succeeded, Is.True, "a block may be reached more than once");
        }

        [Test]
        public void LinkRefusesMissingFunctionsAndBlocks()
        {
            var (editor, _) = Editor();
            Assert.That(editor.Link("nope", "a", "next", "b").Succeeded, Is.False);
            Assert.That(editor.Link("main", "x", "next", "b").Succeeded, Is.False);
            Assert.That(editor.Link("main", "a", "next", "x").Succeeded, Is.False);
        }

        [Test]
        public void UnlinkRemovesTheLink()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.Unlink("main", "a", "next").Succeeded, Is.True);
            Assert.That(Find(editor, "a").Flow, Is.Empty);
            Assert.That(editor.Unlink("main", "a", "next").Succeeded, Is.False, "nothing left to remove");
            Assert.That(editor.Unlink("main", "x", "next").Succeeded, Is.False);
            Assert.That(editor.Unlink("nope", "a", "next").Succeeded, Is.False);
            Assert.That(changes[0], Is.EqualTo(1));
        }

        [Test]
        public void ALinkedGraphCompilesInTheOrderOfItsLinks()
        {
            var (editor, _) = Editor();
            editor.Unlink("main", "a", "next");
            editor.Link("main", "a", "next", "c");
            editor.Link("main", "c", "next", "b");
            editor.Unlink("main", "b", "next");
            editor.Connect("main", "c", "arg0", "w");
            var result = BlockCompiler.Compile(editor.Graph, editor.Library);
            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Diagnostics));
            Assert.That(result.Source.IndexOf("print(", System.StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
        }

        // ---- Start ----

        [Test]
        public void SetStartChoosesOrClearsTheFirstBlock()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.SetStart("main", "c").Succeeded, Is.True);
            Assert.That(editor.Graph.Functions[0].Start, Is.EqualTo("c"));
            Assert.That(editor.SetStart("main", null).Succeeded, Is.True);
            Assert.That(editor.Graph.Functions[0].Start, Is.Null);
            Assert.That(changes[0], Is.EqualTo(2));
        }

        [Test]
        public void SetStartRefusesAValueBlockAndMissingOnes()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.SetStart("main", "v").Succeeded, Is.False);
            Assert.That(editor.SetStart("main", "x").Succeeded, Is.False);
            Assert.That(editor.SetStart("nope", "a").Succeeded, Is.False);
            Assert.That(editor.Graph.Functions[0].Start, Is.EqualTo("a"));
            Assert.That(changes[0], Is.EqualTo(0));
        }
    }
}
