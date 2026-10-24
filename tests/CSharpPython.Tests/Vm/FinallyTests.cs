using System.Linq;
using CSharpPython.Editor;
using CSharpPython.Syntax;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class FinallyTests
    {
        private static PySyntaxError CompileFails(string source)
        {
            return Assert.Throws<PySyntaxError>(() => new PyVm().Compile(source));
        }

        [Test]
        public void ReturnInsideFinallyIsRejected()
        {
            Assert.That(CompileFails("def f():\n    try:\n        pass\n    finally:\n        return 1\n").Message, Does.Contain("'return' inside a 'finally'"));
        }

        [Test]
        public void BreakAndContinueCannotLeaveFinally()
        {
            Assert.That(CompileFails("for i in range(2):\n    try:\n        pass\n    finally:\n        break\n").Message, Does.Contain("'break' cannot leave"));
            Assert.That(CompileFails("for i in range(2):\n    try:\n        pass\n    finally:\n        continue\n").Message, Does.Contain("'continue' cannot leave"));
        }

        [Test]
        public void FinallyOfOneTryDoesNotAffectLoopsInsideIt()
        {
            Assert.That(Out("try:\n    pass\nfinally:\n    while True:\n        print('x')\n        break"), Is.EqualTo("x"));
        }

        [Test]
        public void ExceptionStillReachesTheHostAfterFinallyRuns()
        {
            var error = Fails("try:\n    raise ValueError('boom')\nfinally:\n    print('cleanup')\n");
            Assert.That(error.TypeName, Is.EqualTo("ValueError"));
            Assert.That(error.Problem, Is.EqualTo("boom"));
        }

        [Test]
        public void FinallyRunsOnceWhenNothingFails()
        {
            Assert.That(Run("n = 0\ndef f():\n    global n\n    try:\n        return 1\n    finally:\n        n += 1\nf()\nf()\nprint(n)"), Is.EqualTo("2\n"));
        }

        [Test]
        public void StepLimitIsNotSwallowedByFinally()
        {
            var options = new VmOptions { MaxSteps = 2000 };
            var error = Fails("try:\n    while True:\n        pass\nfinally:\n    print('never')\n", null, options);
            Assert.That(error.TypeName, Is.EqualTo("StepLimitExceeded"));
        }

        [Test]
        public void WaitingInsideTryFinallyResumes()
        {
            var vm = new PyVm { Output = s => { } };
            var sb = new System.Text.StringBuilder();
            vm.Output = s => sb.Append(s);
            vm.RegisterModule("game", m => m.Native("wait", (c, a) => { c.Wait(2); return Value.None; }));
            var task = vm.Start("import game\ntry:\n    game.wait()\n    print('woke')\nfinally:\n    print('done')\n");
            for (int i = 0; i < 10 && task.State != TaskState.Finished; i++) task.Update();
            Assert.That(sb.ToString(), Is.EqualTo("woke\ndone\n"));
        }

        [Test]
        public void SnapshotInsideATryFinallyResumesIntoTheFinally()
        {
            var options = new VmOptions { EnableSnapshots = true };
            string source = "import game\ndef f():\n    try:\n        game.wait()\n        return 'r'\n    finally:\n        print('cleanup')\nprint(f())\n";
            void Setup(PyVm vm, System.Text.StringBuilder sb)
            {
                vm.Output = s => sb.Append(s);
                vm.RegisterModule("game", m => m.Native("wait", (c, a) => { c.Wait(1); return Value.None; }));
            }
            var sb1 = new System.Text.StringBuilder();
            var vm1 = new PyVm(options);
            Setup(vm1, sb1);
            var script = vm1.Compile(source);
            var task = vm1.Start(script);
            task.Update();
            byte[] data = task.Save();

            var sb2 = new System.Text.StringBuilder();
            var vm2 = new PyVm(options);
            Setup(vm2, sb2);
            var restored = vm2.Restore(vm2.Compile(source), data);
            for (int i = 0; i < 5; i++) restored.Update();
            Assert.That(sb2.ToString(), Is.EqualTo("cleanup\nr\n"));
        }

        [Test]
        public void FinallyIsAKeywordForTheEditor()
        {
            var spans = SyntaxHighlighter.Highlight("try:\n    pass\nfinally:\n    pass\n");
            Assert.That(spans.Count(s => s.Kind == HighlightKind.Keyword), Is.EqualTo(4));
            Assert.That(spans.Any(s => s.Kind == HighlightKind.UnsupportedKeyword), Is.False);
            Assert.That(Analyzer.Analyze("try:\n    pass\nfinally:\n    pass\n"), Is.Empty);
            Assert.That(Completer.Complete("fin", 3).Items.Select(i => i.Text), Does.Contain("finally"));
        }
    }
}
