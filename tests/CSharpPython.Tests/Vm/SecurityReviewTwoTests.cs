using System.Linq;
using System.Threading;

namespace CSharpPython.Tests.Vm
{
    public class SecurityReviewTwoTests
    {
        [Test]
        public void IsqrtWorksOnHugeIntegers()
        {
            Assert.That(Py.Out("import math\nn = 10 ** 4000\nr = math.isqrt(n)\nprint(r == 10 ** 2000, math.isqrt(n + 1) == r, math.isqrt(n - 1) == r - 1)"), Is.EqualTo("True True True"));
            Assert.That(Py.Out("import math\nprint(math.isqrt(0), math.isqrt(1), math.isqrt(15), math.isqrt(16), math.isqrt(2 ** 64))"), Is.EqualTo("0 1 3 4 4294967296"));
        }

        [TestCase("['k' + str(i) for i in range(3000)]")]
        [TestCase("[chr(65 + i % 26) * 5 + chr(65 + i // 26 % 26) + str(i) for i in range(3000)]")]
        [TestCase("['Aa' * 12 + str(i) for i in range(3000)]")]
        public void StringHashesAreSpread(string keys)
        {
            Assert.That(int.Parse(Py.Out("keys = " + keys + "\nprint(len(set(hash(k) for k in keys)))")), Is.GreaterThan(2990));
        }

        [Test]
        public void EqualStringsHashAlikeAndDictsWork()
        {
            Assert.That(Py.Out("a = 'hel'\nb = a + 'lo'\nd = {'hello': 1}\nprint(hash(b) == hash('hello'), d[b], hash('') == hash(''))"), Is.EqualTo("True 1 True"));
        }

        [Test]
        public void TheExceptionRegistryIsCapped()
        {
            int count = ExceptionTypes.All.Count();
            Assert.That(ExceptionTypes.GetOrCreate("OneMoreName", count), Is.SameAs(ExceptionTypes.Exception));
            Assert.That(ExceptionTypes.TryGet("OneMoreName", out _), Is.False);
            Assert.That(ExceptionTypes.GetOrCreate("ValueError", count), Is.SameAs(ExceptionTypes.ValueError));
            Assert.That(ExceptionTypes.GetOrCreate("OneMoreName", count + 1).Name, Is.EqualTo("OneMoreName"));
        }

        [Test]
        public void VmsOnDifferentThreadsKeepTheirOwnLimits()
        {
            string[] outcomes = new string[2];
            var threads = new[] { 100, 5000 }.Select((limit, n) => new Thread(() =>
            {
                for (int round = 0; round < 50; round++)
                {
                    var task = new PyVm(new VmOptions { MaxSequenceLength = limit }).Start("l = [0] * 1000");
                    task.RunUntilDone();
                    outcomes[n] = task.Error == null ? "finished" : task.Error.TypeName;
                }
            })).ToList();
            threads.ForEach(t => t.Start());
            threads.ForEach(t => t.Join());
            Assert.That(outcomes, Is.EqualTo(new[] { "MemoryError", "finished" }));
        }
    }
}
