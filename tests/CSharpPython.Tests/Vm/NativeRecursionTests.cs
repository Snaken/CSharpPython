using System;
using System.Threading;

namespace CSharpPython.Tests.Vm
{
    /// <summary>
    /// Values and callables that nest without limit must end with a RecursionError the script can catch, never a stack
    /// overflow (which would end the host process). Every case runs on a thread with a small stack, so an unguarded
    /// recursion would crash the test run.
    /// </summary>
    public class NativeRecursionTests
    {
        private const int SmallStack = 512 * 1024;

        private static string Nest(string setup, string wrap, string use) =>
            setup + "\nfor _ in range(100000):\n    " + wrap + "\n" + use + "\n";

        public static readonly object[][] Hostile =
        {
            new object[] { Nest("a = ()", "a = (a,)", "hash(a)") },
            new object[] { Nest("a = ()", "a = (a,)", "d = {a: 1}") },
            new object[] { Nest("a = ()", "a = (a,)", "s = {a}") },
            new object[] { Nest("a = ()", "a = frozenset([a])", "hash(a)") },
            new object[] { Nest("x = [1]", "x = map(lambda v: v, x)", "list(x)") },
            new object[] { Nest("x = [1]", "x = zip(x)", "list(x)") },
            new object[] { Nest("x = [1]", "x = filter(None, x)", "list(x)") },
            new object[] { Nest("x = [1]", "x = enumerate(x)", "list(x)") },
            new object[] { "import itertools\n" + Nest("x = [1]", "x = itertools.chain(x, [])", "list(x)") },
            new object[] { "import itertools\n" + Nest("x = [1]", "x = itertools.islice(x, 5)", "list(x)") },
            new object[] { "import functools\n" + Nest("f = lambda: 1", "f = functools.partial(f)", "f()") },
            new object[] { "import functools\n" + Nest("f = lambda: 1", "f = functools.lru_cache()(f)", "f()") },
        };

        private static string RunOnSmallStack(string source)
        {
            string outcome = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var task = new PyVm().Start(source);
                    task.RunUntilDone();
                    outcome = task.Error == null ? "finished" : task.Error.TypeName;
                }
                catch (Exception e) { outcome = "escaped " + e.GetType().Name; }
            }, SmallStack);
            thread.Start();
            thread.Join();
            return outcome;
        }

        [TestCaseSource(nameof(Hostile))]
        public void DeepNestingRaisesARecursionError(string source) =>
            Assert.That(RunOnSmallStack(source), Is.EqualTo("RecursionError"));

        [Test]
        public void TheScriptCanCatchTheRecursionError()
        {
            string source = "import functools\nf = lambda: 1\nfor _ in range(100000):\n    f = functools.partial(f)\n" +
                "try:\n    f()\n    caught = False\nexcept RecursionError:\n    caught = True\nassert caught\n";
            Assert.That(RunOnSmallStack(source), Is.EqualTo("finished"));
        }

        [Test]
        public void ReasonableNestingStillWorks()
        {
            Assert.That(Py.Out("print(hash(((((1,),),),)) == hash(((((1,),),),)))"), Is.EqualTo("True"));
            Assert.That(Py.Out("print(list(filter(None, zip([1, 2], [3, 4]))))"), Is.EqualTo("[(1, 3), (2, 4)]"));
            Assert.That(Py.Out("import functools\nf = functools.partial(functools.partial(max), 3)\nprint(f(9))"), Is.EqualTo("9"));
        }

        [Test]
        public void SavingAValueNestedTooDeeplyIsASnapshotException()
        {
            Exception caught = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var vm = new PyVm(new VmOptions { EnableSnapshots = true });
                    var task = vm.Start("x = []\nfor _ in range(100000):\n    x = [x]\nwhile True:\n    pass\n");
                    for (int i = 0; i < 40; i++) task.Update();
                    task.Save();
                }
                catch (Exception e) { caught = e; }
            }, SmallStack);
            thread.Start();
            thread.Join();
            Assert.That(caught, Is.InstanceOf<SnapshotException>());
        }
    }
}
