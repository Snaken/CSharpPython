using System.Text;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class GeneratorControlTests
    {
        private static (PyTask task, StringBuilder output) Start(string source)
        {
            var vm = new PyVm();
            var sb = new StringBuilder();
            vm.Output = s => sb.Append(s);
            vm.RegisterModule("game", m => m.Native("wait", (ctx, a) => { ctx.Wait((int)a[0].AsInt()); return Value.None; }, 1, 1));
            return (vm.Start(source), sb);
        }

        [Test]
        public void CloseCanWaitInsideFinally()
        {
            var (task, sb) = Start("import game\ndef g():\n    try:\n        yield 1\n    finally:\n        print('start cleanup')\n        game.wait(2)\n        print('end cleanup')\nit = g()\nnext(it)\nit.close()\nprint('closed')");
            Assert.That(task.Update(), Is.EqualTo(TaskState.Waiting));
            Assert.That(sb.ToString(), Is.EqualTo("start cleanup\n"));
            task.Update();
            Assert.That(task.Update(), Is.EqualTo(TaskState.Finished));
            Assert.That(sb.ToString(), Is.EqualTo("start cleanup\nend cleanup\nclosed\n"));
        }

        [Test]
        public void ThrowCanWaitInsideTheHandler()
        {
            var (task, sb) = Start("import game\ndef g():\n    try:\n        yield 1\n    except ValueError:\n        game.wait(1)\n        yield 'handled'\nit = g()\nnext(it)\nprint(it.throw(ValueError()))");
            Assert.That(task.Update(), Is.EqualTo(TaskState.Waiting));
            Assert.That(task.Update(), Is.EqualTo(TaskState.Finished));
            Assert.That(sb.ToString(), Is.EqualTo("handled\n"));
        }

        [Test]
        public void ASnapshotTakenWhileCloseIsWaitingRestoresAndFinishesTheClose()
        {
            var vm = new PyVm(new VmOptions { EnableSnapshots = true });
            var sb = new StringBuilder();
            vm.Output = s => sb.Append(s);
            vm.RegisterModule("game", m => m.Native("wait", (ctx, a) => { ctx.Wait((int)a[0].AsInt()); return Value.None; }, 1, 1));
            var script = vm.Compile("import game\ndef g():\n    try:\n        yield 1\n    finally:\n        game.wait(3)\n        print('cleaned')\nit = g()\nnext(it)\nit.close()\nprint('closed', list(it))");
            var task = vm.Start(script);
            Assert.That(task.Update(), Is.EqualTo(TaskState.Waiting));
            var restored = vm.Restore(script, task.Save());
            int guard = 0;
            while (restored.Update() != TaskState.Finished && guard++ < 10) { }
            Assert.That(sb.ToString(), Is.EqualTo("cleaned\nclosed []\n"));
        }

        [Test]
        public void CloseOfARunningGeneratorFails()
        {
            var e = Fails("def g():\n    yield it.close()\nit = g()\nnext(it)");
            Assert.That(e.TypeName, Is.EqualTo("ValueError"));
        }

        [Test]
        public void ThrowIntoARunningGeneratorFails()
        {
            Assert.That(Fails("def g():\n    it.throw(ValueError)\n    yield 1\nit = g()\nnext(it)").TypeName, Is.EqualTo("ValueError"));
        }

        [Test]
        public void AGeneratorThatIgnoresCloseIsFinishedAfterTheError()
        {
            Assert.That(Out("def g():\n    while True:\n        try:\n            yield 1\n        except GeneratorExit:\n            pass\nit = g()\nnext(it)\ntry:\n    it.close()\nexcept RuntimeError as e:\n    print(e)\nprint(list(it))"), Is.EqualTo("generator ignored GeneratorExit\n[]"));
        }

        [Test]
        public void ThrowingStopIterationBecomesRuntimeError()
        {
            Assert.That(Out("def g():\n    yield 1\nit = g()\nnext(it)\ntry:\n    it.throw(StopIteration)\nexcept RuntimeError as e:\n    print(e)"), Is.EqualTo("generator raised StopIteration"));
        }

        [Test]
        public void ClosingWithAnExceptionInTheGeneratorPropagatesIt()
        {
            Assert.That(Fails("def g():\n    try:\n        yield 1\n    except GeneratorExit:\n        raise KeyError('k')\nit = g()\nnext(it)\nit.close()").TypeName, Is.EqualTo("KeyError"));
        }

        [Test]
        public void SendToPlainIterablesInYieldFromIsIgnored()
        {
            Assert.That(Out("def g():\n    got = yield from [1, 2]\n    print('got', got)\n    yield 'tail'\nit = g()\nprint(next(it), it.send('ignored'), it.send('x'))"), Is.EqualTo("got None\n1 2 tail"));
        }

        [Test]
        public void CloseIsForwardedIntoTheInnerGenerator()
        {
            const string src = "def inner():\n    try:\n        yield 1\n    finally:\n        print('inner finally')\ndef outer():\n    try:\n        yield from inner()\n    finally:\n        print('outer finally')\nit = outer()\nnext(it)\nit.close()\nprint('closed')";
            Assert.That(Out(src), Is.EqualTo("inner finally\nouter finally\nclosed"));
        }

        [Test]
        public void ForwardedCloseCanWaitInTheInnerFinally()
        {
            var (task, sb) = Start("import game\ndef inner():\n    try:\n        yield 1\n    finally:\n        print('inner cleanup')\n        game.wait(2)\n        print('inner done')\ndef outer():\n    try:\n        yield from inner()\n    finally:\n        print('outer cleanup')\nit = outer()\nnext(it)\nit.close()\nprint('closed')");
            Assert.That(task.Update(), Is.EqualTo(TaskState.Waiting));
            Assert.That(sb.ToString(), Is.EqualTo("inner cleanup\n"));
            task.Update();
            Assert.That(task.Update(), Is.EqualTo(TaskState.Finished));
            Assert.That(sb.ToString(), Is.EqualTo("inner cleanup\ninner done\nouter cleanup\nclosed\n"));
        }

        [Test]
        public void ForwardedThrowCanWaitInTheInnerHandler()
        {
            var (task, sb) = Start("import game\ndef inner():\n    try:\n        yield 1\n    except ValueError:\n        game.wait(1)\n        yield 'recovered'\ndef outer():\n    yield from inner()\nit = outer()\nnext(it)\nprint(it.throw(ValueError))");
            Assert.That(task.Update(), Is.EqualTo(TaskState.Waiting));
            Assert.That(task.Update(), Is.EqualTo(TaskState.Finished));
            Assert.That(sb.ToString(), Is.EqualTo("recovered\n"));
        }

        [Test]
        public void ThrowingAnObjectIntoADelegatingGeneratorKeepsItsIdentity()
        {
            Assert.That(Out("class E(Exception):\n    pass\nboom = E('x')\ndef inner():\n    try:\n        yield 1\n    except E as e:\n        print(e is boom)\n        yield 2\ndef outer():\n    yield from inner()\nit = outer()\nnext(it)\nprint(it.throw(boom))"), Is.EqualTo("True\n2"));
        }

        [Test]
        public void ThrowingStopIterationIntoADelegateBecomesARuntimeError()
        {
            Assert.That(Out("def inner():\n    yield 1\ndef outer():\n    yield from inner()\nit = outer()\nnext(it)\ntry:\n    it.throw(StopIteration)\nexcept RuntimeError as e:\n    print('RuntimeError')"), Is.EqualTo("RuntimeError"));
        }

        [Test]
        public void StopIterationValueFallsBackToTheMessageOrNone()
        {
            Assert.That(Out("try:\n    raise StopIteration('why')\nexcept StopIteration as e:\n    print(e.value, e.args)"), Is.EqualTo("why ('why',)"));
            Assert.That(Out("try:\n    raise StopIteration\nexcept StopIteration as e:\n    print(e.value, e.args)"), Is.EqualTo("None ()"));
        }

        [Test]
        public void OtherExceptionsHaveNoValueAttribute()
        {
            Assert.That(Fails("try:\n    raise ValueError('x')\nexcept ValueError as e:\n    e.value").TypeName, Is.EqualTo("AttributeError"));
        }

        [Test]
        public void ThrowedInstanceKeepsItsIdentityAndArguments()
        {
            const string src = "class E(Exception): pass\ndef g():\n    try:\n        yield 1\n    except E as e:\n        print(e is err, e.args)\nerr = E('a', 2)\nit = g()\nnext(it)\ntry:\n    it.throw(err)\nexcept StopIteration:\n    print('done')";
            Assert.That(Out(src), Is.EqualTo("True ('a', 2)\ndone"));
        }

        [Test]
        public void GeneratorExpressionsAndMethodsSupportCloseAndThrow()
        {
            Assert.That(Out("g = (x for x in range(3))\nnext(g)\ng.close()\nprint(list(g))"), Is.EqualTo("[]"));
            Assert.That(Fails("g = (x for x in range(3))\nnext(g)\ng.throw(ValueError('v'))").TypeName, Is.EqualTo("ValueError"));
        }

        [Test]
        public void CloseAndThrowOnOtherIteratorsAreMissing()
        {
            Assert.That(Fails("it = iter([1])\nit.throw(ValueError)").TypeName, Is.EqualTo("AttributeError"));
            Assert.That(Fails("it = iter([1])\nit.close()").TypeName, Is.EqualTo("AttributeError"));
        }
    }
}
