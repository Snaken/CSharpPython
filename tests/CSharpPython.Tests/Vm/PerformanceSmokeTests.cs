using System.Diagnostics;

namespace CSharpPython.Tests.Vm
{
    /// <summary>Loose guards against accidental slowdowns. They only fail if something is badly wrong.</summary>
    public class PerformanceSmokeTests
    {
        [Test]
        public void RecursiveFibonacciRunsInReasonableTime()
        {
            var vm = new PyVm(new VmOptions { StepsPerSlice = 1000000, MaxCallDepth = 1000 }) { Output = s => { } };
            var task = vm.Start("def fib(n):\n    if n < 2:\n        return n\n    return fib(n - 1) + fib(n - 2)\nresult = fib(24)");
            var watch = Stopwatch.StartNew();
            task.RunUntilDone();
            watch.Stop();
            Assert.That(task.Globals["result"].AsInt(), Is.EqualTo(46368));
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(10000));
            TestContext.Out.WriteLine("fib(24): " + watch.ElapsedMilliseconds + " ms, " + task.StepsExecuted + " steps");
        }

        [Test]
        public void TightLoopRunsInReasonableTime()
        {
            var vm = new PyVm(new VmOptions { StepsPerSlice = 10000000 }) { Output = s => { } };
            var task = vm.Start("total = 0\nfor i in range(300000):\n    total += i % 7");
            var watch = Stopwatch.StartNew();
            task.RunUntilDone();
            watch.Stop();
            Assert.That(task.Globals["total"].AsInt(), Is.EqualTo(899997));
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(10000));
            TestContext.Out.WriteLine("loop: " + watch.ElapsedMilliseconds + " ms, " + task.StepsExecuted + " steps");
        }

        [Test]
        public void ManyShortTasksDoNotLeakOrSlowDown()
        {
            var vm = new PyVm { Output = s => { } };
            var script = vm.Compile("x = 0\nfor i in range(10):\n    x += i");
            for (int i = 0; i < 5000; i++)
            {
                var task = vm.Start(script);
                Assert.That(task.Run(), Is.EqualTo(TaskState.Finished));
            }
        }
    }
}
