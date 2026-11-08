using System;
using System.Reflection;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Vm
{
    // PyVm.WarmUp and BlockEditor.WarmUp only make the first real use faster; they must be harmless and repeatable.
    public class WarmUpTests
    {
        [Test]
        public void ThePythonWarmUpScriptRunsToTheEndAndSurvivesASnapshot()
        {
            // WarmUp swallows errors, so check here that its script and its save and restore do work.
            var source = (string)typeof(PyVm).GetField("WarmUpScript", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var vm = new PyVm(new VmOptions { EnableSnapshots = true });
            vm.RegisterModule("warm", m => m.Native("pause", (ctx, args) => { ctx.Wait(1); return Value.None; }, 0, 0));
            var script = vm.Compile(source, "<warm-up>");
            var task = vm.Start(script);
            task.Update();
            var copy = vm.Restore(script, task.Save());
            for (int i = 0; i < 100 && task.State != TaskState.Finished; i++) { task.Update(); Assert.That(task.State, Is.Not.EqualTo(TaskState.Error), task.Error?.ToString()); }
            for (int i = 0; i < 100 && copy.State != TaskState.Finished; i++) { copy.Update(); Assert.That(copy.State, Is.Not.EqualTo(TaskState.Error), copy.Error?.ToString()); }
            Assert.That(task.State, Is.EqualTo(TaskState.Finished));
            Assert.That(copy.State, Is.EqualTo(TaskState.Finished));
        }

        [Test]
        public void WarmUpsCanBeCalledAgainAndLeaveOtherVmsAlone()
        {
            var vm = new PyVm();
            string output = "";
            vm.Output = s => output += s;
            PyVm.WarmUp();
            PyVm.WarmUp();
            BlockEditor.WarmUp();
            BlockEditor.WarmUp();
            Assert.That(vm.Modules.ContainsKey("warm"), Is.False, "the private module stays private");
            Assert.That(output, Is.Empty, "nothing a warm-up script prints reaches another VM");
            vm.Start("print(1 + 1)\n").RunUntilDone();
            Assert.That(output, Is.EqualTo("2\n"));
        }

        [Test]
        public void WarmUpsAreSafeFromSeveralThreads()
        {
            var threads = new System.Threading.Thread[4];
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new System.Threading.Thread(() => { PyVm.WarmUp(); BlockEditor.WarmUp(); });
                threads[i].Start();
            }
            foreach (var t in threads) Assert.That(t.Join(30000), Is.True);
        }
    }
}
