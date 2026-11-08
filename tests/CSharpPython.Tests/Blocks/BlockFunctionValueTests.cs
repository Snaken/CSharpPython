using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // The "Function" block holds a function as a value, and the sorted block takes one as its key.
    public class BlockFunctionValueTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        // main: print(sorted([(1, 9), (2, 3), (3, 5)], key=<keyName>, reverse=<reverse>))
        private static BlockGraph Sorting(string keyName, bool reverse = false, params BlockFunction[] others)
        {
            var main = new BlockFunction("main") { Start = "p" };
            main.Add(N("p", "call", "print").Connect("arg0", "s"));
            var sort = N("s", "sorted").Connect("items", "l").Connect("key", "k");
            if (reverse) sort.Literal("reverse", "True");
            main.Add(sort);
            main.Add(N("k", "function_ref", keyName));
            main.Add(N("l", "list").Connect("item0", "a").Connect("item1", "b").Connect("item2", "c"));
            foreach (var (id, x, y) in new[] { ("a", "1", "9"), ("b", "2", "3"), ("c", "3", "5") })
                main.Add(N(id, "tuple").Literal("item0", x).Literal("item1", y));
            var graph = new BlockGraph().AddFunction(main);
            foreach (var f in others) graph.AddFunction(f);
            return graph;
        }

        private static BlockFunction SecondOf()
        {
            var f = new BlockFunction("second", "t") { Start = "r" };
            f.Add(N("r", "return").Connect("value", "i"));
            f.Add(N("i", "tuple_item").Connect("tuple", "g").Literal("index", "1"));
            f.Add(N("g", "get", "t"));
            return f;
        }

        private static string Run(BlockCompileResult result) =>
            Vm.Py.Run(result.Source + "main()\n");

        [Test]
        public void AFunctionOfTheGraphIsTheKeyOfASort()
        {
            var result = BlockCompiler.Compile(Sorting("second", false, SecondOf()));
            Assert.That(result.Errors.Select(e => e.ToString()), Is.Empty);
            Assert.That(result.Source, Does.Contain("sorted([(1, 9), (2, 3), (3, 5)], key=second)"));
            Assert.That(Run(result), Is.EqualTo("[(2, 3), (3, 5), (1, 9)]\n"));
        }

        [Test]
        public void TheKeyAndReverseTogether()
        {
            var result = BlockCompiler.Compile(Sorting("second", true, SecondOf()));
            Assert.That(result.Source, Does.Contain("key=second, reverse=True"));
            Assert.That(Run(result), Is.EqualTo("[(1, 9), (3, 5), (2, 3)]\n"));
        }

        [Test]
        public void ALibraryFunctionCanBeTheKey()
        {
            var main = new BlockFunction("main") { Start = "p" };
            main.Add(N("p", "call", "print").Connect("arg0", "s"));
            main.Add(N("s", "sorted").Connect("items", "l").Connect("key", "k"));
            main.Add(N("k", "function_ref", "abs"));
            main.Add(N("l", "list").Literal("item0", "-3").Literal("item1", "1").Literal("item2", "-2"));
            var result = BlockCompiler.Compile(new BlockGraph().AddFunction(main));
            Assert.That(result.Errors.Select(e => e.ToString()), Is.Empty);
            Assert.That(result.Source, Does.Contain("key=abs)"));
            Assert.That(Run(result), Is.EqualTo("[1, -2, -3]\n"));
        }

        [Test]
        public void AHostFunctionIsImportedAndQualified()
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m => m
                .Function("weight", a => Value.FromInt(-a.AsInt()))
                .Describe("weight", "x: int -> int", pure: true));
            var main = new BlockFunction("main") { Start = "p" };
            main.Add(N("p", "call", "print").Connect("arg0", "s"));
            main.Add(N("s", "sorted").Connect("items", "l").Connect("key", "k"));
            main.Add(N("k", "function_ref", "game.weight"));
            main.Add(N("l", "list").Literal("item0", "1").Literal("item1", "3").Literal("item2", "2"));
            var result = BlockCompiler.Compile(new BlockGraph().AddFunction(main), new BlockLibrary().AddAllModules(vm));
            Assert.That(result.Errors.Select(e => e.ToString()), Is.Empty);
            Assert.That(result.Source, Does.StartWith("import game\n").And.Contain("key=game.weight)"));
        }

        [Test]
        public void ASortWithoutAKeyIsWrittenAsBefore()
        {
            var main = new BlockFunction("main") { Start = "p" };
            main.Add(N("p", "call", "print").Connect("arg0", "s"));
            main.Add(N("s", "sorted").Connect("items", "l"));
            main.Add(N("l", "list").Literal("item0", "2").Literal("item1", "1"));
            Assert.That(BlockCompiler.Compile(new BlockGraph().AddFunction(main)).Source, Does.Contain("print(sorted([2, 1]))"));
        }

        [Test]
        public void ABadKeyFunctionIsReported()
        {
            var two = new BlockFunction("pair", "a", "b") { Start = "r" };
            two.Add(N("r", "return").Literal("value", "1"));
            var result = BlockCompiler.Compile(Sorting("pair", false, two));
            Assert.That(result.Errors.Select(e => e.ToString()), Has.Some.Contain("key function takes one item").And.Some.Contain("'pair' takes 2"));

            Assert.That(BlockCompiler.Compile(Sorting("nowhere")).Errors.Select(e => e.ToString()), Has.Some.Contain("unknown function 'nowhere'"));
            Assert.That(BlockCompiler.Compile(Sorting(null)).Errors.Select(e => e.ToString()), Has.Some.Contain("needs the name of a function"));
        }

        [Test]
        public void TheEditorChecksTheNameAndKeepsItWhenAFunctionIsRenamed()
        {
            var editor = new BlockEditor(Sorting("second", false, SecondOf()));
            Assert.That(editor.AddNode("main", "function_ref", "nowhere").Succeeded, Is.False);
            Assert.That(editor.AddNode("main", "function_ref").Succeeded, Is.True, "the name can be set later");
            var added = editor.AddNode("main", "function_ref", "second").Node;
            Assert.That(editor.SetName("main", added.Id, "nowhere").Succeeded, Is.False);
            Assert.That(editor.SetName("main", added.Id, "abs").Succeeded, Is.True);

            Assert.That(editor.RenameFunction("second", "by_score").Succeeded, Is.True);
            Assert.That(editor.Graph.Functions[0].Nodes.Single(n => n.Id == "k").Name, Is.EqualTo("by_score"));
            Assert.That(editor.Compile().Source, Does.Contain("key=by_score"));
        }

        [Test]
        public void AFunctionBlockOfAMissingFunctionIsAPlaceholder()
        {
            var graph = Sorting("gone");
            var found = BlockPlaceholders.Find(graph, new BlockLibrary());
            var placeholder = found.Single();
            Assert.That((placeholder.Block, placeholder.Name), Is.EqualTo(("function_ref", "gone")));
        }
    }
}
