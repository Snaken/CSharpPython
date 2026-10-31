using System.Linq;

namespace CSharpPython.Tests.Vm
{
    // PyTask.CurrentLine and GetCallStack tell an editor where a task is (docs/node-view-design.md, decision 15).
    public class CurrentLineTests
    {
        private static PyVm NewVm()
        {
            var vm = new PyVm { Output = s => { } };
            vm.RegisterModule("game", m => m.Native("wait", (c, a) => { c.Wait((int)a[0].AsInt()); return Value.None; }, 1, 1));
            return vm;
        }

        private const string Nested = "import game\n" +        // 1
                                      "def inner():\n" +        // 2
                                      "    game.wait(3)\n" +    // 3
                                      "    print(1)\n" +        // 4
                                      "def outer():\n" +        // 5
                                      "    inner()\n" +         // 6
                                      "    print(2)\n" +        // 7
                                      "outer()\n" +             // 8
                                      "print(3)\n";             // 9

        [Test]
        public void ATaskThatHasNotRunIsAtItsFirstLine()
        {
            var task = NewVm().Start("x = 1\ny = 2\n");
            Assert.That(task.CurrentLine, Is.EqualTo(1));
        }

        [Test]
        public void AWaitingTaskIsOnTheLineOfTheCallThatWaits()
        {
            var task = NewVm().Start(Nested);
            Assert.That(task.Update(), Is.EqualTo(TaskState.Waiting));
            Assert.That(task.CurrentLine, Is.EqualTo(3));
        }

        [Test]
        public void TheCallStackListsEachFunctionInnermostFirst()
        {
            var task = NewVm().Start(Nested);
            task.Update();
            var stack = task.GetCallStack();
            Assert.That(stack.Select(f => f.Line), Is.EqualTo(new[] { 3, 6, 8 }));
            Assert.That(stack.Select(f => f.Function).Take(2), Is.EqualTo(new[] { "inner", "outer" }));
            Assert.That(stack, Has.Count.EqualTo(task.CallDepth));
        }

        [Test]
        public void TheLineMovesOnWhenTheWaitEnds()
        {
            var task = NewVm().Start("import game\ngame.wait(1)\nx = 1\ngame.wait(1)\nx = 2\n");
            task.Update();
            Assert.That(task.CurrentLine, Is.EqualTo(2));
            task.Tick();
            task.Update();
            Assert.That(task.CurrentLine, Is.EqualTo(4));
        }

        [Test]
        public void AFinishedTaskIsOnNoLine()
        {
            var task = NewVm().Start(Nested);
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Finished));
            Assert.That(task.CurrentLine, Is.EqualTo(0));
            Assert.That(task.GetCallStack(), Is.Empty);
        }

        [Test]
        public void AFailedTaskIsOnNoLineButTheErrorHasTheLines()
        {
            var task = NewVm().Start("def f():\n    return 1 / 0\nf()\n");
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Error));
            Assert.That(task.CurrentLine, Is.EqualTo(0));
            Assert.That(task.GetCallStack(), Is.Empty);
            Assert.That(task.Error.Trace[0].Line, Is.EqualTo(2));
        }

        [Test]
        public void TheCallStackIsACopy()
        {
            var task = NewVm().Start(Nested);
            task.Update();
            var before = task.GetCallStack();
            task.Tick();
            task.Tick();
            task.Tick();
            task.Update();
            Assert.That(before.Select(f => f.Line), Is.EqualTo(new[] { 3, 6, 8 }));
        }

        [Test]
        public void ARestoredTaskIsOnTheSameLine()
        {
            var vm = new PyVm(new VmOptions { EnableSnapshots = true }) { Output = s => { } };
            vm.RegisterModule("game", m => m.Native("wait", (c, a) => { c.Wait((int)a[0].AsInt()); return Value.None; }, 1, 1));
            var script = vm.Compile(Nested);
            var task = vm.Start(script);
            task.Update();
            var restored = vm.Restore(script, task.Save());
            Assert.That(restored.CurrentLine, Is.EqualTo(3));
            Assert.That(restored.GetCallStack().Select(f => f.Line), Is.EqualTo(new[] { 3, 6, 8 }));
        }

        [Test]
        public void ALineInsideALoopFollowsTheIterations()
        {
            var task = NewVm().Start("import game\nfor i in range(2):\n    game.wait(1)\n    x = i\n");
            task.Update();
            Assert.That(task.CurrentLine, Is.EqualTo(3));
            task.Tick();
            task.Update();
            Assert.That(task.CurrentLine, Is.EqualTo(3));
            task.Tick();
            task.Update();
            Assert.That(task.State, Is.EqualTo(TaskState.Finished));
        }
    }
}
