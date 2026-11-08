using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // The tuple, set, dictionary and sorted blocks: what they write, that the result runs, and how they are typed.
    public class BlockCollectionTests
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

        private static string S(string text) => BlockLiterals.String(text);

        private static BlockCompileResult Compile(BlockFunction main, params string[] variables)
        {
            var g = new BlockGraph();
            foreach (var v in variables) g.AddVariable(v, null);
            return BlockCompiler.Compile(g.AddFunction(main));
        }

        private static string Source(BlockFunction main, params string[] variables)
        {
            var r = Compile(main, variables);
            Assert.That(r.Errors.Select(e => e.ToString()), Is.Empty);
            return r.Source;
        }

        private static string Run(BlockFunction main, params string[] variables)
        {
            string source = Source(main, variables);
            var sb = new System.Text.StringBuilder();
            var vm = new PyVm { Output = s => sb.Append(s) };
            var task = vm.Start(source + "\nmain()\n");
            while (task.Update() != TaskState.Finished && task.State != TaskState.Error) { }
            Assert.That(task.State, Is.EqualTo(TaskState.Finished), task.Error?.Message);
            return sb.ToString();
        }

        [Test]
        public void ATupleIsWrittenWithItsItems()
        {
            var main = Chain("main", Print("p1", "t2"), Print("p2", "t1"), Print("p3", "t0"));
            main.Add(N("t2", "tuple").Literal("item0", "1").Literal("item1", "2"));
            main.Add(N("t1", "tuple").Literal("item0", "7"));
            main.Add(N("t0", "tuple"));
            string source = Source(main);
            Assert.That(source, Does.Contain("print((1, 2))").And.Contain("print((7,))").And.Contain("print(())"));
            Assert.That(Run(main), Is.EqualTo("(1, 2)\n(7,)\n()\n"));
        }

        [Test]
        public void ATupleItemIsReadByPosition()
        {
            var main = Chain("main", Print("p", "item"));
            main.Add(N("item", "tuple_item").Connect("tuple", "t").Literal("index", "1"));
            main.Add(N("t", "tuple").Literal("item0", S("a")).Literal("item1", S("b")));
            Assert.That(Source(main), Does.Contain("(\"a\", \"b\")[1]"));
            Assert.That(Run(main), Is.EqualTo("b\n"));
        }

        [Test]
        public void ASetKeepsEachMemberOnceAndTheFlowBlocksChangeIt()
        {
            var main = Chain("main",
                N("init", "set", "seen").Connect("value", "make"),
                N("add1", "set_add").Connect("set", "g1").Literal("value", "5"),
                N("add2", "set_add").Connect("set", "g2").Literal("value", "5"),
                N("add3", "set_add").Connect("set", "g3").Literal("value", "6"),
                N("drop", "set_discard").Connect("set", "g4").Literal("value", "1"),
                Print("count", "len"), Print("has5", "in5"), Print("has1", "in1"));
            main.Add(N("make", "set_of").Literal("item0", "1").Literal("item1", "2"));
            for (int i = 1; i <= 4; i++) main.Add(N("g" + i, "get", "seen"));
            main.Add(N("len", "call", "len").Connect("arg0", "g5"));
            main.Add(N("g5", "get", "seen"));
            main.Add(N("in5", "contains").Literal("item", "5").Connect("collection", "g6"));
            main.Add(N("in1", "contains").Literal("item", "1").Connect("collection", "g7"));
            main.Add(N("g6", "get", "seen"));
            main.Add(N("g7", "get", "seen"));
            string source = Source(main, "seen");
            Assert.That(source, Does.Contain("seen = {1, 2}").And.Contain("seen.add(5)").And.Contain("seen.discard(1)"));
            Assert.That(Run(main, "seen"), Is.EqualTo("3\nTrue\nFalse\n"));
        }

        [Test]
        public void AnEmptySetAndAnEmptyDictionaryAreWrittenAsCalls()
        {
            var main = Chain("main", Print("p1", "s"), Print("p2", "d"));
            main.Add(N("s", "set_of"));
            main.Add(N("d", "dict_of"));
            Assert.That(Run(main), Is.EqualTo("set()\n{}\n"));
        }

        [Test]
        public void ADictionaryHasPairsAndItsBlocksReadWriteAndRemove()
        {
            var main = Chain("main",
                N("init", "set", "counts").Connect("value", "make"),
                N("put", "dict_set").Connect("dict", "g1").Literal("key", S("b")).Literal("value", "5"),
                N("del", "dict_remove").Connect("dict", "g2").Literal("key", S("a")),
                N("del2", "dict_remove").Connect("dict", "g3").Literal("key", S("missing")),
                Print("pb", "getb"), Print("pa", "geta"), Print("pz", "getz"), Print("size", "len"));
            main.Add(N("make", "dict_of").Literal("key0", S("a")).Literal("value0", "1").Literal("key1", S("b")).Literal("value1", "2"));
            main.Add(N("g1", "get", "counts"));
            main.Add(N("g2", "get", "counts"));
            main.Add(N("g3", "get", "counts"));
            main.Add(N("getb", "dict_get").Connect("dict", "g4").Literal("key", S("b")));
            main.Add(N("geta", "dict_get").Connect("dict", "g5").Literal("key", S("a")).Literal("default", "0"));
            main.Add(N("getz", "dict_get").Connect("dict", "g6").Literal("key", S("z")).Literal("default", "-1"));
            main.Add(N("len", "call", "len").Connect("arg0", "g7"));
            for (int i = 4; i <= 7; i++) main.Add(N("g" + i, "get", "counts"));
            string source = Source(main, "counts");
            Assert.That(source, Does.Contain("counts = {\"a\": 1, \"b\": 2}").And.Contain("counts[\"b\"] = 5")
                .And.Contain("counts.pop(\"a\", None)").And.Contain("counts.get(\"b\")").And.Contain("counts.get(\"z\", -1)"));
            Assert.That(Run(main, "counts"), Is.EqualTo("5\n0\n-1\n1\n"));
        }

        [Test]
        public void SortedTakesAnOptionalReverseAndAnyCollection()
        {
            var main = Chain("main", Print("p1", "up"), Print("p2", "down"), Print("p3", "of_set"));
            main.Add(N("xs", "list").Literal("item0", "3").Literal("item1", "1").Literal("item2", "2"));
            main.Add(N("up", "sorted").Connect("items", "xs"));
            main.Add(N("down", "sorted").Connect("items", "xs").Literal("reverse", "True"));
            main.Add(N("of_set", "sorted").Connect("items", "st"));
            main.Add(N("st", "set_of").Literal("item0", "9").Literal("item1", "4"));
            string source = Source(main);
            Assert.That(source, Does.Contain("sorted(_b1)").And.Contain("sorted(_b1, reverse=True)"), "the list block is read twice, so it is worked out once");
            Assert.That(Run(main), Is.EqualTo("[1, 2, 3]\n[3, 2, 1]\n[4, 9]\n"));
        }

        [Test]
        public void NearestFirstCanBeWrittenWithTuplesAndSorted()
        {
            // The Explorer's order: pair each direction with its distance, sort the pairs, read the direction back.
            var main = Chain("main", Print("p", "first_dir"));
            main.Add(N("pairs", "list").Connect("item0", "a").Connect("item1", "b").Connect("item2", "c"));
            main.Add(N("a", "tuple").Literal("item0", "5").Literal("item1", "0"));
            main.Add(N("b", "tuple").Literal("item0", "2").Literal("item1", "1"));
            main.Add(N("c", "tuple").Literal("item0", "7").Literal("item1", "2"));
            main.Add(N("ordered", "sorted").Connect("items", "pairs"));
            main.Add(N("first", "index").Connect("list", "ordered").Literal("index", "0"));
            main.Add(N("first_dir", "tuple_item").Connect("tuple", "first").Literal("index", "1"));
            Assert.That(Run(main), Is.EqualTo("1\n"));
        }

        [Test]
        public void MembershipLooksThroughSetsDictionariesAndTuples()
        {
            var main = Chain("main", Print("p1", "in_set"), Print("p2", "in_dict"), Print("p3", "in_tuple"));
            main.Add(N("in_set", "contains").Literal("item", "2").Connect("collection", "st"));
            main.Add(N("in_dict", "contains").Literal("item", S("k")).Connect("collection", "d"));
            main.Add(N("in_tuple", "contains").Literal("item", "9").Connect("collection", "t"));
            main.Add(N("st", "set_of").Literal("item0", "2"));
            main.Add(N("d", "dict_of").Literal("key0", S("k")).Literal("value0", "1"));
            main.Add(N("t", "tuple").Literal("item0", "1"));
            Assert.That(Run(main), Is.EqualTo("True\nTrue\nFalse\n"));
        }

        [Test]
        public void TheTypesStopWiresThatCannotWork()
        {
            var main = Chain("main", N("add", "set_add").Connect("set", "xs").Literal("value", "1"));
            main.Add(N("xs", "list").Literal("item0", "1"));
            Assert.That(Compile(main).Errors.Select(e => e.Message), Has.Some.Contains("cannot take this wire"));

            var tuples = Chain("main", Print("p", "item"));
            tuples.Add(N("item", "tuple_item").Connect("tuple", "xs").Literal("index", "0"));
            tuples.Add(N("xs", "list").Literal("item0", "1"));
            Assert.That(Compile(tuples).Errors.Select(e => e.Message), Has.Some.Contains("cannot take this wire"));

            var number = Chain("main", Print("p", "in_number"));
            number.Add(N("in_number", "contains").Literal("item", "1").Literal("collection", "5"));
            Assert.That(Compile(number).Errors, Is.Not.Empty, "a number is not something to look inside");

            var dict = Chain("main", N("put", "dict_set").Connect("dict", "d").Literal("key", S("a")).Literal("value", "1"));
            dict.Add(N("d", "dict_of"));
            Assert.That(Compile(dict).Errors, Is.Empty, "a dictionary block fits a dictionary pin");
        }

        [Test]
        public void GapsInNumberedPinsAreReported()
        {
            var tuple = Chain("main", Print("p", "t"));
            tuple.Add(N("t", "tuple").Literal("item1", "1"));
            Assert.That(Compile(tuple).Errors.Select(e => e.Message), Has.Some.Contains("numbered item0, item1"));

            var dict = Chain("main", Print("p", "d"));
            dict.Add(N("d", "dict_of").Literal("key0", "1"));
            Assert.That(Compile(dict).Errors.Select(e => e.Message), Has.Some.Contains("key0 with value0"));

            var wrong = Chain("main", Print("p", "a"));
            wrong.Add(N("a", "add").Literal("item0", "1").Literal("b", "2"));
            Assert.That(Compile(wrong).Errors.Select(e => e.Message), Has.Some.Contains("has no input pin 'item0'"));
        }

        [Test]
        public void TheEditorOffersAnotherPinWhenOneIsInUse()
        {
            var main = new BlockFunction("main");
            var editor = new BlockEditor(new BlockGraph().AddFunction(main), new BlockLibrary());
            string set = editor.AddNode("main", "set_of", null, 0, 0).Node.Id;
            string dict = editor.AddNode("main", "dict_of", null, 0, 0).Node.Id;
            string tuple = editor.AddNode("main", "tuple", null, 0, 0).Node.Id;
            string add = editor.AddNode("main", "add", null, 0, 0).Node.Id;

            Assert.That(editor.PinsOf("main", set).Where(p => p.Kind == BlockPinKind.Input).Select(p => p.Name), Is.EqualTo(new[] { "item0" }));
            Assert.That(editor.PinsOf("main", dict).Where(p => p.Kind == BlockPinKind.Input).Select(p => p.Name), Is.EqualTo(new[] { "key0", "value0" }));
            Assert.That(editor.SetLiteral("main", set, "item0", "1").Succeeded, Is.True);
            Assert.That(editor.SetLiteral("main", dict, "key0", S("a")).Succeeded, Is.True);
            Assert.That(editor.SetLiteral("main", tuple, "item0", "1").Succeeded, Is.True);
            Assert.That(editor.PinsOf("main", set).Where(p => p.Kind == BlockPinKind.Input).Select(p => p.Name), Is.EqualTo(new[] { "item0", "item1" }));
            Assert.That(editor.PinsOf("main", dict).Where(p => p.Kind == BlockPinKind.Input).Select(p => p.Name), Is.EqualTo(new[] { "key0", "value0", "key1", "value1" }));
            Assert.That(editor.SetLiteral("main", add, "item0", "1").Succeeded, Is.False, "only the collection blocks have numbered pins");
            Assert.That(editor.PinsOf("main", set).Single(p => p.Kind == BlockPinKind.Output).Type.ToString(), Is.EqualTo("set"));
            Assert.That(editor.PinsOf("main", dict).Single(p => p.Kind == BlockPinKind.Output).Type.ToString(), Is.EqualTo("dict"));
            Assert.That(editor.PinsOf("main", tuple).Single(p => p.Kind == BlockPinKind.Output).Type.ToString(), Is.EqualTo("tuple"));
        }

        [Test]
        public void AVariableCanBeDeclaredAsASetADictionaryOrATuple()
        {
            var main = Chain("main",
                N("a", "set", "seen").Connect("value", "make_set"),
                N("b", "set", "counts").Connect("value", "make_dict"),
                N("c", "set", "pair").Connect("value", "make_tuple"),
                N("add", "set_add").Connect("set", "get_seen").Literal("value", "3"),
                N("put", "dict_set").Connect("dict", "get_counts").Literal("key", S("k")).Literal("value", "1"),
                Print("p1", "item"), Print("p2", "size"));
            main.Add(N("make_set", "set_of"));
            main.Add(N("make_dict", "dict_of"));
            main.Add(N("make_tuple", "tuple").Literal("item0", "4").Literal("item1", "5"));
            main.Add(N("get_seen", "get", "seen"));
            main.Add(N("get_counts", "get", "counts"));
            main.Add(N("get_pair", "get", "pair"));
            main.Add(N("item", "tuple_item").Connect("tuple", "get_pair").Literal("index", "1"));
            main.Add(N("size", "call", "len").Connect("arg0", "get_seen2"));
            main.Add(N("get_seen2", "get", "seen"));
            var g = new BlockGraph().AddVariable("seen", null, "set").AddVariable("counts", null, "dict").AddVariable("pair", null, "tuple").AddFunction(main);
            var result = BlockCompiler.Compile(g);
            Assert.That(result.Errors.Select(e => e.ToString()), Is.Empty);
            var sb = new System.Text.StringBuilder();
            var vm = new PyVm { Output = t => sb.Append(t) };
            var task = vm.Start(result.Source + "\nmain()\n");
            while (task.Update() != TaskState.Finished && task.State != TaskState.Error) { }
            Assert.That(task.State, Is.EqualTo(TaskState.Finished), task.Error?.Message);
            Assert.That(sb.ToString(), Is.EqualTo("5\n1\n"));

            var wrong = new BlockGraph().AddVariable("seen", null, "set")
                .AddFunction(Chain("main", N("a", "set", "seen").Connect("value", "xs")).Add(N("xs", "list")));
            Assert.That(BlockCompiler.Compile(wrong).Errors, Is.Not.Empty, "a list does not fit a set variable");
        }

        [Test]
        public void TheNewBlocksAreInThePaletteAndTheSearch()
        {
            var library = new BlockLibrary();
            foreach (var id in new[] { "tuple", "tuple_item", "set_of", "set_add", "set_discard", "dict_of", "dict_get", "dict_set", "dict_remove", "sorted" })
            {
                var spec = library.FindBlock(id);
                Assert.That(spec, Is.Not.Null, id);
                Assert.That(spec.Category, Is.EqualTo("Collections"), id);
            }
            var editor = new BlockEditor(new BlockGraph().AddFunction(new BlockFunction("main")), library);
            Assert.That(editor.Search("dictionary", 10).Select(c => c.Block), Does.Contain("dict_get").And.Contain("dict_set").And.Contain("dict_of"));
            Assert.That(editor.Search("set", 20).Select(c => c.Block), Does.Contain("set_add").And.Contain("set_of"));
        }
    }
}
