using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    public class BlockExtraBlocksTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        private static BlockFunction Chain(string name, params BlockNode[] nodes)
        {
            var f = new BlockFunction(name);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (i + 1 < nodes.Length) nodes[i].Then(nodes[i + 1].Id);
                f.Add(nodes[i]);
            }
            if (nodes.Length > 0) f.Start = nodes[0].Id;
            return f;
        }

        private static BlockGraph Graph(BlockFunction f) => new BlockGraph().AddFunction(f);

        private static string Source(BlockGraph g)
        {
            var r = BlockCompiler.Compile(g);
            Assert.That(r.Errors.Select(e => e.ToString()), Is.Empty);
            Assert.That(new PyVm().Compile(r.Source), Is.Not.Null, r.Source);
            return r.Source;
        }

        private static string Run(BlockGraph g) => Vm.Py.Run(Source(g) + "main()\n");

        private static BlockNode Print(string id, string from) => N(id, "call", "print").Connect("arg0", from);

        [Test]
        public void ForRangeCountsWithOptionalStartAndStep()
        {
            var f = Chain("main",
                N("a", "for_range", "i").Literal("stop", "3").Then("body", "p1"),
                N("b", "for_range", "j").Literal("start", "2").Literal("stop", "5").Then("body", "p2"),
                N("c", "for_range", "k").Literal("stop", "6").Literal("step", "2").Then("body", "p3"),
                N("d", "for_range", "m").Literal("start", "5").Literal("stop", "0").Literal("step", "-2").Then("body", "p4"));
            f.Add(Print("p1", "gi")).Add(N("gi", "get", "i"));
            f.Add(Print("p2", "gj")).Add(N("gj", "get", "j"));
            f.Add(Print("p3", "gk")).Add(N("gk", "get", "k"));
            f.Add(Print("p4", "gm")).Add(N("gm", "get", "m"));
            var g = Graph(f);
            Assert.That(Source(g), Does.Contain("for i in range(3):").And.Contain("for j in range(2, 5):")
                .And.Contain("for k in range(0, 6, 2):").And.Contain("for m in range(5, 0, -2):"));
            Assert.That(Run(g), Is.EqualTo("0\n1\n2\n2\n3\n4\n0\n2\n4\n5\n3\n1\n"));
        }

        [Test]
        public void ForRangeNeedsAStopAndAName()
        {
            var noStop = Graph(Chain("main", N("a", "for_range", "i")));
            Assert.That(BlockCompiler.Compile(noStop).Errors.Single().Pin, Is.EqualTo("stop"));
            var noName = Graph(Chain("main", N("a", "for_range", "1x").Literal("stop", "3")));
            Assert.That(BlockCompiler.Compile(noName).Errors.Single().Message, Does.Contain("not a valid variable name"));
        }

        [Test]
        public void ValueBlocksContainsIsNoneAndChoose()
        {
            var f = Chain("main",
                N("s", "set", "xs").Connect("value", "list"),
                Print("p1", "has"), Print("p2", "none"), Print("p3", "pick"), Print("p4", "neg"));
            f.Add(N("list", "list").Literal("item0", "1").Literal("item1", "2"));
            f.Add(N("has", "contains").Literal("a", "2").Connect("b", "xs"));
            f.Add(N("xs", "get", "xs"));
            f.Add(N("none", "is_none").Literal("a", "None"));
            f.Add(N("pick", "choose").Connect("cond", "has").Literal("a", "\"yes\"").Literal("b", "\"no\""));
            f.Add(N("neg", "not").Connect("a", "has2"));
            f.Add(N("has2", "contains").Literal("a", "9").Connect("b", "xs"));
            // chain order: s, p1..p4 are linked by Chain in the order given
            var g = Graph(f);
            Assert.That(Source(g), Does.Contain("print(2 in xs)").And.Contain("print(None is None)")
                .And.Contain("print(\"yes\" if (2 in xs) else \"no\")").And.Contain("print(not (9 in xs))"));
            Assert.That(Run(g), Is.EqualTo("True\nTrue\nyes\nTrue\n"));
        }

        [Test]
        public void ChooseAndIsNoneBracketTheirOperandsAndResult()
        {
            var f = Chain("main", Print("p", "sum"));
            f.Add(N("sum", "add").Connect("a", "pick").Literal("b", "1"));
            f.Add(N("pick", "choose").Literal("cond", "True").Literal("a", "1").Connect("b", "alt"));
            f.Add(N("alt", "choose").Literal("cond", "False").Literal("a", "2").Literal("b", "3"));
            Assert.That(Source(Graph(f)), Does.Contain("(1 if True else (2 if False else 3)) + 1"));
            Assert.That(Run(Graph(f)), Is.EqualTo("2\n"));
        }

        [Test]
        public void RemoveAtDeletesAnItem()
        {
            var f = Chain("main",
                N("s", "set", "xs").Connect("value", "list"),
                N("r", "remove_at").Connect("list", "xs").Literal("index", "0"),
                Print("p", "xs"));
            f.Add(N("list", "list").Literal("item0", "1").Literal("item1", "2"));
            f.Add(N("xs", "get", "xs"));
            Assert.That(Run(Graph(f)), Is.EqualTo("[2]\n"));
        }

        [Test]
        public void FieldsOnHostObjectsAndNumbersGenerateValidSource()
        {
            var f = Chain("main", Print("p", "num"), N("w", "set_attr", "x").Literal("object", "1").Literal("value", "2"));
            f.Add(N("num", "get_attr", "real").Literal("object", "5"));
            Assert.That(Source(Graph(f)), Does.Contain("print((5).real)").And.Contain("(1).x = 2"));
        }

        [Test]
        public void FieldNamesMustBeIdentifiers()
        {
            var f = Chain("main", N("w", "set_attr", "class").Literal("object", "1").Literal("value", "2"));
            Assert.That(BlockCompiler.Compile(Graph(f)).Errors.Single().Message, Is.EqualTo("'class' is not a valid field name"));
            var g = Chain("main", Print("p", "r"));
            g.Add(N("r", "get_attr", "a b").Literal("object", "1"));
            Assert.That(BlockCompiler.Compile(Graph(g)).Errors.Single().Message, Does.Contain("field name"));
        }

        [Test]
        public void CommentsBecomePythonCommentsOnTheirOwnLines()
        {
            var f = Chain("main",
                N("c1", "comment", "set things up"),
                PrintLit("p", "1"),
                N("c2", "comment", "two\nlines\r\nand\tcontrol\u0001chars\u2028here"),
                N("c3", "comment", ""));
            var r = BlockCompiler.Compile(Graph(f));
            Assert.That(r.Source, Is.EqualTo("def main():\n    # set things up\n    print(1)\n    # two\n    # lines\n    # and\tcontrol chars here\n    #\n"));
            Assert.That(r.Map.NodeAt(2), Is.EqualTo("c1"));
            Assert.That(r.Map.LinesOf("c2"), Is.EqualTo(new[] { 4, 5, 6 }));
            Assert.That(new PyVm().Compile(r.Source), Is.Not.Null);
        }

        [Test]
        public void ABranchOrFunctionOfOnlyCommentsStillGetsAPass()
        {
            var f = new BlockFunction("main") { Start = "i" };
            f.Add(N("i", "if").Literal("cond", "True").Then("then", "c").Then("else", "c2"));
            f.Add(N("c", "comment", "nothing here"));
            f.Add(N("c2", "comment", "nor here"));
            var only = Chain("only", N("x", "comment", "just a note"));
            var g = new BlockGraph().AddFunction(f).AddFunction(only);
            string source = Source(g);
            Assert.That(source, Does.Contain("    if True:\n        # nothing here\n        pass\n    else:\n        # nor here\n        pass\n"));
            Assert.That(source, Does.Contain("def only():\n    # just a note\n    pass\n"));
        }

        [Test]
        public void CommentsCanBeUsedAsValuesNeverAndAreNotUnconnected()
        {
            var f = Chain("main", Print("p", "c"));
            f.Add(N("c", "comment", "x"));
            Assert.That(BlockCompiler.Compile(Graph(f)).Errors.Single().Message, Does.Contain("has no value"));
        }

        private static BlockNode PrintLit(string id, string literal) => N(id, "call", "print").Literal("arg0", literal);

        [Test]
        public void PaletteDescribesTheNewBlocks()
        {
            var lib = new BlockLibrary();
            foreach (var name in new[] { "contains", "is_none", "choose", "remove_at", "get_attr", "set_attr", "for_range", "comment" })
                Assert.That(lib.FindBlock(name), Is.Not.Null, name);
            Assert.That(lib.FindBlock("for_range").NeedsName, Is.True);
            Assert.That(lib.FindBlock("comment").NeedsName, Is.False);
            Assert.That(lib.FindBlock("get_attr").Kind, Is.EqualTo(BlockKind.Value));
            Assert.That(lib.FindBlock("comment").FlowPins, Is.EqualTo(new[] { "next" }));
        }
    }
}
