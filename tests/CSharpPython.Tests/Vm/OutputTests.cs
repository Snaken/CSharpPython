using System.Collections.Generic;

namespace CSharpPython.Tests.Vm
{
    // Where print goes: the VM's Output, a task's own Output, or nowhere, in which case print does no work.
    public class OutputTests
    {
        private const string CountingStr =
            "calls = []\nclass Loud:\n    def __str__(self):\n        calls.append(1)\n        return 'loud'\nprint('before', Loud())\nn = len(calls)\n";

        private static PyTask Run(PyVm vm, string source, PyTask task = null)
        {
            task = task ?? vm.Start(source);
            task.RunUntilDone();
            return task;
        }

        [Test]
        public void TheVmOutputReceivesWhatScriptsPrint()
        {
            var lines = new List<string>();
            var vm = new PyVm { Output = lines.Add };
            Run(vm, "print('a', 1)\nprint('b', end='')\n");
            Assert.That(lines, Is.EqualTo(new[] { "a 1\n", "b" }));
        }

        [Test]
        public void ATaskCanHaveItsOwnOutputThatReplacesTheVmOne()
        {
            var shared = new List<string>();
            var mine = new List<string>();
            var vm = new PyVm { Output = shared.Add };
            var first = vm.Start("print('from first')\n");
            first.Output = mine.Add;
            var second = vm.Start("print('from second')\n");
            first.RunUntilDone();
            second.RunUntilDone();
            Assert.That(mine, Is.EqualTo(new[] { "from first\n" }));
            Assert.That(shared, Is.EqualTo(new[] { "from second\n" }), "a task with no output of its own uses the VM's");
        }

        [Test]
        public void ATaskOutputWorksWithNoVmOutputAtAll()
        {
            var mine = new List<string>();
            var vm = new PyVm();
            var task = vm.Start("print('hello')\n");
            Assert.That(task.HasOutput, Is.False);
            task.Output = mine.Add;
            Assert.That(task.HasOutput, Is.True);
            Run(vm, null, task);
            Assert.That(mine, Is.EqualTo(new[] { "hello\n" }));
        }

        [Test]
        public void WithNobodyListeningPrintDoesNotRunAScriptsStr()
        {
            var task = Run(new PyVm(), CountingStr);
            Assert.That(task.Error, Is.Null);
            Assert.That(task.Globals["n"].AsInt(), Is.EqualTo(0), "no listener, so __str__ was not called");
        }

        [Test]
        public void WithAListenerPrintRunsItAsBefore()
        {
            var lines = new List<string>();
            var task = Run(new PyVm { Output = lines.Add }, CountingStr);
            Assert.That(task.Globals["n"].AsInt(), Is.EqualTo(1));
            Assert.That(lines, Is.EqualTo(new[] { "before loud\n" }));
        }

        [Test]
        public void AStrThatRaisesOnlyShowsWhenSomebodyListens()
        {
            const string source = "class Bad:\n    def __str__(self):\n        raise ValueError('no')\nprint(Bad())\n";
            Assert.That(Run(new PyVm(), source).Error, Is.Null);
            var task = Run(new PyVm { Output = s => { } }, source);
            Assert.That(task.Error?.TypeName, Is.EqualTo("ValueError"));
        }

        [TestCase("print(1, foo=2)")]
        [TestCase("print(1, sep=5)")]
        [TestCase("print(1, end=5)")]
        public void BadArgumentsAreStillRefusedWhenNobodyListens(string source)
        {
            var hidden = Run(new PyVm(), source);
            var shown = Run(new PyVm { Output = s => { } }, source);
            Assert.That(hidden.Error?.TypeName, Is.EqualTo("TypeError"));
            Assert.That(shown.Error?.TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void OutputCanBeTurnedOnAndOffWhileTasksRun()
        {
            var lines = new List<string>();
            var vm = new PyVm();
            vm.RegisterModule("game", m => m.Native("tick", (c, a) => { c.Wait(1); return Value.None; }, 0, 0));
            var task = vm.Start("import game\nfor i in range(3):\n    print('line', i)\n    game.tick()\n");
            task.Update();                              // prints line 0 with nobody listening
            vm.Output = lines.Add;
            task.Update();
            task.Update();                              // prints lines 1 and 2
            vm.Output = null;
            task.Update();
            Assert.That(lines, Is.EqualTo(new[] { "line 1\n", "line 2\n" }));
        }

        [Test]
        public void AHostFunctionCanAskWhetherAnyoneIsListening()
        {
            bool? seen = null;
            var vm = new PyVm();
            vm.RegisterModule("game", m => m.Native("listening", (c, a) => { seen = c.HasOutput; return Value.FromBool(c.HasOutput); }, 0, 0));
            var task = Run(vm, "import game\nbefore = game.listening()\n");
            Assert.That(task.Globals["before"].AsBool(), Is.False);
            vm.Output = s => { };
            var again = Run(vm, "import game\nafter = game.listening()\n");
            Assert.That(again.Globals["after"].AsBool(), Is.True);
            Assert.That(seen, Is.True);
        }

        [Test]
        public void ATasksOwnOutputIsNotSavedInASnapshot()
        {
            var vm = new PyVm(new VmOptions { EnableSnapshots = true }) { Output = s => { } };
            var script = vm.Compile("print('x')\nwait = 1\n");
            var task = vm.Start(script);
            task.Output = s => { };
            task.RunUntilDone();
            var restored = vm.Restore(script, task.Save());
            Assert.That(restored.Output, Is.Null, "set it again on a restored task");
            Assert.That(restored.HasOutput, Is.True, "the VM's output still applies");
        }
    }
}
