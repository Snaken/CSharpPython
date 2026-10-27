using System.Linq;
using CSharpPython.Compilation;
using CSharpPython.Syntax;

namespace CSharpPython.Tests.Compilation
{
    public class PeepholeTests
    {
        private static Code Function(string src) => Compiler.Compile(Parser.ParseModule(src), "test").Functions[0];

        [Test]
        public void CombinedInstructionsReplaceTheFirstOfARunOnly()
        {
            var code = Function("def f(n):\n    if n < 2:\n        return n\n    return n - 1\n");
            Assert.That(code.Instructions[0].Op, Is.EqualTo(Op.LoadLocal));
            Assert.That(code.Fast[0].Op, Is.EqualTo(Op.LocalConstCmpJump));
            Assert.That(code.Fast.Length, Is.EqualTo(code.Instructions.Length));
            for (int i = 1; i < 4; i++) Assert.That(code.Fast[i].Op, Is.EqualTo(code.Instructions[i].Op), "the rest of the run stays for a jump into it");
            Assert.That(code.Fast.Select(i => i.Op), Does.Contain(Op.LocalConstArith));
        }

        [TestCase("def f(a, b):\n    return a + b\n", "LoadLocal2")]
        [TestCase("def f(a):\n    return a.x\n", "LoadLocalAttr")]
        [TestCase("def f(a):\n    return a + 1\n", "LocalConstArith")]
        [TestCase("def f(a):\n    return a, 1\n", "LoadLocalConst")]
        [TestCase("def f(a):\n    return (a * 2) % 7\n", "ConstArith")]
        [TestCase("def f(a, b):\n    if a < b:\n        return 1\n    return 2\n", "CmpJump")]
        public void EachKindOfRunIsCombined(string source, string expected)
        {
            Assert.That(Function(source).Fast.Select(i => i.Op.ToString()), Does.Contain(expected));
        }

        [Test]
        public void ALocalBeforeALongerRunIsLeftAlone()
        {
            var code = Function("def f(a, b):\n    return a + b * 2\n");
            Assert.That(code.Fast.Select(i => i.Op), Does.Not.Contain(Op.LoadLocal2));
            Assert.That(code.Fast.Select(i => i.Op), Does.Contain(Op.LocalConstArith));
        }

        [Test]
        public void TheListingStaysTheOriginalCode()
        {
            var code = Function("def f(n):\n    return n + 1\n");
            Assert.That(code.Disassemble(), Does.Contain("LoadLocal 0").And.Contain("LoadConst 1").And.Contain("Add"));
            Assert.That(code.Disassemble(), Does.Not.Contain("LocalConstArith"));
        }

        [Test]
        public void HandBuiltCodeRunsItsInstructions()
        {
            var code = new Code { Instructions = new[] { new Instruction(Op.Return, 0) } };
            Assert.That(code.Fast, Is.SameAs(code.Instructions));
        }

        [TestCase("lambda x: -x", true)]
        [TestCase("lambda x, y: x * 2 + y if x > y else y", true)]
        [TestCase("lambda p: p.x < 3 and p.y", true)]
        [TestCase("lambda x: abs(x)", false)]
        [TestCase("lambda x: [x]", false)]
        [TestCase("lambda x: x ** 2", false)]
        [TestCase("lambda x=1: x", true)] // only used when every argument is given
        [TestCase("lambda *a: a", false)]
        [TestCase("lambda x: (yield x)", false)]
        [TestCase("lambda a, b, c: a", false)]
        public void OnlyPlainFunctionsAreLeaves(string lambda, bool expected)
        {
            var module = Compiler.Compile(Parser.ParseModule("f = " + lambda + "\n"), "test");
            Assert.That(module.Functions[0].IsLeaf, Is.EqualTo(expected));
        }

        [Test]
        public void AFunctionThatLoopsIsNotALeaf()
        {
            Assert.That(Function("def f(x):\n    while True:\n        pass\n").IsLeaf, Is.False);
            Assert.That(Function("def f(x):\n    while x:\n        x = x - 1\n    return x\n").IsLeaf, Is.False);
            Assert.That(Function("def f(x):\n    return x\n").IsLeaf, Is.True);
        }

        [Test]
        public void AGeneratorOrAFunctionWithManyLocalsIsNotALeaf()
        {
            Assert.That(Function("def f(x):\n    yield x\n").IsLeaf, Is.False);
            Assert.That(Function("def f(x):\n    a = b = c = d = e = g = h = i = x\n    return a\n").IsLeaf, Is.False);
        }
    }
}
