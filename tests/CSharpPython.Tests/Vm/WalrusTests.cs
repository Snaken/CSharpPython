using CSharpPython.Editor;
using CSharpPython.Syntax;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class WalrusTests
    {
        private static PySyntaxError SyntaxFails(string src) => Assert.Throws<PySyntaxError>(() => new PyVm().Compile(src));

        [TestCase("x := 1")]
        [TestCase("x = y := 1")]
        [TestCase("a, b := 1, 2")]
        [TestCase("f(a=b := 1)")]
        [TestCase("lambda: x := 1")]
        [TestCase("return_value = [x for x in xs if x := 1]")]
        [TestCase("(a.b := 1)")]
        [TestCase("(a[0] := 1)")]
        [TestCase("(1 := 2)")]
        [TestCase("d = {a := 1: 2}")]
        [TestCase("(x := )")]
        [TestCase("x := := 1")]
        public void MisplacedAssignmentExpressionsAreSyntaxErrors(string src) => SyntaxFails(src);

        [Test]
        public void ErrorsForComprehensionRules()
        {
            Assert.That(SyntaxFails("xs = [1]\nys = [(x := 1) for x in xs]").Problem, Does.Contain("cannot rebind comprehension iteration variable 'x'"));
            Assert.That(SyntaxFails("class A:\n    ys = [(y := 1) for _ in [1]]").Problem, Does.Contain("cannot be used in a class body"));
            Assert.That(SyntaxFails("ys = [[(x := 1) for _ in [1]] for x in [1]]").Problem, Does.Contain("cannot rebind comprehension iteration variable 'x'"));
        }

        [Test]
        public void ClassBodiesBindTheNameAsAClassVariable()
        {
            Assert.That(Out("class A:\n    if (n := 3) > 1:\n        pass\nprint(A.n)"), Is.EqualTo("3"));
        }

        [Test]
        public void ValueIsEvaluatedBeforeTheNameIsBound()
        {
            Assert.That(Out("x = 1\nprint((x := x + 1), x)"), Is.EqualTo("2 2"));
        }

        [Test]
        public void ErrorInTheValueLeavesTheNameUnbound()
        {
            Assert.That(Out("try:\n    (v := 1 // 0)\nexcept ZeroDivisionError:\n    print('caught')\nprint('v' in dir() if False else 'ok')"), Is.EqualTo("caught\nok"));
            Assert.That(Fails("def f():\n    (v := 1 // 0)\nf()\n").TypeName, Is.EqualTo("ZeroDivisionError"));
            Assert.That(Fails("def f():\n    try:\n        (v := 1 // 0)\n    except ZeroDivisionError:\n        pass\n    return v\nf()\n").TypeName, Is.EqualTo("UnboundLocalError"));
        }

        [Test]
        public void ComprehensionBindingSeenByAnInnerFunctionKeepsUpdating()
        {
            Assert.That(Out("def f(data):\n    xs = [(last := x) for x in data]\n    def get():\n        return last\n    return get\ng = f([1, 2, 3])\nprint(g())"), Is.EqualTo("3"));
        }

        [Test]
        public void ComprehensionBindingInsideAModuleLevelFunctionDefinedLater()
        {
            Assert.That(Out("ys = [(t := i) for i in range(3)]\ndef show():\n    return t\nprint(show())"), Is.EqualTo("2"));
        }

        [Test]
        public void GlobalDeclaredNameStaysGlobalInsideAComprehension()
        {
            Assert.That(Out("def f():\n    global g\n    xs = [(g := i) for i in range(3)]\nf()\nprint(g)"), Is.EqualTo("2"));
        }

        [Test]
        public void NonlocalDeclaredNameStaysNonlocalInsideAComprehension()
        {
            Assert.That(Out("def f():\n    c = 0\n    def g():\n        nonlocal c\n        xs = [(c := c + i) for i in range(4)]\n    g()\n    return c\nprint(f())"), Is.EqualTo("6"));
        }

        [Test]
        public void WorksWithBlankNamesAndUnicode()
        {
            Assert.That(Out("print((_ := 5), _)\nprint((é := 2), é)"), Is.EqualTo("5 5\n2 2"));
        }

        [Test]
        public void ColonEqualsStillLexesInsideSlicesAndFormatSpecs()
        {
            Assert.That(Out("xs = [1, 2, 3]\nprint(xs[1:], xs[:2], '{:=+4d}'.format(5), f'{5:=+4d}')"), Is.EqualTo("[2, 3] [1, 2] +  5 +  5"));
            Assert.That(Out("d = {1: 2}\nprint(d)"), Is.EqualTo("{1: 2}"));
        }

        [Test]
        public void SourceModelKnowsWalrusVariables()
        {
            string src = "if (count := len([1])) > 0:\n    co";
            var list = Completer.Complete(src, src.Length, new PyVm());
            Assert.That(System.Linq.Enumerable.Any(list.Items, i => i.Text == "count"), Is.True);
        }

        [Test]
        public void HighlighterTreatsWalrusAsAnOperator()
        {
            var spans = SyntaxHighlighter.Highlight("(a := 1)");
            Assert.That(spans.Exists(sp => sp.Start == 3 && sp.Length == 2 && sp.Kind == HighlightKind.Operator), Is.True);
        }
    }
}
