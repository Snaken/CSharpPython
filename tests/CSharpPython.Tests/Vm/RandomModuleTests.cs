using System.Text;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    /// <summary>What the CPython comparison scripts cannot show: per-task state, snapshots, limits and the host side.</summary>
    public class RandomModuleTests
    {
        private static PyVm NewVm(StringBuilder output, bool snapshots = false)
        {
            var vm = new PyVm(new VmOptions { EnableSnapshots = snapshots }) { Output = s => output.Append(s) };
            vm.RegisterModule("game", m => m.Native("wait", (ctx, a) => { ctx.Wait((int)a[0].AsInt()); return Value.None; }, 1, 1));
            return vm;
        }

        private const string Script = "import game\nimport random\nrandom.seed(99)\nfor i in range(4):\n    print(random.random(), random.randint(1, 1000))\n    game.wait(1)\n";

        private static void Run(PyTask task)
        {
            for (int i = 0; i < 100 && task.State != TaskState.Finished && task.State != TaskState.Error; i++) task.Update();
        }

        [Test]
        public void EachTaskHasItsOwnSequence()
        {
            var vm = NewVm(new StringBuilder());
            var a = vm.Start("import random\nrandom.seed(5)\nx = random.random()");
            var b = vm.Start("import random\nrandom.seed(5)\nx = random.random()");
            var c = vm.Start("import random\nrandom.seed(6)\nx = random.random()");
            Run(a); Run(b); Run(c);
            Assert.That(a.Globals["x"], Is.EqualTo(b.Globals["x"]));
            Assert.That(a.Globals["x"], Is.Not.EqualTo(c.Globals["x"]));
        }

        [Test]
        public void TasksThatInterleaveDoNotDisturbEachOther()
        {
            string source = "import game\nimport random\nrandom.seed(3)\nout = []\nfor i in range(5):\n    out.append(random.random())\n    game.wait(1)\n";
            var vm = NewVm(new StringBuilder());
            var alone = vm.Start(source);
            Run(alone);
            var first = vm.Start(source);
            var second = vm.Start("import game\nimport random\nrandom.seed(4)\nfor i in range(5):\n    random.random()\n    game.wait(1)\n");
            for (int i = 0; i < 20; i++) { first.Update(); second.Update(); }
            Assert.That(first.Globals["out"].Repr(), Is.EqualTo(alone.Globals["out"].Repr()));
        }

        [Test]
        public void UnseededTasksDiffer()
        {
            var vm = NewVm(new StringBuilder());
            var a = vm.Start("import random\nx = random.getrandbits(64)");
            var b = vm.Start("import random\nx = random.getrandbits(64)");
            Run(a); Run(b);
            Assert.That(a.Globals["x"], Is.Not.EqualTo(b.Globals["x"]));
        }

        [Test]
        public void ARestoredTaskContinuesTheSameSequence()
        {
            var whole = new StringBuilder();
            Run(NewVm(whole).Start(Script));

            var first = new StringBuilder();
            var vm = NewVm(first, true);
            var script = vm.Compile(Script);
            var task = vm.Start(script);
            task.Update();
            task.Update();
            byte[] bytes = task.Save();

            var second = new StringBuilder();
            var restored = NewVm(second, true).Restore(script, bytes);
            Run(restored);
            Assert.That(first.ToString() + second.ToString(), Is.EqualTo(whole.ToString()));
        }

        [Test]
        public void TheGaussCacheSurvivesASnapshot()
        {
            const string source = "import game\nimport random\nrandom.seed(8)\na = random.gauss()\ngame.wait(1)\nb = random.gauss()\n";
            var vm = NewVm(new StringBuilder(), true);
            var script = vm.Compile(source);
            var whole = vm.Start(script);
            Run(whole);

            var task = NewVm(new StringBuilder(), true).Start(script);
            task.Update();
            var restored = NewVm(new StringBuilder(), true).Restore(script, task.Save());
            Run(restored);
            Assert.That(restored.Globals["b"], Is.EqualTo(whole.Globals["b"]));
        }

        [Test]
        public void ATaskThatNeverUsedRandomSavesNothingExtra()
        {
            var vm = NewVm(new StringBuilder(), true);
            var plain = vm.Start("import game\ngame.wait(5)");
            plain.Update();
            var withRandom = vm.Start("import game\nimport random\nrandom.random()\ngame.wait(5)");
            withRandom.Update();
            Assert.That(withRandom.Save().Length - plain.Save().Length, Is.GreaterThan(2000));
        }

        [Test]
        public void ARandomBitCountIsLimitedByMaxIntBits()
        {
            var e = Fails("import random\nrandom.getrandbits(100000)");
            Assert.That(e.TypeName, Is.EqualTo("OverflowError"));
            Assert.That(Out("import random\nprint(random.getrandbits(16384).bit_length() <= 16384)"), Is.EqualTo("True"));
        }

        [Test]
        public void AHugeNumberOfChoicesIsRefused()
        {
            Assert.That(Fails("import random\nrandom.choices([1, 2], k=10**9)").TypeName, Is.EqualTo("MemoryError"));
        }

        [Test]
        public void OnlyIntAndNoneSeedsAreSupported()
        {
            foreach (string seed in new[] { "1.5", "'abc'", "[1]" })
                Assert.That(Fails("import random\nrandom.seed(" + seed + ")").TypeName, Is.EqualTo("TypeError"), seed);
        }

        [Test]
        public void AnExceptionTypeWithADamagedNameIsRejectedWhenRestoring()
        {
            var vm = NewVm(new StringBuilder(), true);
            var script = vm.Compile("import game\ne = ValueError('x')\ngame.wait(5)");
            var task = vm.Start(script);
            task.Update();
            byte[] bytes = task.Save();

            // Overwrite the first letter of the name "ValueError" (stored as UTF-16) with a character a name cannot have.
            byte[] needle = Encoding.Unicode.GetBytes("ValueError");
            int at = -1;
            for (int i = 0; i + needle.Length <= bytes.Length && at < 0; i++)
            {
                bool match = true;
                for (int k = 0; k < needle.Length && match; k++) match = bytes[i + k] == needle[k];
                if (match) at = i;
            }
            Assert.That(at, Is.GreaterThan(0));
            bytes[at] = (byte)'!';
            Assert.Throws<SnapshotException>(() => NewVm(new StringBuilder(), true).Restore(script, bytes));
            Assert.That(ExceptionTypes.TryGet("!alueError", out _), Is.False);
        }
    }
}
