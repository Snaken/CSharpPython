namespace CSharpPython.Tests.Vm
{
    /// <summary>
    /// A script must not be able to pick keys that all share one hash, which would make every dict or set insert walk the
    /// same chain. These check that the hashes of such keys are spread, and that hashing is still consistent.
    /// </summary>
    public class HashFloodingTests
    {
        // Each script builds 2,000 keys of a kind that used to collide, and prints how many different hashes they have.
        [TestCase("[(a << 32) | a for a in range(1, 2001)]")]
        [TestCase("[(a << 40) + a * 7 for a in range(1, 2001)]")]
        [TestCase("[(a << 70) | a for a in range(1, 2001)]")]
        [TestCase("[-((a << 32) | a) for a in range(1, 2001)]")]
        [TestCase("[(k, 31 * (100000 - k)) for k in range(2000)]")]
        [TestCase("[(k, 0, 31 * (100000 - k)) for k in range(2000)]")]
        [TestCase("[((a << 32) | a, 1) for a in range(1, 2001)]")]
        [TestCase("[frozenset([k, 100000 - k]) for k in range(1, 2001)]")]
        [TestCase("[slice(k, 31 * (100000 - k)) for k in range(2000)]")]
        [TestCase("[range(k, 0, 1) if False else range(k, 100000 - 31 * k) for k in range(2000)]")]
        [TestCase("[float((1 << 60) + a * (1 << 12)) / 3 for a in range(1, 2001)]")]
        public void CraftedKeysHaveSpreadHashes(string keys)
        {
            string output = Py.Out("keys = " + keys + "\nprint(len(set(hash(k) for k in keys)))");
            Assert.That(int.Parse(output), Is.GreaterThan(1990), keys);
        }

        [Test]
        public void SmallIntegersAreTheirOwnHash()
        {
            Assert.That(Py.Out("print(hash(5), hash(0), hash(-5), hash(123456789), hash(2147483647), hash(-2147483648))"),
                Is.EqualTo("5 0 -5 123456789 2147483647 -2147483648"));
        }

        [Test]
        public void EqualValuesHashAlike()
        {
            string script =
                "print(hash(1) == hash(1.0) == hash(True), hash(0) == hash(0.0) == hash(-0.0) == hash(False))\n" +
                "print(hash(2 ** 40) == hash(2.0 ** 40), hash(2 ** 62) == hash(float(2 ** 62)), hash(2 ** 100) == hash(float(2 ** 100)))\n" +
                "print(hash(-(2 ** 70)) == hash(-float(2 ** 70)), hash((1, 2.0, True)) == hash((1.0, 2, 1)))\n" +
                "print(hash(frozenset([1, 2, 3])) == hash(frozenset([3, 2, 1.0])), hash(slice(1, 2, 3)) == hash(slice(1.0, 2, True + 2)))\n" +
                "print(hash(range(3, 10, 2)) == hash(range(3, 10, 2)), hash(2 ** 100) == hash(2 ** 100))\n";
            Assert.That(Py.Out(script), Is.EqualTo("True True\nTrue True True\nTrue True\nTrue True\nTrue True"));
        }

        [Test]
        public void KeysOfDifferentNumberTypesFindEachOther()
        {
            string script =
                "d = {2 ** 40: 'a', 2 ** 100: 'b', 1.5: 'c', (1, 2 ** 40): 'd', frozenset([2 ** 40]): 'e'}\n" +
                "print(d[2.0 ** 40], d[float(2 ** 100)], d[3 / 2], d[(1.0, 2.0 ** 40)], d[frozenset([2.0 ** 40])])\n" +
                "s = {1, 2 ** 33}\nprint(1.0 in s, float(2 ** 33) in s, True in s)\n";
            Assert.That(Py.Out(script), Is.EqualTo("a b c d e\nTrue True True"));
        }

        [Test]
        public void CollidingKeysStillWorkAsDictKeys()
        {
            string script = "d = {}\nfor a in range(1, 3001):\n    d[(a << 32) | a] = a\n" +
                "print(len(d), d[(7 << 32) | 7], sum(d.values()))";
            Assert.That(Py.Out(script), Is.EqualTo("3000 7 4501500"));
        }
    }
}
