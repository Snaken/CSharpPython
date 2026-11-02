using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharpPython.Tests.Vm
{
    // A short list with ints, floats or strings for keys is sorted in place by an insertion sort; it has to agree with a
    // stable sort in every case, and leave everything else to the general path.
    public class SortSmallTests
    {
        private static Value Make(Random random, int kind)
        {
            int n = random.Next(0, 6);
            switch (kind)
            {
                case 0: return Value.FromInt(n);
                case 1: return Value.FromFloat(n / 2.0);
                default: return Value.FromString("s" + n);
            }
        }

        [Test]
        public void RandomShortListsAgreeWithAStableSort()
        {
            var random = new Random(4242);
            for (int round = 0; round < 3000; round++)
            {
                int kind = random.Next(3), size = random.Next(0, 17);
                bool reverse = random.Next(2) == 0, withKeys = random.Next(2) == 0;
                var items = new List<Value>();
                var keys = new Value[size];
                for (int i = 0; i < size; i++)
                {
                    items.Add(Value.FromInt(i)); // the identity of each item, to check that equal keys keep their order
                    keys[i] = Make(random, kind);
                }
                var sourceKeys = withKeys ? keys : items.ToArray();
                if (!withKeys) for (int i = 0; i < size; i++) { items[i] = Make(random, kind); sourceKeys[i] = items[i]; }

                var ordering = Enumerable.Range(0, size).ToList();
                Func<int, IComparable> keyOf = i => kind == 0 ? (IComparable)sourceKeys[i].AsInt() : kind == 1 ? sourceKeys[i].AsFloat() : sourceKeys[i].AsString();
                var expected = (reverse
                    ? ordering.OrderByDescending(keyOf, kind == 2 ? (IComparer<IComparable>)new OrdinalComparer() : Comparer<IComparable>.Default)
                    : ordering.OrderBy(keyOf, kind == 2 ? (IComparer<IComparable>)new OrdinalComparer() : Comparer<IComparable>.Default))
                    .Select(i => items[i]).ToList();

                var actual = Methods.SortValues(items, reverse, withKeys ? (Value[])keys.Clone() : null);
                Assert.That(actual.Select(v => v.ToString()), Is.EqualTo(expected.Select(v => v.ToString())), $"round {round}");
            }
        }

        private sealed class OrdinalComparer : IComparer<IComparable>
        {
            public int Compare(IComparable x, IComparable y) => string.CompareOrdinal((string)x, (string)y);
        }

        [Test]
        public void MixedKeysAndNanAreLeftToTheGeneralPath()
        {
            var vm = new PyVm();
            var task = vm.Start(
                "a = sorted([3, 1.5, 2, True])\n" +
                "b = sorted([2.0, float('nan'), 1.0])\n" +
                "try:\n    sorted([1, 'a'])\n    c = 'no error'\nexcept TypeError:\n    c = 'TypeError'\n" +
                "d = sorted(['b', 'a', 'B'], reverse=True)\n" +
                "e = sorted([3, 1, 2], key=lambda v: -v)\n" +
                "f = sorted(['bb', 'a', 'ccc', 'dd'], key=len, reverse=True)\n");
            task.RunUntilDone();
            Assert.That(task.Error, Is.Null);
            Assert.That(task.Globals["a"].AsList().Items.Select(v => Ops.Str(v)), Is.EqualTo(new[] { "True", "1.5", "2", "3" }));
            Assert.That(task.Globals["c"].AsString(), Is.EqualTo("TypeError"));
            Assert.That(task.Globals["d"].AsList().Items.Select(v => v.AsString()), Is.EqualTo(new[] { "b", "a", "B" }));
            Assert.That(task.Globals["e"].AsList().Items.Select(v => v.AsInt()), Is.EqualTo(new long[] { 3, 2, 1 }));
            Assert.That(task.Globals["f"].AsList().Items.Select(v => v.AsString()), Is.EqualTo(new[] { "ccc", "bb", "dd", "a" }), "equal keys keep their order under reverse=True");
        }

        [Test]
        public void SortingARangeMakesTheRightListAndRespectsTheLengthLimit()
        {
            var vm = new PyVm();
            var task = vm.Start(
                "a = sorted(range(5, 0, -2))\nb = sorted(range(0))\nc = sorted(range(3), reverse=True)\n" +
                "try:\n    sorted(range(100001))\n    d = 'no error'\nexcept MemoryError:\n    d = 'MemoryError'\n" +
                "e = len(sorted(range(100000)))\n");
            task.RunUntilDone();
            Assert.That(task.Error, Is.Null);
            Assert.That(task.Globals["a"].AsList().Items.Select(v => v.AsInt()), Is.EqualTo(new long[] { 1, 3, 5 }));
            Assert.That(task.Globals["b"].AsList().Items.Count, Is.EqualTo(0));
            Assert.That(task.Globals["c"].AsList().Items.Select(v => v.AsInt()), Is.EqualTo(new long[] { 2, 1, 0 }));
            Assert.That(task.Globals["d"].AsString(), Is.EqualTo("MemoryError"));
            Assert.That(task.Globals["e"].AsInt(), Is.EqualTo(100000));
        }
    }
}
