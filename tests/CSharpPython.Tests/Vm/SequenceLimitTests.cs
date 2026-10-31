using System;

namespace CSharpPython.Tests.Vm
{
    /// <summary>VmOptions.MaxSequenceLength: 100,000 by default, up to 4,194,304, and it applies to every way a container grows.</summary>
    public class SequenceLimitTests
    {
        private static string Outcome(string source, VmOptions options = null)
        {
            var vm = new PyVm(options);
            var task = vm.Start(source);
            task.RunUntilDone();
            return task.Error == null ? "finished" : task.Error.TypeName;
        }

        [Test]
        public void TheDefaultIsOneHundredThousand()
        {
            Assert.That(new VmOptions().MaxSequenceLength, Is.EqualTo(100000));
            Assert.That(VmOptions.SequenceLengthCeiling, Is.EqualTo(4194304));
        }

        // Each script grows one kind of container past 100,000 items.
        [TestCase("l = []\nfor i in range(200000):\n    l.append(i)")]
        [TestCase("l = [0] * 200000")]
        [TestCase("l = list(range(200000))")]
        [TestCase("l = [i for i in range(200000)]")]
        [TestCase("l = [0]\nfor i in range(20):\n    l = l + l")]
        [TestCase("l = []\nfor i in range(200):\n    l.extend([0] * 1000)")]
        [TestCase("l = [0] * 99999\nl.insert(0, 1)\nl.insert(0, 2)")]
        [TestCase("l = [0] * 100000\nl[0:0] = [1, 2]")]
        [TestCase("d = {}\nfor i in range(200000):\n    d[i] = 1")]
        [TestCase("d = {i: i for i in range(200000)}")]
        [TestCase("d = {}\nfor i in range(200):\n    d.update({j: 1 for j in range(i * 1000, i * 1000 + 1000)})")]
        [TestCase("d = {}\nfor i in range(200000):\n    d.setdefault(i, 1)")]
        [TestCase("s = set()\nfor i in range(200000):\n    s.add(i)")]
        [TestCase("s = {i for i in range(200000)}")]
        [TestCase("s = set()\nfor i in range(200):\n    s.update(range(i * 1000, i * 1000 + 1000))")]
        [TestCase("import collections\nq = collections.deque()\nfor i in range(200000):\n    q.append(i)")]
        [TestCase("import collections\nq = collections.deque()\nfor i in range(200000):\n    q.appendleft(i)")]
        [TestCase("import collections\nq = collections.deque()\nfor i in range(200):\n    q.extend(range(1000))")]
        [TestCase("s = \"x\" * 200000")]
        [TestCase("s = \"x\"\nfor i in range(20):\n    s = s + s")]
        [TestCase("s = \"\".join([\"ab\"] * 100000)")]
        public void AContainerCannotGrowPastTheLimit(string source) =>
            Assert.That(Outcome(source), Is.EqualTo("MemoryError"), source);

        [TestCase("s = \"%200000d\" % 1")]
        [TestCase("s = f\"{1:200000}\"")]
        public void AFormatWidthPastTheLimitIsAValueError(string source) =>
            Assert.That(Outcome(source), Is.EqualTo("ValueError"), source);

        [Test]
        public void TheScriptCanCatchTheMemoryError()
        {
            var output = Py.Out("l = []\ntry:\n    for i in range(200000):\n        l.append(i)\nexcept MemoryError:\n    print('stopped at', len(l))");
            Assert.That(output, Is.EqualTo("stopped at 100000"));
        }

        [Test]
        public void ContainersWithinTheLimitStillWork()
        {
            string source = "l = list(range(100000))\nd = {i: i for i in range(100000)}\ns = set(range(100000))\nx = \"x\" * 100000\nprint(len(l), len(d), len(s), len(x))";
            Assert.That(Py.Out(source), Is.EqualTo("100000 100000 100000 100000"));
        }

        [Test]
        public void AHostCanRaiseTheLimit()
        {
            var options = new VmOptions { MaxSequenceLength = 1000000 };
            Assert.That(Outcome("l = list(range(500000))\nd = {i: 1 for i in range(500000)}", options), Is.EqualTo("finished"));
            Assert.That(Outcome("l = list(range(2000000))", options), Is.EqualTo("MemoryError"));
        }

        [Test]
        public void AHostCanLowerTheLimit()
        {
            var options = new VmOptions { MaxSequenceLength = 10 };
            Assert.That(Outcome("l = list(range(10))", options), Is.EqualTo("finished"));
            Assert.That(Outcome("l = list(range(11))", options), Is.EqualTo("MemoryError"));
        }

        [Test]
        public void TheCeilingCanBeUsed() =>
            Assert.That(Outcome("l = [0] * 4194304", new VmOptions { MaxSequenceLength = VmOptions.SequenceLengthCeiling, MaxMemoryBytes = 0 }), Is.EqualTo("finished"));

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(4194305)]
        [TestCase(int.MaxValue)]
        public void AValueOutsideTheAllowedRangeIsRefused(int value) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new PyVm(new VmOptions { MaxSequenceLength = value }));

        [Test]
        public void TwoVmsKeepTheirOwnLimits()
        {
            var small = new PyVm(new VmOptions { MaxSequenceLength = 10 });
            var large = new PyVm(new VmOptions { MaxSequenceLength = 1000 });
            var a = small.Start("l = list(range(100))");
            var b = large.Start("l = list(range(100))");
            a.RunUntilDone();
            b.RunUntilDone();
            Assert.That(a.State, Is.EqualTo(TaskState.Error));
            Assert.That(b.State, Is.EqualTo(TaskState.Finished));
        }

        [Test]
        public void ASnapshotRestoresWhateverTheLimitWas()
        {
            var raised = new VmOptions { MaxSequenceLength = 1000000, EnableSnapshots = true };
            var vm = new PyVm(raised);
            var script = vm.Compile("l = list(range(300000))\nwhile True:\n    pass\n");
            var task = vm.Start(script);
            for (int i = 0; i < 50; i++) task.Update();
            var data = task.Save();
            var back = new PyVm(raised).Restore(script, data);
            Assert.That(back.Update(), Is.EqualTo(TaskState.Running));
        }
    }
}
