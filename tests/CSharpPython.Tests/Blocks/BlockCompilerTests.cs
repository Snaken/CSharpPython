using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    public class BlockCompilerTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        private static BlockGraph Graph(params BlockFunction[] functions)
        {
            var g = new BlockGraph();
            foreach (var f in functions) g.AddFunction(f);
            return g;
        }

        /// <summary>A function whose blocks run in the order given, linked through their next pins.</summary>
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

        private static BlockNode Print(string id, string valueNode) => N(id, "call", "print").Connect("arg0", valueNode);
        private static BlockNode PrintLit(string id, string literal) => N(id, "call", "print").Literal("arg0", literal);
        private static BlockNode Lit(string id, string literal) => N(id, "literal").Literal("value", literal);

        private static string Source(BlockGraph g, BlockLibrary lib = null)
        {
            var r = BlockCompiler.Compile(g, lib);
            Assert.That(r.Errors.Select(e => e.ToString()), Is.Empty);
            Assert.That(r.Source, Is.Not.Null);
            return r.Source;
        }

        private static BlockCompileResult Fail(BlockGraph g, BlockLibrary lib = null)
        {
            var r = BlockCompiler.Compile(g, lib);
            Assert.That(r.Succeeded, Is.False, "expected errors but got:\n" + r.Source);
            Assert.That(r.Source, Is.Null);
            Assert.That(r.Map, Is.Null);
            return r;
        }

        private static BlockDiagnostic OneError(BlockGraph g, BlockLibrary lib = null)
        {
            var r = Fail(g, lib);
            return r.Errors.First();
        }

        /// <summary>Compiles the graph, checks the real compiler accepts the output, runs <c>main</c> and returns what it printed.</summary>
        private static string RunMain(BlockGraph g, BlockLibrary lib = null, System.Action<PyVm> setup = null)
        {
            string source = Source(g, lib);
            return Vm.Py.Run(source + "main()\n", setup);
        }

        [Test]
        public void EmptyGraphIsEmptySource()
        {
            var r = BlockCompiler.Compile(new BlockGraph());
            Assert.That(r.Succeeded, Is.True);
            Assert.That(r.Source, Is.EqualTo(""));
            Assert.That(r.Map.LineCount, Is.EqualTo(0));
        }

        [Test]
        public void NullGraphIsRejected()
        {
            Assert.Throws<System.ArgumentNullException>(() => BlockCompiler.Compile(null));
            Assert.Throws<System.ArgumentNullException>(() => new BlockGraph().AddFunction(null));
        }

        [Test]
        public void FunctionWithNoBlocksDoesNothing()
        {
            Assert.That(Source(Graph(new BlockFunction("tick"))), Is.EqualTo("def tick():\n    pass\n"));
        }

        [Test]
        public void HelloWorldRuns()
        {
            var g = Graph(Chain("main", PrintLit("p", "\"hello\"")));
            Assert.That(Source(g), Is.EqualTo("def main():\n    print(\"hello\")\n"));
            Assert.That(RunMain(g), Is.EqualTo("hello\n"));
        }

        [Test]
        public void GlobalsAreDeclaredAndAssignedWithGlobalStatements()
        {
            var g = new BlockGraph().AddVariable("score", "0").AddVariable("name", null);
            g.AddFunction(Chain("main",
                N("a", "change", "score").Literal("value", "5"),
                N("b", "set", "name").Literal("value", "\"zed\""),
                Print("p1", "g1"), Print("p2", "g2")));
            g.Functions[0].Add(N("g1", "get", "score")).Add(N("g2", "get", "name"));
            Assert.That(Source(g), Is.EqualTo(
                "score = 0\nname = None\n\ndef main():\n    global score, name\n    score += 5\n    name = \"zed\"\n    print(score)\n    print(name)\n"));
            Assert.That(RunMain(g), Is.EqualTo("5\nzed\n"));
        }

        [Test]
        public void ParametersAndLocalsDoNotBecomeGlobals()
        {
            var g = new BlockGraph().AddVariable("x", "1");
            var f = Chain("main", N("a", "set", "x").Literal("value", "9"), Print("p", "gx"));
            f.Parameters.Add("x");
            f.Add(N("gx", "get", "x"));
            g.AddFunction(f);
            string source = Source(g);
            Assert.That(source, Does.Not.Contain("global"));
            Assert.That(Vm.Py.Run(source + "main(3)\nprint(x)\n"), Is.EqualTo("9\n1\n"));
        }

        [Test]
        public void ArithmeticAndComparisonsKeepTheirMeaning()
        {
            // (1 + 2) * 3, -(2 ** 2), not (1 < 2), 10 - (4 - 3), 7 // 2 % 2
            var f = Chain("main",
                Print("p1", "mul"), Print("p2", "neg"), Print("p3", "not"), Print("p4", "sub"), Print("p5", "mod"), Print("p6", "pw"));
            f.Add(N("add", "add").Literal("a", "1").Literal("b", "2"));
            f.Add(N("mul", "mul").Connect("a", "add").Literal("b", "3"));
            f.Add(N("sq", "pow").Literal("a", "2").Literal("b", "2"));
            f.Add(N("neg", "neg").Connect("a", "sq"));
            f.Add(N("lt", "lt").Literal("a", "1").Literal("b", "2"));
            f.Add(N("not", "not").Connect("a", "lt"));
            f.Add(N("inner", "sub").Literal("a", "4").Literal("b", "3"));
            f.Add(N("sub", "sub").Literal("a", "10").Connect("b", "inner"));
            f.Add(N("fd", "floordiv").Literal("a", "7").Literal("b", "2"));
            f.Add(N("mod", "mod").Connect("a", "fd").Literal("b", "2"));
            f.Add(N("pw", "pow").Literal("a", "-2").Literal("b", "2"));
            var g = Graph(f);
            Assert.That(RunMain(g), Is.EqualTo("9\n-4\nFalse\n9\n1\n4\n"));
            Assert.That(Source(g), Does.Contain("(1 + 2) * 3").And.Contain("-(2 ** 2)").And.Contain("not (1 < 2)").And.Contain("(-2) ** 2"));
        }

        [Test]
        public void LogicAndComparisonOperators()
        {
            var f = Chain("main", Print("p1", "a"), Print("p2", "o"), Print("p3", "e"), Print("p4", "n"), Print("p5", "ge"));
            f.Add(N("a", "and").Literal("a", "True").Literal("b", "False"));
            f.Add(N("o", "or").Connect("a", "a").Literal("b", "True"));
            f.Add(N("e", "eq").Literal("a", "3").Literal("b", "3"));
            f.Add(N("n", "ne").Literal("a", "\"x\"").Literal("b", "\"x\""));
            f.Add(N("ge", "ge").Literal("a", "2.5").Literal("b", "2"));
            Assert.That(RunMain(Graph(f)), Is.EqualTo("False\nTrue\nTrue\nFalse\nTrue\n"));
        }

        [Test]
        public void IfBranchesAndNextAfterTheIf()
        {
            var f = new BlockFunction("main", "n");
            f.Add(N("if", "if").Connect("cond", "gt").Then("then", "yes").Then("else", "no").Then("after"));
            f.Add(N("gt", "gt").Connect("a", "n").Literal("b", "5"));
            f.Add(N("n", "get", "n"));
            f.Add(PrintLit("yes", "\"big\""));
            f.Add(PrintLit("no", "\"small\""));
            f.Add(PrintLit("after", "\"done\""));
            f.Start = "if";
            string source = Source(Graph(f));
            Assert.That(source, Is.EqualTo(
                "def main(n):\n    if n > 5:\n        print(\"big\")\n    else:\n        print(\"small\")\n    print(\"done\")\n"));
            Assert.That(Vm.Py.Run(source + "main(9)\nmain(1)\n"), Is.EqualTo("big\ndone\nsmall\ndone\n"));
        }

        [Test]
        public void EmptyBranchesBecomePass()
        {
            var f = new BlockFunction("main");
            f.Add(N("if", "if").Literal("cond", "True").Then("else", "no"));
            f.Add(PrintLit("no", "\"x\""));
            f.Start = "if";
            Assert.That(Source(Graph(f)), Is.EqualTo("def main():\n    if True:\n        pass\n    else:\n        print(\"x\")\n"));
        }

        [Test]
        public void WhileLoopWithBreakAndContinue()
        {
            var g = new BlockGraph().AddVariable("i", "0");
            var f = new BlockFunction("main");
            f.Add(N("w", "while").Connect("cond", "lt").Then("body", "inc").Then("end"));
            f.Add(N("lt", "lt").Connect("a", "gi").Literal("b", "10"));
            f.Add(N("gi", "get", "i"));
            f.Add(N("inc", "change", "i").Literal("value", "1").Then("skip"));
            f.Add(N("skip", "if").Connect("cond", "odd").Then("then", "cont").Then("next", "stop"));
            f.Add(N("odd", "eq").Connect("a", "m").Literal("b", "1"));
            f.Add(N("m", "mod").Connect("a", "gi").Literal("b", "2"));
            f.Add(N("cont", "continue"));
            f.Add(N("stop", "if").Connect("cond", "big").Then("then", "brk").Then("next", "show"));
            f.Add(N("big", "ge").Connect("a", "gi").Literal("b", "6"));
            f.Add(N("brk", "break"));
            f.Add(Print("show", "gi"));
            f.Add(PrintLit("end", "\"end\""));
            f.Start = "w";
            g.AddFunction(f);
            Assert.That(RunMain(g), Is.EqualTo("2\n4\nend\n"));
        }

        [Test]
        public void RepeatUsesAnOptionalCounterVariable()
        {
            var f = Chain("main", N("r", "repeat", "k").Literal("count", "3").Then("body", "p"));
            f.Add(Print("p", "gk"));
            f.Add(N("gk", "get", "k"));
            Assert.That(RunMain(Graph(f)), Is.EqualTo("0\n1\n2\n"));

            var anon = new BlockFunction("main");
            anon.Add(N("r", "repeat").Literal("count", "2").Then("body", "p"));
            anon.Add(PrintLit("p", "\"hi\""));
            anon.Start = "r";
            string source = Source(Graph(anon));
            Assert.That(source, Does.Contain("for _b1 in range(2):"));
            Assert.That(RunMain(Graph(anon)), Is.EqualTo("hi\nhi\n"));
        }

        [Test]
        public void ForEachOverAListBlock()
        {
            var f = new BlockFunction("main");
            f.Add(N("fe", "for_each", "item").Connect("items", "xs").Then("body", "p"));
            f.Add(N("xs", "list").Literal("item0", "\"a\"").Literal("item1", "\"b\"").Literal("item2", "-3"));
            f.Add(Print("p", "gi"));
            f.Add(N("gi", "get", "item"));
            f.Start = "fe";
            var g = Graph(f);
            Assert.That(Source(g), Does.Contain("for item in [\"a\", \"b\", -3]:"));
            Assert.That(RunMain(g), Is.EqualTo("a\nb\n-3\n"));
        }

        [Test]
        public void ListIndexAppendAndSetIndex()
        {
            var g = new BlockGraph().AddVariable("scores", null);
            var f = Chain("main",
                N("init", "set", "scores").Connect("value", "mk"),
                N("ap", "append").Connect("list", "gs").Literal("value", "7"),
                N("si", "set_index").Connect("list", "gs").Literal("index", "0").Literal("value", "99"),
                Print("p1", "ix"), Print("p2", "len"));
            f.Add(N("mk", "list").Literal("item0", "1").Literal("item1", "2"));
            f.Add(N("gs", "get", "scores"));
            f.Add(N("ix", "index").Connect("list", "gs").Literal("index", "-1"));
            f.Add(N("len", "call", "len").Connect("arg0", "gs"));
            g.AddFunction(f);
            Assert.That(RunMain(g), Is.EqualTo("7\n3\n"));
        }

        [Test]
        public void PureCallsAreInlineAndFlowCallsKeepTheirResult()
        {
            var lib = new BlockLibrary().AddFunction("roll", new[] { "sides" }, module: "dice");
            var f = Chain("main", N("r", "call", "dice.roll").Literal("arg0", "6"), Print("p1", "r"), Print("p2", "r"), Print("p3", "mx"));
            f.Add(N("mx", "call", "max").Connect("arg0", "r").Literal("arg1", "3"));
            var g = Graph(f);
            string source = Source(g, lib);
            Assert.That(source, Is.EqualTo(
                "import dice\n\ndef main():\n    _b1 = dice.roll(6)\n    print(_b1)\n    print(_b1)\n    print(max(_b1, 3))\n"));
            int calls = 0;
            string output = RunMain(g, lib, vm => vm.RegisterModule("dice", m => m.Function("roll", (Value sides) => Value.FromInt(++calls * 10))));
            Assert.That(output, Is.EqualTo("10\n10\n10\n"));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void FlowCallWithUnusedResultIsAStatement()
        {
            var lib = new BlockLibrary().AddFunction("ping", System.Array.Empty<string>(), module: "net");
            var f = Chain("main", N("c", "call", "net.ping"));
            Assert.That(Source(Graph(f), lib), Is.EqualTo("import net\n\ndef main():\n    net.ping()\n"));
        }

        [Test]
        public void EachModuleIsImportedOnceInFirstUseOrder()
        {
            var lib = new BlockLibrary()
                .AddFunction("b1", System.Array.Empty<string>(), module: "beta").AddFunction("b2", System.Array.Empty<string>(), module: "beta").AddFunction("a1", System.Array.Empty<string>(), module: "alpha");
            var f = Chain("main", N("x", "call", "beta.b1"), N("y", "call", "alpha.a1"), N("z", "call", "beta.b2"));
            Assert.That(Source(Graph(f), lib), Does.StartWith("import beta\nimport alpha\n\ndef main():"));
        }

        [Test]
        public void UserBlocksAreFunctionsCalledThroughCallBlocks()
        {
            var add = new BlockFunction("add_twice", "a", "b");
            add.Add(N("s", "add").Connect("a", "ga").Connect("b", "gb"));
            add.Add(N("ga", "get", "a")).Add(N("gb", "get", "b"));
            add.Add(N("d", "mul").Connect("a", "s").Literal("b", "2"));
            add.Add(N("ret", "return").Connect("value", "d"));
            add.Start = "ret";

            var main = Chain("main",
                N("c", "call", "add_twice").Literal("arg0", "1").Literal("arg1", "2"),
                Print("p", "c"));
            var g = Graph(add, main);
            Assert.That(RunMain(g), Is.EqualTo("6\n"));
            Assert.That(Source(g), Does.Contain("def add_twice(a, b):\n    return (a + b) * 2\n"));
        }

        [Test]
        public void RecursionThroughUserBlocks()
        {
            var fact = new BlockFunction("fact", "n");
            fact.Add(N("if", "if").Connect("cond", "le").Then("then", "one").Then("next", "rec"));
            fact.Add(N("le", "le").Connect("a", "n").Literal("b", "1"));
            fact.Add(N("n", "get", "n"));
            fact.Add(N("one", "return").Literal("value", "1"));
            fact.Add(N("rec", "call", "fact").Connect("arg0", "dec").Then("out"));
            fact.Add(N("dec", "sub").Connect("a", "n").Literal("b", "1"));
            fact.Add(N("out", "return").Connect("value", "prod"));
            fact.Add(N("prod", "mul").Connect("a", "n").Connect("b", "rec"));
            fact.Start = "if";
            var main = Chain("main", N("c", "call", "fact").Literal("arg0", "5"), Print("p", "c"));
            Assert.That(RunMain(Graph(fact, main)), Is.EqualTo("120\n"));
        }

        [Test]
        public void ReturnWithoutAValueEndsTheFunction()
        {
            var f = Chain("main", PrintLit("a", "1"), N("r", "return"), PrintLit("never", "2"));
            // "never" hangs off the return block's next pin, which a return block does not have.
            var g = Graph(f);
            Assert.That(OneError(g).Pin, Is.EqualTo("next"));
        }

        [Test]
        public void EventHandlersAreJustNamedFunctions()
        {
            var g = new BlockGraph().AddVariable("hp", "100");
            g.AddFunction(Chain("on_hit", N("a", "change", "hp").Literal("value", "-10"), Print("p", "g")));
            g.Functions[0].Add(N("g", "get", "hp"));
            var vm = new PyVm { Output = s => { } };
            var sb = new System.Text.StringBuilder();
            vm.Output = s => sb.Append(s);
            var task = vm.Start(Source(g));
            task.RunUntilDone();
            task.QueueEvent("on_hit");
            task.QueueEvent("on_hit");
            task.RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("90\n80\n"));
        }

        // ---- Source map ----

        [Test]
        public void SourceMapPointsLinesAtBlocks()
        {
            var g = new BlockGraph().AddVariable("n", "1");
            var f = new BlockFunction("main");
            f.Add(N("if", "if").Connect("cond", "t").Then("then", "a").Then("else", "b").Then("c"));
            f.Add(N("t", "literal").Literal("value", "True"));
            f.Add(PrintLit("a", "1")).Add(PrintLit("b", "2")).Add(PrintLit("c", "3"));
            f.Start = "if";
            g.AddFunction(f);
            var r = BlockCompiler.Compile(g);
            // 1 n = 1 / 2 blank / 3 def / 4 if / 5 print a / 6 else / 7 print b / 8 print c
            Assert.That(r.Map.LineCount, Is.EqualTo(8));
            Assert.That(Enumerable.Range(1, 8).Select(r.Map.NodeAt), Is.EqualTo(new string[] { null, null, null, "if", "a", "if", "b", "c" }));
            Assert.That(r.Map.LinesOf("if"), Is.EqualTo(new[] { 4, 6 }));
            Assert.That(r.Map.LinesOf("t"), Is.Empty);
            Assert.That(r.Map.NodeAt(0), Is.Null);
            Assert.That(r.Map.NodeAt(99), Is.Null);
        }

        [Test]
        public void RuntimeErrorsAreLocatedOnTheBlock()
        {
            var inner = Chain("boom", N("s", "set", "x").Connect("value", "d"));
            inner.Add(N("d", "div").Literal("a", "1").Literal("b", "0"));
            var main = Chain("main", PrintLit("p", "\"go\""), N("c", "call", "boom"));
            var g = Graph(inner, main);
            var r = BlockCompiler.Compile(g);
            Assert.That(r.Succeeded, Is.True);
            var vm = new PyVm { Output = s => { } };
            var task = vm.Start(r.Source + "main()\n");
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Error));
            Assert.That(r.Map.Locate(task.Error), Is.EqualTo("s"));
            Assert.Throws<System.ArgumentNullException>(() => r.Map.Locate(null));
        }

        [Test]
        public void LocateFallsBackToOuterFramesAndNull()
        {
            var r = BlockCompiler.Compile(Graph(new BlockFunction("f")));
            var vm = new PyVm { Output = s => { } };
            var task = vm.Start("raise ValueError('x')\n");
            task.RunUntilDone();
            Assert.That(r.Map.Locate(task.Error), Is.Null);
        }

        // ---- Determinism and the real compiler ----

        [Test]
        public void SameGraphGivesSameSourceEveryTime()
        {
            var g = Graph(Chain("main", PrintLit("p", "\"x\"")));
            Assert.That(Source(g), Is.EqualTo(Source(g)));
        }

        [Test]
        public void LiteralHelpersProduceValidLiterals()
        {
            Assert.That(BlockLiterals.Int(-5), Is.EqualTo("-5"));
            Assert.That(BlockLiterals.Bool(true), Is.EqualTo("True"));
            Assert.That(BlockLiterals.Bool(false), Is.EqualTo("False"));
            Assert.That(BlockLiterals.None, Is.EqualTo("None"));
            Assert.That(BlockLiterals.Float(2), Is.EqualTo("2.0"));
            Assert.That(BlockLiterals.Float(-0.25), Is.EqualTo("-0.25"));
            Assert.Throws<System.ArgumentException>(() => BlockLiterals.Float(double.NaN));
            Assert.Throws<System.ArgumentException>(() => BlockLiterals.Float(double.PositiveInfinity));
            Assert.That(BlockLiterals.String(null), Is.EqualTo("\"\""));
        }

        [TestCase("plain")]
        [TestCase("with \"quotes\" and \\ backslash")]
        [TestCase("line1\nline2\r\ttab")]
        [TestCase("bell\u0007 and é中")]
        public void StringLiteralsReadBackExactly(string text)
        {
            var f = Chain("main", PrintLit("p", BlockLiterals.String(text)));
            Assert.That(RunMain(Graph(f)), Is.EqualTo(text + "\n"));
        }

        [TestCase(1e20)]
        [TestCase(0.1)]
        [TestCase(-3.5)]
        [TestCase(123456789.125)]
        public void FloatLiteralsReadBackExactly(double d)
        {
            var f = Chain("main", PrintLit("p", BlockLiterals.Float(d)));
            string source = Source(Graph(f));
            var vm = new PyVm { Output = s => { } };
            Assert.That(vm.Compile(source), Is.Not.Null);
        }

        [Test]
        public void EveryDiagnosticTextMentionsTheProblem()
        {
            var d = new BlockDiagnostic(BlockSeverity.Warning, "careful", "f", "n1", "pin");
            Assert.That(d.ToString(), Is.EqualTo("Warning [n1.pin]: careful"));
            Assert.That(new BlockDiagnostic(BlockSeverity.Error, "bad").ToString(), Is.EqualTo("Error: bad"));
        }

        [Test]
        public void ReadmeExampleWorks()
        {
            var graph = new BlockGraph().AddVariable("hp", "100");
            var onHit = new BlockFunction("on_hit", "amount") { Start = "sub" };
            onHit.Add(new BlockNode("sub", "change", "hp").Connect("value", "neg").Then("dead"));
            onHit.Add(new BlockNode("neg", "neg").Connect("a", "amount"));
            onHit.Add(new BlockNode("amount", "get", "amount"));
            onHit.Add(new BlockNode("dead", "if").Connect("cond", "low").Then("then", "boom"));
            onHit.Add(new BlockNode("low", "le").Connect("a", "hp").Literal("b", "0"));
            onHit.Add(new BlockNode("hp", "get", "hp"));
            onHit.Add(new BlockNode("boom", "call", "game.explode"));
            graph.AddFunction(onHit);
            var library = new BlockLibrary().AddFunction("explode", System.Array.Empty<string>(), module: "game");
            var result = BlockCompiler.Compile(graph, library);
            Assert.That(result.Diagnostics, Is.Empty);

            var sb = new System.Text.StringBuilder();
            var vm = new PyVm { Output = s => sb.Append(s) };
            vm.RegisterModule("game", m => m.Action("explode", () => sb.Append("boom\n")));
            var task = vm.Start(result.Source);
            task.RunUntilDone();
            task.QueueEvent("on_hit", Value.FromInt(60));
            task.QueueEvent("on_hit", Value.FromInt(60));
            task.RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("boom\n"));
            Assert.That(task.Globals["hp"].AsInt, Is.EqualTo(-20));
        }
    }
}
