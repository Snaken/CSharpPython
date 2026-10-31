using System;
using CSharpPython.Syntax;

namespace CSharpPython.Tests.Vm
{
    /// <summary>VmOptions.SnapshotKey (signed snapshots) and VmOptions.MaxSourceLength.</summary>
    public class SnapshotSigningTests
    {
        private const string Source = "total = 0\nwhile True:\n    total += 1\n";
        private static readonly byte[] KeyA = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        private static readonly byte[] KeyB = new byte[] { 9, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

        private static PyVm Vm(byte[] key) => new PyVm(new VmOptions { EnableSnapshots = true, SnapshotKey = key });

        private static (PyVm vm, Script script, byte[] data) Saved(byte[] key)
        {
            var vm = Vm(key);
            var script = vm.Compile(Source);
            var task = vm.Start(script);
            task.Update();
            return (vm, script, task.Save());
        }

        [Test]
        public void ASignedSnapshotRestoresAndKeepsRunning()
        {
            var (vm, script, data) = Saved(KeyA);
            var back = vm.Restore(script, data);
            long before = back.Globals["total"].AsInt();
            back.Update();
            Assert.That(back.State, Is.EqualTo(TaskState.Running));
            Assert.That(back.Globals["total"].AsInt(), Is.GreaterThan(before), "it carries on from where it was saved");
        }

        [Test]
        public void ASignedSnapshotIsLongerByTheSignatureOnly()
        {
            var (_, _, signed) = Saved(KeyA);
            var (_, _, plain) = Saved(null);
            Assert.That(signed.Length, Is.EqualTo(plain.Length + 32));
        }

        [Test]
        public void ChangingAnyByteIsRefused()
        {
            var (vm, script, data) = Saved(KeyA);
            for (int i = 0; i < data.Length; i += Math.Max(1, data.Length / 200))
            {
                var edited = (byte[])data.Clone();
                edited[i] ^= 0x01;
                Assert.Throws<SnapshotException>(() => vm.Restore(script, edited), "byte " + i);
            }
            var last = (byte[])data.Clone();
            last[last.Length - 1] ^= 0x80;
            Assert.Throws<SnapshotException>(() => vm.Restore(script, last));
        }

        [Test]
        public void ResettingTheStepCountInASignedSnapshotIsRefused()
        {
            var vm = Vm(KeyA);
            var script = vm.Compile(Source);
            var task = vm.Start(script);
            for (int i = 0; i < 20; i++) task.Update();
            var data = task.Save();
            // The step count sits among the numbers after the object table; zeroing a stretch of bytes near the end
            // would reset it in an unsigned snapshot.
            var edited = (byte[])data.Clone();
            for (int i = edited.Length - 120; i < edited.Length - 60; i++) edited[i] = 0;
            Assert.Throws<SnapshotException>(() => vm.Restore(script, edited));
        }

        [Test]
        public void TheWrongKeyIsRefused()
        {
            var (_, script, data) = Saved(KeyA);
            Assert.Throws<SnapshotException>(() => Vm(KeyB).Restore(script, data));
        }

        [Test]
        public void AnUnsignedSnapshotIsRefusedByAVmWithAKey()
        {
            var (_, script, data) = Saved(null);
            Assert.Throws<SnapshotException>(() => Vm(KeyA).Restore(script, data));
        }

        [Test]
        public void ASignedSnapshotIsRefusedByAVmWithoutAKey()
        {
            var (_, script, data) = Saved(KeyA);
            Assert.Throws<SnapshotException>(() => Vm(null).Restore(script, data));
        }

        [Test]
        public void TooShortDataIsRefused()
        {
            var vm = Vm(KeyA);
            var script = vm.Compile(Source);
            Assert.Throws<SnapshotException>(() => vm.Restore(script, Array.Empty<byte>()));
            Assert.Throws<SnapshotException>(() => vm.Restore(script, new byte[31]));
            Assert.Throws<SnapshotException>(() => vm.Restore(script, new byte[32]));
        }

        [Test]
        public void WithoutAKeySnapshotsAreAsBefore()
        {
            var (vm, script, data) = Saved(null);
            Assert.That(vm.Restore(script, data).Update(), Is.EqualTo(TaskState.Running));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(15)]
        public void AShortKeyIsRefused(int length) =>
            Assert.Throws<ArgumentException>(() => new PyVm(new VmOptions { SnapshotKey = new byte[length] }));

        // ---- MaxSourceLength ----

        [Test]
        public void TheDefaultSourceLimitIsOneMillionCharacters()
        {
            Assert.That(new VmOptions().MaxSourceLength, Is.EqualTo(1000000));
            var vm = new PyVm();
            var error = Assert.Throws<PySyntaxError>(() => vm.Compile("x = '" + new string('a', 1000000) + "'"));
            Assert.That(error.Problem, Does.Contain("too long").And.Contain("1000000"));
            Assert.DoesNotThrow(() => vm.Compile("x = 1\n" + new string('#', 999990)));
        }

        [Test]
        public void TheLimitAppliesToStartToo() =>
            Assert.Throws<PySyntaxError>(() => new PyVm(new VmOptions { MaxSourceLength = 10 }).Start("x = 1 + 1 + 1"));

        [Test]
        public void ASourceExactlyAtTheLimitIsAccepted()
        {
            var vm = new PyVm(new VmOptions { MaxSourceLength = 5 });
            Assert.DoesNotThrow(() => vm.Compile("x = 1"));
            Assert.Throws<PySyntaxError>(() => vm.Compile("x = 12"));
        }

        [Test]
        public void ZeroTurnsTheSourceLimitOff()
        {
            var vm = new PyVm(new VmOptions { MaxSourceLength = 0, MaxSequenceLength = 4194304 });
            Assert.DoesNotThrow(() => vm.Compile("x = 1\n" + new string('#', 3000000)));
        }

        [Test]
        public void ANegativeSourceLimitIsRefused() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new PyVm(new VmOptions { MaxSourceLength = -1 }));
    }
}
