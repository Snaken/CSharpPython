using System;
using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // A pure block that several inputs read is worked out once per flow step (docs/node-view-design.md, decision 11).
    public class BlockSharedNodesTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);
        private static BlockNode Print(string id, string from) => N(id, "call", "print").Connect("arg0", from);
        private static BlockNode Set(string id, string name, string literal) => N(id, "set", name).Literal("value", literal);

        // "x + 1", the block most tests share.
        private static BlockNode[] Plus1(string id) =>
            new[] { N(id, "add").Connect("a", id + "_x").Literal("b", "1"), N(id + "_x", "get", "x") };

        // The blocks run in the order given; the rest are value blocks.
        private static BlockFunction Fn(string[] flow, params BlockNode[] all)
        {
            var f = new BlockFunction("main") { Start = flow[0] };
            foreach (var n in all) f.Add(n);
            for (int i = 0; i + 1 < flow.Length; i++) all.First(n => n.Id == flow[i]).Then(flow[i + 1]);
            return f;
        }

        private static BlockNode[] With(BlockNode[] more, params BlockNode[] nodes) => nodes.Concat(more).ToArray();

        private static void Setup(PyVm vm)
        {
            vm.RegisterModule("game", m => m
                .Native("wait", (c, a) => Value.None, 0, 0)
                .Native("noop", (c, a) => Value.None, 0, 0)
                .Describe("wait", "", latent: true)
                .Describe("noop", ""));
        }

        private static BlockLibrary Library()
        {
            var vm = new PyVm();
            Setup(vm);
            return new BlockLibrary().AddAllModules(vm);
        }

        private static string Source(BlockFunction f, params BlockFunction[] others)
        {
            var g = new BlockGraph().AddFunction(f);
            foreach (var o in others) g.AddFunction(o);
            var r = BlockCompiler.Compile(g, Library());
            Assert.That(r.Errors.Select(e => e.ToString()), Is.Empty);
            return r.Source;
        }

        private static string Run(BlockFunction f, params BlockFunction[] others) =>
            Vm.Py.Run(Source(f, others) + "main()\n", Setup);

        private static int Count(string text, string part)
        {
            int n = 0;
            for (int i = text.IndexOf(part, StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + 1, StringComparison.Ordinal)) n++;
            return n;
        }

        // ---- What is shared ----

        [Test]
        public void ABlockReadTwiceIsWorkedOutOnceBeforeTheFirstStatementThatNeedsIt()
        {
            var f = Fn(new[] { "s", "p1", "p2" }, With(Plus1("p"), Set("s", "x", "4"), Print("p1", "p"), Print("p2", "p")));
            Assert.That(Source(f), Does.Contain("x = 4\n    _b1 = x + 1\n    print(_b1)\n    print(_b1)\n"));
            Assert.That(Run(f), Is.EqualTo("5\n5\n"));
        }

        [Test]
        public void ABlockReadOnceStaysInline()
        {
            var f = Fn(new[] { "s", "p1" }, With(Plus1("p"), Set("s", "x", "4"), Print("p1", "p")));
            Assert.That(Source(f), Does.Contain("print(x + 1)").And.Not.Contain("_b"));
        }

        [Test]
        public void TheSameBlockOnTwoPinsOfOneStatementIsAlsoShared()
        {
            var f = Fn(new[] { "s", "p1" }, With(Plus1("p"), Set("s", "x", "4"),
                N("both", "mul").Connect("a", "p").Connect("b", "p"), Print("p1", "both")));
            Assert.That(Source(f), Does.Contain("_b1 = x + 1\n    print(_b1 * _b1)"));
            Assert.That(Run(f), Is.EqualTo("25\n"));
        }

        [Test]
        public void VariableReadsAndLiteralsAreNeverShared()
        {
            var f = Fn(new[] { "s", "p1", "p2", "p3", "p4" }, Set("s", "x", "4"), Print("p1", "gx"), Print("p2", "gx"),
                Print("p3", "lit"), Print("p4", "lit"), N("gx", "get", "x"), N("lit", "literal").Literal("value", "7"));
            Assert.That(Source(f), Does.Not.Contain("_b"));
        }

        [Test]
        public void APureHostCallIsSharedToo()
        {
            var f = Fn(new[] { "p1", "p2" }, N("root", "call", "math.sqrt").Literal("arg0", "16"), Print("p1", "root"), Print("p2", "root"));
            Assert.That(Source(f), Does.Contain("_b1 = math.sqrt(16)"));
            Assert.That(Run(f), Is.EqualTo("4.0\n4.0\n"));
        }

        [Test]
        public void SharedBlocksThatReadSharedBlocksAreWrittenInOrder()
        {
            var f = Fn(new[] { "s", "p1", "p2" }, With(Plus1("a"), Set("s", "x", "2"),
                N("sq", "mul").Connect("a", "a").Connect("b", "a"), Print("p1", "sq"), Print("p2", "sq")));
            Assert.That(Source(f), Does.Contain("_b1 = x + 1\n    _b2 = _b1 * _b1\n    print(_b2)\n    print(_b2)"));
            Assert.That(Run(f), Is.EqualTo("9\n9\n"));
        }

        [Test]
        public void TheLineThatWorksItOutBelongsToThatBlockInTheSourceMap()
        {
            var f = Fn(new[] { "s", "p1", "p2" }, With(Plus1("p"), Set("s", "x", "4"), Print("p1", "p"), Print("p2", "p")));
            var r = BlockCompiler.Compile(new BlockGraph().AddFunction(f), Library());
            Assert.That(r.Map.LinesOf("p").Count, Is.EqualTo(1));
            Assert.That(r.Source.Split('\n')[r.Map.LinesOf("p").Single() - 1], Does.Contain("_b1 = x + 1"));
        }

        // ---- The value is kept for the flow step ----

        [Test]
        public void AVariableChangedBetweenTwoUsesDoesNotChangeTheSharedValue()
        {
            var f = Fn(new[] { "s1", "p1", "s2", "p2" }, With(Plus1("p"), Set("s1", "x", "1"), Print("p1", "p"), Set("s2", "x", "5"), Print("p2", "p")));
            Assert.That(Run(f), Is.EqualTo("2\n2\n"));
        }

        [Test]
        public void EachPassOfALoopWorksItOutAgain()
        {
            var f = Fn(new[] { "loop" }, With(Plus1("p"), N("loop", "for_range", "x").Literal("stop", "3").Then("body", "p1"),
                Print("p1", "p").Then("p2"), Print("p2", "p")));
            Assert.That(Source(f), Does.Contain("for x in range(3):\n        _b1 = x + 1\n        print(_b1)\n        print(_b1)"));
            Assert.That(Run(f), Is.EqualTo("1\n1\n2\n2\n3\n3\n"));
        }

        [Test]
        public void AValueWorkedOutBeforeALoopIsNotReusedInsideIt()
        {
            var f = Fn(new[] { "s", "before", "loop" }, With(Plus1("p"), Set("s", "x", "0"), Print("before", "p"),
                N("loop", "repeat").Literal("count", "2").Then("body", "bump"),
                N("bump", "change", "x").Literal("value", "1").Then("inside"), Print("inside", "p")));
            Assert.That(Run(f), Is.EqualTo("1\n2\n3\n"));
            Assert.That(Count(Source(f), "x + 1"), Is.EqualTo(2));
        }

        [Test]
        public void ALoopConditionIsWrittenInlineSoItRunsOnEveryPass()
        {
            var f = Fn(new[] { "s", "loop" }, With(Plus1("p"), Set("s", "x", "-1"),
                N("loop", "while").Connect("cond", "small").Then("body", "bump"),
                N("small", "lt").Connect("a", "p").Literal("b", "3"),
                N("bump", "change", "x").Literal("value", "1").Then("show"), Print("show", "p")));
            Assert.That(Source(f), Does.Contain("while (x + 1) < 3:"));
            Assert.That(Run(f), Is.EqualTo("1\n2\n3\n"));
        }

        // ---- Branches ----

        [Test]
        public void AValueWorkedOutInsideABranchIsNotReusedAfterIt()
        {
            Func<string, BlockFunction> graph = cond => Fn(new[] { "s", "check", "after" }, With(Plus1("p"), Set("s", "x", "1"),
                N("check", "if").Literal("cond", cond).Then("then", "inside"), Print("inside", "p"), Print("after", "p")));
            Assert.That(Run(graph("True")), Is.EqualTo("2\n2\n"));
            Assert.That(Run(graph("False")), Is.EqualTo("2\n"), "the branch is skipped, so nothing was worked out there");
        }

        [Test]
        public void AValueWorkedOutBeforeABranchIsReusedInBothSides()
        {
            Func<string, BlockFunction> graph = cond => Fn(new[] { "s", "first", "check" }, With(Plus1("p"), Set("s", "x", "1"), Print("first", "p"),
                N("check", "if").Literal("cond", cond).Then("then", "t").Then("else", "e"), Print("t", "p"), Print("e", "p")));
            var source = Source(graph("True"));
            Assert.That(Count(source, "x + 1"), Is.EqualTo(1));
            Assert.That(Run(graph("True")), Is.EqualTo("2\n2\n"));
            Assert.That(Run(graph("False")), Is.EqualTo("2\n2\n"));
        }

        [Test]
        public void TheTwoSidesOfABranchDoNotShareWithEachOther()
        {
            var f = Fn(new[] { "s", "check" }, With(Plus1("p"), Set("s", "x", "1"),
                N("check", "if").Literal("cond", "False").Then("then", "t").Then("else", "e"), Print("t", "p"), Print("e", "p")));
            Assert.That(Run(f), Is.EqualTo("2\n"));
        }

        // ---- Pauses end the step ----

        [Test]
        public void ALatentCallBetweenTwoUsesMakesTheSecondWorkItOutAgain()
        {
            var f = Fn(new[] { "s1", "p1", "s2", "pause", "p2" }, With(Plus1("p"), Set("s1", "x", "1"), Print("p1", "p"), Set("s2", "x", "10"),
                N("pause", "call", "game.wait"), Print("p2", "p")));
            Assert.That(Run(f), Is.EqualTo("2\n11\n"));
        }

        [Test]
        public void ACallThatCannotPauseDoesNotEndTheStep()
        {
            var f = Fn(new[] { "s1", "p1", "s2", "other", "p2" }, With(Plus1("p"), Set("s1", "x", "1"), Print("p1", "p"), Set("s2", "x", "10"),
                N("other", "call", "game.noop"), Print("p2", "p")));
            Assert.That(Run(f), Is.EqualTo("2\n2\n"));
        }

        [Test]
        public void ACallToAFunctionInTheGraphEndsTheStepBecauseItMayPause()
        {
            var helper = new BlockFunction("helper");
            var f = Fn(new[] { "s1", "p1", "s2", "other", "p2" }, With(Plus1("p"), Set("s1", "x", "1"), Print("p1", "p"), Set("s2", "x", "10"),
                N("other", "call", "helper"), Print("p2", "p")));
            Assert.That(Run(f, helper), Is.EqualTo("2\n11\n"));
        }

        [Test]
        public void APauseInsideALoopEndsTheStepAfterTheLoopToo()
        {
            var f = Fn(new[] { "s", "first", "loop", "last" }, With(Plus1("p"), Set("s", "x", "0"), Print("first", "p"),
                N("loop", "repeat").Literal("count", "1").Then("body", "bump"),
                N("bump", "change", "x").Literal("value", "5").Then("pause"), N("pause", "call", "game.wait"), Print("last", "p")));
            Assert.That(Run(f), Is.EqualTo("1\n6\n"));
        }

        // ---- Values that only sometimes run ----

        [Test]
        public void ARightHandOperandIsNeverWorkedOutEarly()
        {
            // 10 / x fails for x = 0; it is only reached when x != 0, so it must not be hoisted above the "and".
            var f = Fn(new[] { "s", "p1", "p2" }, Set("s", "x", "0"),
                N("quot", "div").Literal("a", "10").Connect("b", "gx"), N("gx", "get", "x"),
                N("nz", "ne").Connect("a", "gx").Literal("b", "0"),
                N("big1", "gt").Connect("a", "quot").Literal("b", "1"), N("both1", "and").Connect("a", "nz").Connect("b", "big1"),
                N("big2", "gt").Connect("a", "quot").Literal("b", "2"), N("both2", "and").Connect("a", "nz").Connect("b", "big2"),
                Print("p1", "both1"), Print("p2", "both2"));
            // The guard "x != 0" is read twice and always runs, so it is shared; the division is not.
            var source = Source(f);
            Assert.That(source, Does.Contain("_b1 = x != 0"));
            Assert.That(source, Does.Not.Contain("= 10 / x").And.Not.Contain("= (10 / x)"));
            Assert.That(Count(source, "10 / x"), Is.EqualTo(2));
            Assert.That(Run(f), Is.EqualTo("False\nFalse\n"));
        }

        [Test]
        public void TheBranchesOfChooseAreNeverWorkedOutEarly()
        {
            var f = Fn(new[] { "s", "p1", "p2" }, Set("s", "x", "0"),
                N("quot", "div").Literal("a", "10").Connect("b", "gx"), N("gx", "get", "x"),
                N("nz", "ne").Connect("a", "gx").Literal("b", "0"),
                N("pick1", "choose").Connect("cond", "nz").Connect("a", "quot").Literal("b", "-1"),
                N("pick2", "choose").Connect("cond", "nz").Connect("a", "quot").Literal("b", "-2"),
                Print("p1", "pick1"), Print("p2", "pick2"));
            var source = Source(f);
            Assert.That(source, Does.Contain("_b1 = x != 0"));
            Assert.That(source, Does.Not.Contain("= 10 / x").And.Not.Contain("= (10 / x)"));
            Assert.That(Count(source, "10 / x"), Is.EqualTo(2));
            Assert.That(Run(f), Is.EqualTo("-1\n-2\n"));
        }

        [Test]
        public void TheLeftOperandOfAndAndTheConditionOfChooseAreSharedAsUsual()
        {
            var f = Fn(new[] { "s", "p1", "p2" }, With(Plus1("p"), Set("s", "x", "1"),
                N("pos", "gt").Connect("a", "p").Literal("b", "0"),
                N("both", "and").Connect("a", "pos").Literal("b", "True"),
                N("pick", "choose").Connect("cond", "pos").Literal("a", "\"y\"").Literal("b", "\"n\""),
                Print("p1", "both"), Print("p2", "pick")));
            var source = Source(f);
            Assert.That(source, Does.Contain("_b1 = (x + 1) > 0\n    print(_b1 and True)"));
            Assert.That(Run(f), Is.EqualTo("True\ny\n"));
        }

        [Test]
        public void ASharedValueWorkedOutEarlierCanStillBeReadInALazyOperand()
        {
            var f = Fn(new[] { "s", "first", "second" }, With(Plus1("p"), Set("s", "x", "1"), Print("first", "p"),
                N("both", "and").Literal("a", "True").Connect("b", "p"), Print("second", "both")));
            Assert.That(Source(f), Does.Contain("print(True and _b1)"));
        }

        // ---- Results of calls ----

        [Test]
        public void ResultsOfCallsInTheFlowAreStillWorkedOutOnce()
        {
            var f = Fn(new[] { "call", "p1", "p2" }, N("call", "call", "game.noop"), Print("p1", "call"), Print("p2", "call"));
            Assert.That(Source(f), Does.Contain("_b1 = game.noop()\n    print(_b1)\n    print(_b1)"));
        }

        // ---- Types still work ----

        [Test]
        public void SharingDoesNotChangeTheTypeChecks()
        {
            var f = Fn(new[] { "s", "r1", "r2" }, With(Plus1("p"), Set("s", "x", "1"),
                N("r1", "repeat").Connect("count", "p"), N("r2", "repeat").Literal("count", "\"two\"")));
            var g = new BlockGraph().AddFunction(f);
            Assert.That(BlockCompiler.Compile(g, Library()).Errors.Select(e => e.NodeId), Is.EqualTo(new[] { "r2" }));
        }
    }
}
