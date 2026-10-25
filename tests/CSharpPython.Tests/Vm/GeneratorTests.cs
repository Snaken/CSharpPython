using System.Linq;
using System.Text;
using CSharpPython.Syntax;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class GeneratorTests
    {
        private static void Game(PyVm vm) =>
            vm.RegisterModule("game", m => m.Native("wait", (ctx, a) => { ctx.Wait((int)a[0].AsInt()); return Value.None; }, 1, 1));

        [TestCase("yield 1", "'yield' outside function")]
        [TestCase("class A:\n    yield 1", "'yield' outside function")]
        [TestCase("def f():\n    return [(yield 1) for i in [1]]", "'yield' inside listcomp is not allowed")]
        [TestCase("def f():\n    return {(yield 1) for i in [1]}", "'yield' inside setcomp is not allowed")]
        [TestCase("def f():\n    return list((yield 1) for i in [1])", "'yield' inside genexpr is not allowed")]
        public void MisplacedYieldIsASyntaxError(string source, string message)
        {
            var error = Assert.Throws<PySyntaxError>(() => new PyVm().Compile(source));
            Assert.That(error.Message, Does.Contain(message));
        }

        [TestCase("def f():\n    yield from")]
        [TestCase("def f():\n    yield = 1")]
        [TestCase("def f():\n    x = yield from\n")]
        [TestCase("def f():\n    yield 1 +")]
        public void BadYieldsAreSyntaxErrors(string source)
        {
            Assert.Throws<PySyntaxError>(() => new PyVm().Compile(source), source);
        }

        [Test]
        public void ForLoopsResumeGeneratorsAsFramesSoTheyCanWait()
        {
            const string source = "import game\ndef ticker(n):\n    for i in range(n):\n        game.wait(1)\n        yield i\nfor t in ticker(3):\n    print('tick', t)\nprint('done')";
            var vm = new PyVm();
            var sb = new StringBuilder();
            vm.Output = s => sb.Append(s);
            Game(vm);
            var task = vm.Start(source);
            int updates = 0;
            while (task.Update() != TaskState.Finished && updates++ < 20) { }
            Assert.That(sb.ToString(), Is.EqualTo("tick 0\ntick 1\ntick 2\ndone\n"));
            Assert.That(updates, Is.EqualTo(3));
        }

        [Test]
        public void NextAndSendCanWaitInsideTheGenerator()
        {
            const string source = "import game\ndef g():\n    x = yield 1\n    game.wait(2)\n    y = yield x\n    return y\nit = g()\nprint(next(it))\nprint(it.send('a'))\nprint(it.__next__() if False else 'skip')\ntry:\n    it.send('b')\nexcept StopIteration:\n    print('end')";
            var vm = new PyVm();
            var sb = new StringBuilder();
            vm.Output = s => sb.Append(s);
            Game(vm);
            var task = vm.Start(source);
            Assert.That(task.Update(), Is.EqualTo(TaskState.Waiting));
            Assert.That(sb.ToString(), Is.EqualTo("1\n"));
            task.Update();
            Assert.That(task.Update(), Is.EqualTo(TaskState.Finished));
            Assert.That(sb.ToString(), Is.EqualTo("1\na\nskip\nend\n"));
        }

        [Test]
        public void YieldFromForwardsWaits()
        {
            const string source = "import game\ndef inner():\n    game.wait(1)\n    yield 'i'\n    return 'r'\ndef outer():\n    v = yield from inner()\n    game.wait(1)\n    yield v\nprint(list(x for x in range(0)))\nfor x in outer():\n    print(x)";
            var vm = new PyVm();
            var sb = new StringBuilder();
            vm.Output = s => sb.Append(s);
            Game(vm);
            var task = vm.Start(source);
            int updates = 0;
            while (task.Update() != TaskState.Finished && updates++ < 20) { }
            Assert.That(sb.ToString(), Is.EqualTo("[]\ni\nr\n"));
        }

        [Test]
        public void GeneratorsConsumedByBuiltInsCannotWait()
        {
            var error = Fails("import game\ndef g():\n    game.wait(1)\n    yield 1\nprint(list(g()))", Game);
            Assert.That(error.TypeName, Is.EqualTo("RuntimeError"));
            Assert.That(error.Problem, Does.Contain("cannot wait"));
        }

        [Test]
        public void AGeneratorThatFailedToWaitIsFinished()
        {
            Assert.That(Run("import game\ndef g():\n    game.wait(1)\n    yield 1\nit = g()\ntry:\n    list(it)\nexcept RuntimeError:\n    print('failed')\nprint(list(it))", Game).TrimEnd('\n'),
                Is.EqualTo("failed\n[]"));
        }

        [Test]
        public void CallingAGeneratorFunctionBindsArgumentsButRunsNothing()
        {
            var error = Fails("def g(a, b):\n    print('never')\n    yield a\ng(1)\n");
            Assert.That(error.TypeName, Is.EqualTo("TypeError"));
            Assert.That(Out("def g(a):\n    print('ran')\n    yield a\nx = g(1)\nprint('made')"), Is.EqualTo("made"));
        }

        [Test]
        public void ErrorsInsideGeneratorsReportTheYieldingLine()
        {
            var error = Fails("def g():\n    yield 1\n    x = 1 / 0\nfor v in g():\n    pass\n");
            Assert.That(error.TypeName, Is.EqualTo("ZeroDivisionError"));
            Assert.That(error.Trace[0].Line, Is.EqualTo(3));
            Assert.That(error.Trace[0].Function, Is.EqualTo("g"));
            Assert.That(error.Trace[1].Line, Is.EqualTo(4));
        }

        [Test]
        public void ErrorsInGeneratorsCalledByBuiltInsReportTheBodyLine()
        {
            var error = Fails("def g():\n    yield 1\n    raise ValueError('bad')\nsum(g())\n");
            Assert.That(error.TypeName, Is.EqualTo("ValueError"));
            Assert.That(error.Trace[0].Line, Is.EqualTo(3));
        }

        [Test]
        public void StopIterationEscapingBecomesARuntimeError()
        {
            var error = Fails("def g():\n    yield 1\n    raise StopIteration\nfor x in g():\n    pass\n");
            Assert.That(error.TypeName, Is.EqualTo("RuntimeError"));
            Assert.That(error.Problem, Is.EqualTo("generator raised StopIteration"));
        }

        [Test]
        public void ResumingARunningGeneratorIsAnError()
        {
            Assert.That(Out("def f():\n    try:\n        g.send(1)\n    except ValueError as e:\n        yield str(e)\ng = f()\nprint(next(g))"),
                Is.EqualTo("generator already executing"));
            Assert.That(Out("def f():\n    try:\n        g.close()\n    except ValueError as e:\n        yield str(e)\ng = f()\nprint(next(g))"),
                Is.EqualTo("generator already executing"));
        }

        [Test]
        public void SendingToAFreshGeneratorNeedsNone()
        {
            Assert.That(Out("def f():\n    got = yield 1\n    yield got\ng = f()\ntry:\n    g.send(5)\nexcept TypeError as e:\n    print(e)\nprint(g.send(None), g.send(7))"),
                Is.EqualTo("can't send non-None value to a just-started generator\n1 7"));
        }

        [Test]
        public void NextWithDefaultAfterExhaustionAndMidRun()
        {
            Assert.That(Out("def f():\n    yield 1\ng = f()\nprint(next(g, 'd'), next(g, 'd'), next(g, 'd'))"), Is.EqualTo("1 d d"));
            Assert.That(Fails("def f():\n    yield 1\ng = f()\nnext(g)\nnext(g)\n").TypeName, Is.EqualTo("StopIteration"));
        }

        [Test]
        public void GeneratorValuesPrintAndHaveAType()
        {
            var text = Out("def count():\n    yield 1\ng = count()\nprint(g)\nprint(type(g).__name__, type(g) == type(count()), type(iter([1])).__name__)");
            Assert.That(text, Does.StartWith("<generator object count at 0x"));
            Assert.That(text, Does.EndWith("generator True iterator"));
        }

        [Test]
        public void CloseRunsFinallyAndEndsTheGenerator()
        {
            Assert.That(Out("def f():\n    try:\n        yield 1\n    finally:\n        print('fin')\ng = f()\nnext(g)\ng.close()\nprint(list(g))"), Is.EqualTo("fin\n[]"));
        }

        [Test]
        public void OtherIteratorsHaveNextButNotSend()
        {
            Assert.That(Out("it = iter([1, 2])\nprint(it.__next__(), next(it))\ntry:\n    it.__next__()\nexcept StopIteration:\n    print('stop')"), Is.EqualTo("1 2\nstop"));
            Assert.That(Fails("it = iter([1])\nit.send(2)\n").TypeName, Is.EqualTo("AttributeError"));
            Assert.That(Fails("it = iter([1])\nit.close()\n").TypeName, Is.EqualTo("AttributeError"));
        }

        [Test]
        public void GeneratorMethodsAreCallableByNativeCode()
        {
            Assert.That(Out("def f():\n    got = yield 1\n    yield got\ng = f()\nsend = g.send\nnext(g)\nprint(send('x'))\nprint(list(map(lambda v: v, [1])) if False else 'ok')"), Is.EqualTo("x\nok"));
            Assert.That(Out("def f():\n    yield 1\nnxt = f().__next__\nprint(nxt())"), Is.EqualTo("1"));
        }

        [Test]
        public void ConstructorsCannotBeGenerators()
        {
            var error = Fails("class A:\n    def __init__(self):\n        yield 1\nA()\n");
            Assert.That(error.TypeName, Is.EqualTo("TypeError"));
            Assert.That(error.Problem, Does.Contain("__init__() should return None"));
        }

        [Test]
        public void EventHandlersCannotBeGenerators()
        {
            var vm = new PyVm();
            var task = vm.Start("def on_hit():\n    yield 1\n");
            task.RunUntilDone();
            task.QueueEvent("on_hit");
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Error));
            Assert.That(task.Error.Problem, Does.Contain("generator function"));
        }

        [Test]
        public void ADeepYieldFromChainHitsTheRecursionLimit()
        {
            var error = Fails("def deep(n):\n    if n == 0:\n        yield 0\n    else:\n        yield from deep(n - 1)\nlist(deep(500))\n",
                options: new VmOptions { MaxCallDepth = 50 });
            Assert.That(error.TypeName, Is.EqualTo("RecursionError"));
        }

        [Test]
        public void RunawayGeneratorsStopAtMaxSteps()
        {
            var error = Fails("def forever():\n    while True:\n        pass\n        yield 1\nfor x in forever():\n    pass\n",
                options: new VmOptions { MaxSteps = 5000 });
            Assert.That(error.TypeName, Is.EqualTo("StepLimitExceeded"));
            var inner = Fails("def forever():\n    while True:\n        pass\nlist(forever())\n", options: new VmOptions { MaxSteps = 5000 });
            Assert.That(inner.TypeName, Is.EqualTo("StepLimitExceeded"));
        }

        [Test]
        public void GeneratorExpressionsEvaluateTheFirstIterableEagerly()
        {
            var error = Fails("g = (x for x in 5)\n");
            Assert.That(error.TypeName, Is.EqualTo("TypeError"));
            Assert.That(error.Problem, Is.EqualTo("'int' object is not iterable"));
            Assert.That(Out("n = 0\ndef src():\n    global n\n    n += 1\n    return [1, 2]\ng = (x for x in src())\nprint(n)\nlist(g)\nlist(g)\nprint(n)"), Is.EqualTo("1\n1"));
        }

        [Test]
        public void GeneratorExpressionsCannotBeShadowedByUserIter()
        {
            Assert.That(Out("iter = 5\nprint(list(x for x in [1, 2]))"), Is.EqualTo("[1, 2]"));
        }

        [Test]
        public void UnpackingAndStarCallsConsumeGenerators()
        {
            Assert.That(Out("def g():\n    yield 1\n    yield 2\na, b = g()\nprint(a, b)\nprint(*g())\nprint(max(*g()))"), Is.EqualTo("1 2\n1 2\n2"));
        }

        [Test]
        public void ClassesCanYieldFromTheirIterMethod()
        {
            Assert.That(Out("class R:\n    def __init__(self, n):\n        self.n = n\n    def __iter__(self):\n        i = 0\n        while i < self.n:\n            yield i\n            i += 1\nprint(list(R(3)), sum(R(4)), [x for x in R(2)])\nfor x in R(2):\n    print(x)"),
                Is.EqualTo("[0, 1, 2] 6 [0, 1]\n0\n1"));
        }

        [Test]
        public void SuperAndOwnerSurviveInsideGenerators()
        {
            Assert.That(Out("class A:\n    def items(self):\n        yield 'a'\nclass B(A):\n    def items(self):\n        yield 'b'\n        yield from super().items()\nprint(list(B().items()))"), Is.EqualTo("['b', 'a']"));
        }

        [Test]
        public void YieldFromRaisesForNonIterables()
        {
            var error = Fails("def g():\n    yield from 5\nlist(g())\n");
            Assert.That(error.TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void ExceptionsThrownIntoHandlersKeepTheGeneratorUsable()
        {
            Assert.That(Out("def g():\n    for i in range(3):\n        try:\n            yield i\n        except Exception:\n            pass\n        try:\n            1 / 0\n        except ZeroDivisionError as e:\n            yield 'z' + str(i)\nprint(list(g()))"),
                Is.EqualTo("[0, 'z0', 1, 'z1', 2, 'z2']"));
        }

        [Test]
        public void BareRaiseInsideAGeneratorHandlerReRaises()
        {
            var error = Fails("def g():\n    try:\n        yield 1\n        raise KeyError('k')\n    except KeyError:\n        yield 2\n        raise\nfor x in g():\n    pass\n");
            Assert.That(error.TypeName, Is.EqualTo("KeyError"));
        }

        [Test]
        public void EditorOffersTheKeywordAndHighlightsIt()
        {
            var names = CSharpPython.Editor.Completer.Complete("yi", 2, new PyVm()).Items.Select(i => i.Text);
            Assert.That(names, Does.Contain("yield"));
            var tokens = Lexer.Tokenize("yield x");
            Assert.That(tokens[0].Kind, Is.EqualTo(TokenKind.KwYield));
        }

        [Test]
        public void AnExhaustedGeneratorEndsALoopImmediately()
        {
            Assert.That(Out("def g():\n    yield 1\nit = g()\nprint(list(it))\nfor x in it:\n    print('never')\nprint('ok')"), Is.EqualTo("[1]\nok"));
        }

        [Test]
        public void ADeepYieldFromChainInALoopHitsTheRecursionLimit()
        {
            var error = Fails("def deep(n):\n    if n == 0:\n        yield 0\n    else:\n        yield from deep(n - 1)\nfor x in deep(500):\n    pass\n",
                options: new VmOptions { MaxCallDepth = 50 });
            Assert.That(error.TypeName, Is.EqualTo("RecursionError"));
        }

        [Test]
        public void SendThroughANativeCallbackRunsTheGenerator()
        {
            Assert.That(Out("def f():\n    got = yield 0\n    while True:\n        got = yield got * 2\ng = f()\nnext(g)\nprint(sorted([3, 1, 2], key=g.send))"), Is.EqualTo("[1, 2, 3]"));
        }

        [Test]
        public void NextAsAKeyFunctionRunsGeneratorsToTheirEnd()
        {
            Assert.That(Out("def g(*xs):\n    for x in xs:\n        yield x\ngens = [g(3, 9), g(1, 9), g(2, 9)]\nordered = sorted(gens, key=next)\nprint([next(i) for i in ordered])"), Is.EqualTo("[9, 9, 9]"));
            Assert.That(Fails("def g():\n    return\n    yield\nsorted([g()], key=next)\n").TypeName, Is.EqualTo("StopIteration"));
        }

        [Test]
        public void EditorToolsCopeWithGeneratorSource()
        {
            const string source = "def g(n):\n    x = yield\n    (yield n)\n    y = yield from range(n)\n    z = (a for a in [x, y])\n    yield x, y\n";
            var vm = new PyVm();
            for (int cursor = 0; cursor <= source.Length; cursor++)
            {
                Assert.DoesNotThrow(() => CSharpPython.Editor.Completer.Complete(source, cursor, vm), "completion at " + cursor);
                Assert.DoesNotThrow(() => CSharpPython.Editor.SignatureHelper.Get(source, cursor, vm), "signature help at " + cursor);
            }
            Assert.DoesNotThrow(() => CSharpPython.Editor.SyntaxHighlighter.Highlight(source));
        }
    }
}
