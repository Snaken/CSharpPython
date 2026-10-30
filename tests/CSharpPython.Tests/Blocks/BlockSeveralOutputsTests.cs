using System;
using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    public class BlockSeveralOutputsTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        private static BlockNode Print(string id, string from, string fromPin = null) =>
            N(id, "call", "print").Connect("arg0", from, fromPin);

        // The blocks run in the order given. Value blocks are added without a place in the flow.
        private static BlockFunction Fn(string[] flow, params BlockNode[] all)
        {
            var f = new BlockFunction("main") { Start = flow[0] };
            foreach (var n in all) f.Add(n);
            for (int i = 0; i + 1 < flow.Length; i++) all.First(n => n.Id == flow[i]).Then(flow[i + 1]);
            return f;
        }

        private static void Setup(PyVm vm)
        {
            vm.RegisterModule("game", m => m
                .Native("raycast", (c, a) => Value.FromTuple(new PyTuple(new[] { Value.FromBool(a[0].AsInt() > 0), Value.FromString("p" + a[0].AsInt()) })), 1, 1)
                .Native("pair", (c, a) => Value.FromTuple(new PyTuple(new[] { Value.FromInt(1), Value.FromInt(2) })), 0, 0)
                .Describe("raycast", "origin: int -> (hit: bool, point: str)")
                .Describe("pair", "-> (first: int, second: int)"));
        }

        private static BlockLibrary Library()
        {
            var vm = new PyVm();
            Setup(vm);
            return new BlockLibrary().AddAllModules(vm);
        }

        private static BlockCompileResult Compile(BlockFunction f, BlockLibrary lib = null) =>
            BlockCompiler.Compile(new BlockGraph().AddFunction(f), lib ?? Library());

        private static string Run(BlockFunction f)
        {
            var r = Compile(f);
            Assert.That(r.Errors.Select(e => e.ToString()), Is.Empty);
            return Vm.Py.Run(r.Source + "main()\n", Setup);
        }

        // ---- Running ----

        [Test]
        public void EachOutputOfACallInTheFlowCanBeWiredOnItsOwn()
        {
            var f = Fn(new[] { "ray", "p1", "p2" },
                N("ray", "call", "game.raycast").Literal("arg0", "5"),
                Print("p1", "ray", "hit"), Print("p2", "ray", "point"));
            var r = Compile(f);
            Assert.That(r.Source, Does.Contain("_b1, _b2 = game.raycast(5)"));
            Assert.That(r.Source, Does.Contain("print(_b1)").And.Contain("print(_b2)"));
            Assert.That(Run(f), Is.EqualTo("True\np5\n"));
        }

        [Test]
        public void OnlyTheOutputsUsedAreReadButAllAreUnpacked()
        {
            var f = Fn(new[] { "ray", "p" }, N("ray", "call", "game.raycast").Literal("arg0", "0"), Print("p", "ray", "point"));
            Assert.That(Compile(f).Source, Does.Contain("_b1, _b2 = game.raycast(0)"));
            Assert.That(Run(f), Is.EqualTo("p0\n"));
        }

        [Test]
        public void BothOutputsCanFeedOneBlock()
        {
            var f = Fn(new[] { "pr", "show" },
                N("pr", "call", "game.pair"),
                N("sum", "add").Connect("a", "pr", "first").Connect("b", "pr", "second"),
                Print("show", "sum"));
            Assert.That(Run(f), Is.EqualTo("3\n"));
        }

        [Test]
        public void ACallWithNoOneReadingItsOutputsIsJustCalled()
        {
            var f = Fn(new[] { "ray" }, N("ray", "call", "game.raycast").Literal("arg0", "1"));
            var r = Compile(f);
            Assert.That(r.Source, Does.Contain("    game.raycast(1)\n").And.Not.Contain("_b1"));
        }

        [Test]
        public void APureCallWithSeveralOutputsIsCalledOnceAndIndexedWhereItIsUsed()
        {
            var f = Fn(new[] { "p1", "p2" },
                N("modf", "call", "math.modf").Literal("arg0", "3.75"),
                Print("p1", "modf", "fractional"), Print("p2", "modf", "whole"));
            var r = Compile(f);
            Assert.That(r.Source, Does.Contain("_b1 = math.modf(3.75)\n    print(_b1[0])\n    print(_b1[1])"));
            Assert.That(Run(f), Is.EqualTo("0.75\n3.0\n"));
        }

        [Test]
        public void ASingleOutputCallIsUnchanged()
        {
            var f = Fn(new[] { "p" }, N("l", "call", "len").Literal("arg0", "\"abc\""), Print("p", "l"));
            Assert.That(Compile(f).Source, Does.Contain("print(len(\"abc\"))"));
            Assert.That(Run(f), Is.EqualTo("3\n"));
        }

        [Test]
        public void TheSourceMapPointsTheUnpackingLineAtTheCall()
        {
            var f = Fn(new[] { "ray", "p1" }, N("ray", "call", "game.raycast").Literal("arg0", "5"), Print("p1", "ray", "hit"));
            var r = Compile(f);
            Assert.That(r.Map.LinesOf("ray").Count, Is.EqualTo(1));
        }

        // ---- Mistakes ----

        private static BlockDiagnostic OneError(BlockFunction f)
        {
            var r = Compile(f);
            Assert.That(r.Succeeded, Is.False);
            return r.Errors.First();
        }

        [Test]
        public void ReadingABlockWithSeveralOutputsWithoutSayingWhichIsAnError()
        {
            var f = Fn(new[] { "ray", "p" }, N("ray", "call", "game.raycast").Literal("arg0", "5"), Print("p", "ray"));
            var e = OneError(f);
            Assert.That(e.NodeId, Is.EqualTo("p"));
            Assert.That(e.Pin, Is.EqualTo("arg0"));
            Assert.That(e.Message, Does.Contain("several outputs").And.Contain("hit, point"));
        }

        [Test]
        public void NamingAnOutputThatDoesNotExistIsAnError()
        {
            var f = Fn(new[] { "ray", "p" }, N("ray", "call", "game.raycast").Literal("arg0", "5"), Print("p", "ray", "normal"));
            Assert.That(OneError(f).Message, Does.Contain("no output 'normal'").And.Contain("hit, point"));
        }

        [Test]
        public void NamingAnOutputOfABlockWithOneIsAnError()
        {
            var f = Fn(new[] { "p" }, N("l", "call", "len").Literal("arg0", "\"abc\""), Print("p", "l", "result"));
            Assert.That(OneError(f).Message, Does.Contain("only one output"));
            var g = Fn(new[] { "p" }, N("v", "literal").Literal("value", "1"), Print("p", "v", "x"));
            Assert.That(OneError(g).Message, Does.Contain("only one output"));
        }

        [Test]
        public void AnOutputIsNotAvailableBeforeTheCallRuns()
        {
            var f = Fn(new[] { "p1", "ray" }, Print("p1", "ray", "hit"), N("ray", "call", "game.raycast").Literal("arg0", "5"));
            Assert.That(OneError(f).Message, Does.Contain("before it has run"));
        }

        [Test]
        public void ALoopConditionCannotUseAnOutput()
        {
            var f = Fn(new[] { "ray", "loop" },
                N("ray", "call", "game.raycast").Literal("arg0", "5"),
                N("loop", "while").Connect("cond", "ray", "hit"));
            Assert.That(OneError(f).Message, Does.Contain("loop condition"));
        }

        // ---- Types ----

        [Test]
        public void EachOutputHasItsOwnType()
        {
            var f = Fn(new[] { "ray" }, N("ray", "call", "game.raycast").Literal("arg0", "5"));
            var g = new BlockGraph().AddFunction(f);
            var lib = Library();
            Assert.That(BlockTypeChecker.OutputType(g, "main", "ray", lib, "hit"), Is.EqualTo(PinType.Bool));
            Assert.That(BlockTypeChecker.OutputType(g, "main", "ray", lib, "point"), Is.EqualTo(PinType.Str));
            Assert.That(BlockTypeChecker.OutputType(g, "main", "ray", lib), Is.EqualTo(PinType.Any), "no output named");
            Assert.That(BlockTypeChecker.OutputType(g, "main", "ray", lib, "nope"), Is.EqualTo(PinType.Any));
        }

        [Test]
        public void AnOutputIsCheckedAgainstThePinItIsWiredTo()
        {
            var ok = Fn(new[] { "if" }, N("ray", "call", "game.raycast").Literal("arg0", "5"),
                N("if", "if").Connect("cond", "ray", "hit"));
            Assert.That(BlockTypeChecker.Check(new BlockGraph().AddFunction(ok), Library()), Is.Empty);

            // point is a str; a repeat count needs an int.
            var bad = Fn(new[] { "ray", "r" }, N("ray", "call", "game.raycast").Literal("arg0", "5"),
                N("r", "repeat").Connect("count", "ray", "point"));
            var errors = BlockTypeChecker.Check(new BlockGraph().AddFunction(bad), Library());
            Assert.That(errors.Single().Pin, Is.EqualTo("count"));
            Assert.That(errors.Single().Message, Does.Contain("str"));

            var good = Fn(new[] { "pr", "r" }, N("pr", "call", "game.pair"), N("r", "repeat").Connect("count", "pr", "second"));
            Assert.That(BlockTypeChecker.Check(new BlockGraph().AddFunction(good), Library()), Is.Empty);
        }

        [Test]
        public void CheckWireJudgesTheNamedOutput()
        {
            var g = new BlockGraph().AddFunction(Fn(new[] { "ray", "r" },
                N("ray", "call", "game.raycast").Literal("arg0", "5"), N("r", "repeat")));
            var lib = Library();
            Assert.That(BlockTypeChecker.CheckWire(g, "main", "r", "count", "ray", lib, "point").Allowed, Is.False);
            Assert.That(BlockTypeChecker.CheckWire(g, "main", "r", "count", "ray", lib, "hit").Allowed, Is.False, "bool does not fit int");
            Assert.That(BlockTypeChecker.CheckWire(g, "main", "r", "count", "ray", lib).Allowed, Is.True, "an unnamed output is the compiler's to report");
        }

        // ---- Saving ----

        [Test]
        public void TheOutputNameIsSavedAndLoaded()
        {
            var g = new BlockGraph().AddFunction(Fn(new[] { "ray", "p" },
                N("ray", "call", "game.raycast").Literal("arg0", "5"), Print("p", "ray", "hit")));
            string json = g.ToJson();
            Assert.That(json, Does.Contain("\"FromPin\": \"hit\""));
            var back = BlockGraph.FromJson(json);
            var input = back.Functions[0].Nodes.First(n => n.Id == "p").Inputs.Single();
            Assert.That(input.From, Is.EqualTo("ray"));
            Assert.That(input.FromPin, Is.EqualTo("hit"));
            Assert.That(back.ToJson(), Is.EqualTo(json));
        }

        [Test]
        public void GraphsSavedBeforeOutputNamesStillLoad()
        {
            var g = new BlockGraph().AddFunction(Fn(new[] { "p" }, Print("p", "v"), N("v", "literal").Literal("value", "1")));
            string json = g.ToJson();
            Assert.That(json, Does.Not.Contain("FromPin"));
            Assert.That(BlockGraph.FromJson(json).Functions[0].Nodes.First(n => n.Id == "p").Inputs.Single().FromPin, Is.Null);
        }

        [Test]
        public void DescribeOutputsComeBackAsTheFunctionsOutputs()
        {
            var spec = Library().FindFunction("game.raycast");
            Assert.That(spec.Outputs.Select(o => o.Name), Is.EqualTo(new[] { "hit", "point" }));
            Assert.That(spec.Outputs.Select(o => o.Type.ToString()), Is.EqualTo(new[] { "bool", "str" }));
        }

        [Test]
        public void ANativeThatReturnsTheWrongNumberOfValuesFailsWhenItRuns()
        {
            Action<PyVm> setup = vm => vm.RegisterModule("game", m => m
                .Native("bad", (c, a) => Value.FromTuple(new PyTuple(new[] { Value.FromInt(1) })), 0, 0)
                .Describe("bad", "-> (a: int, b: int)"));
            var probe = new PyVm();
            setup(probe);
            var lib = new BlockLibrary().AddAllModules(probe);
            var f = Fn(new[] { "c", "p" }, N("c", "call", "game.bad"), Print("p", "c", "a"));
            var r = BlockCompiler.Compile(new BlockGraph().AddFunction(f), lib);
            Assert.That(r.Succeeded, Is.True);
            Assert.Throws<PyRuntimeError>(() => Vm.Py.Run(r.Source + "main()\n", setup));
        }
    }
}
