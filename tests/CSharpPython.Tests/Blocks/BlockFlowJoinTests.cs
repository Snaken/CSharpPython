using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // Two execution wires may enter one block; the flow from it on is written once, as a local function
    // (docs/node-view-design.md, decision 13).
    public class BlockFlowJoinTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);
        private static BlockNode Say(string id, string text) => N(id, "call", "print").Literal("arg0", "\"" + text + "\"");
        private static BlockNode Set(string id, string name, string literal) => N(id, "set", name).Literal("value", literal);
        private static BlockNode Get(string id, string name) => N(id, "get", name);
        private static BlockNode Show(string id, string from) => N(id, "call", "print").Connect("arg0", from);

        // "if cond then A else B", with the blocks given after it.
        private static BlockNode If(string id, string cond, string then, string other) =>
            N(id, "if").Literal("cond", cond).Then("then", then).Then("else", other);

        private static BlockFunction Fn(string start, params BlockNode[] nodes)
        {
            var f = new BlockFunction("main") { Start = start };
            foreach (var n in nodes) f.Add(n);
            return f;
        }

        private static BlockCompileResult Compile(params BlockFunction[] fns)
        {
            var g = new BlockGraph();
            foreach (var f in fns) g.AddFunction(f);
            return BlockCompiler.Compile(g);
        }

        private static string Source(params BlockFunction[] fns)
        {
            var r = Compile(fns);
            Assert.That(r.Errors.Select(e => e.ToString()), Is.Empty);
            return r.Source;
        }

        private static string Run(params BlockFunction[] fns) => Vm.Py.Run(Source(fns) + "main()\n");

        private static string FirstError(BlockFunction f, params BlockFunction[] more) => FirstErrorOf(new[] { f }.Concat(more).ToArray());

        private static string FirstErrorOf(BlockFunction[] fns)
        {
            var r = Compile(fns);
            Assert.That(r.Succeeded, Is.False, "expected an error but got:\n" + r.Source);
            return r.Errors.First().Message;
        }

        // The usual shape: a choice, two paths, and a shared "C" after them.
        private static BlockFunction Diamond(string cond) => Fn("i",
            If("i", cond, "a", "b"),
            Say("a", "A").Then("c"), Say("b", "B").Then("c"), Say("c", "C"));

        // ---- The shape of the code ----

        [Test]
        public void TheFlowAfterAJoinIsWrittenOnceAndCalledFromEachPath()
        {
            string source = Source(Diamond("True"));
            Assert.That(source, Is.EqualTo(
                "def main():\n" +
                "    def _join1():\n" +
                "        print(\"C\")\n" +
                "    if True:\n" +
                "        print(\"A\")\n" +
                "        _join1()\n" +
                "    else:\n" +
                "        print(\"B\")\n" +
                "        _join1()\n"));
        }

        [Test]
        public void BothPathsReachTheBlockAfterTheJoin()
        {
            Assert.That(Run(Diamond("True")), Is.EqualTo("A\nC\n"));
            Assert.That(Run(Diamond("False")), Is.EqualTo("B\nC\n"));
        }

        [Test]
        public void LinesOfTheTailBelongToItsBlocks()
        {
            var r = Compile(Diamond("True"));
            Assert.That(r.Map.NodeAt(3), Is.EqualTo("c"));
            Assert.That(r.Map.NodeAt(2), Is.Null, "the def line belongs to no block");
            Assert.That(r.Map.LinesOf("c"), Has.Count.EqualTo(3), "the tail and its two calls");
        }

        [Test]
        public void ABlockWithOneWireIsWrittenInPlace()
        {
            var f = Fn("a", Say("a", "A").Then("b"), Say("b", "B"));
            Assert.That(Source(f), Does.Not.Contain("_join"));
        }

        [Test]
        public void TheFlowRunsOnAfterTheJoinReturns()
        {
            // The if has its own next, which runs after the branches, and their joined tail, finish.
            var f = Fn("i",
                If("i", "False", "a", "b").Then("z"),
                Say("a", "A").Then("c"), Say("b", "B").Then("c"), Say("c", "C"), Say("z", "Z"));
            Assert.That(Run(f), Is.EqualTo("B\nC\nZ\n"));
        }

        [Test]
        public void ThreeWiresIntoOneBlock()
        {
            var f = Fn("i",
                If("i", "True", "a", "j"),
                N("j", "if").Literal("cond", "False").Then("then", "b").Then("else", "d"),
                Say("a", "A").Then("c"), Say("b", "B").Then("c"), Say("d", "D").Then("c"), Say("c", "C"));
            Assert.That(Run(f), Is.EqualTo("A\nC\n"));
            Assert.That(Source(f), Does.Contain("def _join1"));
            Assert.That(Source(f), Does.Not.Contain("_join2"));
        }

        [Test]
        public void AWireFromABlockThatNeverRunsStillMakesAJoin()
        {
            var f = Fn("a", Say("a", "A").Then("c"), Say("c", "C"), Say("x", "X").Then("c"));
            Assert.That(Run(f), Is.EqualTo("A\nC\n"));
        }

        [Test]
        public void JoinsInOneAnotherWork()
        {
            // a -> m <- b, then m -> n <- x: the tail of the first join ends in the second.
            var f = Fn("i",
                If("i", "True", "a", "b"),
                Say("a", "A").Then("m"), Say("b", "B").Then("m"),
                If("m", "True", "x", "n"),
                Say("x", "X").Then("n"), Say("n", "N"));
            Assert.That(Run(f), Is.EqualTo("A\nX\nN\n"));
            f = Fn("i",
                If("i", "False", "a", "b"),
                Say("a", "A").Then("m"), Say("b", "B").Then("m"),
                If("m", "False", "x", "n"),
                Say("x", "X").Then("n"), Say("n", "N"));
            Assert.That(Run(f), Is.EqualTo("B\nN\n"));
        }

        // ---- Variables ----

        [Test]
        public void AVariableSetOnEachPathCanBeReadAfterTheJoin()
        {
            var f = Fn("i",
                If("i", "False", "a", "b"),
                Set("a", "x", "1").Then("c"), Set("b", "x", "2").Then("c"), Show("c", "g"), Get("g", "x"));
            Assert.That(Run(f), Is.EqualTo("2\n"));
            Assert.That(Source(f), Does.Not.Contain("nonlocal"), "the tail only reads x");
        }

        [Test]
        public void AVariableTheTailChangesIsNonlocal()
        {
            var f = Fn("s",
                Set("s", "x", "10").Then("i"),
                If("i", "True", "a", "b"),
                Say("a", "A").Then("c"), Say("b", "B").Then("c"),
                N("c", "change", "x").Literal("value", "5").Then("p"), Show("p", "g"), Get("g", "x"));
            Assert.That(Run(f), Is.EqualTo("A\n15\n"));
            Assert.That(Source(f), Does.Contain("nonlocal x"));
        }

        [Test]
        public void TheTailCanChangeAVariableThatTheFlowAfterTheIfReads()
        {
            var f = Fn("s",
                Set("s", "x", "1").Then("i"),
                If("i", "True", "a", "b").Then("p"),
                Say("a", "A").Then("c"), Say("b", "B").Then("c"),
                Set("c", "x", "2"), Show("p", "g"), Get("g", "x"));
            Assert.That(Run(f), Is.EqualTo("A\n2\n"));
        }

        [Test]
        public void AParameterTheTailAssignsIsNonlocal()
        {
            var f = new BlockFunction("main", "x") { Start = "i" };
            foreach (var n in new[]
            {
                If("i", "True", "a", "b"), Say("a", "A").Then("c"), Say("b", "B").Then("c"),
                Set("c", "x", "9").Then("p"), Show("p", "g"), Get("g", "x"),
            }) f.Add(n);
            string source = Source(f);
            Assert.That(source, Does.Contain("nonlocal x"));
            Assert.That(Vm.Py.Run(source + "main(1)\n"), Is.EqualTo("A\n9\n"));
        }

        [Test]
        public void AGlobalTheTailAssignsIsDeclaredGlobalInIt()
        {
            var g = new BlockGraph();
            g.Variables.Add(new BlockVariable("total", "0"));
            g.AddFunction(Fn("i",
                If("i", "True", "a", "b"), Say("a", "A").Then("c"), Say("b", "B").Then("c"),
                N("c", "change", "total").Literal("value", "4")));
            var r = BlockCompiler.Compile(g);
            Assert.That(r.Errors.Select(e => e.ToString()), Is.Empty);
            Assert.That(r.Source, Does.Contain("        global total\n"));
            Assert.That(Vm.Py.Run(r.Source + "main()\nprint(total)\n"), Is.EqualTo("A\n4\n"));
        }

        [Test]
        public void AVariableOnlyTheTailUsesStaysLocalToIt()
        {
            var f = Fn("i",
                If("i", "True", "a", "b"), Say("a", "A").Then("c"), Say("b", "B").Then("c"),
                Set("c", "y", "3").Then("p"), Show("p", "g"), Get("g", "y"));
            Assert.That(Run(f), Is.EqualTo("A\n3\n"));
            Assert.That(Source(f), Does.Not.Contain("nonlocal"));
        }

        [Test]
        public void ANameTheTailSetsAndTheFlowAfterTheIfReadsIsAnError()
        {
            var f = Fn("i",
                If("i", "True", "a", "b").Then("p"),
                Say("a", "A").Then("c"), Say("b", "B").Then("c"),
                Set("c", "y", "3"), Show("p", "g"), Get("g", "y"));
            Assert.That(FirstError(f), Does.Contain("'y' is first set after a join"));
        }

        // ---- Return ----

        [Test]
        public void AReturnInTheTailLeavesTheFunction()
        {
            var inner = new BlockFunction("pick") { Start = "i" };
            foreach (var n in new[]
            {
                If("i", "True", "a", "b").Then("z"), Say("a", "A").Then("c"), Say("b", "B").Then("c"),
                N("c", "return").Literal("value", "7"), Say("z", "after"),
            }) inner.Add(n);
            var main = Fn("k", N("k", "call", "pick").Then("p"), Show("p", "k"));
            string source = Source(main, inner);
            Assert.That(Vm.Py.Run(source + "main()\n"), Is.EqualTo("A\n7\n"), "nothing runs after the return");
            Assert.That(source, Does.Contain("return (7,)"));
            Assert.That(source, Does.Contain("is not None"));
        }

        [Test]
        public void AFunctionWithoutAReturnInTheTailCallsItPlain()
        {
            Assert.That(Source(Diamond("True")), Does.Not.Contain("is not None"));
        }

        [Test]
        public void AReturnPassesThroughNestedTails()
        {
            var f = new BlockFunction("pick") { Start = "i" };
            foreach (var n in new[]
            {
                If("i", "True", "a", "b"), Say("a", "A").Then("m"), Say("b", "B").Then("m"),
                If("m", "True", "x", "n").Then("late"),
                Say("x", "X").Then("n"), N("n", "return").Literal("value", "5"), Say("late", "late"),
            }) f.Add(n);
            Assert.That(Vm.Py.Run(Source(f) + "print(pick())\n"), Is.EqualTo("A\nX\n5\n"));
        }

        [Test]
        public void ABareReturnInTheTailReturnsNone()
        {
            var f = new BlockFunction("pick") { Start = "i" };
            foreach (var n in new[]
            {
                If("i", "True", "a", "b").Then("late"), Say("a", "A").Then("c"), Say("b", "B").Then("c"),
                N("c", "return"), Say("late", "late"),
            }) f.Add(n);
            Assert.That(Vm.Py.Run(Source(f) + "print(pick())\n"), Is.EqualTo("A\nNone\n"));
        }

        // ---- Loops ----

        [Test]
        public void AJoinInsideALoopBodyRunsOnEachPass()
        {
            var f = Fn("l",
                N("l", "for_range", "i").Literal("stop", "3").Then("body", "t"),
                If("t", "True", "a", "b"),
                Say("a", "A").Then("c"), Say("b", "B").Then("c"), Show("c", "g"), Get("g", "i"));
            Assert.That(Run(f), Is.EqualTo("A\n0\nA\n1\nA\n2\n"));
        }

        [Test]
        public void BreakAfterAJoinInALoopIsAnError()
        {
            var f = Fn("l",
                N("l", "while").Literal("cond", "True").Then("body", "t"),
                If("t", "True", "a", "b"), Say("a", "A").Then("c"), Say("b", "B").Then("c"), N("c", "break"));
            Assert.That(FirstError(f), Does.Contain("cannot come after a join"));
        }

        [Test]
        public void ALoopInsideTheTailMayUseBreak()
        {
            var f = Fn("i",
                If("i", "True", "a", "b"), Say("a", "A").Then("w"), Say("b", "B").Then("w"),
                N("w", "while").Literal("cond", "True").Then("body", "k"), N("k", "break"));
            Assert.That(Run(f), Is.EqualTo("A\n"));
        }

        [Test]
        public void AJoinEnteredFromInsideAndOutsideALoopIsAnError()
        {
            var f = Fn("l",
                N("l", "for_range", "i").Literal("stop", "2").Then("body", "x").Then("c"),
                Say("x", "X").Then("c"), Say("c", "C"));
            Assert.That(FirstError(f), Does.Contain("inside a loop and from outside"));
        }

        // ---- Results and loops back ----

        // A function of the graph that returns 3; its call is part of the flow and its result is a stored value.
        private static BlockFunction Three()
        {
            var f = new BlockFunction("three") { Start = "r" };
            f.Add(N("r", "return").Literal("value", "3"));
            return f;
        }

        [Test]
        public void AResultNotAvailableOnEveryPathIsAnError()
        {
            var f = Fn("i",
                If("i", "True", "a", "b"),
                N("a", "call", "three").Then("c"), Say("b", "B").Then("c"), Show("c", "a"));
            Assert.That(FirstError(f, Three()), Does.Contain("has not run on every path"));
        }

        [Test]
        public void AResultFromBeforeTheSplitCanBeUsedAfterTheJoin()
        {
            var f = Fn("k",
                N("k", "call", "three").Then("i"),
                If("i", "False", "a", "b"), Say("a", "A").Then("c"), Say("b", "B").Then("c"), Show("c", "k"));
            Assert.That(Run(f, Three()), Is.EqualTo("B\n3\n"));
        }

        [Test]
        public void FlowStillCannotLoopBack()
        {
            var f = Fn("a", Say("a", "A").Then("b"), Say("b", "B").Then("a"));
            Assert.That(FirstError(f), Does.Contain("cannot loop back"));
        }

        [Test]
        public void ATailThatWouldCallItselfIsAnError()
        {
            var f = Fn("i",
                If("i", "True", "a", "b"), Say("a", "A").Then("c"), Say("b", "B").Then("c"), Say("c", "C").Then("a"));
            Assert.That(FirstError(f), Does.Contain("cannot loop back"));
        }

        [Test]
        public void ABlockEnteredFromTheRootAndFromInsideATailIsAnError()
        {
            // 'd' follows the if itself (in the function) and 'c', which is the tail of a join.
            var f = Fn("i",
                If("i", "True", "a", "b").Then("d"),
                Say("a", "A").Then("c"), Say("b", "B").Then("c"), Say("c", "C").Then("d"), Say("d", "D"));
            Assert.That(FirstError(f), Does.Contain("do not start in the same place"));
        }

        // ---- Names and shared values ----

        [Test]
        public void TheNamesOfTailsAreReserved()
        {
            var f = Fn("a", Say("a", "A"));
            f.Name = "_join1";
            Assert.That(FirstError(f), Does.Contain("not a valid function name"));
            var g = new BlockGraph();
            g.Variables.Add(new BlockVariable("_join3", "0"));
            g.AddFunction(Fn("a", Say("a", "A")));
            Assert.That(BlockCompiler.Compile(g).Errors.First().Message, Does.Contain("not a valid variable name"));
        }

        [Test]
        public void ASharedValueIsWorkedOutAgainInsideTheTail()
        {
            var f = Fn("s",
                Set("s", "x", "4").Then("p"),
                Show("p", "sum").Then("i"),
                If("i", "True", "a", "b"), Say("a", "A").Then("c"), Say("b", "B").Then("c"),
                Show("c", "sum"),
                N("sum", "add").Connect("a", "g").Literal("b", "1"), Get("g", "x"));
            string source = Source(f);
            Assert.That(Run(f), Is.EqualTo("5\nA\n5\n"));
            Assert.That(source, Does.Contain("_b1 = x + 1"));
            Assert.That(source, Does.Contain("_b2 = x + 1"), "the tail does not reuse a value worked out before it");
        }
    }
}
