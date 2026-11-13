using System.Collections.Generic;
using NUnit.Framework;

namespace CSharpPython.Tests.Vm
{
    public class TaskTotalsTests
    {
        [Test]
        public void NoTasksAddUpToNothing()
        {
            foreach (var totals in new[] { TaskTotals.Of(null), TaskTotals.Of(System.Array.Empty<PyTask>()), TaskTotals.Of(new PyTask[] { null }) })
            {
                Assert.That(totals.Tasks, Is.EqualTo(0));
                Assert.That(totals.StepsExecuted, Is.EqualTo(0));
                Assert.That(totals.LargestMeasuredMemoryBytes, Is.EqualTo(0));
                Assert.That(totals.TasksNearMemoryLimit, Is.EqualTo(0));
            }
        }

        [Test]
        public void TheTotalIsTheSumOfEachTasksDiagnostics()
        {
            var vm = new PyVm(new VmOptions { MaxMemoryBytes = 1L << 20 });
            var tasks = new List<PyTask>
            {
                vm.Start("data = list(range(5000))\nfor i in range(100000):\n    x = i\n"),
                vm.Start("x = 1\n"),
                vm.Start("for i in range(50000):\n    y = i\n"),
            };
            foreach (var t in tasks) t.RunUntilDone();

            var totals = TaskTotals.Of(tasks);
            long steps = 0, walks = 0, bytes = 0, largest = 0;
            foreach (var t in tasks)
            {
                var d = t.GetDiagnostics();
                steps += d.StepsExecuted;
                walks += d.MemoryMeasurements;
                bytes += d.LastMeasuredMemoryBytes;
                if (d.LastMeasuredMemoryBytes > largest) largest = d.LastMeasuredMemoryBytes;
            }
            Assert.That(totals.Tasks, Is.EqualTo(3));
            Assert.That(totals.StepsExecuted, Is.EqualTo(steps).And.GreaterThan(0));
            Assert.That(totals.MemoryMeasurements, Is.EqualTo(walks));
            Assert.That(totals.LastMeasuredMemoryBytes, Is.EqualTo(bytes));
            Assert.That(totals.LargestMeasuredMemoryBytes, Is.EqualTo(largest));
            Assert.That(totals.MemoryWalkMilliseconds, Is.EqualTo(0), "timing is off by default");
        }

        [Test]
        public void TotallingDoesNotWalkAnyHeap()
        {
            var vm = new PyVm(new VmOptions { MaxMemoryBytes = 1L << 20 });
            var task = vm.Start("x = 1\n");
            task.RunUntilDone();
            int walks = task.MemoryMeasurements;
            TaskTotals.Of(new[] { task });
            Assert.That(task.MemoryMeasurements, Is.EqualTo(walks));
        }

        [Test]
        public void TasksDueAWalkAreCountedAsNearTheLimit()
        {
            // A limit of one slice's growth makes a task due a walk as soon as it has run; an untouched task is not due.
            var vm = new PyVm(new VmOptions { MaxMemoryBytes = 1024, StepsPerSlice = 1000 });
            var busy = vm.Start("i = 0\nwhile True:\n    i += 1\n");
            busy.Run();
            var fresh = vm.Start("x = 1\n");
            var totals = TaskTotals.Of(new[] { busy, fresh });
            Assert.That(busy.GetDiagnostics().StepsUntilNextMemoryMeasurement, Is.EqualTo(0));
            Assert.That(fresh.GetDiagnostics().StepsUntilNextMemoryMeasurement, Is.GreaterThan(0));
            Assert.That(totals.TasksNearMemoryLimit, Is.EqualTo(1));
        }

        [Test]
        public void WithoutALimitNoTaskIsNearIt()
        {
            var vm = new PyVm(new VmOptions { MaxMemoryBytes = 0 });
            var task = vm.Start("x = 1\n");
            task.RunUntilDone();
            Assert.That(TaskTotals.Of(new[] { task }).TasksNearMemoryLimit, Is.EqualTo(0));
        }

        [Test]
        public void WalkTimesAddUpWhenTimingIsOn()
        {
            var vm = new PyVm(new VmOptions { MaxMemoryBytes = 1L << 20, CollectDiagnostics = true });
            var task = vm.Start("data = list(range(5000))\nfor i in range(100000):\n    x = i\n");
            task.RunUntilDone();
            Assert.That(TaskTotals.Of(new[] { task }).MemoryWalkMilliseconds, Is.EqualTo(task.GetDiagnostics().MemoryWalkMilliseconds));
        }
    }
}
