using System;
using System.Linq;

namespace CSharpPython.Tests.Vm
{
    // Operations that scripts repeat in loops should not make garbage they do not need, and must behave as before.
    public class AllocationTests
    {
        private static PyTask Run(string source)
        {
            var vm = new PyVm(new VmOptions { MaxSteps = 0 });
            var task = vm.Start(source);
            task.RunUntilDone();
            Assert.That(task.State, Is.EqualTo(TaskState.Finished), task.Error?.ToString());
            return task;
        }

        [Test]
        public void ATupleOfConstantsIsMadeOnce()
        {
            var task = Run("def f():\n    return (1, 'a', (2.5, None), -3)\nsame = f() is f()\nvalue = f()\nneg = (0, -1, -1.5)\nempty = () is ()\nmixed = (1, x_unset) if False else 0\n");
            Assert.That(task.Globals["same"].AsBool(), Is.True);
            Assert.That(Ops.Repr(task.Globals["value"]), Is.EqualTo("(1, 'a', (2.5, None), -3)"));
            Assert.That(Ops.Repr(task.Globals["neg"]), Is.EqualTo("(0, -1, -1.5)"));
        }

        [Test]
        public void ATupleThatHoldsANameIsStillBuiltEachTime()
        {
            var task = Run("def f(a):\n    return (a, 1)\nsame = f(5) is f(5)\nvalue = f(5)\n");
            Assert.That(task.Globals["same"].AsBool(), Is.False);
            Assert.That(Ops.Repr(task.Globals["value"]), Is.EqualTo("(5, 1)"));
        }

        [Test]
        public void ASharedConstantTupleWorksAsAKeyAndInASet()
        {
            var task = Run("seen = set()\nfor i in range(3):\n    seen.add((0, 1))\nd = {(1, 2): 'x'}\nhit = d[(1, 2)]\nn = len(seen)\n");
            Assert.That(task.Globals["n"].AsInt(), Is.EqualTo(1));
            Assert.That(task.Globals["hit"].AsString(), Is.EqualTo("x"));
        }

        [Test]
        public void TheFingerprintSeparatesScriptsWithDifferentConstantTuples()
        {
            var vm = new PyVm();
            Assert.That(vm.Compile("x = (1, 2)").Fingerprint, Is.Not.EqualTo(vm.Compile("x = (1, 3)").Fingerprint));
        }

        [Test]
        public void UnpackingATupleOrListNeedsNoCopy()
        {
            var task = Run("t = (1, 2, 3)\na, b, c = t\nxs = [4, 5]\nd, e = xs\nxs.append(9)\n");
            Assert.That(new[] { "a", "b", "c", "d", "e" }.Select(n => task.Globals[n].AsInt()), Is.EqualTo(new long[] { 1, 2, 3, 4, 5 }));
        }

        [TestCase("a, b = (1, 2, 3)", "too many values to unpack (expected 2)")]
        [TestCase("a, b, c = (1, 2)", "not enough values to unpack (expected 3, got 2)")]
        [TestCase("a, b = [1]", "not enough values to unpack (expected 2, got 1)")]
        [TestCase("a, b = [1, 2, 3]", "too many values to unpack (expected 2)")]
        public void UnpackingTheWrongNumberKeepsItsMessages(string line, string message)
        {
            var vm = new PyVm();
            var task = vm.Start(line + "\n");
            task.RunUntilDone();
            Assert.That(task.Error.TypeName, Is.EqualTo("ValueError"));
            Assert.That(task.Error.Problem, Is.EqualTo(message));
        }

        [Test]
        public void MinAndMaxOfSeveralArgumentsKeepTheirRules()
        {
            var task = Run("a = min(3, 1, 2)\nb = max(3, 1, 2)\nc = min(2.5, 1)\nd = max('a', 'b')\ne = min(1, 1.0)\nf = max(1, True)\ng = min((1, 2), (1, 1))\nh = min(4, 7, key=lambda v: -v)\n");
            Assert.That(task.Globals["a"].AsInt(), Is.EqualTo(1));
            Assert.That(task.Globals["b"].AsInt(), Is.EqualTo(3));
            Assert.That(task.Globals["c"].AsInt(), Is.EqualTo(1));
            Assert.That(task.Globals["d"].AsString(), Is.EqualTo("b"));
            Assert.That(task.Globals["e"].Kind, Is.EqualTo(ValueKind.Int), "the first of equal values wins");
            Assert.That(task.Globals["f"].Kind, Is.EqualTo(ValueKind.Int), "the first of equal values wins");
            Assert.That(Ops.Repr(task.Globals["g"]), Is.EqualTo("(1, 1)"));
            Assert.That(task.Globals["h"].AsInt(), Is.EqualTo(7));
        }

        [Test]
        public void MinOfValuesThatCannotBeComparedStillRaises()
        {
            var vm = new PyVm();
            var task = vm.Start("min(1, 'a')\n");
            task.RunUntilDone();
            Assert.That(task.Error.TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void EnumerateInAForLoopGivesTheSamePairs()
        {
            var task = Run("out = []\nfor i, v in enumerate(['a', 'b', 'c'], 5):\n    out.append((i, v))\nfor i, v in enumerate([]):\n    out.append('never')\nfor i, (x, y) in enumerate([(1, 2), (3, 4)]):\n    out.append((i, x + y))\nfor p in enumerate('xy'):\n    out.append(p)\nfor a, b in enumerate(range(0)):\n    pass\nelse:\n    out.append('else')\n");
            Assert.That(Ops.Repr(task.Globals["out"]), Is.EqualTo("[(5, 'a'), (6, 'b'), (7, 'c'), (0, 3), (1, 7), (0, 'x'), (1, 'y'), 'else']"));
        }

        [Test]
        public void EnumerateOverAGeneratorInAForLoopStillWorks()
        {
            var task = Run("def gen():\n    yield 'p'\n    yield 'q'\nout = []\nfor i, v in enumerate(gen()):\n    out.append(str(i) + v)\n");
            Assert.That(Ops.Repr(task.Globals["out"]), Is.EqualTo("['0p', '1q']"));
        }

        [Test]
        public void SortedAndListSortKeepTheirResults()
        {
            var task = Run("xs = [5, 3, 1, 4, 2]\na = sorted(xs)\nb = sorted(xs, reverse=True)\nc = sorted([(2, 'b'), (1, 'z'), (2, 'a')], key=lambda p: p[0])\nd = sorted([])\ne = sorted([7])\nxs.sort()\nf = sorted('cba')\ng = sorted({3, 1, 2})\nh = sorted(x for x in [3, 1, 2])\nxs2 = [1]\nxs2.sort()\n");
            Assert.That(Ops.Repr(task.Globals["a"]), Is.EqualTo("[1, 2, 3, 4, 5]"));
            Assert.That(Ops.Repr(task.Globals["b"]), Is.EqualTo("[5, 4, 3, 2, 1]"));
            Assert.That(Ops.Repr(task.Globals["c"]), Is.EqualTo("[(1, 'z'), (2, 'b'), (2, 'a')]"));
            Assert.That(Ops.Repr(task.Globals["d"]), Is.EqualTo("[]"));
            Assert.That(Ops.Repr(task.Globals["e"]), Is.EqualTo("[7]"));
            Assert.That(Ops.Repr(task.Globals["xs"]), Is.EqualTo("[1, 2, 3, 4, 5]"));
            Assert.That(Ops.Repr(task.Globals["f"]), Is.EqualTo("['a', 'b', 'c']"));
            Assert.That(Ops.Repr(task.Globals["g"]), Is.EqualTo("[1, 2, 3]"));
            Assert.That(Ops.Repr(task.Globals["h"]), Is.EqualTo("[1, 2, 3]"));
        }

        [Test]
        public void SortedReturnsANewListThatCanBeChanged()
        {
            var task = Run("xs = [2, 1]\nys = sorted(xs)\nys.append(0)\nzs = sorted(ys)\nzs.append(-1)\nn = (len(xs), len(ys), len(zs))\n");
            Assert.That(Ops.Repr(task.Globals["n"]), Is.EqualTo("(2, 3, 4)"));
        }

        [Test]
        public void ASortKeyThatChangesTheListRaisesValueError()
        {
            var vm = new PyVm();
            var task = vm.Start("xs = [3, 1, 2]\ndef key(v):\n    xs.clear()\n    return v\nxs.sort(key=key)\n");
            task.RunUntilDone();
            Assert.That(task.Error.TypeName, Is.EqualTo("ValueError"));
            Assert.That(task.Error.Problem, Is.EqualTo("list modified during sort"));
            var kept = Run("xs = [3, 1, 2]\ndef key(v):\n    return -v\nxs.sort(key=key)\n");
            Assert.That(Ops.Repr(kept.Globals["xs"]), Is.EqualTo("[3, 2, 1]"));
        }
    }
}
