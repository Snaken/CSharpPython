using System;
using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    public class BlockTypeCheckerTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        // A graph with one function, "main", that starts at the first node.
        private static BlockGraph G(params BlockNode[] nodes)
        {
            var f = new BlockFunction("main") { Start = nodes[0].Id };
            foreach (var n in nodes) f.Add(n);
            return new BlockGraph().AddFunction(f);
        }

        private static BlockLibrary Library()
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m => m
                .Native("spawn", (c, a) => Value.None, 1, 2)
                .Native("hurt", (c, a) => Value.None, 1, 1)
                .Native("raycast", (c, a) => Value.None, 1, 1)
                .Describe("spawn", "kind: str, count: int = 1 -> Entity")
                .Describe("hurt", "target: Entity -> none")
                .Describe("raycast", "origin: str -> (hit: bool, point: str)"));
            return new BlockLibrary().AddAllModules(vm);
        }

        private static PinType Out(BlockGraph g, string id, BlockLibrary lib = null) =>
            BlockTypeChecker.OutputType(g, "main", id, lib ?? Library());

        private static string[] Errors(BlockGraph g, BlockLibrary lib = null) =>
            BlockTypeChecker.Check(g, lib ?? Library()).Select(d => d.NodeId + "." + d.Pin).ToArray();

        // ---- Literals and operators ----

        [TestCase("1", "int")]
        [TestCase("-4", "int")]
        [TestCase("2.5", "float")]
        [TestCase("-0.5", "float")]
        [TestCase("\"hi\"", "str")]
        [TestCase("True", "bool")]
        [TestCase("False", "bool")]
        [TestCase("None", "any")]
        public void LiteralsHaveTheTypeOfTheirText(string literal, string type)
        {
            var g = G(N("v", "literal").Literal("value", literal));
            Assert.That(Out(g, "v").ToString(), Is.EqualTo(type));
        }

        [TestCase("add", "1", "2", "int")]
        [TestCase("sub", "1", "2.0", "float")]
        [TestCase("mul", "2.0", "2", "float")]
        [TestCase("div", "4", "2", "float")]
        [TestCase("floordiv", "4", "2", "int")]
        [TestCase("mod", "4", "2", "int")]
        [TestCase("floordiv", "4.0", "2", "float")]
        [TestCase("pow", "2", "3", "any")]
        [TestCase("add", "\"a\"", "\"b\"", "str")]
        [TestCase("mul", "\"a\"", "3", "str")]
        [TestCase("mul", "3", "\"a\"", "str")]
        [TestCase("add", "1", "\"a\"", "any")]
        [TestCase("sub", "\"a\"", "\"b\"", "any")]
        [TestCase("add", "True", "1", "any")]
        public void OperatorsTypeTheirResultFromTheirInputs(string op, string a, string b, string type)
        {
            var g = G(N("x", op).Literal("a", a).Literal("b", b));
            Assert.That(Out(g, "x").ToString(), Is.EqualTo(type));
        }

        [Test]
        public void ListSumsKeepTheListType()
        {
            var g = G(N("x", "add").Connect("a", "l1").Connect("b", "l2"),
                      N("l1", "list").Literal("item0", "1"), N("l2", "list").Literal("item0", "2"));
            Assert.That(Out(g, "x").ToString(), Is.EqualTo("list[int]"));
        }

        [Test]
        public void NegationKeepsANumbersType()
        {
            Assert.That(Out(G(N("x", "neg").Literal("a", "2")), "x"), Is.EqualTo(PinType.Int));
            Assert.That(Out(G(N("x", "neg").Literal("a", "2.5")), "x"), Is.EqualTo(PinType.Float));
            Assert.That(Out(G(N("x", "neg").Literal("a", "\"s\"")), "x"), Is.EqualTo(PinType.Any));
        }

        [Test]
        public void ComparisonsAndLogicGiveBool()
        {
            foreach (var op in new[] { "eq", "ne", "lt", "le", "gt", "ge", "contains" })
                Assert.That(Out(G(N("x", op).Literal("a", "1").Literal("b", "2")), "x"), Is.EqualTo(PinType.Bool), op);
            Assert.That(Out(G(N("x", "not").Literal("a", "1")), "x"), Is.EqualTo(PinType.Bool));
            Assert.That(Out(G(N("x", "is_none").Literal("a", "1")), "x"), Is.EqualTo(PinType.Bool));
        }

        // ---- Wires that do not fit ----

        [Test]
        public void ARepeatCountMustBeAnInt()
        {
            var bad = G(N("r", "repeat").Literal("count", "\"three\""));
            Assert.That(Errors(bad), Is.EqualTo(new[] { "r.count" }));
            var floaty = G(N("r", "repeat").Literal("count", "2.5"));
            Assert.That(Errors(floaty), Is.EqualTo(new[] { "r.count" }));
            Assert.That(Errors(G(N("r", "repeat").Literal("count", "3"))), Is.Empty);
        }

        [Test]
        public void ABadWireStopsTheCompilerWithTheNodeAndPin()
        {
            var g = G(N("r", "repeat").Connect("count", "s"), N("s", "literal").Literal("value", "\"x\""));
            var result = BlockCompiler.Compile(g, Library());
            Assert.That(result.Succeeded, Is.False);
            var error = result.Errors.Single();
            Assert.That(error.NodeId, Is.EqualTo("r"));
            Assert.That(error.Pin, Is.EqualTo("count"));
            Assert.That(error.Function, Is.EqualTo("main"));
            Assert.That(error.Message, Does.Contain("str").And.Contain("int"));
        }

        [Test]
        public void IntFitsAFloatPinButNotTheOtherWay()
        {
            var lib = Library();
            Assert.That(Errors(G(N("c", "call", "math.sqrt").Literal("arg0", "4")), lib), Is.Empty);
            Assert.That(Errors(G(N("c", "call", "math.floor").Literal("arg0", "4.5"))), Is.Empty);
            Assert.That(Errors(G(N("c", "call", "math.factorial").Literal("arg0", "4.5")), lib), Is.EqualTo(new[] { "c.arg0" }));
        }

        [Test]
        public void UntypedPinsAndVariablesTakeAnything()
        {
            var g = G(N("r", "repeat").Connect("count", "v"), N("v", "get", "n"));
            Assert.That(Errors(g), Is.Empty);
            Assert.That(Errors(G(N("p", "call", "print").Literal("arg0", "1"))), Is.Empty);
            Assert.That(Errors(G(N("p", "call", "print").Literal("arg0", "\"s\""))), Is.Empty);
        }

        [Test]
        public void ForRangeBoundsMustBeInts()
        {
            var g = G(N("f", "for_range", "i").Literal("start", "0").Literal("stop", "1.5"));
            Assert.That(Errors(g), Is.EqualTo(new[] { "f.stop" }));
        }

        // ---- Calls ----

        [Test]
        public void CallResultsAreTypedFromTheDescription()
        {
            var lib = Library();
            var spawn = G(N("s", "call", "game.spawn").Literal("arg0", "\"orc\""));
            Assert.That(Out(spawn, "s", lib).Name, Is.EqualTo("Entity"));

            var ok = G(N("h", "call", "game.hurt").Connect("arg0", "s"), N("s", "call", "game.spawn").Literal("arg0", "\"orc\""));
            Assert.That(Errors(ok, lib), Is.Empty);

            var wrong = G(N("h", "call", "game.hurt").Literal("arg0", "5"));
            Assert.That(Errors(wrong, lib), Is.EqualTo(new[] { "h.arg0" }));

            var wrongArg = G(N("s", "call", "game.spawn").Literal("arg0", "7"));
            Assert.That(Errors(wrongArg, lib), Is.EqualTo(new[] { "s.arg0" }));
        }

        [Test]
        public void ANoneResultCannotBeWired()
        {
            var g = G(N("r", "repeat").Connect("count", "h"), N("h", "call", "game.hurt").Literal("arg0", "5"));
            var errors = BlockTypeChecker.Check(g, Library());
            Assert.That(errors.Any(e => e.NodeId == "r" && e.Pin == "count" && e.Message.Contains("none")), Is.True);
            Assert.That(Out(g, "h", Library()), Is.EqualTo(PinType.None));
        }

        [Test]
        public void TheResultOfASeveralOutputFunctionIsUntypedForNow()
        {
            Assert.That(Out(G(N("c", "call", "game.raycast").Literal("arg0", "\"o\"")), "c"), Is.EqualTo(PinType.Any));
        }

        [Test]
        public void MadeUpFunctionsAndUserFunctionsAreUntyped()
        {
            var f = new BlockFunction("helper");
            var g = G(N("c", "call", "helper"), N("m", "call", "nothing_like_it"));
            g.AddFunction(f);
            Assert.That(Out(g, "c"), Is.EqualTo(PinType.Any));
            Assert.That(Out(g, "m"), Is.EqualTo(PinType.Any));
            Assert.That(Errors(g), Is.Empty);
        }

        // ---- Wildcards ----

        [Test]
        public void AppendTakesItsValueTypeFromTheList()
        {
            Func<string, BlockGraph> append = value =>
                G(N("a", "append").Connect("list", "l").Literal("value", value), N("l", "list").Literal("item0", "1"));
            Assert.That(Errors(append("5")), Is.Empty);
            Assert.That(Errors(append("\"x\"")), Is.EqualTo(new[] { "a.value" }));
        }

        [Test]
        public void AFloatValueWidensTheWildcardAndSoClashesWithAnIntList()
        {
            var g = G(N("a", "append").Connect("list", "l").Literal("value", "2.5"), N("l", "list").Literal("item0", "1"));
            var error = BlockTypeChecker.Check(g, Library()).Single();
            Assert.That(error.Pin, Is.EqualTo("list"));
            Assert.That(error.Message, Does.Contain("list[int]").And.Contain("list[float]"));
        }

        [Test]
        public void AnIntValueFitsAFloatList()
        {
            var g = G(N("a", "append").Connect("list", "l").Literal("value", "2"), N("l", "list").Literal("item0", "1.5"));
            Assert.That(Errors(g), Is.Empty);
        }

        [Test]
        public void ItemAtGivesTheListsElementType()
        {
            var g = G(N("r", "repeat").Connect("count", "i"), N("i", "index").Connect("list", "l").Literal("index", "0"),
                      N("l", "list").Literal("item0", "1").Literal("item1", "2"));
            Assert.That(Out(g, "i"), Is.EqualTo(PinType.Int));
            Assert.That(Errors(g), Is.Empty);

            var strings = G(N("r", "repeat").Connect("count", "i"), N("i", "index").Connect("list", "l").Literal("index", "0"),
                            N("l", "list").Literal("item0", "\"a\""));
            Assert.That(Out(strings, "i"), Is.EqualTo(PinType.Str));
            Assert.That(Errors(strings), Is.EqualTo(new[] { "r.count" }));
        }

        [Test]
        public void ItemAtNeedsAListAndAnIntIndex()
        {
            Assert.That(Errors(G(N("i", "index").Literal("list", "5").Literal("index", "0"))), Is.EqualTo(new[] { "i.list" }));
            Assert.That(Errors(G(N("i", "index").Connect("list", "l").Literal("index", "\"k\""), N("l", "list").Literal("item0", "1"))),
                Is.EqualTo(new[] { "i.index" }));
        }

        [Test]
        public void ChooseMakesBothBranchesAgree()
        {
            Assert.That(Out(G(N("c", "choose").Literal("cond", "True").Literal("a", "1").Literal("b", "2")), "c"), Is.EqualTo(PinType.Int));
            Assert.That(Out(G(N("c", "choose").Literal("cond", "True").Literal("a", "1").Literal("b", "2.5")), "c"), Is.EqualTo(PinType.Float));
            var clash = G(N("c", "choose").Literal("cond", "True").Literal("a", "1").Literal("b", "\"x\""));
            Assert.That(Errors(clash), Is.EqualTo(new[] { "c.b" }));
            Assert.That(BlockTypeChecker.Check(clash).Single().Message, Does.Contain("matching types"));
        }

        [Test]
        public void TheAnswerDoesNotDependOnTheOrderTheWiresWereDrawnIn()
        {
            var forward = N("c", "choose").Literal("cond", "True").Literal("a", "1").Literal("b", "\"x\"");
            var reversed = N("c", "choose").Literal("b", "\"x\"").Literal("a", "1").Literal("cond", "True");
            Assert.That(Errors(G(forward)), Is.EqualTo(new[] { "c.b" }));
            Assert.That(Errors(G(reversed)), Is.EqualTo(new[] { "c.b" }));
        }

        [Test]
        public void AnAnyWireLeavesAWildcardOpen()
        {
            var g = G(N("c", "choose").Literal("cond", "True").Connect("a", "v").Literal("b", "\"x\""), N("v", "get", "n"));
            Assert.That(Errors(g), Is.Empty);
            Assert.That(Out(g, "c"), Is.EqualTo(PinType.Any));
        }

        [Test]
        public void ListItemsNeedNotMatch()
        {
            Assert.That(Out(G(N("l", "list").Literal("item0", "1").Literal("item1", "\"a\"")), "l").ToString(), Is.EqualTo("list[any]"));
            Assert.That(Out(G(N("l", "list").Literal("item0", "1").Literal("item1", "2.5")), "l").ToString(), Is.EqualTo("list[float]"));
            Assert.That(Out(G(N("l", "list")), "l").ToString(), Is.EqualTo("list[any]"));
            Assert.That(Errors(G(N("l", "list").Literal("item0", "1").Literal("item1", "\"a\""))), Is.Empty);
        }

        [Test]
        public void ARemovedItemIndexMustBeAnInt()
        {
            var g = G(N("r", "remove_at").Connect("list", "l").Literal("index", "\"a\""), N("l", "list").Literal("item0", "1"));
            Assert.That(Errors(g), Is.EqualTo(new[] { "r.index" }));
        }

        // ---- Robustness ----

        [Test]
        public void ABlockThatDependsOnItselfDoesNotHang()
        {
            var g = G(N("x", "add").Connect("a", "x").Literal("b", "1"));
            Assert.That(Out(g, "x"), Is.Not.Null);
            Assert.That(BlockCompiler.Compile(g).Succeeded, Is.False);
        }

        [Test]
        public void WiresToMissingBlocksAndEmptyPinsAreLeftToTheCompiler()
        {
            var g = G(N("r", "repeat").Connect("count", "ghost"));
            Assert.That(Errors(g), Is.Empty);
            Assert.That(Errors(G(N("r", "repeat"))), Is.Empty);
            Assert.That(Errors(G(N("r", "repeat").Literal("count", "1 +"))), Is.Empty);
        }

        [Test]
        public void ErrorsInEveryFunctionAreReportedWithTheirFunctionName()
        {
            var g = G(N("r", "repeat").Literal("count", "\"a\""));
            var other = new BlockFunction("other") { Start = "q" };
            other.Add(N("q", "repeat").Literal("count", "\"b\""));
            g.AddFunction(other);
            var diagnostics = BlockTypeChecker.Check(g);
            Assert.That(diagnostics.Select(d => d.Function + ":" + d.NodeId), Is.EqualTo(new[] { "main:r", "other:q" }));
        }

        [Test]
        public void NullGraphsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => BlockTypeChecker.Check(null));
            Assert.Throws<ArgumentNullException>(() => BlockTypeChecker.OutputType(null, "main", "x"));
            Assert.Throws<ArgumentNullException>(() => BlockTypeChecker.CheckWire(null, "main", "x", "a", "y"));
        }

        // ---- The editor's questions ----

        [Test]
        public void OutputTypeIsNullForAStepOrAMissingBlock()
        {
            var g = G(N("s", "set", "n").Literal("value", "1"));
            Assert.That(Out(g, "s"), Is.Null);
            Assert.That(Out(g, "nope"), Is.Null);
            Assert.That(BlockTypeChecker.OutputType(g, "nofunction", "s"), Is.Null);
        }

        [Test]
        public void CheckWireAcceptsAFittingWire()
        {
            var g = G(N("r", "repeat"), N("v", "literal").Literal("value", "3"));
            var check = BlockTypeChecker.CheckWire(g, "main", "r", "count", "v");
            Assert.That(check.Allowed, Is.True);
            Assert.That(check.Reason, Is.Null);
        }

        [Test]
        public void CheckWireRefusesAWireThatDoesNotFitAndSaysWhy()
        {
            var g = G(N("r", "repeat"), N("v", "literal").Literal("value", "\"x\""));
            var check = BlockTypeChecker.CheckWire(g, "main", "r", "count", "v");
            Assert.That(check.Allowed, Is.False);
            Assert.That(check.Reason, Does.Contain("str").And.Contain("int"));
        }

        [Test]
        public void CheckWireDoesNotChangeTheGraph()
        {
            var g = G(N("r", "repeat").Literal("count", "2"), N("v", "literal").Literal("value", "\"x\""));
            var json = g.ToJson();
            BlockTypeChecker.CheckWire(g, "main", "r", "count", "v");
            Assert.That(g.ToJson(), Is.EqualTo(json));
        }

        [Test]
        public void CheckWireJudgesTheNewWireInPlaceOfTheOldOne()
        {
            var g = G(N("r", "repeat").Connect("count", "bad"), N("bad", "literal").Literal("value", "\"x\""),
                      N("good", "literal").Literal("value", "2"));
            Assert.That(BlockTypeChecker.CheckWire(g, "main", "r", "count", "good").Allowed, Is.True, "the existing bad wire is replaced");
        }

        [Test]
        public void CheckWireRefusesAWireThatBreaksABlockFurtherAlong()
        {
            // "i" reads the list in "src"; "r" needs an int from it. Wiring a list of strings in would make "r" wrong.
            var g = G(N("r", "repeat").Connect("count", "i"),
                      N("i", "index").Connect("list", "ints").Literal("index", "0"),
                      N("ints", "list").Literal("item0", "1"),
                      N("strs", "list").Literal("item0", "\"a\""));
            var check = BlockTypeChecker.CheckWire(g, "main", "i", "list", "strs");
            Assert.That(check.Allowed, Is.False);
            Assert.That(check.Reason, Does.Contain("count"));
        }

        [Test]
        public void CheckWireDoesNotBlameAWireForAnOldMistake()
        {
            var g = G(N("r", "repeat").Literal("count", "\"already wrong\""), N("t", "repeat"), N("v", "literal").Literal("value", "1"));
            Assert.That(BlockTypeChecker.CheckWire(g, "main", "t", "count", "v").Allowed, Is.True);
        }

        [Test]
        public void CheckWireRefusesAStepAndAMissingSource()
        {
            var g = G(N("r", "repeat"), N("s", "set", "n").Literal("value", "1"));
            Assert.That(BlockTypeChecker.CheckWire(g, "main", "r", "count", "s").Allowed, Is.False);
            Assert.That(BlockTypeChecker.CheckWire(g, "main", "r", "count", "ghost").Allowed, Is.False);
        }

        [Test]
        public void CheckWireRefusesANoneResult()
        {
            var g = G(N("r", "repeat"), N("h", "call", "game.hurt").Literal("arg0", "5"));
            Assert.That(BlockTypeChecker.CheckWire(g, "main", "r", "count", "h", Library()).Allowed, Is.False);
        }

        [Test]
        public void CheckWireThrowsForAnUnknownFunctionOrBlock()
        {
            var g = G(N("r", "repeat"));
            Assert.Throws<ArgumentException>(() => BlockTypeChecker.CheckWire(g, "nofunction", "r", "count", "r"));
            Assert.Throws<ArgumentException>(() => BlockTypeChecker.CheckWire(g, "main", "ghost", "count", "r"));
        }

        // ---- Types of the pieces ----

        [Test]
        public void BlockSpecsKnowTheirPinTypes()
        {
            var lib = new BlockLibrary();
            var append = lib.FindBlock("append");
            Assert.That(append.InputType("list").ToString(), Is.EqualTo("list[$T]"));
            Assert.That(append.InputType("value").IsVariable, Is.True);
            Assert.That(append.OutputType, Is.Null, "a step has no value");
            Assert.That(lib.FindBlock("repeat").InputType("count"), Is.EqualTo(PinType.Int));
            Assert.That(lib.FindBlock("index").OutputType.IsVariable, Is.True);
            Assert.That(lib.FindBlock("eq").OutputType, Is.EqualTo(PinType.Bool));
            Assert.That(lib.FindBlock("add").InputType("a"), Is.EqualTo(PinType.Any));
            Assert.That(lib.FindBlock("add").OutputType, Is.EqualTo(PinType.Any));
            Assert.That(lib.FindBlock("list").InputType("item7"), Is.EqualTo(PinType.Any));
            Assert.That(lib.FindBlock("list").InputType(null), Is.EqualTo(PinType.Any));
        }

        [Test]
        public void BuiltInFunctionPinsGetTheTypeOfTheParameterTheyStandFor()
        {
            var lib = new BlockLibrary();
            // print was added with the pin "value" for "*values": nothing to type it by.
            Assert.That(lib.FindFunction("print").ParameterTypes, Is.EqualTo(new[] { PinType.Any }));
            // round's one pin stands for "number: float".
            Assert.That(lib.FindFunction("round").ParameterTypes, Is.EqualTo(new[] { PinType.Float }));
            Assert.That(lib.FindFunction("len").Outputs.Single().Type, Is.EqualTo(PinType.Int));
            Assert.That(lib.FindFunction("print").Outputs, Is.Empty);
        }

        [Test]
        public void ModuleFunctionPinsAreMatchedByName()
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m => m
                .Native("fn", (c, a) => Value.None, 0, 3, true)
                .Describe("fn", "first: int, *rest, flag: bool = False, label: str = '' -> none"));
            var spec = new BlockLibrary().AddAllModules(vm).FindFunction("game.fn");
            Assert.That(spec.Parameters, Is.EqualTo(new[] { "first", "flag", "label" }));
            Assert.That(spec.ParameterTypes, Is.EqualTo(new[] { PinType.Int, PinType.Bool, PinType.Str }));
        }

        [Test]
        public void TypeVariablesParseAndFitAnything()
        {
            var t = PinType.Parse("list[$T]");
            Assert.That(t.Element.IsVariable, Is.True);
            Assert.That(t.Element.Name, Is.EqualTo("$T"));
            Assert.That(t.ToString(), Is.EqualTo("list[$T]"));
            Assert.That(PinType.Variable("T"), Is.EqualTo(PinType.Parse("$T")));
            Assert.That(PinType.Handle("T").IsVariable, Is.False);
            Assert.That(PinCompatibility.CanConnect(PinType.Parse("list[int]"), t), Is.True);
            Assert.That(PinCompatibility.CanConnect(PinType.Str, PinType.Variable("T")), Is.True);
            Assert.That(PinCompatibility.CanConnect(PinType.Variable("T"), PinType.Int), Is.True);
            Assert.Throws<ArgumentException>(() => PinType.Variable(""));
            Assert.Throws<ArgumentException>(() => PinType.Parse("$"));
        }
    }
}
