using System;
using System.Linq;
using System.Threading;
using CSharpPython.Syntax;

namespace CSharpPython.Tests.Syntax
{
    /// <summary>
    /// Hostile source must produce a syntax error, never a stack overflow (which would end the host process).
    /// Every case runs on a thread with a small stack so that an unguarded recursion would crash the test run.
    /// </summary>
    public class NestingTests
    {
        private const int SmallStack = 512 * 1024;

        private static string Repeat(string s, int n) => string.Concat(Enumerable.Repeat(s, n));

        public static readonly object[][] Hostile =
        {
            new object[] { "x = " + new string('(', 50000) + "1" + new string(')', 50000) },
            new object[] { "x = " + new string('[', 50000) + new string(']', 50000) },
            new object[] { "x = " + new string('{', 50000) + new string('}', 50000) },
            new object[] { "x = " + new string('-', 50000) + "1" },
            new object[] { "x = " + Repeat("not ", 50000) + "1" },
            new object[] { "x = " + Repeat("f(", 50000) + new string(')', 50000) },
            new object[] { "x = " + string.Join("+", Enumerable.Repeat("1", 50000)) },
            new object[] { "x = " + string.Join(" or ", Enumerable.Repeat("1", 50000)) },
            new object[] { "x = " + string.Join(" and ", Enumerable.Repeat("1", 50000)) },
            new object[] { "x = " + string.Join("**", Enumerable.Repeat("1", 50000)) },
            new object[] { "x = a" + Repeat(".b", 50000) },
            new object[] { "x = a" + Repeat("[0]", 50000) },
            new object[] { "x = f" + Repeat("()", 50000) },
            new object[] { "x = " + Repeat("1 if 1 else ", 50000) + "2" },
            new object[] { "x = " + Repeat("lambda: ", 50000) + "2" },
            new object[] { string.Concat(Enumerable.Range(0, 1000).Select(i => new string(' ', i) + "if 1:\n")) + new string(' ', 1000) + "pass\n" },
        };

        private static T OnSmallStack<T>(Func<T> work)
        {
            T result = default;
            Exception error = null;
            var thread = new Thread(() =>
            {
                try { result = work(); }
                catch (Exception e) { error = e; }
            }, SmallStack);
            thread.Start();
            thread.Join();
            if (error != null) throw new InvalidOperationException(error.Message, error);
            return result;
        }

        [TestCaseSource(nameof(Hostile))]
        public void DeepSourceIsRejectedWithASyntaxError(string source)
        {
            var vm = new PyVm();
            var result = OnSmallStack(() =>
            {
                try { vm.Start(source); return null; }
                catch (PySyntaxError e) { return e; }
            });
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Problem, Does.Contain("too deeply").Or.Contain("too many chained"));
        }

        [Test]
        public void ReasonableNestingStillWorks()
        {
            Assert.That(Vm.Py.Eval(new string('(', 100) + "7" + new string(')', 100)), Is.EqualTo("7"));
            Assert.That(Vm.Py.Eval(string.Join("+", Enumerable.Repeat("1", 400))), Is.EqualTo("400"));
            Assert.That(Vm.Py.Eval(Repeat("[", 100) + "5" + Repeat("]", 100) + Repeat("[0]", 100)), Is.EqualTo("5"));
            Assert.That(Vm.Py.Eval(Repeat("not ", 100) + "True"), Is.EqualTo("True"));
        }

        [Test]
        public void ChainLimitCountsEachOperatorKind()
        {
            Assert.That(Vm.Py.Eval(string.Join("+", Enumerable.Repeat("1", 500))), Is.EqualTo("500"));
            var e = Assert.Throws<PySyntaxError>(() => new PyVm().Start("x = " + string.Join("+", Enumerable.Repeat("1", 502))));
            Assert.That(e.Problem, Does.Contain("limit 500"));
        }

        [Test]
        public void TheStackCheckStopsRunawayDepthEvenBelowTheCountLimit()
        {
            // Within the nesting limit but on a stack too small to hold it: the stack check must turn it into an error.
            string source = "x = " + new string('(', 140) + "1" + new string(')', 140);
            var error = OnSmallStack(() =>
            {
                try { return StackHog(source, 300); }
                catch (PySyntaxError e) { return e; }
            });
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Problem, Does.Contain("too deeply"));
        }

        // Burns stack, then parses with whatever is left.
        private static PySyntaxError StackHog(string source, int frames)
        {
            if (frames > 0)
            {
                Span<byte> pad = stackalloc byte[1024];
                pad[0] = (byte)frames;
                var inner = StackHog(source, frames - 1);
                return pad[0] == 0 ? null : inner;
            }
            Parser.ParseModule(source);
            return null;
        }
    }
}
