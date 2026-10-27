using System.Collections.Generic;
using System.Linq;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Runtime
{
    public class ItertoolsTests
    {
        private static PyIterator Source() => new ListIterator(new PyList(new List<Value> { Value.FromInt(1) }));

        private static ItertoolsIterator Make(ItertoolsKind kind, int sources, int args, long[] state, params Value[] items) =>
            new ItertoolsIterator(kind, Enumerable.Range(0, sources).Select(_ => Source()).ToArray(), new Value[args], state, items.ToList());

        [TestCase("Count", 0, 2, 0, true)]
        [TestCase("Count", 1, 2, 0, false)]
        [TestCase("Cycle", 1, 0, 1, true)]
        [TestCase("Cycle", 0, 0, 1, false)]
        [TestCase("Repeat", 0, 1, 1, true)]
        [TestCase("Repeat", 0, 0, 1, false)]
        [TestCase("Chain", 1, 3, 1, true)]
        [TestCase("ChainFrom", 2, 0, 0, true)]
        [TestCase("ChainFrom", 1, 0, 0, false)]
        [TestCase("Islice", 1, 0, 4, true)]
        [TestCase("Islice", 1, 0, 3, false)]
        [TestCase("Accumulate", 1, 2, 1, true)]
        [TestCase("Compress", 2, 0, 0, true)]
        [TestCase("Compress", 1, 0, 0, false)]
        [TestCase("DropWhile", 1, 1, 1, true)]
        [TestCase("TakeWhile", 1, 1, 1, true)]
        [TestCase("TakeWhile", 1, 0, 1, false)]
        [TestCase("FilterFalse", 1, 1, 0, true)]
        [TestCase("StarMap", 1, 1, 0, true)]
        [TestCase("StarMap", 2, 1, 0, false)]
        [TestCase("ZipLongest", 2, 1, 2, false)]
        [TestCase("ZipLongest", 2, 1, 1, true)]
        [TestCase("Pairwise", 1, 1, 1, true)]
        [TestCase("Batched", 1, 0, 2, true)]
        [TestCase("Batched", 1, 0, 1, false)]
        [TestCase("GroupBy", 1, 4, 4, true)]
        [TestCase("GroupBy", 1, 4, 5, false)]
        [TestCase("Grouper", 1, 0, 1, false)] // its source is not a groupby
        [TestCase("Product", 1, 0, 3, false)]
        public void TheShapeOfEachKindIsChecked(string kindName, int sources, int args, int stateLength, bool expected)
        {
            var kind = (ItertoolsKind)System.Enum.Parse(typeof(ItertoolsKind), kindName);
            var state = new long[stateLength];
            if (kind == ItertoolsKind.Islice) state[2] = 1;
            if (kind == ItertoolsKind.Batched) state[0] = 1;
            if (kind == ItertoolsKind.ZipLongest) state[0] = 2;
            Assert.That(Make(kind, sources, args, state).IsConsistent(), Is.EqualTo(expected));
        }

        [Test]
        public void ADamagedStateIsNotConsistent()
        {
            Assert.That(Make(ItertoolsKind.Cycle, 1, 0, new long[] { 3 }).IsConsistent(), Is.False, "replay position past the items");
            Assert.That(Make(ItertoolsKind.Repeat, 0, 1, new long[] { -5 }).IsConsistent(), Is.False);
            Assert.That(Make(ItertoolsKind.Chain, 1, 1, new long[] { 2 }).IsConsistent(), Is.False);
            Assert.That(Make(ItertoolsKind.Islice, 1, 0, new long[] { 0, -2, 1, 0 }).IsConsistent(), Is.False);
            Assert.That(Make(ItertoolsKind.Accumulate, 1, 2, new long[] { 3 }).IsConsistent(), Is.False);
            Assert.That(Make(ItertoolsKind.Pairwise, 1, 1, new long[] { 3 }).IsConsistent(), Is.False);
            Assert.That(Make(ItertoolsKind.Batched, 1, 0, new long[] { 0, 0 }).IsConsistent(), Is.False);
            Assert.That(Make((ItertoolsKind)99, 0, 0, new long[0]).IsConsistent(), Is.False);
            // product: 2 pools of 1 item each, with a position outside the pool
            Assert.That(Make(ItertoolsKind.Product, 0, 0, new long[] { 2, 0, 0, 1, 1, 0, 5 }, Value.FromInt(1), Value.FromInt(2)).IsConsistent(), Is.False);
            Assert.That(Make(ItertoolsKind.Product, 0, 0, new long[] { 2, 0, 0, 1, 1, 0, 0 }, Value.FromInt(1), Value.FromInt(2)).IsConsistent(), Is.True);
            Assert.That(Make(ItertoolsKind.Product, 0, 0, new long[] { 2, 0, 0, 1, 1, 0, 0 }, Value.FromInt(1)).IsConsistent(), Is.False, "pools and items disagree");
            // permutations of 2 items taken 2 at a time: indices must be a permutation
            Assert.That(Make(ItertoolsKind.Permutations, 0, 0, new long[] { 2, 2, 0, 0, 0, 1, 2, 1 }, Value.FromInt(1), Value.FromInt(2)).IsConsistent(), Is.True);
            Assert.That(Make(ItertoolsKind.Permutations, 0, 0, new long[] { 2, 2, 0, 0, 0, 0, 2, 1 }, Value.FromInt(1), Value.FromInt(2)).IsConsistent(), Is.False);
            Assert.That(Make(ItertoolsKind.Permutations, 0, 0, new long[] { 2, 2, 0, 0, 0, 1, 9, 1 }, Value.FromInt(1), Value.FromInt(2)).IsConsistent(), Is.False);
            Assert.That(Make(ItertoolsKind.Combinations, 0, 0, new long[] { 2, 1, 0, 0, 7 }, Value.FromInt(1), Value.FromInt(2)).IsConsistent(), Is.False);
            Assert.That(Make(ItertoolsKind.Combinations, 0, 0, new long[] { 2, 1, 0, 0, 1 }, Value.FromInt(1), Value.FromInt(2)).IsConsistent(), Is.True);
            Assert.That(Make(ItertoolsKind.CombinationsWithReplacement, 0, 0, new long[] { 0, 1, 0, 0, 3 }).IsConsistent(), Is.False);
        }

        [Test]
        public void CallbacksNeedARunningScript()
        {
            var pairs = new ItertoolsIterator(ItertoolsKind.TakeWhile, new[] { Source() }, new[] { Value.None }, new long[1]);
            Assert.That(Assert.Throws<PyError>(() => pairs.Next(out _)).TypeName, Is.EqualTo("RuntimeError"));
        }

        [Test]
        public void NamesAndReprs()
        {
            Assert.That(Out("import itertools\nprint(repr(itertools.chain([1]))[:26], type(itertools.groupby([])).__name__)"), Does.StartWith("<itertools.chain object at"));
            Assert.That(Out("import itertools\nfor g in (itertools.chain.from_iterable([[1]]), itertools.dropwhile(bool, []), itertools.takewhile(bool, []), itertools.filterfalse(bool, []), itertools.starmap(max, []), itertools.zip_longest(), itertools.combinations_with_replacement('a', 1)):\n    print(type(g).__name__)"),
                Is.EqualTo("chain\ndropwhile\ntakewhile\nfilterfalse\nstarmap\nzip_longest\ncombinations_with_replacement"));
            Assert.That(Out("import itertools\nk, g = next(itertools.groupby('a'))\nprint(type(g).__name__)"), Is.EqualTo("_grouper"));
        }

        [Test]
        public void WalkingAnEndlessIteratorIsLimited()
        {
            var error = Fails("import itertools\nprint(sum(itertools.islice(itertools.count(), 10 ** 12)))", null, new VmOptions { MaxSteps = 200000 });
            Assert.That(error.TypeName, Is.EqualTo("StepLimitExceeded"));
            error = Fails("import itertools\nprint(list(itertools.count()))");
            Assert.That(error.TypeName, Is.EqualTo("MemoryError"));
        }

        [Test]
        public void AFloatCountKeepsAddingTheStep()
        {
            Assert.That(Out("import itertools\nprint(list(itertools.islice(itertools.count(0.1, 0.2), 3)), list(itertools.islice(itertools.count(2 ** 62, 2 ** 62), 3)))"),
                Is.EqualTo("[0.1, 0.30000000000000004, 0.5] [4611686018427387904, 9223372036854775808, 13835058055282163712]"));
        }

        [Test]
        public void IteratorsCanBeCopiedByTheScript()
        {
            Assert.That(Out("import itertools\nit = itertools.cycle([1, 2])\nxs = [next(it) for _ in range(5)]\nprint(xs, next(it))"), Is.EqualTo("[1, 2, 1, 2, 1] 2"));
        }
    }
}
