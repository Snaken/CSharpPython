using System;
using System.Threading;

namespace CSharpPython.Tests.Vm
{
    /// <summary>VmOptions.MaxMemoryBytes: an estimate of what a task can reach, 64 MB by default, 0 for no limit.</summary>
    public class MemoryLimitTests
    {
        private static PyTask Run(string source, VmOptions options = null)
        {
            var task = new PyVm(options).Start(source);
            task.RunUntilDone();
            return task;
        }

        private static string Outcome(string source, VmOptions options = null)
        {
            var task = Run(source, options);
            return task.Error == null ? "finished" : task.Error.TypeName;
        }

        [Test]
        public void TheDefaultIsSixtyFourMegabytes()
        {
            Assert.That(new VmOptions().MaxMemoryBytes, Is.EqualTo(64L * 1024 * 1024));
            Assert.That(VmOptions.DefaultMaxMemoryBytes, Is.EqualTo(64L * 1024 * 1024));
        }

        // Each script keeps far more than 64 MB, in a different kind of value.
        [TestCase("L = []\nfor i in range(100000):\n    L.append(\"x\" * 100000)")]
        [TestCase("L = []\nfor i in range(100000):\n    L.append([0] * 100000)")]
        [TestCase("L = []\nfor i in range(100000):\n    L.append(tuple(range(1000)))")]
        [TestCase("L = []\nfor i in range(100000):\n    L.append({j: j for j in range(1000)})")]
        [TestCase("L = []\nfor i in range(100000):\n    L.append(set(range(1000)))")]
        [TestCase("class A:\n    def __init__(self):\n        self.a = [0] * 1000\nL = []\nfor i in range(100000):\n    L.append(A())")]
        [TestCase("L = []\nfor i in range(100000):\n    L.append(lambda: i)\n    L.append([0] * 1000)")]
        public void AScriptThatKeepsTooMuchIsStopped(string source) =>
            Assert.That(Outcome(source), Is.EqualTo("MemoryLimitExceeded"), source);

        [Test]
        public void TheErrorCannotBeCaught()
        {
            var task = Run("try:\n    L = []\n    for i in range(100000):\n        L.append(\"x\" * 100000)\nexcept BaseException:\n    caught = True\nfinally:\n    x = 1\n");
            Assert.That(task.State, Is.EqualTo(TaskState.Error));
            Assert.That(task.Error.TypeName, Is.EqualTo("MemoryLimitExceeded"));
            Assert.That(task.Error.Problem, Does.Contain("67108864"));
            Assert.That(task.Globals.ContainsKey("caught"), Is.False);
        }

        [Test]
        public void ADeadValueIsNotCounted()
        {
            // Allocates far more than 64 MB in total, but keeps almost none of it.
            string source = "x = None\nfor i in range(1500):\n    x = \"y\" * 50000\n    y = [0] * 50000\ny = None\n";
            Assert.That(Outcome(source), Is.EqualTo("finished"));
        }

        [Test]
        public void ALongRunningScriptThatBuildsAndDropsListsIsFine()
        {
            string source = "total = 0\nfor i in range(300000):\n    items = [i, i + 1, i + 2]\n    total += len(str(items))\n";
            Assert.That(Outcome(source), Is.EqualTo("finished"));
        }

        [Test]
        public void ZeroTurnsTheLimitOff()
        {
            var options = new VmOptions { MaxMemoryBytes = 0 };
            Assert.That(Outcome("L = []\nfor i in range(30):\n    L.append([0] * 100000)", options), Is.EqualTo("finished"));
        }

        [Test]
        public void AHostCanRaiseOrLowerTheLimit()
        {
            string source = "L = []\nfor i in range(30):\n    L.append([0] * 100000)";
            Assert.That(Outcome(source, new VmOptions { MaxMemoryBytes = 1L << 30 }), Is.EqualTo("finished"));
            Assert.That(Outcome(source, new VmOptions { MaxMemoryBytes = 1L << 20 }), Is.EqualTo("MemoryLimitExceeded"));
        }

        [Test]
        public void ANegativeLimitIsRefused() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new PyVm(new VmOptions { MaxMemoryBytes = -1 }));

        [Test]
        public void TheEstimateGrowsAndShrinksWithWhatTheScriptKeeps()
        {
            var vm = new PyVm(new VmOptions { MaxMemoryBytes = 0 });
            var task = vm.Start("data = []\nfor i in range(100000):\n    data.append(i)\ndata = None\n");
            long start = task.EstimatedMemoryBytes;
            task.RunUntilDone();
            Assert.That(task.State, Is.EqualTo(TaskState.Finished));
            Assert.That(task.EstimatedMemoryBytes, Is.LessThan(start + 20000), "the list was dropped");

            var big = vm.Start("data = list(range(100000))\n");
            big.RunUntilDone();
            Assert.That(big.EstimatedMemoryBytes, Is.GreaterThan(2000000).And.LessThan(8000000), "about 24 bytes an item");
        }

        [Test]
        public void ASharedValueIsCountedOnce()
        {
            var shared = Run("a = list(range(10000))\nb = [a] * 50\n", new VmOptions { MaxMemoryBytes = 0 });
            var alone = Run("a = list(range(10000))\n", new VmOptions { MaxMemoryBytes = 0 });
            Assert.That(shared.EstimatedMemoryBytes - alone.EstimatedMemoryBytes, Is.LessThan(5000));
        }

        [Test]
        public void ACycleIsWalkedOnce()
        {
            var task = Run("a = []\na.append(a)\nd = {}\nd['me'] = d\nclass N:\n    pass\nn = N()\nn.me = n\n", new VmOptions { MaxMemoryBytes = 0 });
            Assert.That(task.EstimatedMemoryBytes, Is.GreaterThan(0));
        }

        [Test]
        public void DeepNestingIsMeasuredWithoutOverflowingTheStack()
        {
            long measured = -1;
            var thread = new Thread(() =>
            {
                var vm = new PyVm(new VmOptions { MaxMemoryBytes = 0, MaxSequenceLength = 100000 });
                var task = vm.Start("x = []\nfor _ in range(100000):\n    x = [x]\n");
                task.RunUntilDone();
                measured = task.EstimatedMemoryBytes;
            }, 512 * 1024);
            thread.Start();
            thread.Join();
            Assert.That(measured, Is.GreaterThan(2000000));
        }

        [Test]
        public void TasksAreMeasuredSeparately()
        {
            var vm = new PyVm();
            var heavy = vm.Start("L = []\nfor i in range(100000):\n    L.append(\"x\" * 100000)");
            var light = vm.Start("x = 1");
            heavy.RunUntilDone();
            light.RunUntilDone();
            Assert.That(heavy.Error?.TypeName, Is.EqualTo("MemoryLimitExceeded"));
            Assert.That(light.State, Is.EqualTo(TaskState.Finished));
        }

        [Test]
        public void ASleepingTaskKeepsWhatItHolds()
        {
            var vm = new PyVm(new VmOptions { MaxMemoryBytes = 0 });
            var task = vm.Start("data = list(range(50000))\nwhile True:\n    pass\n");
            for (int i = 0; i < 20; i++) task.Update();
            Assert.That(task.EstimatedMemoryBytes, Is.GreaterThan(1000000));
        }
    }
}
