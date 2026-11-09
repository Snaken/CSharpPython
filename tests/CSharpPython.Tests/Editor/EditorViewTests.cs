using System.IO;
using System.Linq;
using CSharpPython.Editor;

namespace CSharpPython.Tests.Editor
{
    public class EditorViewTests
    {
        private static void AssertSameAsSeparateCalls(string source)
        {
            var view = EditorView.Build(source);
            var plain = SyntaxHighlighter.Highlight(source);
            var problems = Analyzer.Analyze(source);

            Assert.That(view.Diagnostics.Select(d => d.ToString()), Is.EqualTo(problems.Select(d => d.ToString())));

            // Every piece lies inside one plain span with its kind, and the pieces of a span add up to it.
            int next = 0;
            foreach (var span in plain)
            {
                int covered = 0, at = span.Start;
                while (next < view.Spans.Count && view.Spans[next].Start < span.End)
                {
                    var piece = view.Spans[next++];
                    Assert.That(piece.Start, Is.EqualTo(at), "pieces are contiguous");
                    Assert.That(piece.Length, Is.GreaterThan(0));
                    Assert.That(piece.Kind, Is.EqualTo(span.Kind));
                    covered += piece.Length;
                    at = piece.End;
                }
                Assert.That(covered, Is.EqualTo(span.Length), span.ToString());
            }
            Assert.That(next, Is.EqualTo(view.Spans.Count));
        }

        [Test]
        public void NoProblemsLeavesTheSpansAsTheyAre()
        {
            const string source = "x = 1\nprint(x)  # hi\n";
            var view = EditorView.Build(source);
            Assert.That(view.Diagnostics, Is.Empty);
            Assert.That(view.Spans.Select(s => s.ToString()), Is.EqualTo(SyntaxHighlighter.Highlight(source).Select(s => s.ToString())));
            Assert.That(view.Spans.All(s => s.Problem == null));
        }

        [Test]
        public void NullAndEmptyTextBuildAnEmptyView()
        {
            foreach (var source in new string[] { null, "" })
            {
                var view = EditorView.Build(source);
                Assert.That(view.Spans, Is.Empty);
                Assert.That(view.Diagnostics, Is.Empty);
            }
        }

        [Test]
        public void AProblemMarksOnlyTheSpansItCovers()
        {
            const string source = "x = 1\ny = 5 $ 3\n";
            var view = EditorView.Build(source);
            Assert.That(view.Diagnostics.Count, Is.EqualTo(1));
            var marked = view.Spans.Where(s => s.Problem != null).ToList();
            Assert.That(marked.Count, Is.EqualTo(1));
            Assert.That(source.Substring(marked[0].Start, marked[0].Length), Is.EqualTo("$"));
            Assert.That(marked[0].Problem, Is.EqualTo(view.Diagnostics[0].Message));
            AssertSameAsSeparateCalls(source);
        }

        [Test]
        public void ASpanIsSplitWhereAProblemStartsInTheMiddleOfIt()
        {
            // The unterminated string runs to the end of the line; the problem is the whole string token.
            const string source = "s = 'abc\nt = 2\n";
            var view = EditorView.Build(source);
            var marked = view.Spans.Where(s => s.Problem != null).ToList();
            Assert.That(marked, Is.Not.Empty);
            foreach (var m in marked) Assert.That(source.Substring(m.Start, m.Length), Does.Not.Contain("t = 2"));
            AssertSameAsSeparateCalls(source);
        }

        [Test]
        public void SeveralProblemsAreAllMarked()
        {
            const string source = "x = 'abc\ny = 5 $ 3\nz = 1 ? 2";
            var view = EditorView.Build(source);
            Assert.That(view.Diagnostics.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(view.Spans.Count(s => s.Problem != null), Is.GreaterThanOrEqualTo(3));
            AssertSameAsSeparateCalls(source);
        }

        [Test]
        public void ASyntaxErrorMarksTheTokenItPointsAt()
        {
            const string source = "x = 1\ny = (2 +\n";
            var view = EditorView.Build(source);
            Assert.That(view.Diagnostics.Count, Is.EqualTo(1));
            AssertSameAsSeparateCalls(source);
        }

        [Test]
        public void ProblemsAtTheEndOfTheTextOrInWhitespaceDoNotBreakAnything()
        {
            foreach (var source in new[] { "def f():\n", "x = 1\n  y = 2\n", "x = (", "if x:\nreturn", "\t\n  \n" })
                AssertSameAsSeparateCalls(source);
        }

        [Test]
        public void SpansStayOrderedAndInsideTheText()
        {
            foreach (var source in new[] { "x = 'abc\ny = 5 $ 3\nz = 1 ? 2", "def f(:\n    pass\n", "print(1 +\n", "a = [1, 2\nb = 3\n" })
            {
                var view = EditorView.Build(source);
                int end = 0;
                foreach (var s in view.Spans)
                {
                    Assert.That(s.Start, Is.GreaterThanOrEqualTo(end), source);
                    Assert.That(s.Length, Is.GreaterThan(0));
                    Assert.That(s.End, Is.LessThanOrEqualTo(source.Length));
                    end = s.End;
                }
            }
        }

        [Test]
        public void EveryHalfTypedPrefixOfAScriptBuilds()
        {
            const string source = "def walk(grid, x, y):\n    seen = set()\n    for dx, dy in [(1, 0), (0, 1)]:\n        s = f\"{x + 1} done\"  # note\n    return len(seen)\n";
            for (int length = 0; length <= source.Length; length++)
                AssertSameAsSeparateCalls(source.Substring(0, length));
        }

        [Test]
        public void EveryScriptInTheTestCorpusGivesTheSameAsTheSeparateCalls()
        {
            string root = Path.Combine(TestContext.CurrentContext.TestDirectory, "Scripts");
            if (!Directory.Exists(root)) Assert.Ignore("the scripts are not copied next to the tests");
            int count = 0;
            foreach (var file in Directory.EnumerateFiles(root, "*.py", SearchOption.AllDirectories))
            {
                AssertSameAsSeparateCalls(File.ReadAllText(file));
                count++;
            }
            Assert.That(count, Is.GreaterThan(100));
        }
    }
}
