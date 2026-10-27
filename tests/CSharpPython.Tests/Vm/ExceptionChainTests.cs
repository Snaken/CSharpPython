using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    /// <summary>What the host sees of <c>raise ... from</c> and of an error raised while another was being handled.</summary>
    public class ExceptionChainTests
    {
        private const string Cause = "The above exception was the direct cause of the following exception:";
        private const string During = "During handling of the above exception, another exception occurred:";

        [Test]
        public void AnUncaughtErrorCarriesItsCause()
        {
            var error = Fails("try:\n    raise ValueError('a')\nexcept ValueError as e:\n    raise RuntimeError('b') from e\n");
            Assert.That(error.TypeName, Is.EqualTo("RuntimeError"));
            Assert.That(error.Cause.TypeName, Is.EqualTo("ValueError"));
            Assert.That(error.Cause.Problem, Is.EqualTo("a"));
            Assert.That(error.Context, Is.Null);
            Assert.That(error.InnerException, Is.SameAs(error.Cause));
            Assert.That(error.Cause.Line, Is.EqualTo(2));
            string text = error.FormatTraceback();
            Assert.That(text, Does.Contain(Cause));
            Assert.That(text.IndexOf("ValueError: a"), Is.LessThan(text.IndexOf("RuntimeError: b")));
            Assert.That(text, Does.EndWith("RuntimeError: b"));
        }

        [Test]
        public void AnUncaughtErrorRaisedWhileHandlingAnotherCarriesItAsContext()
        {
            var error = Fails("try:\n    1 // 0\nexcept ZeroDivisionError:\n    raise KeyError('k')\n");
            Assert.That(error.Cause, Is.Null);
            Assert.That(error.Context.TypeName, Is.EqualTo("ZeroDivisionError"));
            Assert.That(error.InnerException, Is.SameAs(error.Context));
            string text = error.FormatTraceback();
            Assert.That(text, Does.Contain(During));
            Assert.That(text, Does.Not.Contain(Cause));
        }

        [Test]
        public void FromNoneHidesTheContext()
        {
            var error = Fails("try:\n    1 // 0\nexcept ZeroDivisionError:\n    raise KeyError('k') from None\n");
            Assert.That(error.Cause, Is.Null);
            Assert.That(error.Context, Is.Null);
            Assert.That(error.InnerException, Is.Null);
            Assert.That(error.FormatTraceback(), Does.Not.Contain("above exception"));
        }

        [Test]
        public void AnErrorFromACauseKeepsItsOwnChain()
        {
            var error = Fails("try:\n    try:\n        raise KeyError('one')\n    except KeyError as one:\n        raise ValueError('two') from one\nexcept ValueError:\n    raise RuntimeError('three')\n");
            Assert.That(error.Context.TypeName, Is.EqualTo("ValueError"));
            Assert.That(error.Context.Cause.TypeName, Is.EqualTo("KeyError"));
            string text = error.FormatTraceback();
            Assert.That(text.IndexOf("KeyError"), Is.LessThan(text.IndexOf("ValueError: two")));
            Assert.That(text.IndexOf("ValueError: two"), Is.LessThan(text.IndexOf("RuntimeError: three")));
        }

        [Test]
        public void ARaiseInAnEventHandlerDoesNotPickUpTheInterruptedExceptionHandler()
        {
            var vm = new PyVm { Output = s => { } };
            vm.RegisterModule("game", m => m.Native("wait", (c, a) => { c.Wait(3); return Value.None; }));
            var task = vm.Start("import game\ndef on_hit():\n    raise ValueError('event')\ntry:\n    raise KeyError('main')\nexcept KeyError:\n    game.wait()\n");
            Assert.That(task.Update(), Is.EqualTo(TaskState.Waiting));
            task.QueueEvent("on_hit");
            task.RunUntilDone();
            Assert.That(task.Error.TypeName, Is.EqualTo("ValueError"));
            Assert.That(task.Error.Context, Is.Null);
        }

        [Test]
        public void ALoopInTheChainCannotHangTheHost()
        {
            var error = Fails("class E(Exception):\n    pass\na = E('a')\nb = E('b')\na.__cause__ = b\nb.__cause__ = a\nraise a\n");
            int depth = 0;
            for (var e = error; e != null; e = e.Cause) depth++;
            Assert.That(depth, Is.GreaterThan(1).And.LessThan(20));
        }

        [Test]
        public void ARaiseThatCreatesALoopIsCut()
        {
            Assert.That(Out("class E(Exception):\n    pass\ntry:\n    try:\n        raise E('a')\n    except E as a:\n        try:\n            raise E('b')\n        except E as b:\n            raise a\nexcept E as e:\n    print(e, e.__context__, e.__context__.__context__)"),
                Is.EqualTo("a b None"));
        }

        [Test]
        public void ExceptionsThatNeverWereChainedHaveNoneAttributes()
        {
            Assert.That(Out("class E(Exception):\n    pass\ntry:\n    raise E('x')\nexcept E as e:\n    print(e.__cause__, e.__context__, e.__suppress_context__)\ntry:\n    {}['a']\nexcept KeyError as e:\n    print(e.__cause__, e.__context__, e.__suppress_context__)"),
                Is.EqualTo("None None False\nNone None False"));
        }
    }
}

namespace CSharpPython.Tests.Vm
{
    public class HandledExceptionsTests
    {
        private static Value Error(string message) => Value.FromException(new PyExceptionValue(ExceptionTypes.GetOrCreate("ValueError"), message));

        private static string Name(Value v) => v.Kind == ValueKind.None ? "-" : ((PyExceptionValue)v.RawObject).Message;

        [Test]
        public void NestedBlocksGiveTheOuterExceptionBack()
        {
            Value info = HandledExceptions.Enter(Value.None, 1, Error("outer"));
            Assert.That(Name(HandledExceptions.Current(info)), Is.EqualTo("outer"));
            info = HandledExceptions.Enter(info, 2, Error("inner"));
            Assert.That(Name(HandledExceptions.Current(info)), Is.EqualTo("inner"));
            info = HandledExceptions.Leave(info, 1);
            Assert.That(Name(HandledExceptions.Current(info)), Is.EqualTo("outer"));
            info = HandledExceptions.Leave(info, 0);
            Assert.That(info.Kind, Is.EqualTo(ValueKind.None));
        }

        [Test]
        public void EnteringAnOuterBlockForgetsTheDeeperOnes()
        {
            Value info = HandledExceptions.Enter(HandledExceptions.Enter(Value.None, 1, Error("a")), 2, Error("b"));
            info = HandledExceptions.Enter(info, 1, Error("c"));
            Assert.That(Name(HandledExceptions.Current(info)), Is.EqualTo("c"));
            info = HandledExceptions.Leave(info, 1);
            Assert.That(Name(HandledExceptions.Current(info)), Is.EqualTo("c"));
        }

        [Test]
        public void ASingleExceptionFromAnOlderSnapshotCountsAsTheFirstSlot()
        {
            Value old = Error("legacy");
            Assert.That(Name(HandledExceptions.Current(old)), Is.EqualTo("legacy"));
            Value info = HandledExceptions.Enter(old, 2, Error("inner"));
            info = HandledExceptions.Leave(info, 1);
            Assert.That(Name(HandledExceptions.Current(info)), Is.EqualTo("legacy"));
            Assert.That(HandledExceptions.Current(Value.FromInt(5)).Kind, Is.EqualTo(ValueKind.None));
        }
    }
}
