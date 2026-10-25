using System.Linq;
using CSharpPython.Editor;
using CSharpPython.Syntax;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class AssertTests
    {
        [TestCase("assert")]
        [TestCase("assert x,")]
        [TestCase("assert x y")]
        [TestCase("assert , 'm'")]
        public void BadAssertsAreSyntaxErrors(string source)
        {
            Assert.Throws<PySyntaxError>(() => new PyVm().Compile(source), source);
        }

        [Test]
        public void UncaughtFailureReportsTheAssertLine()
        {
            var error = Fails("x = 1\nassert x == 2, 'x should be 2'\n");
            Assert.That(error.TypeName, Is.EqualTo("AssertionError"));
            Assert.That(error.Problem, Is.EqualTo("x should be 2"));
            Assert.That(error.Trace[0].Line, Is.EqualTo(2));
            var bare = Fails("def f():\n    assert False\nf()");
            Assert.That(bare.Problem, Is.EqualTo(""));
            Assert.That(bare.Trace[0].Function, Is.EqualTo("f"));
            Assert.That(bare.Trace[0].Line, Is.EqualTo(2));
        }

        [Test]
        public void AParenthesisedTupleIsAlwaysTrue()
        {
            Assert.That(Out("assert (False, 'msg')\nprint('passed')"), Is.EqualTo("passed"));
        }

        [Test]
        public void TheMessageIsOnlyEvaluatedOnFailure()
        {
            Assert.That(Out("n = 0\ndef m():\n    global n\n    n += 1\n    return 'x'\nassert True, m()\nprint(n)"), Is.EqualTo("0"));
        }

        [Test]
        public void AssertionErrorIsABuiltInExceptionClass()
        {
            Assert.That(Out("print(issubclass(AssertionError, Exception), AssertionError('a'), repr(AssertionError('a')))"), Is.EqualTo("True a AssertionError('a')"));
            Assert.That(Out("class Mine(AssertionError):\n    pass\ntry:\n    raise Mine('m')\nexcept AssertionError as e:\n    print(type(e).__name__, e)"), Is.EqualTo("Mine m"));
        }

        [Test]
        public void AssertWorksInEventHandlersAndIsCatchable()
        {
            var sb = new System.Text.StringBuilder();
            var vm = new PyVm { Output = s => sb.Append(s) };
            var task = vm.Start("hp = 10\ndef on_hit(n):\n    global hp\n    hp -= n\n    assert hp >= 0, 'dead'\n    print(hp)\n");
            task.RunUntilDone();
            task.QueueEvent("on_hit", Value.FromInt(4));
            task.QueueEvent("on_hit", Value.FromInt(7));
            task.RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("6\n"));
            Assert.That(task.State, Is.EqualTo(TaskState.Error));
            Assert.That(task.Error.TypeName, Is.EqualTo("AssertionError"));
            Assert.That(task.Error.Problem, Is.EqualTo("dead"));
        }

        [Test]
        public void EditorsKnowTheKeyword()
        {
            Assert.That(Completer.Complete("ass", 3, new PyVm()).Items.Select(i => i.Text), Does.Contain("assert"));
            Assert.That(SyntaxHighlighter.Highlight("assert x\n")[0].Kind, Is.EqualTo(HighlightKind.Keyword));
            Assert.That(Analyzer.Analyze("x = 1\nassert x == 1, 'one'\n"), Is.Empty);
        }
    }
}
