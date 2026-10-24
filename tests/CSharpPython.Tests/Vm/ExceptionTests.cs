using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class ExceptionTests
    {
        [Test]
        public void TryExceptCatchesMatchingType()
        {
            Assert.That(Out("try:\n    1 / 0\nexcept ZeroDivisionError:\n    print('caught')"), Is.EqualTo("caught"));
        }

        [Test]
        public void ParentClassCatchesChild()
        {
            Assert.That(Out("try:\n    1 / 0\nexcept ArithmeticError:\n    print('arith')"), Is.EqualTo("arith"));
            Assert.That(Out("try:\n    [][1]\nexcept LookupError:\n    print('lookup')"), Is.EqualTo("lookup"));
            Assert.That(Out("try:\n    {}['a']\nexcept Exception:\n    print('any')"), Is.EqualTo("any"));
        }

        [Test]
        public void ExceptionVariableHoldsTheError()
        {
            Assert.That(Out("try:\n    int('x')\nexcept ValueError as e:\n    print(e)"), Is.EqualTo("invalid literal for int() with base 10: 'x'"));
            Assert.That(Out("try:\n    raise ValueError('boom')\nexcept ValueError as e:\n    print(repr(e), e.args)"), Is.EqualTo("ValueError('boom') ('boom',)"));
        }

        [Test]
        public void TupleOfTypesMatches()
        {
            Assert.That(Out("try:\n    [][0]\nexcept (KeyError, IndexError):\n    print('either')"), Is.EqualTo("either"));
        }

        [Test]
        public void FirstMatchingHandlerWins()
        {
            const string src = @"
try:
    raise KeyError('k')
except ValueError:
    print('value')
except KeyError:
    print('key')
except Exception:
    print('other')";
            Assert.That(Out(src), Is.EqualTo("key"));
        }

        [Test]
        public void BareExceptCatchesEverything()
        {
            Assert.That(Out("try:\n    1 / 0\nexcept:\n    print('bare')"), Is.EqualTo("bare"));
        }

        [Test]
        public void UnmatchedExceptionPropagates()
        {
            var e = Fails("try:\n    1 / 0\nexcept ValueError:\n    print('no')");
            Assert.That(e.TypeName, Is.EqualTo("ZeroDivisionError"));
            Assert.That(e.Line, Is.EqualTo(2));
        }

        [Test]
        public void ExceptionsPropagateAcrossFunctions()
        {
            const string src = @"
def f():
    raise ValueError('deep')
def g():
    f()
try:
    g()
except ValueError as e:
    print('got', e)";
            Assert.That(Out(src), Is.EqualTo("got deep"));
        }

        [Test]
        public void StackIsRestoredAfterCatch()
        {
            const string src = @"
def f():
    return [1, 2, 3][10]
total = 0
for i in range(50):
    try:
        x = 1 + f()
    except IndexError:
        total += 1
print(total)";
            Assert.That(Out(src), Is.EqualTo("50"));
        }

        [Test]
        public void CatchInsideLoopWithBreakAndContinue()
        {
            const string src = @"
out = []
for i in range(5):
    try:
        if i == 1:
            continue
        if i == 3:
            break
        out.append(i)
    except Exception:
        out.append('x')
print(out)";
            Assert.That(Out(src), Is.EqualTo("[0, 2]"));
        }

        [Test]
        public void HandlerStackIsClearedByBreakInsideTry()
        {
            // After the break the try block is gone, so a later error must not jump into its handler.
            const string src = @"
for i in range(3):
    try:
        break
    except Exception:
        print('wrong')
1 / 0";
            Assert.That(Fails(src).TypeName, Is.EqualTo("ZeroDivisionError"));
        }

        [Test]
        public void RaiseInstanceAndClass()
        {
            Assert.That(Out("try:\n    raise ValueError\nexcept ValueError as e:\n    print(repr(e))"), Is.EqualTo("ValueError()"));
            Assert.That(Out("try:\n    raise ValueError('a', 1)\nexcept ValueError as e:\n    print(e)"), Is.EqualTo("('a', 1)"));
            Assert.That(Out("e = RuntimeError('x')\ntry:\n    raise e\nexcept RuntimeError as r:\n    print(r is e)"), Is.EqualTo("True"));
        }

        [Test]
        public void RaiseOfNonExceptionIsATypeError()
        {
            Assert.That(Fails("raise 5").Problem, Is.EqualTo("exceptions must derive from BaseException"));
        }

        [Test]
        public void BareRaiseReraisesCurrentException()
        {
            const string src = @"
try:
    try:
        1 / 0
    except ZeroDivisionError:
        print('inner')
        raise
except ZeroDivisionError:
    print('outer')";
            Assert.That(Out(src), Is.EqualTo("inner\nouter"));
        }

        [Test]
        public void ExceptionRaisedInsideHandlerReplacesOriginal()
        {
            var e = Fails("try:\n    1 / 0\nexcept ZeroDivisionError:\n    raise ValueError('new')");
            Assert.That(e.TypeName, Is.EqualTo("ValueError"));
            Assert.That(e.Line, Is.EqualTo(4));
        }

        [Test]
        public void TracebackKeepsOriginalRaiseSiteWhenReraised()
        {
            const string src = @"
def f():
    1 / 0
try:
    f()
except ZeroDivisionError:
    raise";
            var e = Fails(src);
            Assert.That(e.Trace[0].Function, Is.EqualTo("f"));
            Assert.That(e.Trace[0].Line, Is.EqualTo(3));
        }

        [Test]
        public void CatchingNonExceptionClassIsATypeError()
        {
            Assert.That(Fails("try:\n    1 / 0\nexcept 5:\n    pass").Problem, Does.Contain("catching classes that do not inherit"));
        }

        [Test]
        public void ErrorsInTheHandlerBodyAreNotCaughtByItsOwnTry()
        {
            const string src = @"
try:
    try:
        1 / 0
    except ZeroDivisionError:
        [][0]
except IndexError:
    print('outer got index')";
            Assert.That(Out(src), Is.EqualTo("outer got index"));
        }

        [Test]
        public void NestedTryInsideLoopRepeatedly()
        {
            const string src = @"
n = 0
for i in range(100):
    try:
        try:
            if i % 2:
                raise KeyError(i)
        except KeyError:
            n += 1
            raise ValueError('x')
    except ValueError:
        n += 10
print(n)";
            Assert.That(Out(src), Is.EqualTo("550"));
        }

        [Test]
        public void ReturnInsideTryPopsHandlerWithFrame()
        {
            const string src = @"
def f():
    try:
        return 1
    except Exception:
        return 2
print(f(), f())
1 / 0";
            Assert.That(Fails(src).TypeName, Is.EqualTo("ZeroDivisionError"));
        }

        [Test]
        public void HostErrorsAreCatchableAsTheirNamedType()
        {
            const string src = @"
import game
try:
    game.fail()
except GameError as e:
    print('caught', e)";
            string output = Run(src, vm =>
            {
                vm.RegisterExceptionType("GameError");
                vm.RegisterModule("game", m => m.Function("fail", () => throw new PyError("GameError", "no energy")));
            });
            Assert.That(output.TrimEnd(), Is.EqualTo("caught no energy"));
        }

        [Test]
        public void RegisteredExceptionHierarchyIsRespected()
        {
            const string src = @"
import game
try:
    game.fail()
except VmTestBaseError:
    print('base')";
            string output = Run(src, vm =>
            {
                vm.RegisterExceptionType("VmTestBaseError");
                vm.RegisterExceptionType("VmTestChildError", "VmTestBaseError");
                vm.RegisterModule("game", m => m.Function("fail", () => throw new PyError("VmTestChildError", "x")));
            });
            Assert.That(output.TrimEnd(), Is.EqualTo("base"));
        }

        [Test]
        public void NonPythonHostExceptionsStopTheTaskWithHostError()
        {
            var e = Fails("import game\ngame.crash()", vm =>
                vm.RegisterModule("game", m => m.Function("crash", () => throw new System.InvalidOperationException("bug"))));
            Assert.That(e.TypeName, Is.EqualTo("HostError"));
            Assert.That(e.Problem, Is.EqualTo("bug"));
            Assert.That(e.InnerException, Is.InstanceOf<System.InvalidOperationException>());
            Assert.That(e.Line, Is.EqualTo(2));
        }

        [Test]
        public void HostErrorCannotBeCaughtByScripts()
        {
            var e = Fails("import game\ntry:\n    game.crash()\nexcept Exception:\n    print('no')", vm =>
                vm.RegisterModule("game", m => m.Function("crash", () => throw new System.InvalidOperationException("bug"))));
            Assert.That(e.TypeName, Is.EqualTo("HostError"));
        }
    }
}

namespace CSharpPython.Tests.Vm
{
    public class ExceptionRegistrationTests
    {
        [Test]
        public void UnknownParentIsRejected()
        {
            Assert.Throws<System.ArgumentException>(() => new PyVm().RegisterExceptionType("X1Error", "NopeError"));
        }

        [Test]
        public void UnregisteredHostErrorsStillMatchException()
        {
            string output = Py.Run("import game\ntry:\n    game.fail()\nexcept Exception as e:\n    print(e)",
                vm => vm.RegisterModule("game", m => m.Function("fail", () => throw new PyError("NeverRegisteredError", "x"))));
            Assert.That(output.TrimEnd(), Is.EqualTo("x"));
        }
    }
}
