using System.Linq;

namespace CSharpPython.Tests.Vm
{
    // 'a, b = x, y' is compiled without building the tuple; it has to behave exactly as the tuple form does.
    public class PairedAssignTests
    {
        private static PyTask Run(string source)
        {
            var task = new PyVm().Start(source);
            task.RunUntilDone();
            return task;
        }

        private static long Int(PyTask task, string name) => task.Globals[name].AsInt();

        [Test]
        public void ASwapExchangesTheValues()
        {
            var task = Run("a, b = 1, 2\na, b = b, a\n");
            Assert.That(Int(task, "a"), Is.EqualTo(2));
            Assert.That(Int(task, "b"), Is.EqualTo(1));
        }

        [Test]
        public void ThreeNamesTakeTheirValuesInOrder()
        {
            var task = Run("a, b, c = 1, 2, 3\nx, y, z = c, a, b\n");
            Assert.That(new[] { Int(task, "x"), Int(task, "y"), Int(task, "z") }, Is.EqualTo(new long[] { 3, 1, 2 }));
            Assert.That(new[] { Int(task, "a"), Int(task, "b"), Int(task, "c") }, Is.EqualTo(new long[] { 1, 2, 3 }));
        }

        [Test]
        public void ValuesAreComputedLeftToRightAndTargetsStoredLeftToRight()
        {
            var task = Run(
                "log = []\n" +
                "def f(name, v):\n    log.append(name)\n    return v\n" +
                "p, q, r = f('p', 1), f('q', 2), f('r', 3)\n" +
                "i = 0\nL = [0, 0, 0]\ni, L[i] = 1, 9\n" +
                "a, a = 1, 2\n");
            Assert.That(task.Error, Is.Null);
            Assert.That(task.Globals["log"].AsList().Items.Select(v => v.AsString()), Is.EqualTo(new[] { "p", "q", "r" }));
            Assert.That(new[] { Int(task, "p"), Int(task, "q"), Int(task, "r") }, Is.EqualTo(new long[] { 1, 2, 3 }));
            Assert.That(task.Globals["L"].AsList().Items.Select(v => v.AsInt()), Is.EqualTo(new long[] { 0, 9, 0 }), "i is stored before L[i]");
            Assert.That(Int(task, "a"), Is.EqualTo(2), "the last store of a repeated name wins");
        }

        [Test]
        public void ListTargetsNestedTargetsAndAttributeAndIndexTargetsWork()
        {
            var task = Run(
                "[a, b] = 1, 2\n(c, d), e = (3, 4), 5\n" +
                "class O:\n    pass\no = O()\nk = {}\no.x, k['y'] = 6, 7\nox = o.x\nky = k['y']\n");
            Assert.That(task.Error, Is.Null);
            Assert.That(new[] { Int(task, "a"), Int(task, "b"), Int(task, "c"), Int(task, "d"), Int(task, "e") }, Is.EqualTo(new long[] { 1, 2, 3, 4, 5 }));
            Assert.That(Int(task, "ox"), Is.EqualTo(6));
            Assert.That(Int(task, "ky"), Is.EqualTo(7));
        }

        [Test]
        public void NoTargetIsStoredWhenAValueRaises()
        {
            var task = Run("a = 5\nb = 6\ntry:\n    a, b = 1, 1 / 0\nexcept ZeroDivisionError:\n    pass\n");
            Assert.That(Int(task, "a"), Is.EqualTo(5));
            Assert.That(Int(task, "b"), Is.EqualTo(6));
        }

        [TestCase("a, b = 1, 2, 3")]
        [TestCase("a, b, c = 1, 2")]
        public void AMismatchedCountStillRaisesValueError(string source)
        {
            var task = Run(source);
            Assert.That(task.Error?.TypeName, Is.EqualTo("ValueError"));
        }

        [Test]
        public void WorksInsideFunctionsWhereNamesAreLocals()
        {
            var task = Run("def f(x, y):\n    a, b = y, x\n    a, b, c = b, a, a + b\n    return a * 100 + b * 10 + c\nr = f(1, 2)\n");
            Assert.That(task.Error, Is.Null);
            Assert.That(Int(task, "r"), Is.EqualTo(1 * 100 + 2 * 10 + 3), "a, b = 2, 1 then a, b, c = 1, 2, 3");
        }
    }
}
