using System.Threading;

namespace CSharpPython.Tests.Vm
{
    /// <summary>
    /// .NET's sort wraps what its comparison throws in an InvalidOperationException. An error raised by a script's
    /// __lt__ or key function has to reach the script as itself, not as a HostError.
    /// </summary>
    public class SortErrorTests
    {
        [Test]
        public void ARecursionErrorInsideSortedCanBeCaught()
        {
            const string source =
                "class A:\n    def __lt__(self, other):\n        return sorted([A(), A()]) is None\n" +
                "try:\n    sorted([A(), A()])\nexcept RecursionError:\n    print('caught recursion')\n";
            Assert.That(Py.Out(source), Is.EqualTo("caught recursion"));
        }

        [TestCase("sorted([A(), A()])")]
        [TestCase("l = [A(), A()]\nl.sort()")]
        [TestCase("sorted([1, 2, 3], key=boom)")]
        [TestCase("[3, 2, 1].sort(key=boom)")]
        [TestCase("min([A(), A()])")]
        [TestCase("max([A(), A()], key=boom)")]
        public void AnErrorFromAComparisonOrKeyReachesTheScriptAsItself(string statement)
        {
            string source =
                "class A:\n    def __lt__(self, other):\n        raise KeyError('lt')\n" +
                "def boom(v):\n    raise KeyError('key')\n" +
                "try:\n" + Indent(statement) + "\nexcept KeyError as e:\n    print('KeyError', e.args[0])\n";
            string output = Py.Out(source);
            Assert.That(output, Is.EqualTo("KeyError lt").Or.EqualTo("KeyError key"), statement);
        }

        [Test]
        public void ARuntimeErrorFromAComparisonIsNotAHostError()
        {
            var error = Py.Fails("class A:\n    def __lt__(self, other):\n        return 1 / 0\nsorted([A(), A()])");
            Assert.That(error.TypeName, Is.EqualTo("ZeroDivisionError"));
        }

        [Test]
        public void TheStepLimitStillStopsASortThatNeverEnds()
        {
            const string source = "class A:\n    def __lt__(self, other):\n        while True:\n            pass\nsorted([A(), A()])";
            var vm = new PyVm(new VmOptions { MaxSteps = 100000 });
            var task = vm.Start(source);
            task.RunUntilDone();
            Assert.That(task.Error.TypeName, Is.EqualTo("StepLimitExceeded"));
        }

        [Test]
        public void DeeplyNestedSortsOnASmallStackEndInARecursionError()
        {
            string outcome = null;
            var thread = new Thread(() =>
            {
                var task = new PyVm().Start("class A:\n    def __lt__(self, other):\n        return sorted([A(), A()]) is None\nsorted([A(), A()])");
                task.RunUntilDone();
                outcome = task.Error?.TypeName ?? "finished";
            }, 512 * 1024);
            thread.Start();
            thread.Join();
            Assert.That(outcome, Is.EqualTo("RecursionError"));
        }

        // Objects with __lt__ cannot use the fast paths for ints, floats and strings, so they go through the merge sort.
        private const string Keyed =
            "class K:\n    def __init__(self, v, i):\n        self.v = v\n        self.i = i\n    def __lt__(self, other):\n        return self.v < other.v\n" +
            "import random\nrandom.seed(7)\nitems = [K(random.randrange(20), i) for i in range(2000)]\n";

        [Test]
        public void TheSortOrdersItemsAndKeepsEqualOnesInOrder()
        {
            string script = Keyed +
                "out = sorted(items)\nok = len(out) == 2000\n" +
                "for a, b in zip(out, out[1:]):\n    ok = ok and (a.v < b.v or (a.v == b.v and a.i < b.i))\nprint(ok)";
            Assert.That(Py.Out(script), Is.EqualTo("True"));
        }

        [Test]
        public void ReverseKeepsEqualItemsInTheirOriginalOrder()
        {
            string script = Keyed +
                "out = sorted(items, reverse=True)\nok = len(out) == 2000\n" +
                "for a, b in zip(out, out[1:]):\n    ok = ok and (a.v > b.v or (a.v == b.v and a.i < b.i))\nprint(ok)";
            Assert.That(Py.Out(script), Is.EqualTo("True"));
        }

        [Test]
        public void SortingByAKeyFunctionAndListSortWork()
        {
            string script = Keyed +
                "a = sorted(items, key=lambda k: (k.v % 3, k.i))\nb = list(items)\nb.sort(key=lambda k: (k.v % 3, k.i))\n" +
                "print(all(x is y for x, y in zip(a, b)), a[0].v % 3, a[-1].v % 3)\n" +
                "c = [K(1, 0), K(0, 1), K(1, 2), K(0, 3)]\nc.sort()\nprint([k.i for k in c])\n" +
                "d = [K(1, 0), K(0, 1), K(1, 2), K(0, 3)]\nd.sort(reverse=True)\nprint([k.i for k in d])";
            Assert.That(Py.Out(script), Is.EqualTo("True 0 2\n[1, 3, 0, 2]\n[0, 2, 1, 3]"));
        }

        [Test]
        public void SortingNeedsOnlyLessThan()
        {
            // Python calls __lt__ and nothing else, so a class with only __lt__ sorts.
            Assert.That(Py.Out(Keyed + "print(len(sorted(items)), sorted([K(2, 0), K(1, 1)])[0].v)"), Is.EqualTo("2000 1"));
        }

        [Test]
        public void ListsOfDifferentTypesStillRaiseATypeError()
        {
            var error = Py.Fails("sorted([1, 'a', 2.5, [3]])");
            Assert.That(error.TypeName, Is.EqualTo("TypeError"));
            Assert.That(error.Problem, Does.Contain("not supported between instances"));
        }

        private static string Indent(string text) => "    " + text.Replace("\n", "\n    ");
    }
}
