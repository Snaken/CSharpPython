using System.Linq;
using CSharpPython.Editor;

namespace CSharpPython.Tests.Editor
{
    public class AnalyzerTests
    {
        [Test]
        public void ValidSourceHasNoDiagnostics()
        {
            Assert.That(Analyzer.Analyze("x = 1\nprint(x)\n"), Is.Empty);
            Assert.That(Analyzer.Analyze(""), Is.Empty);
            Assert.That(Analyzer.Analyze(null), Is.Empty);
            Assert.That(Analyzer.Analyze("def f():\n    return 1\n# done"), Is.Empty);
        }

        [Test]
        public void LexicalProblemsAreAllReported()
        {
            var d = Analyzer.Analyze("x = 'abc\ny = 5 $ 3\nz = 1 ? 2");
            Assert.That(d.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(d.Select(x => x.Line).Distinct(), Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void ASyntaxErrorIsReportedWithItsPosition()
        {
            var d = Analyzer.Analyze("x = 1\ny = (2 +\n");
            Assert.That(d.Count, Is.EqualTo(1));
            Assert.That(d[0].Line, Is.GreaterThanOrEqualTo(2));
            Assert.That(d[0].Message, Is.Not.Empty);
        }

        [Test]
        public void UnsupportedFeaturesGetAClearMessage()
        {
            Assert.That(Analyzer.Analyze("x: int = 5"), Is.Empty);
            Assert.That(Analyzer.Analyze("f(): int").Single().Message, Does.Contain("illegal target for annotation"));
            Assert.That(Analyzer.Analyze("class A(B, C):\n    pass").Single().Message, Does.Contain("multiple inheritance is not supported"));
        }

        [Test]
        public void ErrorsFoundWhileCompilingAreReported()
        {
            foreach (var src in new[] { "break", "continue", "def f():\n    x = 1\n    global x\n", "return 1" })
            {
                Assert.That(Analyzer.Analyze(src), Is.Not.Empty, src);
                Assert.Throws<CSharpPython.Syntax.PySyntaxError>(() => new PyVm().Compile(src), src);
            }
        }

        [Test]
        public void MatchesWhatCompileThrows()
        {
            foreach (var src in new[] { "x = ", "if x\n    pass", "def f(:\n  pass", "return return", "x = 1 +", "1 = x", "print('a' 'b'", "else:" })
            {
                var d = Analyzer.Analyze(src);
                var error = Assert.Throws<CSharpPython.Syntax.PySyntaxError>(() => new PyVm().Compile(src), src);
                Assert.That(d, Is.Not.Empty, src);
                if (d.Count == 1)
                {
                    Assert.That(d[0].Line, Is.EqualTo(error.Line), src);
                    Assert.That(d[0].Column, Is.EqualTo(error.Column), src);
                    Assert.That(d[0].Message, Is.EqualTo(error.Problem), src);
                }
            }
        }

        [Test]
        public void DecoratedCodeAnalysesCleanlyAndReportsTheMissingDef()
        {
            Assert.That(Analyzer.Analyze("def d(f):\n    return f\n@d\ndef g():\n    pass\ng()\n"), Is.Empty);
            Assert.That(Analyzer.Analyze("class A:\n    @property\n    def p(self):\n        return 1\nA().p\n"), Is.Empty);
            var bad = Analyzer.Analyze("@d\nx = 1\n");
            Assert.That(bad, Is.Not.Empty);
            Assert.That(bad[0].Message, Does.Contain("after a decorator"));
        }

        [Test]
        public void MatchStatementsAreValid()
        {
            Assert.That(Analyzer.Analyze("match [1, 2]:\n    case [a, b] if a < b:\n        print(a)\n    case {'k': v, **r}:\n        print(v, r)\n    case Point(x=1) | None:\n        pass\n    case _:\n        pass\n"), Is.Empty);
            Assert.That(Analyzer.Analyze("match = 1\ncase = match\nprint(match, case)\nmatch(case)\n"), Is.Empty);
        }

        [Test]
        public void RaiseFromIsValid()
        {
            Assert.That(Analyzer.Analyze("try:\n    pass\nexcept ValueError as e:\n    raise KeyError('k') from e\n"), Is.Empty);
            Assert.That(Analyzer.Analyze("raise ValueError('x') from None\n"), Is.Empty);
        }

        [TestCase("match x:\n    case [*a, *b]:\n        pass\n", "multiple starred")]
        [TestCase("match x:\n    case _:\n        pass\n    case 1:\n        pass\n", "unreachable")]
        [TestCase("match x:\n    case f(1, a=2, 3):\n        pass\n", "positional patterns follow keyword")]
        [TestCase("match x:\n    case 1 as _:\n        pass\n", "cannot use '_'")]
        [TestCase("match x:\n    case 'a' 'b' | f'{x}':\n        pass\n", "literals")]
        [TestCase("match x:\n    case {a: 1}:\n        pass\n", "literals or dotted names")]
        [TestCase("match x:\n    case -x:\n        pass\n", "expected a number")]
        [TestCase("match x:\n    case f(a=1, a=2):\n        pass\n", "repeated")]
        [TestCase("match x:\n    case 1:\n        pass\n    other\n", "expected a 'case' block")]
        [TestCase("match x:\n    case +:\n        pass\n", "invalid pattern")]
        public void BadPatternsAreSyntaxErrors(string source, string message)
        {
            Assert.That(Analyzer.Analyze(source).First().Message, Does.Contain(message));
        }
    }
}
