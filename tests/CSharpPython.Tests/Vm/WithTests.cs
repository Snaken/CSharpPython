using System.Linq;
using CSharpPython.Editor;
using CSharpPython.Syntax;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class WithTests
    {
        [TestCase("with")]
        [TestCase("with x")]
        [TestCase("with x:")]
        [TestCase("with x as:\n    pass")]
        [TestCase("with x as 1:\n    pass")]
        [TestCase("with x, :\n    pass")]
        [TestCase("with x as y z:\n    pass")]
        public void BadWithStatementsAreSyntaxErrors(string source)
        {
            Assert.Throws<PySyntaxError>(() => new PyVm().Compile(source), source);
        }

        [Test]
        public void ProtocolErrorsNameTheMissingMethod()
        {
            var noExit = Fails("class A:\n    def __enter__(self):\n        return self\nwith A():\n    pass");
            Assert.That(noExit.TypeName, Is.EqualTo("TypeError"));
            Assert.That(noExit.Problem, Is.EqualTo("'A' object does not support the context manager protocol (missed __exit__ method)"));
            var noEnter = Fails("class A:\n    def __exit__(self, *a):\n        pass\nwith A():\n    pass");
            Assert.That(noEnter.Problem, Does.EndWith("(missed __enter__ method)"));
            Assert.That(Fails("with 5:\n    pass").Problem, Does.StartWith("'int' object does not support the context manager protocol"));
        }

        [Test]
        public void InternalHelperNamesCannotBeUsedFromScripts()
        {
            Assert.Throws<PySyntaxError>(() => new PyVm().Compile("x = .with"));
        }

        [Test]
        public void UncaughtErrorKeepsTheBodyLine()
        {
            var error = Fails("class A:\n    def __enter__(self):\n        return self\n    def __exit__(self, *a):\n        return False\nwith A():\n    x = 1\n    raise ValueError('boom')\n");
            Assert.That(error.TypeName, Is.EqualTo("ValueError"));
            Assert.That(error.Trace[0].Line, Is.EqualTo(8));
        }

        [Test]
        public void EnterAndExitMayWait()
        {
            const string source = "import game\nclass A:\n    def __enter__(self):\n        game.wait(1)\n        print('in')\n        return self\n    def __exit__(self, *a):\n        game.wait(1)\n        print('out')\nwith A():\n    game.wait(1)\n    print('body')\nprint('done')";
            var vm = new PyVm();
            var sb = new System.Text.StringBuilder();
            vm.Output = o => sb.Append(o);
            vm.RegisterModule("game", m => m.Native("wait", (ctx, a) => { ctx.Wait((int)a[0].AsInt()); return Value.None; }, 1, 1));
            var task = vm.Start(source);
            int ticks = 0;
            while (task.Update() != TaskState.Finished && ticks++ < 20) { }
            Assert.That(sb.ToString(), Is.EqualTo("in\nbody\nout\ndone\n"));
        }

        [Test]
        public void WithWorksInEventHandlers()
        {
            Assert.That(Out("class A:\n    def __enter__(self):\n        print('in')\n    def __exit__(self, *a):\n        print('out')\ndef handler():\n    with A():\n        print('body')\nhandler()\nhandler()"),
                Is.EqualTo("in\nbody\nout\nin\nbody\nout"));
        }

        [Test]
        public void EditorOffersTheKeywordButHidesInternals()
        {
            var names = Completer.Complete("w", 1, new PyVm()).Items.Select(i => i.Text).ToList();
            Assert.That(names, Does.Contain("with"));
            Assert.That(Completer.Complete("", 0, new PyVm()).Items.Select(i => i.Text), Has.None.StartsWith("."));
        }
    }
}
