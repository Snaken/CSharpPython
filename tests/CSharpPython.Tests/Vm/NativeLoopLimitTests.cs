using System.Text;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    /// <summary>Built-ins that walk a whole iterable count their work as steps, so MaxSteps and the slice size apply.</summary>
    public class NativeLoopLimitTests
    {
        [TestCase("sum(range(10 ** 12))")]
        [TestCase("min(range(10 ** 12))")]
        [TestCase("max(range(10 ** 12), key=lambda x: x)")]
        [TestCase("all(x >= 0 for x in range(10 ** 12))")]
        [TestCase("any(x < 0 for x in range(10 ** 12))")]
        [TestCase("sum(float(x) for x in range(10 ** 12))")]
        [TestCase("sum(range(10 ** 12), 0.5)")]
        public void AHugeWalkHitsTheStepLimit(string expression)
        {
            var error = Fails("print(" + expression + ")", null, new VmOptions { MaxSteps = 200000 });
            Assert.That(error.TypeName, Is.EqualTo("StepLimitExceeded"));
        }

        [Test]
        public void AWalkWithinTheLimitFinishes()
        {
            Assert.That(Run("print(sum(range(100000)), max(range(5000)), any(range(3)))", null, new VmOptions { MaxSteps = 500000 }), Is.EqualTo("4999950000 4999 True\n"));
        }

        [Test]
        public void EndlessGeneratorsAreFineWhenTheAnswerIsFound()
        {
            const string source = "def forever():\n    n = 0\n    while True:\n        yield n\n        n += 1\nprint(any(n > 100 for n in forever()), all(n < 100 for n in forever()))";
            Assert.That(Out(source), Is.EqualTo("True False"));
        }

        [Test]
        public void ABigWalkEndsTheSlice()
        {
            var sb = new StringBuilder();
            var vm = new PyVm(new VmOptions { StepsPerSlice = 100 }) { Output = s => sb.Append(s) };
            var task = vm.Start("total = sum(range(5000))\nprint(total)\n");
            Assert.That(task.Update(), Is.EqualTo(TaskState.Running));
            Assert.That(sb.ToString(), Is.Empty);
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Finished));
            Assert.That(sb.ToString(), Is.EqualTo("12497500\n"));
            Assert.That(task.StepsExecuted, Is.GreaterThan(5000));
        }

        [Test]
        public void HostFunctionsCanChargeSteps()
        {
            var vm = new PyVm(new VmOptions { MaxSteps = 1000 }) { Output = s => { } };
            vm.RegisterModule("game", m => m.Native("spin", (c, a) => { c.Charge(a[0].AsInt()); return Value.None; }, 1, 1));
            var task = vm.Start("import game\ngame.spin(10)\ngame.spin(0)\ngame.spin(-5)\ngame.spin(5000)\nprint('not reached')");
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Error));
            Assert.That(task.Error.TypeName, Is.EqualTo("StepLimitExceeded"));
        }

        [Test]
        public void SortingFloatsWithNanStillReturnsEveryItem()
        {
            Assert.That(Out("xs = sorted([2.5, float('nan'), 0.5, 1.5])\nprint(len(xs), 0.5 in xs, 2.5 in xs)"), Is.EqualTo("4 True True"));
        }
    }
}
