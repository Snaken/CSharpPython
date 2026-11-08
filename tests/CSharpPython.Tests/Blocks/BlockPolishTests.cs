using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // The "Is in" block's pins are item and collection (they were a and b), and a host can ask whether a block is a step.
    public class BlockPolishTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        // A graph written for the old pin names: print(2 in xs), with a reroute point on the wire into 'b'.
        private static BlockGraph OldGraph()
        {
            var main = new BlockFunction("main") { Start = "p" };
            main.Add(N("p", "call", "print").Connect("arg0", "has"));
            main.Add(N("has", "contains").Literal("a", "2").Connect("b", "xs"));
            main.Add(N("xs", "list").Literal("item0", "1").Literal("item1", "2"));
            var graph = new BlockGraph().AddFunction(main);
            graph.Editor.Wires.Add(new BlockWireBends("has", "b"));
            graph.Editor.Wires[0].Points.Add(new BlockPoint { X = 5, Y = 6 });
            return graph;
        }

        private static string Pins(BlockGraph g) =>
            string.Join(",", g.Functions[0].Nodes.Single(n => n.Id == "has").Inputs.Select(i => i.Pin));

        [Test]
        public void AnOldSavedGraphIsMigratedWhenItIsRead()
        {
            var graph = BlockGraph.FromJson(OldGraph().ToJson());
            Assert.That(Pins(graph), Is.EqualTo("item,collection"));
            Assert.That(graph.Editor.Wires.Single().Pin, Is.EqualTo("collection"), "the reroute point moves with its wire");
            var result = BlockCompiler.Compile(graph);
            Assert.That(result.Errors.Select(e => e.ToString()), Is.Empty);
            Assert.That(result.Source, Does.Contain("2 in "));
        }

        [Test]
        public void AnEditorBringsAnOlderGraphUpToDate()
        {
            var graph = OldGraph();
            var editor = new BlockEditor(graph);
            Assert.That(Pins(graph), Is.EqualTo("item,collection"));
            Assert.That(editor.Compile().Succeeded, Is.True);
            Assert.That(editor.PinsOf("main", "has").Where(p => p.Kind == BlockPinKind.Input).Select(p => p.Name), Is.EqualTo(new[] { "item", "collection" }));
        }

        [Test]
        public void MigrateCountsTheRenamesAndLeavesNewNamesAlone()
        {
            var graph = OldGraph();
            Assert.That(graph.Migrate(), Is.EqualTo(2));
            Assert.That(graph.Migrate(), Is.EqualTo(0), "nothing left to do");

            var main = new BlockFunction("main");
            main.Add(N("has", "contains").Literal("item", "1").Literal("a", "2"));
            var both = new BlockGraph().AddFunction(main);
            Assert.That(both.Migrate(), Is.EqualTo(0), "a block that already has an item pin keeps its a pin");
            Assert.That(Pins(both), Is.EqualTo("item,a"));
        }

        [Test]
        public void ACodeBuiltGraphWithTheOldNamesIsToldWhatChanged()
        {
            var errors = BlockCompiler.Compile(OldGraph()).Errors.Select(e => e.Message).ToList();
            Assert.That(errors, Has.Some.Contains("has no input pin 'a'").And.Contains("renamed 'item'"));
            Assert.That(errors, Has.Some.Contains("has no input pin 'b'").And.Contains("renamed 'collection'"));
        }

        [Test]
        public void TheLibraryKnowsWhichBlocksAndCallsAreSteps()
        {
            var library = new BlockLibrary().AddFunction("explode", System.Array.Empty<string>(), module: "game")
                .AddFunction("distance", new[] { "x" }, pure: true, module: "game");
            foreach (var step in new[] { "set", "change", "if", "while", "for_each", "append", "pop", "set_add", "dict_set", "return", "comment" })
                Assert.That(library.IsStep(step), Is.True, step);
            foreach (var value in new[] { "literal", "get", "add", "contains", "list", "tuple", "set_of", "dict_get", "sorted", "index" })
                Assert.That(library.IsStep(value), Is.False, value);
            Assert.That(library.IsStep("call", "print"), Is.True);
            Assert.That(library.IsStep("call", "game.explode"), Is.True);
            Assert.That(library.IsStep("call", "len"), Is.False, "a pure built-in is a value");
            Assert.That(library.IsStep("call", "game.distance"), Is.False);
            Assert.That(library.IsStep("call", "patrol"), Is.True, "a name the library does not have is a function of the graph");
            Assert.That(library.IsStep("nope"), Is.False);
        }

        [Test]
        public void TheEditorKnowsWhichBlocksOfAGraphAreSteps()
        {
            var main = new BlockFunction("main");
            main.Add(N("own", "call", "patrol"));
            main.Add(N("pure", "call", "len"));
            main.Add(N("print", "call", "print"));
            main.Add(N("sum", "add"));
            main.Add(N("set", "set_add"));
            var editor = new BlockEditor(new BlockGraph().AddFunction(main).AddFunction(new BlockFunction("patrol")));
            Assert.That(editor.IsStep("main", "own"), Is.True, "calling a function of the graph is a step");
            Assert.That(editor.IsStep("main", "print"), Is.True);
            Assert.That(editor.IsStep("main", "set"), Is.True);
            Assert.That(editor.IsStep("main", "pure"), Is.False);
            Assert.That(editor.IsStep("main", "sum"), Is.False);
            Assert.That(editor.IsStep("main", "missing"), Is.False);
            Assert.That(editor.IsStep("nowhere", "own"), Is.False);
        }

        private static string Source(BlockFunction f) => BlockCompiler.Compile(new BlockGraph().AddFunction(f)).Source;

        [Test]
        public void AnIfAloneInAnElseBranchIsWrittenAsElif()
        {
            var f = new BlockFunction("main", "n");
            f.Start = "a";
            f.Add(N("a", "if").Connect("cond", "c1").Then("then", "p1").Then("else", "b"));
            f.Add(N("b", "if").Connect("cond", "c2").Then("then", "p2").Then("else", "p3"));
            f.Add(N("c1", "gt").Connect("a", "n").Literal("b", "5"));
            f.Add(N("c2", "gt").Connect("a", "n").Literal("b", "2"));
            f.Add(N("n", "get", "n"));
            foreach (var id in new[] { "p1", "p2", "p3" })
                f.Add(N(id, "call", "print").Literal("arg0", "\"" + id + "\""));
            string source = Source(f);
            Assert.That(source, Is.EqualTo(
                "def main(n):\n    if n > 5:\n        print(\"p1\")\n    elif n > 2:\n        print(\"p2\")\n    else:\n        print(\"p3\")\n"));
            Assert.That(Vm.Py.Run(source + "main(9)\nmain(3)\nmain(0)\n"), Is.EqualTo("p1\np2\np3\n"));
        }

        [Test]
        public void AnIfWithBlocksAfterItStaysNestedInTheElse()
        {
            var f = new BlockFunction("main");
            f.Start = "a";
            f.Add(N("a", "if").Literal("cond", "False").Then("else", "b"));
            f.Add(N("b", "if").Literal("cond", "True").Then("then", "p1").Then("next", "p2"));
            f.Add(N("p1", "call", "print").Literal("arg0", "1"));
            f.Add(N("p2", "call", "print").Literal("arg0", "2"));
            string source = Source(f);
            Assert.That(source, Does.Contain("    else:\n        if True:").And.Not.Contain("elif"));
            Assert.That(Vm.Py.Run(source + "main()\n"), Is.EqualTo("1\n2\n"));
        }

        [Test]
        public void ARangeThatStartsAtZeroIsWrittenWithJustTheStop()
        {
            var f = new BlockFunction("main");
            f.Start = "r";
            f.Add(N("r", "for_range", "i").Literal("start", "0").Literal("stop", "4"));
            Assert.That(Source(f), Does.Contain("for i in range(4):"));
        }

        private static BlockLibrary DocumentedLibrary()
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m => m
                .Native("spawn", (c, a) => Value.None, 1, 2)
                .Describe("spawn", "kind: str, count: int = 1 -> none", doc: "Puts new entities in the world.")
                .DescribeParameter("spawn", "count", "How many to make.")
                .Native("plain", (c, a) => Value.None, 1, 1)
                .Describe("plain", "x -> none"));
            return new BlockLibrary().AddAllModules(vm);
        }

        [Test]
        public void ThePaletteHasTheTextAHostDescribed()
        {
            var spec = DocumentedLibrary().FindFunction("game.spawn");
            Assert.That(spec.Doc, Is.EqualTo("Puts new entities in the world."));
            Assert.That(spec.ParameterDocs, Is.EqualTo(new string[] { null, "How many to make." }));
            var plain = DocumentedLibrary().FindFunction("game.plain");
            Assert.That(plain.Doc, Is.Null);
            Assert.That(plain.ParameterDocs, Is.EqualTo(new string[] { null }));
        }

        [Test]
        public void AFunctionAddedByHandCanCarryText()
        {
            var lib = new BlockLibrary().AddFunction("jump", new[] { "height" }, doc: "Jumps.",
                parameterDocs: new System.Collections.Generic.Dictionary<string, string> { { "height", "In metres." } });
            var spec = lib.FindFunction("jump");
            Assert.That((spec.Doc, spec.ParameterDocs[0]), Is.EqualTo(("Jumps.", "In metres.")));
        }

        [Test]
        public void ACallsInputPinsCarryTheirParameterText()
        {
            var main = new BlockFunction("main") { Start = "s" };
            main.Add(N("s", "call", "game.spawn"));
            main.Add(N("o", "call", "game.spawn"));
            var editor = new BlockEditor(new BlockGraph().AddFunction(main), DocumentedLibrary());
            var inputs = editor.PinsOf("main", "s").Where(p => p.Kind == BlockPinKind.Input).ToList();
            Assert.That(inputs.Select(p => p.Doc), Is.EqualTo(new string[] { null, "How many to make." }));
            Assert.That(editor.PinsOf("main", "s").Where(p => p.Kind != BlockPinKind.Input).Select(p => p.Doc), Is.All.Null);
        }
    }
}
