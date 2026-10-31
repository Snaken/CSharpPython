using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // The loop blocks have an output, the loop's own variable, which only blocks inside the loop may use
    // (docs/node-view-design.md, decision 12).
    public class BlockLoopVariableTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);
        private static BlockNode Show(string id, string from) => N(id, "call", "print").Connect("arg0", from);

        private static BlockFunction Fn(string start, params BlockNode[] nodes)
        {
            var f = new BlockFunction("main") { Start = start };
            foreach (var n in nodes) f.Add(n);
            return f;
        }

        private static BlockGraph G(BlockFunction f) => new BlockGraph().AddFunction(f);

        private static string Run(BlockFunction f)
        {
            var r = BlockCompiler.Compile(G(f));
            Assert.That(r.Errors.Select(e => e.ToString()), Is.Empty);
            return Vm.Py.Run(r.Source + "main()\n");
        }

        private static string FirstError(BlockFunction f)
        {
            var r = BlockCompiler.Compile(G(f));
            Assert.That(r.Succeeded, Is.False, "expected an error but got:\n" + r.Source);
            return r.Errors.First().Message;
        }

        // for each x in [1, 2, 3]: print(x)
        private static BlockFunction EachPrint() => Fn("l",
            N("l", "for_each", "x").Connect("items", "xs").Then("body", "p"),
            N("xs", "list").Literal("item0", "1").Literal("item1", "2").Literal("item2", "3"),
            Show("p", "l"));

        // ---- Using the variable ----

        [Test]
        public void TheItemOfAForEachCanBeWiredInsideItsBody()
        {
            Assert.That(Run(EachPrint()), Is.EqualTo("1\n2\n3\n"));
        }

        [Test]
        public void TheCounterOfACountLoopCanBeWiredInsideItsBody()
        {
            var f = Fn("l", N("l", "for_range", "i").Literal("stop", "3").Then("body", "p"), Show("p", "l"));
            Assert.That(Run(f), Is.EqualTo("0\n1\n2\n"));
        }

        [Test]
        public void ANamedRepeatHasItsPassNumberAsAnOutput()
        {
            var f = Fn("l", N("l", "repeat", "n").Literal("count", "2").Then("body", "p"), Show("p", "l"));
            Assert.That(Run(f), Is.EqualTo("0\n1\n"));
        }

        [Test]
        public void ARepeatWithoutANameHasNoOutput()
        {
            var f = Fn("l", N("l", "repeat").Literal("count", "2").Then("body", "p"), Show("p", "l"));
            Assert.That(FirstError(f), Does.Contain("unless it is given a name"));
        }

        [Test]
        public void TheVariableCanBeUsedByAValueBlockAndByANestedLoop()
        {
            var f = Fn("l",
                N("l", "for_range", "i").Literal("stop", "2").Then("body", "m"),
                N("m", "for_range", "j").Connect("stop", "plus").Then("body", "p"),
                N("plus", "add").Connect("a", "l").Literal("b", "1"),
                Show("p", "tw"),
                N("tw", "mul").Connect("a", "l").Connect("b", "m"));
            // i = 0: j in range(1) -> 0 * 0; i = 1: j in range(2) -> 1 * 0, 1 * 1
            Assert.That(Run(f), Is.EqualTo("0\n0\n1\n"));
        }

        [Test]
        public void TheVariableCanBeUsedInsideAJoinInTheBody()
        {
            var f = Fn("l",
                N("l", "for_range", "i").Literal("stop", "2").Then("body", "t"),
                N("t", "if").Literal("cond", "True").Then("then", "a").Then("else", "b"),
                N("a", "call", "print").Literal("arg0", "\"A\"").Then("c"),
                N("b", "call", "print").Literal("arg0", "\"B\"").Then("c"),
                Show("c", "l"));
            Assert.That(Run(f), Is.EqualTo("A\n0\nA\n1\n"));
        }

        // ---- Leaving the body ----

        [Test]
        public void TheVariableCannotBeUsedAfterTheLoop()
        {
            var f = Fn("l",
                N("l", "for_each", "x").Connect("items", "xs").Then("body", "p").Then("q"),
                N("xs", "list").Literal("item0", "1"),
                N("p", "comment", "inside"), Show("q", "l"));
            Assert.That(FirstError(f), Does.Contain("only be used by blocks inside that loop"));
        }

        [Test]
        public void TheVariableCannotBeUsedBeforeTheLoopOrByItsOwnHeader()
        {
            var before = Fn("p", Show("p", "l").Then("l"), N("l", "for_range", "i").Literal("stop", "2"));
            Assert.That(FirstError(before), Does.Contain("only be used by blocks inside that loop"));
            var own = Fn("l", N("l", "for_range", "i").Connect("stop", "l"));
            Assert.That(FirstError(own), Does.Contain("only be used by blocks inside that loop"));
        }

        [Test]
        public void AValueBlockReadInsideAndOutsideTheLoopIsAnError()
        {
            var f = Fn("l",
                N("l", "for_range", "i").Literal("stop", "2").Then("body", "p").Then("q"),
                Show("p", "plus"), Show("q", "plus"), N("plus", "add").Connect("a", "l").Literal("b", "1"));
            Assert.That(FirstError(f), Does.Contain("only be used by blocks inside that loop"));
        }

        [Test]
        public void TheLoopVariableStillExistsAfterTheLoopThroughAGetBlock()
        {
            var f = Fn("l",
                N("l", "for_range", "i").Literal("stop", "3").Then("q"),
                Show("q", "g"), N("g", "get", "i"));
            Assert.That(Run(f), Is.EqualTo("2\n"));
        }

        // ---- Types ----

        [Test]
        public void TheVariableHasTheTypeOfTheItemsOrInt()
        {
            var g = G(EachPrint());
            Assert.That(BlockTypeChecker.OutputType(g, "main", "l").ToString(), Is.EqualTo("int"));
            var range = G(Fn("l", N("l", "for_range", "i").Literal("stop", "3")));
            Assert.That(BlockTypeChecker.OutputType(range, "main", "l").ToString(), Is.EqualTo("int"));
            var any = G(Fn("l", N("l", "for_each", "x").Literal("items", "\"abc\"")));
            Assert.That(BlockTypeChecker.OutputType(any, "main", "l").ToString(), Is.EqualTo("any"));
            var text = G(Fn("l", N("l", "for_each", "x").Connect("items", "xs"), N("xs", "list").Literal("item0", "\"a\"")));
            Assert.That(BlockTypeChecker.OutputType(text, "main", "l").ToString(), Is.EqualTo("str"));
        }

        [Test]
        public void ATypedPinChecksTheLoopVariable()
        {
            // 'index' takes a list; a str item cannot be wired to it.
            var f = Fn("l",
                N("l", "for_each", "x").Connect("items", "xs").Then("body", "p"),
                N("xs", "list").Literal("item0", "\"a\""),
                N("p", "append").Connect("list", "l").Literal("value", "1"));
            Assert.That(BlockTypeChecker.Check(G(f)).Select(d => d.NodeId + "." + d.Pin), Contains.Item("p.list"));
        }

        // ---- Wires the editor may draw ----

        private static BlockGraph Placed() => G(Fn("l",
            N("l", "for_range", "i").Literal("stop", "3").Then("body", "p").Then("q"),
            N("p", "comment", "x").Then("r"), N("r", "call", "print").Literal("arg0", "1"),
            N("q", "call", "print").Literal("arg0", "2"),
            N("v", "add").Literal("a", "1").Literal("b", "2"),
            N("free", "call", "print").Literal("arg0", "1")));

        [Test]
        public void TheEditorMayWireTheVariableIntoTheBody()
        {
            Assert.That(BlockTypeChecker.CheckWire(Placed(), "main", "r", "arg0", "l").Allowed, Is.True);
        }

        [Test]
        public void TheEditorRefusesTheVariableOutsideTheBody()
        {
            var check = BlockTypeChecker.CheckWire(Placed(), "main", "q", "arg0", "l");
            Assert.That(check.Allowed, Is.False);
            Assert.That(check.Reason, Does.Contain("inside that loop"));
            Assert.That(BlockTypeChecker.CheckWire(Placed(), "main", "l", "stop", "l").Allowed, Is.False, "the loop's own header is outside its body");
        }

        [Test]
        public void TheEditorDoesNotJudgeABlockNotYetInTheFlow()
        {
            Assert.That(BlockTypeChecker.CheckWire(Placed(), "main", "free", "arg0", "l").Allowed, Is.True);
            Assert.That(BlockTypeChecker.CheckWire(Placed(), "main", "v", "a", "l").Allowed, Is.True);
        }

        [Test]
        public void TheEditorFollowsAValueBlockToTheBlocksThatReadIt()
        {
            var g = G(Fn("l",
                N("l", "for_range", "i").Literal("stop", "3").Then("body", "p").Then("q"),
                N("p", "call", "print").Connect("arg0", "v"), N("q", "call", "print").Connect("arg0", "v"),
                N("v", "add").Literal("a", "1").Literal("b", "2"), N("w", "add").Literal("a", "1").Literal("b", "2")));
            Assert.That(BlockTypeChecker.CheckWire(g, "main", "v", "a", "l").Allowed, Is.False, "v is also read after the loop");
            // A value that depends on the variable cannot be wired to a block outside the body either.
            var dependent = G(Fn("l",
                N("l", "for_range", "i").Literal("stop", "3").Then("body", "p").Then("q"),
                N("p", "call", "print"), N("q", "call", "print"),
                N("v", "add").Connect("a", "l").Literal("b", "1")));
            Assert.That(BlockTypeChecker.CheckWire(dependent, "main", "q", "arg0", "v").Allowed, Is.False);
            Assert.That(BlockTypeChecker.CheckWire(dependent, "main", "p", "arg0", "v").Allowed, Is.True);
        }
    }
}
