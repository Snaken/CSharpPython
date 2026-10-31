using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // Global variables can be declared with a type (docs/node-view-design.md, decision 16).
    public class BlockTypedVariableTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        private static BlockGraph G(BlockVariable variable, params BlockNode[] nodes)
        {
            var f = new BlockFunction("main") { Start = nodes[0].Id };
            foreach (var n in nodes) f.Add(n);
            var g = new BlockGraph().AddFunction(f);
            g.Variables.Add(variable);
            return g;
        }

        private static string FirstError(BlockGraph g)
        {
            var r = BlockCompiler.Compile(g);
            Assert.That(r.Succeeded, Is.False, "expected an error but got:\n" + r.Source);
            return r.Errors.First().Message;
        }

        private static BlockGraph Plain(string type, string initial) => G(new BlockVariable("v", initial, type), N("c", "comment", "x"));

        // ---- Starting values ----

        [TestCase("int", null, "v = 0\n")]
        [TestCase("float", "", "v = 0.0\n")]
        [TestCase("bool", " ", "v = False\n")]
        [TestCase("str", null, "v = \"\"\n")]
        [TestCase("list[int]", null, "v = None\n")]
        [TestCase("Entity", "", "v = None\n")]
        [TestCase(null, null, "v = None\n")]
        [TestCase("int", "5", "v = 5\n")]
        [TestCase("float", "2", "v = 2.0\n")]
        [TestCase("float", "-2", "v = -2.0\n")]
        [TestCase("float", "2.5", "v = 2.5\n")]
        [TestCase("list[int]", "None", "v = None\n")]
        [TestCase("any", "\"a\"", "v = \"a\"\n")]
        public void AVariableStartsWithItsTypesValue(string type, string initial, string line)
        {
            var r = BlockCompiler.Compile(Plain(type, initial));
            Assert.That(r.Errors.Select(e => e.ToString()), Is.Empty);
            Assert.That(r.Source, Does.StartWith(line));
        }

        [TestCase("int", "\"a\"")]
        [TestCase("int", "1.5")]
        [TestCase("str", "3")]
        [TestCase("bool", "1")]
        public void AStartingValueOfAnotherTypeIsAnError(string type, string initial)
        {
            Assert.That(FirstError(Plain(type, initial)), Does.Contain("the variable is declared " + type));
        }

        [TestCase("int")]
        [TestCase("float")]
        [TestCase("bool")]
        [TestCase("str")]
        public void ANumberBoolOrStringVariableCannotStartAsNone(string type)
        {
            Assert.That(FirstError(Plain(type, "None")), Does.Contain("cannot start as None"));
        }

        [TestCase("list[")]
        [TestCase("$T")]
        [TestCase("none")]
        [TestCase("list[none]")]
        [TestCase("dict[int]")]
        public void ABadTypeIsAnError(string type)
        {
            Assert.That(FirstError(Plain(type, null)), Does.Contain("type of 'v' is not valid"));
        }

        // ---- Using them ----

        [Test]
        public void ReadingATypedVariableGivesItsType()
        {
            var g = G(new BlockVariable("v", null, "list[int]"), N("c", "comment", "x"), N("r", "get", "v"));
            Assert.That(BlockTypeChecker.OutputType(g, "main", "r").ToString(), Is.EqualTo("list[int]"));
            var plain = G(new BlockVariable("v", null), N("c", "comment", "x"), N("r", "get", "v"));
            Assert.That(BlockTypeChecker.OutputType(plain, "main", "r").ToString(), Is.EqualTo("any"));
        }

        [Test]
        public void AParameterWithTheNameOfAGlobalIsNotTyped()
        {
            var f = new BlockFunction("main", "v") { Start = "p" };
            f.Add(N("p", "call", "print").Connect("arg0", "r"));
            f.Add(N("r", "get", "v"));
            var g = new BlockGraph().AddFunction(f);
            g.Variables.Add(new BlockVariable("v", null, "int"));
            Assert.That(BlockTypeChecker.OutputType(g, "main", "r").ToString(), Is.EqualTo("any"));
        }

        [Test]
        public void ASetMustGiveTheDeclaredType()
        {
            var g = G(new BlockVariable("v", null, "int"), N("s", "set", "v").Literal("value", "\"a\""));
            Assert.That(BlockTypeChecker.Check(g).Select(d => d.NodeId + "." + d.Pin), Contains.Item("s.value"));
            Assert.That(FirstError(g), Does.Contain("cannot take this wire"));
            var ok = G(new BlockVariable("v", null, "float"), N("s", "set", "v").Literal("value", "3"));
            Assert.That(BlockCompiler.Compile(ok).Succeeded, Is.True, "an int fits a float variable");
        }

        [Test]
        public void ChangeMustGiveTheDeclaredType()
        {
            var g = G(new BlockVariable("v", null, "int"), N("s", "change", "v").Literal("value", "\"a\""));
            Assert.That(BlockCompiler.Compile(g).Succeeded, Is.False);
            var ok = G(new BlockVariable("v", null, "int"), N("s", "change", "v").Literal("value", "2"));
            Assert.That(BlockCompiler.Compile(ok).Succeeded, Is.True);
        }

        [Test]
        public void AWireIntoATypedVariableIsCheckedByTheEditor()
        {
            var g = G(new BlockVariable("v", null, "str"),
                N("s", "set", "v"), N("n", "add").Literal("a", "1").Literal("b", "2"), N("t", "literal").Literal("value", "\"x\""));
            Assert.That(BlockTypeChecker.CheckWire(g, "main", "s", "value", "n").Allowed, Is.False);
            Assert.That(BlockTypeChecker.CheckWire(g, "main", "s", "value", "t").Allowed, Is.True);
        }

        [Test]
        public void ALoopMustStoreItsValueInAMatchingVariable()
        {
            var bad = G(new BlockVariable("v", null, "str"), N("l", "for_range", "v").Literal("stop", "3"));
            Assert.That(FirstError(bad), Does.Contain("declared str"));
            var good = G(new BlockVariable("v", null, "int"), N("l", "for_range", "v").Literal("stop", "3"));
            Assert.That(BlockCompiler.Compile(good).Succeeded, Is.True);
        }

        [Test]
        public void ATypedProgramRuns()
        {
            var g = G(new BlockVariable("total", null, "int"),
                N("s", "change", "total").Literal("value", "4").Then("p"),
                N("p", "call", "print").Connect("arg0", "r"), N("r", "get", "total"));
            var r = BlockCompiler.Compile(g);
            Assert.That(r.Errors.Select(e => e.ToString()), Is.Empty);
            Assert.That(Vm.Py.Run(r.Source + "main()\n"), Is.EqualTo("4\n"));
        }

        // ---- Saving ----

        [Test]
        public void TheTypeIsSavedAndLoaded()
        {
            var g = G(new BlockVariable("v", "1", "int"), N("c", "comment", "x"));
            var back = BlockGraph.FromJson(g.ToJson());
            Assert.That(back.Variables[0].Type, Is.EqualTo("int"));
            Assert.That(back.Variables[0].Initial, Is.EqualTo("1"));
            Assert.That(BlockGraph.FromJson(Plain(null, null).ToJson()).Variables[0].Type, Is.Null);
            Assert.That(Plain(null, null).ToJson(), Does.Not.Contain("\"Type\""));
        }
    }
}
