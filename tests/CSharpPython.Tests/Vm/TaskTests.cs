using System.Collections.Generic;
using System.Text;

namespace CSharpPython.Tests.Vm
{
    public class TaskTests
    {
        private static PyVm NewVm(StringBuilder sb, VmOptions options = null)
        {
            var vm = new PyVm(options) { Output = s => sb.Append(s) };
            vm.RegisterModule("game", m => m
                .Native("wait", (ctx, a) => { ctx.Wait((int)a[0].AsInt()); return Value.None; }, 1, 1)
                .Native("fetch", (ctx, a) => { ctx.Wait((int)a[0].AsInt(), a[1]); return Value.None; }, 2, 2)
                .Function("add", (x, y) => x.AsInt() + y.AsInt()));
            return vm;
        }

        [Test]
        public void ANewTaskIsRunningUntilItIsRun()
        {
            var task = NewVm(new StringBuilder()).Start("x = 1");
            Assert.That(task.State, Is.EqualTo(TaskState.Running));
            Assert.That(task.Run(), Is.EqualTo(TaskState.Finished));
            Assert.That(task.Globals["x"].AsInt(), Is.EqualTo(1));
            Assert.That(task.Run(), Is.EqualTo(TaskState.Finished));
        }

        [Test]
        public void WaitPausesUntilEnoughTicksPassed()
        {
            var sb = new StringBuilder();
            var task = NewVm(sb).Start("import game\nprint('a')\ngame.wait(3)\nprint('b')");
            Assert.That(task.Update(), Is.EqualTo(TaskState.Waiting));
            Assert.That(sb.ToString(), Is.EqualTo("a\n"));
            Assert.That(task.WaitTicksRemaining, Is.EqualTo(3));
            Assert.That(task.Update(), Is.EqualTo(TaskState.Waiting));
            Assert.That(task.Update(), Is.EqualTo(TaskState.Waiting));
            Assert.That(sb.ToString(), Is.EqualTo("a\n"));
            Assert.That(task.Update(), Is.EqualTo(TaskState.Finished));
            Assert.That(sb.ToString(), Is.EqualTo("a\nb\n"));
        }

        [Test]
        public void RunDoesNotAdvanceAWaitButTickDoes()
        {
            var task = NewVm(new StringBuilder()).Start("import game\ngame.wait(2)");
            Assert.That(task.Run(), Is.EqualTo(TaskState.Waiting));
            Assert.That(task.Run(), Is.EqualTo(TaskState.Waiting));
            task.Tick();
            Assert.That(task.Run(), Is.EqualTo(TaskState.Waiting));
            task.Tick();
            Assert.That(task.WaitTicksRemaining, Is.EqualTo(0));
            Assert.That(task.Run(), Is.EqualTo(TaskState.Finished));
        }

        [Test]
        public void WaitReturnsTheRequestedResult()
        {
            var sb = new StringBuilder();
            var task = NewVm(sb).Start("import game\nr = game.fetch(1, 'done')\nprint(r)");
            task.RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("done\n"));
        }

        [Test]
        public void WaitZeroYieldsOncePerRun()
        {
            var sb = new StringBuilder();
            var task = NewVm(sb).Start("import game\nfor i in range(3):\n    print(i)\n    game.wait(0)");
            Assert.That(task.Run(), Is.EqualTo(TaskState.Waiting));
            Assert.That(sb.ToString(), Is.EqualTo("0\n"));
            Assert.That(task.Run(), Is.EqualTo(TaskState.Waiting));
            Assert.That(task.Run(), Is.EqualTo(TaskState.Waiting));
            Assert.That(task.Run(), Is.EqualTo(TaskState.Finished));
            Assert.That(sb.ToString(), Is.EqualTo("0\n1\n2\n"));
        }

        [Test]
        public void NegativeWaitCountsAsZero()
        {
            var task = NewVm(new StringBuilder()).Start("import game\ngame.wait(-5)");
            Assert.That(task.Run(), Is.EqualTo(TaskState.Waiting));
            Assert.That(task.WaitTicksRemaining, Is.EqualTo(0));
            Assert.That(task.Run(), Is.EqualTo(TaskState.Finished));
        }

        [Test]
        public void WaitInsideNestedCallsResumesInPlace()
        {
            var sb = new StringBuilder();
            const string src = @"
import game
def step(n):
    game.wait(1)
    return n * 2
def run():
    total = 0
    for i in range(3):
        total += step(i)
    return total
print(run())";
            NewVm(sb).Start(src).RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("6\n"));
        }

        [Test]
        public void WaitInsideTryKeepsTheHandler()
        {
            var sb = new StringBuilder();
            const string src = @"
import game
try:
    game.wait(2)
    1 / 0
except ZeroDivisionError:
    print('caught after wait')";
            NewVm(sb).Start(src).RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("caught after wait\n"));
        }

        [Test]
        public void ManyTasksRunIndependently()
        {
            var sb = new StringBuilder();
            var vm = NewVm(sb);
            var script = vm.Compile("import game\nfor i in range(3):\n    game.wait(i)\ndone = True");
            var tasks = new List<PyTask>();
            for (int i = 0; i < 20; i++) tasks.Add(vm.Start(script));
            for (int tick = 0; tick < 20; tick++)
                foreach (var t in tasks) t.Update();
            foreach (var t in tasks) Assert.That(t.State, Is.EqualTo(TaskState.Finished));
        }

        [Test]
        public void SliceBudgetYieldsRunningState()
        {
            var task = NewVm(new StringBuilder(), new VmOptions { StepsPerSlice = 50 })
                .Start("i = 0\nwhile i < 1000:\n    i += 1");
            Assert.That(task.Run(), Is.EqualTo(TaskState.Running));
            Assert.That(task.Globals["i"].AsInt(), Is.LessThan(1000));
            Assert.That(task.StepsExecuted, Is.EqualTo(50));
            int slices = 1;
            while (task.Run() == TaskState.Running) slices++;
            Assert.That(task.Globals["i"].AsInt(), Is.EqualTo(1000));
            Assert.That(slices, Is.GreaterThan(10));
        }

        [Test]
        public void SliceOfOneStillMakesProgress()
        {
            var task = NewVm(new StringBuilder(), new VmOptions { StepsPerSlice = 0 }).Start("x = 1 + 2");
            task.RunUntilDone();
            Assert.That(task.Globals["x"].AsInt(), Is.EqualTo(3));
        }

        [Test]
        public void ResultsAreIdenticalRegardlessOfSliceSize()
        {
            const string src = "def f(n):\n    return n if n < 2 else f(n-1) + f(n-2)\nr = [f(i) for i in range(1)]" ;
            const string plain = "def f(n):\n    if n < 2:\n        return n\n    return f(n-1) + f(n-2)\nr = f(12)";
            long expected = 0;
            foreach (int slice in new[] { 1, 2, 3, 7, 100, 100000 })
            {
                var task = NewVm(new StringBuilder(), new VmOptions { StepsPerSlice = slice }).Start(plain);
                task.RunUntilDone();
                long r = task.Globals["r"].AsInt();
                if (expected == 0) expected = r;
                Assert.That(r, Is.EqualTo(expected), "slice " + slice);
            }
            Assert.That(expected, Is.EqualTo(144));
            Assert.That(src, Is.Not.Null);
        }

        [Test]
        public void HardStepLimitStopsRunawayLoops()
        {
            var task = NewVm(new StringBuilder(), new VmOptions { StepsPerSlice = 100, MaxSteps = 1000 })
                .Start("while True:\n    pass");
            var state = task.RunUntilDone(1000);
            Assert.That(state, Is.EqualTo(TaskState.Error));
            Assert.That(task.Error.TypeName, Is.EqualTo("StepLimitExceeded"));
            Assert.That(task.Error.Line, Is.InRange(1, 2));
            Assert.That(task.StepsExecuted, Is.EqualTo(1000));
        }

        [Test]
        public void StepLimitCannotBeCaughtByScripts()
        {
            var task = NewVm(new StringBuilder(), new VmOptions { MaxSteps = 500 })
                .Start("try:\n    while True:\n        pass\nexcept Exception:\n    print('swallowed')");
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Error));
            Assert.That(task.Error.TypeName, Is.EqualTo("StepLimitExceeded"));
        }

        [Test]
        public void StepLimitThatIsNotReachedDoesNothing()
        {
            var task = NewVm(new StringBuilder(), new VmOptions { MaxSteps = 100000 }).Start("x = 1");
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Finished));
        }

        [Test]
        public void ErroredTaskStaysErrored()
        {
            var task = NewVm(new StringBuilder()).Start("1 / 0");
            Assert.That(task.Run(), Is.EqualTo(TaskState.Error));
            Assert.That(task.Run(), Is.EqualTo(TaskState.Error));
            Assert.That(task.Update(), Is.EqualTo(TaskState.Error));
            Assert.That(task.State, Is.EqualTo(TaskState.Error));
            Assert.That(task.CallDepth, Is.EqualTo(0));
            Assert.That(task.QueueEvent("on_x"), Is.False);
        }

        [Test]
        public void RunUntilDoneStopsAtTheUpdateLimit()
        {
            var task = NewVm(new StringBuilder()).Start("import game\ngame.wait(1000000)");
            Assert.That(task.RunUntilDone(10), Is.EqualTo(TaskState.Waiting));
        }

        [Test]
        public void CallDepthIsReported()
        {
            var depths = new List<int>();
            var vm = NewVm(new StringBuilder());
            PyTask task = null;
            vm.RegisterModule("probe", m => m.Native("depth", (ctx, a) => { depths.Add(ctx.Task.CallDepth); return Value.None; }, 0, 0));
            task = vm.Start("import probe\ndef f():\n    probe.depth()\n    g()\ndef g():\n    probe.depth()\nprobe.depth()\nf()");
            task.RunUntilDone();
            Assert.That(depths, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void ResultIsNoneForModules()
        {
            var task = NewVm(new StringBuilder()).Start("x = 1");
            task.RunUntilDone();
            Assert.That(task.Result.IsNone, Is.True);
        }
    }
}
