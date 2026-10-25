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
    }
}
