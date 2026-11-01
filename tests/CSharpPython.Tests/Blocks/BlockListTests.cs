using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // The list blocks added for graphs that keep a work list: "Remove and return item" (pop) and "List of size".
    public class BlockListTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        private static BlockNode Print(string id, string valueNode) => N(id, "call", "print").Connect("arg0", valueNode);

        private static BlockFunction Chain(string name, params BlockNode[] nodes)
        {
            var f = new BlockFunction(name);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (i + 1 < nodes.Length) nodes[i].Then(nodes[i + 1].Id);
                f.Add(nodes[i]);
            }
            f.Start = nodes[0].Id;
            return f;
        }

        // A graph with the list variable xs, which main fills with 1, 2, 3 before anything else.
        private static BlockGraph WithList(BlockFunction main)
        {
            var init = N("init", "set", "xs").Connect("value", "init_list").Then(main.Start);
            main.Add(init);
            main.Add(N("init_list", "list").Literal("item0", "1").Literal("item1", "2").Literal("item2", "3"));
            main.Start = "init";
            return new BlockGraph().AddVariable("xs", null, "list[int]").AddFunction(main);
        }

        private static string Source(BlockGraph g)
        {
            var r = BlockCompiler.Compile(g);
            Assert.That(r.Errors.Select(e => e.ToString()), Is.Empty);
            return r.Source;
        }

        private static string Run(BlockGraph g)
        {
            string source = Source(g);
            var sb = new System.Text.StringBuilder();
            var vm = new PyVm { Output = s => sb.Append(s) };
            var task = vm.Start(source + "\nmain()\n");
            while (task.Update() != TaskState.Finished && task.State != TaskState.Error) { }
            Assert.That(task.State, Is.EqualTo(TaskState.Finished), task.Error?.Message);
            return sb.ToString();
        }

        [Test]
        public void APopNobodyReadsIsAPlainCall()
        {
            var g = WithList(Chain("main", N("p", "pop").Connect("list", "x"), N("x2", "comment", "done")));
            g.Functions[0].Add(N("x", "get", "xs"));
            Assert.That(Source(g), Does.Contain("    xs.pop()\n"));
        }

        [Test]
        public void APopTakesAnOptionalIndex()
        {
            var main = Chain("main", N("p", "pop").Connect("list", "x").Literal("index", "0"), N("c", "comment", "done"));
            main.Add(N("x", "get", "xs"));
            Assert.That(Source(WithList(main)), Does.Contain("xs.pop(0)"));
        }

        [Test]
        public void ThePoppedItemCanBeUsedByLaterBlocks()
        {
            var main = Chain("main", N("p", "pop").Connect("list", "x"), Print("show", "p"));
            main.Add(N("x", "get", "xs"));
            var g = WithList(main);
            Assert.That(Source(g), Does.Match(@"_b1 = xs\.pop\(\)\s+print\(_b1\)"));
            Assert.That(Run(g).Trim(), Is.EqualTo("3"));
        }

        [Test]
        public void PopWithAnIndexTakesThatItem()
        {
            var main = Chain("main", N("p", "pop").Connect("list", "x").Literal("index", "0"), Print("show", "p"));
            main.Add(N("x", "get", "xs"));
            Assert.That(Run(WithList(main)).Trim(), Is.EqualTo("1"));
        }

        [Test]
        public void ThePoppedItemCannotBeReadBeforeThePopRuns()
        {
            var main = Chain("main", Print("show", "p"), N("p", "pop").Connect("list", "x"));
            main.Add(N("x", "get", "xs"));
            var r = BlockCompiler.Compile(WithList(main));
            Assert.That(r.Succeeded, Is.False);
            Assert.That(r.Errors.First().Message, Does.Contain("'pop' before it has run"));
        }

        [Test]
        public void ALoopConditionCannotUseThePoppedItem()
        {
            var main = new BlockFunction("main") { Start = "w" };
            main.Add(N("w", "while").Connect("cond", "p"));
            main.Add(N("p", "pop").Connect("list", "x"));
            main.Add(N("x", "get", "xs"));
            var r = BlockCompiler.Compile(WithList(main));
            Assert.That(r.Succeeded, Is.False);
        }

        [Test]
        public void ThePoppedValueHasTheElementTypeOfTheList()
        {
            var main = Chain("main", N("p", "pop").Connect("list", "x"));
            main.Add(N("x", "get", "xs"));
            var g = WithList(main);
            Assert.That(BlockTypeChecker.OutputType(g, "main", "p").ToString(), Is.EqualTo("int"));

            var editor = new BlockEditor(g, new BlockLibrary());
            var pins = editor.PinsOf("main", "p");
            Assert.That(pins.Any(p => p.Kind == BlockPinKind.FlowIn), Is.True);
            Assert.That(pins.Any(p => p.Kind == BlockPinKind.FlowOut), Is.True);
            Assert.That(pins.Single(p => p.Kind == BlockPinKind.Output).Type.ToString(), Is.EqualTo("int"));
        }

        [Test]
        public void ThePoppedValueCanBeWiredAndIsTypeChecked()
        {
            var main = Chain("main", N("p", "pop").Connect("list", "x"), N("s", "set", "name"));
            main.Add(N("x", "get", "xs"));
            var g = WithList(main).AddVariable("name", "\"\"", "str");
            var editor = new BlockEditor(g, new BlockLibrary());
            Assert.That(editor.Connect("main", "s", "value", "p").Succeeded, Is.False);
            g.AddVariable("n", "0", "int");
            main.Add(N("s2", "set", "n"));
            Assert.That(editor.Connect("main", "s2", "value", "p").Succeeded, Is.True);
        }

        [Test]
        public void AListOfSizeRepeatsTheValue()
        {
            var main = Chain("main", N("set", "set", "xs").Connect("value", "size"), Print("show", "xs2"));
            main.Add(N("size", "list_of_size").Literal("value", "0").Literal("count", "4"));
            main.Add(N("xs2", "get", "xs"));
            var g = WithList(main);
            Assert.That(Source(g), Does.Contain("xs = [0] * 4"));
            Assert.That(Run(g).Trim(), Is.EqualTo("[0, 0, 0, 0]"));
        }

        [Test]
        public void AListOfSizeIsTypedByItsValueAndNeedsAnIntCount()
        {
            var main = Chain("main", N("set", "set", "xs").Connect("value", "size"));
            main.Add(N("size", "list_of_size").Literal("value", "0").Literal("count", "4"));
            var g = WithList(main);
            Assert.That(BlockTypeChecker.OutputType(g, "main", "size").ToString(), Is.EqualTo("list[int]"));
            var editor = new BlockEditor(g, new BlockLibrary());
            Assert.That(editor.SetLiteral("main", "size", "count", "\"a\"").Succeeded, Is.False);
        }

        [Test]
        public void ABigListOfSizeStopsAtTheSequenceLimit()
        {
            var main = Chain("main", N("set", "set", "xs").Connect("value", "size"));
            main.Add(N("size", "list_of_size").Literal("value", "0").Literal("count", "5000000"));
            var vm = new PyVm();
            var task = vm.Start(Source(WithList(main)) + "\nmain()\n");
            while (task.Update() != TaskState.Finished && task.State != TaskState.Error) { }
            Assert.That(task.Error.TypeName, Is.EqualTo("MemoryError"));
        }
    }
}
