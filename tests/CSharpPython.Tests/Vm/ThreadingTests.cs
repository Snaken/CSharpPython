using System;
using System.Linq;
using System.Text;
using System.Threading;

namespace CSharpPython.Tests.Vm
{
    // Separate PyVm instances share no mutable state, so each can run on its own thread at the same time and gives
    // the result it gives alone. (A compiled script is not shared between threads: its call-site caches are not locked.)
    public class ThreadingTests
    {
        private const string Script =
            "import random\n" +
            "random.seed(7)\n" +
            "class Shape:\n" +
            "    scale = 1\n" +
            "    def __init__(self, n):\n" +
            "        self.n = n\n" +
            "    def area(self):\n" +
            "        return self.n * self.n * Shape.scale\n" +
            "class Square(Shape):\n" +
            "    def area(self):\n" +
            "        return Shape.area(self) + 1\n" +
            "def gen(k):\n" +
            "    for i in range(k):\n" +
            "        yield i * 2\n" +
            "total = 0\n" +
            "words = {}\n" +
            "for i in range(3000):\n" +
            "    Shape.scale = i % 5 + 1\n" +
            "    s = Square(i % 17) if i % 2 else Shape(i % 13)\n" +
            "    total += s.area() + sum(gen(i % 6))\n" +
            "    w = 'k' + str(i % 40)\n" +
            "    words[w] = words.get(w, 0) + random.randint(1, 9)\n" +
            "    try:\n" +
            "        total += 1 // (i % 3)\n" +
            "    except ZeroDivisionError:\n" +
            "        total += 1\n" +
            "print(total, len(words), sum(words.values()), sorted(words.items())[:3])\n";

        private static string RunOnce()
        {
            var sb = new StringBuilder();
            var vm = new PyVm { Output = s => sb.Append(s) };
            var task = vm.Start(Script);
            while (task.State == TaskState.Running) task.Update();
            Assert.That(task.State, Is.EqualTo(TaskState.Finished), task.Error?.ToString());
            return sb.ToString();
        }

        [Test]
        public void VmsOnSeparateThreadsEachGiveTheResultTheyGiveAlone()
        {
            string expected = RunOnce();
            const int threads = 8;
            var results = new string[threads];
            var errors = new Exception[threads];
            using (var start = new ManualResetEventSlim())
            {
                var workers = Enumerable.Range(0, threads).Select(t => new Thread(() =>
                {
                    try
                    {
                        start.Wait();
                        string last = null;
                        for (int round = 0; round < 5; round++) last = RunOnce();
                        results[t] = last;
                    }
                    catch (Exception e) { errors[t] = e; }
                })).ToArray();
                foreach (var w in workers) w.Start();
                start.Set();
                foreach (var w in workers) w.Join();
            }
            for (int t = 0; t < threads; t++)
            {
                Assert.That(errors[t], Is.Null, errors[t]?.ToString());
                Assert.That(results[t], Is.EqualTo(expected), "thread " + t);
            }
        }
    }
}
