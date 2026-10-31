using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // The operator blocks refuse two typed inputs that Python cannot combine; unknown types are never refused.
    public class BlockOperatorTypeTests
    {
        private static BlockGraph Graph(string op, string a, string b)
        {
            var f = new BlockFunction("main") { Start = "r" };
            f.Add(new BlockNode("r", "return").Connect("value", "o"));
            var node = new BlockNode("o", op);
            if (a != null) node.Literal("a", a);
            if (b != null) node.Literal("b", b);
            f.Add(node);
            return new BlockGraph().AddFunction(f);
        }

        private static string[] Errors(BlockGraph g) => BlockTypeChecker.Check(g).Select(d => d.NodeId + "." + d.Pin).ToArray();

        [TestCase("add", "1", "2.5")]
        [TestCase("add", "\"a\"", "\"b\"")]
        [TestCase("sub", "1", "True")]
        [TestCase("mul", "\"ab\"", "3")]
        [TestCase("mul", "3", "\"ab\"")]
        [TestCase("mul", "2", "2.5")]
        [TestCase("div", "1", "2")]
        [TestCase("floordiv", "7", "2")]
        [TestCase("pow", "2", "3")]
        [TestCase("mod", "7", "2")]
        [TestCase("mod", "\"%d items\"", "3")]
        [TestCase("lt", "1", "2.5")]
        [TestCase("ge", "\"a\"", "\"b\"")]
        [TestCase("contains", "\"a\"", "\"abc\"")]
        public void CombinationsPythonAllowsAreAccepted(string op, string a, string b) =>
            Assert.That(Errors(Graph(op, a, b)), Is.Empty);

        [TestCase("sub", "\"a\"", "1", "o.b")]
        [TestCase("sub", "1", "\"a\"", "o.b")]
        [TestCase("add", "\"a\"", "1", "o.b")]
        [TestCase("mul", "\"a\"", "\"b\"", "o.b")]
        [TestCase("mul", "\"a\"", "2.5", "o.b")]
        [TestCase("div", "\"a\"", "2", "o.b")]
        [TestCase("floordiv", "1", "\"a\"", "o.b")]
        [TestCase("pow", "\"a\"", "2", "o.b")]
        [TestCase("mod", "1", "\"a\"", "o.b")]
        [TestCase("lt", "1", "\"a\"", "o.b")]
        [TestCase("ge", "\"a\"", "1", "o.b")]
        [TestCase("contains", "1", "5", "o.b")]
        [TestCase("contains", "1", "\"abc\"", "o.b")]
        public void CombinationsPythonRefusesAreReported(string op, string a, string b, string where) =>
            Assert.That(Errors(Graph(op, a, b)), Is.EqualTo(new[] { where }));

        [Test]
        public void NegateNeedsANumber()
        {
            Assert.That(Errors(Graph("neg", "2", null)), Is.Empty);
            Assert.That(Errors(Graph("neg", "\"a\"", null)), Is.EqualTo(new[] { "o.a" }));
        }

        [Test]
        public void ANonNumberCannotBeNegated() =>
            Assert.That(BlockTypeChecker.Check(Graph("neg", "\"a\"", null)).Single().Message, Does.Contain("needs a number"));

        [TestCase("sub")]
        [TestCase("add")]
        [TestCase("lt")]
        public void AnUnwiredOrUntypedPinIsNeverRefused(string op)
        {
            Assert.That(Errors(Graph(op, "\"a\"", null)), Is.Empty);
            Assert.That(Errors(Graph(op, null, "1")), Is.Empty);
            Assert.That(Errors(Graph(op, "None", "1")), Is.Empty, "None is untyped");
        }

        [Test]
        public void AnUnwiredOrUntypedContainerIsNeverRefused()
        {
            Assert.That(Errors(Graph("contains", "\"a\"", null)), Is.Empty);
            Assert.That(Errors(Graph("contains", null, "\"abc\"")), Is.Empty);
            Assert.That(Errors(Graph("contains", "1", "None")), Is.Empty);
        }

        [Test]
        public void EqualityAndLogicTakeAnything()
        {
            foreach (var op in new[] { "eq", "ne", "and", "or" })
                Assert.That(Errors(Graph(op, "\"a\"", "1")), Is.Empty, op);
        }

        [Test]
        public void ListsCanBeAddedAndRepeatedButNotSubtracted()
        {
            var f = new BlockFunction("main") { Start = "r" };
            f.Add(new BlockNode("r", "return").Connect("value", "o"));
            f.Add(new BlockNode("l", "list").Literal("item0", "1"));
            f.Add(new BlockNode("m", "list").Literal("item0", "2"));
            f.Add(new BlockNode("o", "add").Connect("a", "l").Connect("b", "m"));
            var graph = new BlockGraph().AddFunction(f);
            Assert.That(Errors(graph), Is.Empty);

            f.Nodes.Single(n => n.Id == "o").Block = "sub";
            Assert.That(Errors(graph), Is.EqualTo(new[] { "o.b" }));

            f.Nodes.Single(n => n.Id == "o").Block = "mul";
            f.Nodes.Single(n => n.Id == "m").Block = "literal";
            f.Nodes.Single(n => n.Id == "m").Inputs.Clear();
            f.Nodes.Single(n => n.Id == "m").Literal("value", "3");
            Assert.That(Errors(graph), Is.Empty);
        }

        [Test]
        public void TheEditorRefusesAWireThatBreaksAnOperator()
        {
            var main = new BlockFunction("main") { Start = "s" };
            main.Add(new BlockNode("s", "set", "x").Connect("value", "o"));
            main.Add(new BlockNode("o", "sub").Literal("a", "5"));
            main.Add(new BlockNode("t", "literal").Literal("value", "\"hi\""));
            main.Add(new BlockNode("n", "literal").Literal("value", "2"));
            var editor = new BlockEditor(new BlockGraph().AddFunction(main), new BlockLibrary());

            var refused = editor.Connect("main", "o", "b", "t");
            Assert.That(refused.Succeeded, Is.False);
            Assert.That(refused.Reason, Does.Contain("cannot take"));
            Assert.That(editor.Connect("main", "o", "b", "n").Succeeded, Is.True);
        }

        [Test]
        public void TheEditorRefusesALiteralThatBreaksAnOperator()
        {
            var main = new BlockFunction("main") { Start = "s" };
            main.Add(new BlockNode("s", "set", "x").Connect("value", "o"));
            main.Add(new BlockNode("o", "sub").Literal("a", "5"));
            var editor = new BlockEditor(new BlockGraph().AddFunction(main), new BlockLibrary());

            Assert.That(editor.SetLiteral("main", "o", "b", "\"hi\"").Succeeded, Is.False);
            Assert.That(editor.SetLiteral("main", "o", "b", "1").Succeeded, Is.True);
        }

        [Test]
        public void ABadOperatorDoesNotCompile()
        {
            var result = BlockCompiler.Compile(Graph("sub", "\"a\"", "1"));
            Assert.That(result.Succeeded, Is.False);
        }
    }
}
