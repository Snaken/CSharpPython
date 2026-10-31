using System.Diagnostics;

namespace CSharpPython.Tests.Vm
{
    /// <summary>
    /// A value can hold the same child twice at every level, so about forty objects have 2^40 paths. Hashing, comparing or
    /// printing such a value must end in an error, not walk every path.
    /// </summary>
    public class SharedStructureTests
    {
        private const string Tuples = "a = (0,)\nfor i in range(40): a = (a, a)\n";
        private const string TwoTuples = "a = (0,); b = (0,)\nfor i in range(40):\n    a = (a, a)\n    b = (b, b)\n";
        private const string TwoLists = "a = [0]; b = [0]\nfor i in range(40):\n    a = [a, a]\n    b = [b, b]\n";

        private static string Outcome(string source)
        {
            var clock = Stopwatch.StartNew();
            var task = new PyVm().Start(source);
            task.RunUntilDone();
            Assert.That(clock.ElapsedMilliseconds, Is.LessThan(5000), "it must stop quickly: " + source);
            return task.Error == null ? "finished" : task.Error.TypeName;
        }

        [TestCase(Tuples + "hash(a)")]
        [TestCase(Tuples + "d = {a: 1}")]
        [TestCase(Tuples + "s = {a, a}")]
        [TestCase(Tuples + "s = frozenset([a])\nfor i in range(40): s = frozenset([(s, s)])\nhash(s)")]
        [TestCase(TwoTuples + "a == b")]
        [TestCase(TwoLists + "a == b")]
        [TestCase(TwoLists + "a < b")]
        [TestCase(TwoLists + "a != b")]
        [TestCase("a = [0]\nfor i in range(40): a = [a, a]\nstr(a)")]
        [TestCase("a = (0,)\nfor i in range(40): a = (a, a)\nrepr(a)")]
        [TestCase("a = {0: 0}\nfor i in range(40): a = {1: a, 2: a}\nstr(a)")]
        public void SharedStructureEndsInAnError(string source)
        {
            Assert.That(Outcome(source), Is.AnyOf("RecursionError", "MemoryError"), source);
        }

        [Test]
        public void TheErrorCanBeCaught()
        {
            Assert.That(Py.Out(Tuples + "try:\n    hash(a)\nexcept RecursionError:\n    print('caught')"), Is.EqualTo("caught"));
        }

        [Test]
        public void AnIdenticalValueIsStillQuicklyEqual()
        {
            Assert.That(Py.Out(TwoLists.Replace("b = [0]", "b = [0]") + "print(a == a, sorted([a, a]) is not None)"), Is.EqualTo("True True"));
        }

        [Test]
        public void ALargeButHonestValueStillWorks()
        {
            string script =
                "rows = [(i, str(i), [i, i + 1]) for i in range(20000)]\n" +
                "copy = [(i, str(i), [i, i + 1]) for i in range(20000)]\n" +
                "print(rows == copy, rows < copy, len(str(rows)) > 100000)\n" +
                "d = {(i, (i, i)): i for i in range(20000)}\n" +
                "print(d[(5, (5, 5))], hash(((1, 2), (3, (4, 5)))) == hash(((1, 2), (3, (4, 5)))))";
            Assert.That(Py.Out(script), Is.EqualTo("True False True\n5 True"));
        }

        // ---- str.replace with a count ----

        [Test]
        public void ReplaceWithACountCannotGrowPastTheLimit()
        {
            Assert.That(Outcome("s = 'a' * 100000\nr = s.replace('a', 'b' * 2000, 1000)"), Is.EqualTo("MemoryError"));
            Assert.That(Outcome("s = 'a' * 90000\nr = s.replace('a', 'bb', 20000)"), Is.EqualTo("MemoryError"));
        }

        [Test]
        public void ReplaceWithACountStillWorksWithinTheLimit()
        {
            Assert.That(Py.Out("print('a-b-c-d'.replace('-', '+', 2), 'aaa'.replace('a', 'bb', 2), 'abc'.replace('', '-', 2))"),
                Is.EqualTo("a+b+c-d bbbba -a-bc"));
        }
    }
}
