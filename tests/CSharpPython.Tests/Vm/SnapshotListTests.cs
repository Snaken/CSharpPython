using System;
using System.Linq;

namespace CSharpPython.Tests.Vm
{
    // Lists are saved in pieces (a repeated value, blocks of ints and bools, other values) since version 17.
    public class SnapshotListTests
    {
        private const string HEX = "43535059100067F8292A36C3ABC403000000003F0000000800000002010000000000000002020000000000000002030000000000000002FCFFFFFFFFFFFFFF040100000061000601000000030000000000000440010101020000001300000000020100000000000000020200000000000000003A00000006000000020000000000000000020000000000000000020000000000000000020000000000000000020000000000000000020000000000000000000000000101000000FFFFFFFF0000150000000000000000000300000004000000670061006D0065000D04000000670061006D00650002000000780073000500000000020000007900730005020000000000000001000000000100000000000000150000000000000000000000000000000000000000000000000000000000";

        private const string Script = @"
import game
ints = [0, 1, -1, 63, 64, -64, -65, 8191, 8192, 2**31, -2**31, 2**62, -2**62, 2**63 - 1, -2**63]
runs = [7] * 10 + [-3] * 5 + [1, 2, 3] + [0] * 4
bools = [True, False, True, True, False]
boolrun = [False] * 9
mixed = [1, 'a', 2.5, None, (1, 2), [3], True, 4, 5, 6]
floats = [0.0, -0.0, 1.5, 1.5, 1.5, 1.5, 1.5]
shared = [1]
sh = [shared] * 5
empty = []
nested = [[0] * 5, [1] * 5]
bigint = [2 ** 70] * 5 + [2 ** 70 + 1]
strs = ['x'] * 6 + ['y']
big = [0] * 90000 + [1]
spread = [i * 3 % 1000 for i in range(5000)]
game.wait(1)
shared.append(2)
print(ints, runs, bools, boolrun, mixed, floats, sh, empty, nested, bigint, strs, sep='|')
print(len(big), sum(big), big[89999], big[90000], sum(spread), sh[0] is sh[4], shared is sh[2])
";

        private static PyVm Vm()
        {
            var vm = new PyVm(new VmOptions { EnableSnapshots = true });
            vm.RegisterModule("game", m => m.Native("wait", (c, a) => { c.Wait(1); return Value.None; }, 1, 1));
            return vm;
        }

        private static PyTask AtTheWait(PyVm vm, Script script)
        {
            var task = vm.Start(script);
            for (int i = 0; i < 2000 && task.State != TaskState.Waiting; i++) task.Update();
            Assert.That(task.State, Is.EqualTo(TaskState.Waiting), task.Error?.ToString());
            return task;
        }

        private static string Finish(PyVm vm, PyTask task)
        {
            string output = "";
            vm.Output = s => output += s;
            for (int i = 0; i < 20 && task.State != TaskState.Finished; i++) task.Update();
            Assert.That(task.State, Is.EqualTo(TaskState.Finished), task.Error?.ToString());
            return output;
        }

        [Test]
        public void ListsOfEveryKindComeBackAsTheyWere()
        {
            var vm = Vm();
            var script = vm.Compile(Script, "<lists>");
            var task = AtTheWait(vm, script);
            byte[] data = task.Save();
            string original = Finish(vm, task);
            string restored = Finish(vm, vm.Restore(script, data));

            Assert.That(restored, Is.EqualTo(original));
            Assert.That(original, Does.StartWith("[0, 1, -1, 63, 64, -64, -65, 8191, 8192, 2147483648"));
            Assert.That(original, Does.Contain("[7, 7, 7, 7, 7, 7, 7, 7, 7, 7, -3, -3, -3, -3, -3, 1, 2, 3, 0, 0, 0, 0]"));
            Assert.That(original, Does.EndWith("90001 1 0 1 " + Enumerable.Range(0, 5000).Sum(i => i * 3 % 1000) + " True True\n"));
            Assert.That(original, Does.Contain("[[1, 2], [1, 2], [1, 2], [1, 2], [1, 2]]"), "a repeated list is still one list");
        }

        [Test]
        public void ARepeatedValueTakesAFewBytesAndSmallNumbersTakeOneOrTwo()
        {
            var vm = Vm();
            var repeated = AtTheWait(vm, vm.Compile("import game\nxs = [0] * 90000\ngame.wait(1)\n", "<a>")).Save();
            Assert.That(repeated.Length, Is.LessThan(400));

            var counted = AtTheWait(vm, vm.Compile("import game\nxs = list(range(1000)) * 90\ngame.wait(1)\n", "<b>")).Save();
            Assert.That(counted.Length, Is.LessThan(90000 * 2), "two bytes an item or less (it was nine)");

            var flags = AtTheWait(vm, vm.Compile("import game\nxs = [i % 3 == 0 for i in range(60000)]\ngame.wait(1)\n", "<c>")).Save();
            Assert.That(flags.Length, Is.LessThan(61000), "a byte a flag (it was two)");
        }

        [Test]
        public void ASnapshotWrittenBeforeVersion17StillRestores()
        {
            // Saved by version 16 of the library from: import game; xs = [1, 2, 3, -4, 'a', (1, 2), 2.5, True]; ys = [0] * 6; game.wait(1); print(xs, ys)
            byte[] old = Enumerable.Range(0, HEX.Length / 2).Select(i => Convert.ToByte(HEX.Substring(i * 2, 2), 16)).ToArray();
            var vm = Vm();
            var script = vm.Compile("import game\nxs = [1, 2, 3, -4, 'a', (1, 2), 2.5, True]\nys = [0] * 6\ngame.wait(1)\nprint(xs, ys)\n", "<a>");
            BitConverter.GetBytes(script.Fingerprint).CopyTo(old, 6); // so that a later change to the compiler does not make the fixture unreadable
            Assert.That(old[4] | old[5] << 8, Is.EqualTo(16), "the fixture is a version 16 snapshot");
            Assert.That(Finish(vm, vm.Restore(script, old)), Is.EqualTo("[1, 2, 3, -4, 'a', (1, 2), 2.5, True] [0, 0, 0, 0, 0, 0]\n"));
        }

        [Test]
        public void DamagedListDataIsRefusedAndNeverCrashes()
        {
            var vm = Vm();
            var script = vm.Compile("import game\nxs = [1, 2, 3, 4, 5, 6, 7, 8]\nys = [0] * 6\nzs = [True, False, 'a', (1, 2), 2.5, 9, 9, 9, 9]\nws = [5] * 7\ngame.wait(1)\n", "<d>");
            byte[] good = AtTheWait(vm, script).Save();
            int restored = 0, refused = 0;
            for (int i = 0; i < good.Length; i++)
                foreach (byte change in new byte[] { 0x01, 0x7F, 0x80, 0xFF })
                {
                    var bad = (byte[])good.Clone();
                    bad[i] ^= change;
                    try { vm.Restore(script, bad); restored++; }
                    catch (SnapshotException) { refused++; }
                }
            Assert.That(refused, Is.GreaterThan(0));
            Assert.That(restored + refused, Is.EqualTo(good.Length * 4));
            for (int length = 0; length < good.Length; length++)
                Assert.Throws<SnapshotException>(() => vm.Restore(script, good.Take(length).ToArray()), "a snapshot cut to " + length + " bytes");
        }
    }
}
