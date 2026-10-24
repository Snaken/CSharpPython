using System.Collections.Generic;
using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    public class BlockDiagnosticsTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);
        private static BlockFunction F(string name, string start, params BlockNode[] nodes)
        {
            var f = new BlockFunction(name) { Start = start };
            foreach (var n in nodes) f.Add(n);
            return f;
        }
        private static BlockGraph G(params BlockFunction[] fns)
        {
            var g = new BlockGraph();
            foreach (var f in fns) g.AddFunction(f);
            return g;
        }

        private static List<BlockDiagnostic> Errors(BlockGraph g, BlockLibrary lib = null)
        {
            var r = BlockCompiler.Compile(g, lib);
            Assert.That(r.Succeeded, Is.False, "expected errors but got:\n" + r.Source);
            return r.Errors.ToList();
        }

        private static void ExpectError(BlockGraph g, string messagePart, string node = null, string pin = null, BlockLibrary lib = null)
        {
            var errors = Errors(g, lib);
            var match = errors.FirstOrDefault(e => e.Message.IndexOf(messagePart, System.StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.That(match, Is.Not.Null, "no error containing '" + messagePart + "' in: " + string.Join(" | ", errors.Select(e => e.ToString())));
            if (node != null) Assert.That(match.NodeId, Is.EqualTo(node));
            if (pin != null) Assert.That(match.Pin, Is.EqualTo(pin));
        }

        private static BlockNode Print(string id, string literal) => N(id, "call", "print").Literal("arg0", literal);

        // ---- Names ----

        [TestCase("1x")]
        [TestCase("class")]
        [TestCase("while")]
        [TestCase("a b")]
        [TestCase("a.b")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase(" x")]
        [TestCase("x\ny")]
        [TestCase("_b1")]
        [TestCase("_b42")]
        [TestCase("x$")]
        public void BadVariableNamesAreRejected(string name)
        {
            ExpectError(new BlockGraph().AddVariable(name, "0"), "not a valid variable name");
        }

        [TestCase("_b")]
        [TestCase("_b1x")]
        [TestCase("b1")]
        [TestCase("_x")]
        [TestCase("naïve")]
        public void UnusualButLegalNamesAreAccepted(string name)
        {
            Assert.That(BlockCompiler.Compile(new BlockGraph().AddVariable(name, "0")).Succeeded, Is.True);
        }

        [Test]
        public void DuplicateAndConflictingNames()
        {
            ExpectError(new BlockGraph().AddVariable("x", "1").AddVariable("x", "2"), "defined twice");
            ExpectError(G(F("f", null), F("f", null)), "defined twice");
            ExpectError(new BlockGraph().AddVariable("f", "1").AddFunction(F("f", null)), "both a variable and a function");
            ExpectError(G(F("not valid", null)), "not a valid function name");
        }

        [Test]
        public void BadStartingValues()
        {
            ExpectError(new BlockGraph().AddVariable("x", "1 + 2"), "starting value");
            ExpectError(new BlockGraph().AddVariable("x", "y"), "starting value");
            ExpectError(new BlockGraph().AddVariable("x", "[1]"), "starting value");
            Assert.That(BlockCompiler.Compile(new BlockGraph().AddVariable("x", "  ")).Source, Is.EqualTo("x = None\n"));
            Assert.That(BlockCompiler.Compile(new BlockGraph().AddVariable("x", " -1.5 ")).Source, Is.EqualTo("x = -1.5\n"));
        }

        [Test]
        public void BadParameters()
        {
            var f = F("f", null);
            f.Parameters.Add("a b");
            ExpectError(G(f), "not a valid parameter name");
            var g = F("g", null);
            g.Parameters.AddRange(new[] { "a", "a" });
            ExpectError(G(g), "appears twice");
        }

        [Test]
        public void LocalNameCannotBeAFunctionName()
        {
            var main = F("main", "s", N("s", "set", "helper").Literal("value", "1"));
            ExpectError(G(main, F("helper", null)), "both a variable and a function", "s");
        }

        // ---- Nodes and flow ----

        [Test]
        public void NodeIdProblems()
        {
            ExpectError(G(F("f", null, N(null, "break"))), "no id");
            ExpectError(G(F("f", null, N("", "break"))), "no id");
            ExpectError(G(F("f", null, N("a", "break"), N("a", "break"))), "more than once", "a");
            ExpectError(G(F("f", null, N("a", "break")), F("g", null, N("a", "break"))), "more than once", "a");
        }

        [Test]
        public void FlowMustLeadToExistingBlocksInTheSameFunction()
        {
            ExpectError(G(F("f", "nope")), "not in this function");
            ExpectError(G(F("f", "a", Print("a", "1").Then("zzz"))), "not in this function");
            ExpectError(G(F("f", "a", Print("a", "1").Then("b")), F("g", null, Print("b", "2"))), "not in this function");
        }

        [Test]
        public void FlowCannotLoopOrJoin()
        {
            ExpectError(G(F("f", "a", Print("a", "1").Then("b"), Print("b", "2").Then("a"))), "reached more than once", "a");
            var join = F("f", "i",
                N("i", "if").Literal("cond", "True").Then("then", "x").Then("else", "y"),
                Print("x", "1").Then("z"), Print("y", "2").Then("z"), Print("z", "3"));
            ExpectError(G(join), "reached more than once", "z");
        }

        [Test]
        public void ValueBlocksCannotBeInTheFlow()
        {
            ExpectError(G(F("f", "a", N("a", "add").Literal("a", "1").Literal("b", "2"))), "produces a value", "a");
            ExpectError(G(F("f", "a", N("a", "get", "x"))), "produces a value", "a");
            ExpectError(G(F("f", "a", N("a", "call", "len").Literal("arg0", "\"x\""))), "returns a value", "a");
        }

        [Test]
        public void UnknownBlocksAndPins()
        {
            ExpectError(G(F("f", "a", N("a", "frobnicate"))), "unknown block", "a");
            ExpectError(G(F("f", "a", N("a", null))), "unknown block", "a");
            ExpectError(G(F("f", "a", N("a", "set", "x").Literal("value", "1").Literal("bogus", "1"))), "no input pin", "a", "bogus");
            ExpectError(G(F("f", "a", N("a", "set", "x").Literal("value", "1").Literal("value", "2"))), "connected twice", "a", "value");
            ExpectError(G(F("f", "a", N("a", "set", "x").Literal("value", "1").Then("then", "b"), Print("b", "1"))), "no execution pin", "a", "then");
            ExpectError(G(F("f", "a", N("a", "if").Literal("cond", "True").Then("then", "b").Then("then", "c"), Print("b", "1"), Print("c", "1"))), "linked twice", "a", "then");
        }

        [Test]
        public void ValueBlocksHaveNoExecutionPins()
        {
            var f = F("f", "s",
                N("s", "set", "x").Connect("value", "v"),
                N("v", "add").Literal("a", "1").Literal("b", "2").Then("s"));
            ExpectError(G(f), "no execution pin", "v", "next");
        }

        // ---- Inputs and literals ----

        [Test]
        public void MissingInputsAreReported()
        {
            ExpectError(G(F("f", "a", N("a", "set", "x"))), "has no value", "a", "value");
            var empty = N("a", "set", "x");
            empty.Inputs.Add(new BlockInput("value", "", "  "));
            ExpectError(G(F("f", "a", empty)), "has no value", "a", "value");
            ExpectError(G(F("f", "a", N("a", "call", "print"))), "has no value", "a", "arg0");
        }

        [TestCase("1 + 2")]
        [TestCase("x")]
        [TestCase("f\"{x}\"")]
        [TestCase("\"a\" \"b\"")]
        [TestCase("-x")]
        [TestCase("--1")]
        [TestCase("(1)")]
        [TestCase("[1]")]
        [TestCase("1;2")]
        [TestCase("\"\"\"a\nb\"\"\"")]
        [TestCase("1\n2")]
        [TestCase("\"unterminated")]
        [TestCase("1 $")]
        [TestCase("not True")]
        public void OnlySingleLiteralsAreAccepted(string literal)
        {
            ExpectError(G(F("f", "a", N("a", "set", "x").Literal("value", literal))), "single number, string", "a", "value");
        }

        [TestCase("1")]
        [TestCase("-1")]
        [TestCase("- 1")]
        [TestCase("2.5")]
        [TestCase("-.5")]
        [TestCase("\"x\"")]
        [TestCase("'x'")]
        [TestCase("True")]
        [TestCase("False")]
        [TestCase("None")]
        [TestCase("  7  ")]
        public void SingleLiteralsAreAccepted(string literal)
        {
            var g = G(F("f", "a", N("a", "set", "x").Literal("value", literal)));
            var r = BlockCompiler.Compile(g);
            Assert.That(r.Errors.Select(e => e.ToString()), Is.Empty);
            Assert.That(new PyVm().Compile(r.Source), Is.Not.Null);
        }

        [Test]
        public void ConnectionsMustPointAtValues()
        {
            ExpectError(G(F("f", "a", N("a", "set", "x").Connect("value", "zzz"))), "not in this function", "a", "value");
            ExpectError(G(F("f", "a", N("a", "set", "x").Connect("value", "b"), N("b", "set", "y").Literal("value", "1"))), "no value to connect", "a", "value");
            ExpectError(G(F("f", "a", N("a", "set", "x").Connect("value", "b"), N("b", "mystery"))), "unknown block", "b");
            ExpectError(G(F("f", "a", N("a", "set", "x").Connect("value", "b")), F("g", null, N("b", "literal").Literal("value", "1"))), "not in this function", "a", "value");
        }

        [Test]
        public void ValuesCannotDependOnThemselves()
        {
            var cycle = F("f", "s",
                N("s", "set", "x").Connect("value", "a"),
                N("a", "add").Connect("a", "b").Literal("b", "1"),
                N("b", "add").Connect("a", "a").Literal("b", "1"));
            ExpectError(G(cycle), "depends on itself");
            var self = F("f", "s", N("s", "set", "x").Connect("value", "a"), N("a", "neg").Connect("a", "a"));
            ExpectError(G(self), "depends on itself", "a");
        }

        [Test]
        public void ListItemsMustBeNumberedWithoutGaps()
        {
            var f = F("f", "s", N("s", "set", "x").Connect("value", "l"), N("l", "list").Literal("item0", "1").Literal("item2", "3"));
            ExpectError(G(f), "numbered item0", "l");
            var bad = F("f", "s", N("s", "set", "x").Connect("value", "l"), N("l", "list").Literal("items", "1"));
            ExpectError(G(bad), "no input pin", "l");
            var empty = F("f", "s", N("s", "set", "x").Connect("value", "l"), N("l", "list"));
            Assert.That(BlockCompiler.Compile(G(empty)).Source, Does.Contain("x = []"));
        }

        // ---- Variables ----

        [Test]
        public void UnknownVariablesAreReported()
        {
            var read = F("f", "s", N("s", "set", "x").Connect("value", "g"), N("g", "get", "ghost"));
            ExpectError(G(read), "does not exist", "g");
            var change = F("f", "c", N("c", "change", "ghost").Literal("value", "1"));
            ExpectError(G(change), "does not exist", "c");
            Assert.That(BlockCompiler.Compile(G(F("f", "s", N("s", "set", "x").Connect("value", "g"), N("g", "get", "x")))).Succeeded, Is.True);
        }

        [Test]
        public void NamedBlocksNeedValidNames()
        {
            ExpectError(G(F("f", "s", N("s", "set", "").Literal("value", "1"))), "not a valid variable name", "s");
            ExpectError(G(F("f", "s", N("s", "for_each", "class").Literal("items", "1"))), "not a valid variable name", "s");
            ExpectError(G(F("f", "r", N("r", "repeat", "a b").Literal("count", "2"))), "not a valid variable name", "r");
        }

        // ---- Calls ----

        [Test]
        public void CallProblems()
        {
            ExpectError(G(F("f", "c", N("c", "call"))), "needs a function name", "c");
            ExpectError(G(F("f", "c", N("c", "call", "nope"))), "unknown function 'nope'", "c");
            ExpectError(G(F("f", "c", N("c", "call", "print").Literal("arg0", "1").Literal("arg1", "2"))), "no input pin", "c", "arg1");
            var s = F("f", "s", N("s", "set", "x").Connect("value", "c"), N("c", "call", "ghost"));
            ExpectError(G(s), "unknown function 'ghost'");
        }

        [Test]
        public void ModuleFunctionsNeedTheirModulePrefix()
        {
            var lib = new BlockLibrary().AddFunction("spawn", new[] { "kind" }, module: "game");
            ExpectError(G(F("f", "c", N("c", "call", "spawn").Literal("arg0", "1"))), "unknown function 'spawn'", lib: lib);
            Assert.That(BlockCompiler.Compile(G(F("f", "c", N("c", "call", "game.spawn").Literal("arg0", "1"))), lib).Succeeded, Is.True);
        }

        [Test]
        public void UserBlocksTakeTheirParametersAsPins()
        {
            var helper = new BlockFunction("helper", "a", "b");
            var main = F("main", "c", N("c", "call", "helper").Literal("arg0", "1"));
            ExpectError(G(helper, main), "has no value", "c", "arg1");
            var extra = F("main", "c", N("c", "call", "helper").Literal("arg0", "1").Literal("arg1", "2").Literal("arg2", "3"));
            ExpectError(G(helper, extra), "no input pin", "c", "arg2");
        }

        // ---- Results of calls ----

        private static BlockLibrary Dice() => new BlockLibrary().AddFunction("roll", new string[0], module: "dice");

        [Test]
        public void ResultsCannotBeUsedBeforeTheCallRuns()
        {
            var f = F("f", "p",
                Print("p", "1").Then("r"), N("r", "call", "dice.roll").Then("q"),
                N("q", "set", "x").Connect("value", "r"));
            // Fine: r runs before q.
            Assert.That(BlockCompiler.Compile(G(f), Dice()).Succeeded, Is.True);

            var early = F("f", "q", N("q", "set", "x").Connect("value", "r").Then("r"), N("r", "call", "dice.roll"));
            ExpectError(G(early), "before it has run", "q", "value", Dice());
        }

        [Test]
        public void ResultsStayInsideTheirBranch()
        {
            var f = F("f", "i",
                N("i", "if").Literal("cond", "True").Then("then", "r").Then("next", "use"),
                N("r", "call", "dice.roll"),
                N("use", "set", "x").Connect("value", "r"));
            ExpectError(G(f), "before it has run", "use", "value", Dice());

            var inside = F("f", "i",
                N("i", "if").Literal("cond", "True").Then("then", "r"),
                N("r", "call", "dice.roll").Then("use"),
                N("use", "set", "x").Connect("value", "r"));
            Assert.That(BlockCompiler.Compile(G(inside), Dice()).Succeeded, Is.True);

            var outer = F("f", "r",
                N("r", "call", "dice.roll").Then("i"),
                N("i", "if").Literal("cond", "True").Then("then", "use"),
                N("use", "set", "x").Connect("value", "r"));
            Assert.That(BlockCompiler.Compile(G(outer), Dice()).Succeeded, Is.True);
        }

        [Test]
        public void ALoopConditionCannotUseAStoredResult()
        {
            var f = F("f", "r",
                N("r", "call", "dice.roll").Then("w"),
                N("w", "while").Connect("cond", "ne").Then("body", "b"),
                N("ne", "ne").Connect("a", "r").Literal("b", "0"),
                Print("b", "1"));
            ExpectError(G(f), "loop condition", "ne", "a", Dice());
        }

        [Test]
        public void LoopControlOnlyWorksInsideLoops()
        {
            ExpectError(G(F("f", "b", N("b", "break"))), "only works inside a loop", "b");
            ExpectError(G(F("f", "b", N("b", "continue"))), "only works inside a loop", "b");
            var after = F("f", "w", N("w", "while").Literal("cond", "True").Then("next", "b"), N("b", "break"));
            ExpectError(G(after), "only works inside a loop", "b");
            var inside = F("f", "w", N("w", "while").Literal("cond", "True").Then("body", "b"), N("b", "break"));
            Assert.That(BlockCompiler.Compile(G(inside)).Succeeded, Is.True);
        }

        // ---- Warnings ----

        [Test]
        public void DisconnectedBlocksAreWarnedAbout()
        {
            var f = F("f", "a", Print("a", "1"), Print("lonely", "2"), N("v", "add").Literal("a", "1").Literal("b", "2"));
            var r = BlockCompiler.Compile(G(f));
            Assert.That(r.Succeeded, Is.True);
            var warnings = r.Diagnostics.Where(d => d.Severity == BlockSeverity.Warning).ToList();
            Assert.That(warnings.Select(w => w.NodeId), Is.EquivalentTo(new[] { "lonely", "v" }));
            Assert.That(warnings.All(w => w.Function == "f"), Is.True);
            Assert.That(r.Errors, Is.Empty);
        }

        [Test]
        public void ValueBlocksOnlyUsedByDisconnectedBlocksAreWarnedToo()
        {
            var f = F("f", "a", Print("a", "1"), N("s", "set", "x").Connect("value", "v"), N("v", "neg").Literal("a", "1"));
            var r = BlockCompiler.Compile(G(f));
            Assert.That(r.Diagnostics.Select(d => d.NodeId), Is.EquivalentTo(new[] { "s", "v" }));
        }

        // ---- Robustness ----

        [Test]
        public void NullsInsideTheGraphAreTolerated()
        {
            var node = N("a", "set", "x");
            node.Inputs = null;
            node.Flow = null;
            var g = new BlockGraph();
            g.Variables.Add(null);
            g.Functions.Add(null);
            g.Functions.Add(F("f", "a", node, null));
            g.Functions[1].Nodes.Add(N("b", "if"));
            g.Functions[1].Nodes[2].Inputs.Add(null);
            g.Functions[1].Nodes[2].Flow.Add(null);
            Assert.DoesNotThrow(() => BlockCompiler.Compile(g));
            Assert.That(BlockCompiler.Compile(g).Succeeded, Is.False);

            var noLists = new BlockFunction("g") { Parameters = new List<string>(), Nodes = new List<BlockNode>() };
            Assert.That(BlockCompiler.Compile(G(noLists)).Succeeded, Is.True);
        }

        [Test]
        public void DiagnosticsCarryTheFunctionName()
        {
            var e = Errors(G(F("first", null), F("second", "x")));
            Assert.That(e[0].Function, Is.EqualTo("second"));
            Assert.That(e[0].Severity, Is.EqualTo(BlockSeverity.Error));
        }
    }
}
