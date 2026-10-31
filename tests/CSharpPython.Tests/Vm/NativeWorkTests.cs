using System;

namespace CSharpPython.Tests.Vm
{
    /// <summary>
    /// A native operation on a large value costs steps in proportion to its size, so MaxSteps and StepsPerSlice bound the
    /// time a script takes (one "x in big_list" used to be a single step however long the list was).
    /// </summary>
    public class NativeWorkTests
    {
        private static TaskState Run(string source, long maxSteps, out PyRuntimeError error, int stepsPerSlice = 10000)
        {
            var vm = new PyVm(new VmOptions { MaxSteps = maxSteps, StepsPerSlice = stepsPerSlice });
            var task = vm.Start(source);
            var state = task.RunUntilDone();
            error = task.Error;
            return state;
        }

        // Each statement walks or copies a 20,000 item value; 3,000 of them would be 60 million items of work.
        [TestCase("-1 in l")]
        [TestCase("-1 in t")]
        [TestCase("\"zz\" in s")]
        [TestCase("l.count(-1)")]
        [TestCase("l.index(19999)")]
        [TestCase("l.copy()")]
        [TestCase("l + l")]
        [TestCase("l * 2")]
        [TestCase("l[1:]")]
        [TestCase("l == m")]
        [TestCase("l < m")]
        [TestCase("sorted(l)")]
        [TestCase("tuple(l)")]
        [TestCase("set(l)")]
        [TestCase("list(reversed(l))")]
        [TestCase("s.replace(\"a\", \"b\")")]
        [TestCase("s.split(\"b\")")]
        [TestCase("s.upper()")]
        [TestCase("\",\".join(l2)")]
        [TestCase("d.copy()")]
        [TestCase("dict(d)")]
        [TestCase("d == d2")]
        [TestCase("st.union(st)")]
        [TestCase("st.issubset(st)")]
        [TestCase("l.sort()")]
        public void ABigOperationIsChargedAndStopsAtMaxSteps(string statement)
        {
            string source =
                "l = list(range(20000))\nm = list(range(20000))\nl2 = [\"a\"] * 20000\nt = tuple(l)\ns = \"a\" * 20000\n" +
                "d = dict(zip(l, l))\nd2 = dict(zip(l, l))\nst = set(l)\n" +
                "for i in range(3000):\n    " + statement + "\n";
            var state = Run(source, 300000, out var error);
            Assert.That(state, Is.EqualTo(TaskState.Error), statement);
            Assert.That(error.TypeName, Is.EqualTo("StepLimitExceeded"), statement);
        }

        [Test]
        public void ASliceEndsAfterABigOperation()
        {
            var vm = new PyVm(new VmOptions { StepsPerSlice = 1000 });
            var task = vm.Start("l = list(range(100000))\nfor i in range(10):\n    -1 in l\n");
            int updates = 0;
            while (task.Update() == TaskState.Running && updates < 1000) updates++;
            Assert.That(task.State, Is.EqualTo(TaskState.Finished));
            Assert.That(updates, Is.GreaterThan(5), "each big operation uses up the slice");
        }

        [Test]
        public void SmallOperationsAreNotCharged()
        {
            string source = "l = list(range(10))\nfor i in range(10000):\n    x = (i in l) or l.count(1) or sorted(l) or l[1:] or l + l\n";
            // About 10 steps per pass: the loop itself, not the operations on 10 items, uses the steps.
            Assert.That(Run(source, 200000, out _), Is.EqualTo(TaskState.Finished));
        }

        [Test]
        public void ResultsAreUnchanged()
        {
            Assert.That(Vm.Py.Out("l = list(range(100000))\nprint(-1 in l, 99999 in l, l.count(5), l.index(777), len(l + l), len(sorted(l, reverse=True)), l[-1])"),
                Is.EqualTo("False True 1 777 200000 100000 99999"));
        }
    }
}
