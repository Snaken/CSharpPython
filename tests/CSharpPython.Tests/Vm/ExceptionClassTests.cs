using System.Collections.Generic;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class ExceptionClassTests
    {
        private static string Message(string source) => Fails(source).Problem;

        [Test]
        public void UncaughtScriptExceptionReportsTheClassName()
        {
            var error = Fails("class HitError(ValueError):\n    pass\nraise HitError('ouch')");
            Assert.That(error.TypeName, Is.EqualTo("HitError"));
            Assert.That(error.Problem, Is.EqualTo("ouch"));
            Assert.That(error.Message, Does.Contain("HitError: ouch"));
        }

        [Test]
        public void UncaughtExceptionWithoutMessageShowsJustTheName()
        {
            var error = Fails("class E(Exception):\n    pass\nraise E");
            Assert.That(error.TypeName, Is.EqualTo("E"));
            Assert.That(error.Problem, Is.EqualTo(""));
        }

        [Test]
        public void UncaughtExceptionUsesTheCustomStr()
        {
            Assert.That(Message("class E(Exception):\n    def __str__(self):\n        return 'custom'\nraise E('plain')"), Is.EqualTo("custom"));
        }

        [Test]
        public void ABrokenStrDoesNotHideTheOriginalError()
        {
            var error = Fails("class E(Exception):\n    def __str__(self):\n        return 1 / 0\nraise E('plain')");
            Assert.That(error.TypeName, Is.EqualTo("E"));
            Assert.That(error.Problem, Is.EqualTo("plain"));
        }

        [Test]
        public void TracebackPointsAtTheRaiseEvenAfterAReraise()
        {
            var error = Fails("class E(Exception):\n    pass\ndef f():\n    raise E('x')\ndef g():\n    try:\n        f()\n    except E:\n        raise\ng()");
            Assert.That(error.Trace[0].Line, Is.EqualTo(4));
            Assert.That(error.Trace[0].Function, Is.EqualTo("f"));
        }

        [Test]
        public void ConstructorErrors()
        {
            Assert.That(Message("class E(Exception):\n    pass\nE(a=1)"), Is.EqualTo("E() takes no keyword arguments"));
            Assert.That(Message("class E(Exception):\n    def __init__(self, a):\n        pass\nraise E"), Does.Contain("missing 1 required positional argument: 'a'"));
            Assert.That(Out("class E(Exception):\n    def __init__(self, a=1, *, k=2):\n        self.k = k\nprint(E(k=5).k, E(k=5).args)"), Is.EqualTo("5 ()"));
        }

        [Test]
        public void RaiseAndCatchRejectNonExceptions()
        {
            Assert.That(Message("class P:\n    pass\nraise P()"), Is.EqualTo("exceptions must derive from BaseException"));
            Assert.That(Message("class P:\n    pass\nraise P"), Is.EqualTo("exceptions must derive from BaseException"));
            Assert.That(Message("class P:\n    pass\ntry:\n    raise ValueError('x')\nexcept P:\n    pass"),
                Is.EqualTo("catching classes that do not inherit from BaseException is not allowed"));
            Assert.That(Message("try:\n    raise ValueError('x')\nexcept ((ValueError,), KeyError):\n    pass"),
                Is.EqualTo("catching classes that do not inherit from BaseException is not allowed"));
        }

        [Test]
        public void BuiltInErrorsAreNotCaughtByScriptSubclasses()
        {
            Assert.That(Fails("class E(ZeroDivisionError):\n    pass\ntry:\n    1 / 0\nexcept E:\n    print('wrong')").TypeName,
                Is.EqualTo("ZeroDivisionError"));
        }

        [Test]
        public void ExceptionsRaisedInsideStrAndReprPropagate()
        {
            Assert.That(Out("class P:\n    def __str__(self):\n        raise ValueError('inner')\ntry:\n    str(P())\nexcept ValueError as e:\n    print('caught', e)"),
                Is.EqualTo("caught inner"));
        }

        [Test]
        public void CanDeriveFromAHostRegisteredExceptionType()
        {
            var vm = new PyVm { Output = s => { } };
            vm.RegisterExceptionType("GameError");
            var sb = new System.Text.StringBuilder();
            vm.Output = s => sb.Append(s);
            vm.RegisterModule("game", m => m.Native("fail", (ctx, a) => throw new PyError("GameError", "host says no"), 0, 0));
            var task = vm.Start("import game\nclass Mine(GameError):\n    pass\ntry:\n    game.fail()\nexcept GameError as e:\n    print(type(e).__name__, e)\ntry:\n    raise Mine('m')\nexcept GameError as e:\n    print(type(e).__name__, e)");
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Finished));
            Assert.That(sb.ToString(), Is.EqualTo("GameError host says no\nMine m\n"));
        }

        [Test]
        public void EventHandlersCanRaiseAndCatchScriptExceptions()
        {
            var sb = new System.Text.StringBuilder();
            var vm = new PyVm { Output = s => sb.Append(s) };
            var task = vm.Start("class Hit(Exception):\n    pass\ndef on_hit(n):\n    try:\n        raise Hit(n)\n    except Hit as h:\n        print('hit', h.args[0])\n");
            task.RunUntilDone();
            task.QueueEvent("on_hit", Value.FromInt(4));
            task.RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("hit 4\n"));

            var bad = vm.Start("class Hit(Exception):\n    pass\ndef on_hit():\n    raise Hit('late')\n");
            bad.RunUntilDone();
            bad.QueueEvent("on_hit");
            Assert.That(bad.RunUntilDone(), Is.EqualTo(TaskState.Error));
            Assert.That(bad.Error.TypeName, Is.EqualTo("Hit"));
        }

        [Test]
        public void ExceptionInstancesAreOrdinaryObjects()
        {
            Assert.That(Out("class E(Exception):\n    pass\ne = E('a')\ne.extra = [1]\nd = {e: 1}\nprint(d[e], getattr(e, 'extra'), hasattr(e, 'args'), bool(e), callable(E))"),
                Is.EqualTo("1 [1] True True True"));
        }

        [Test]
        public void ExceptionMayBeRaisedFromAWaitingFunction()
        {
            var vm = new PyVm { Output = s => { } };
            var sb = new System.Text.StringBuilder();
            vm.Output = s => sb.Append(s);
            vm.RegisterModule("game", m => m.Native("wait", (ctx, a) => { ctx.Wait(1); return Value.None; }, 1, 1));
            var task = vm.Start("import game\nclass E(Exception):\n    def __init__(self, n):\n        super().__init__('n=' + str(n))\n        self.n = n\n" +
                                "def work():\n    game.wait(1)\n    raise E(3)\ntry:\n    work()\nexcept E as e:\n    game.wait(1)\n    print(e, e.n)");
            for (int i = 0; i < 10 && task.State != TaskState.Finished; i++) { task.Run(); task.Tick(); }
            Assert.That(sb.ToString(), Is.EqualTo("n=3 3\n"));
        }
    }
}
