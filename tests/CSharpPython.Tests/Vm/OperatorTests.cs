using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class OperatorTests
    {
        private static string Message(string source) => Fails(source).Problem;

        [Test]
        public void ComparisonsReturnWhateverTheMethodReturns()
        {
            Assert.That(Out("class A:\n    def __eq__(self, o):\n        return [1]\n    def __lt__(self, o):\n        return 'lt'\nprint(A() == 1, A() < 1)"), Is.EqualTo("[1] lt"));
        }

        [Test]
        public void ChainedComparisonsUseTheTruthOfEachResult()
        {
            Assert.That(Out("class V:\n    def __init__(self, n):\n        self.n = n\n    def __lt__(self, o):\n        return self.n < o.n\nprint(V(1) < V(2) < V(3), V(1) < V(3) < V(2))"), Is.EqualTo("True False"));
        }

        [Test]
        public void RunawayEqualityHitsTheCallLimit()
        {
            Assert.That(Fails("class A:\n    def __eq__(self, o):\n        return self == o\nA() == A()").TypeName, Is.EqualTo("RecursionError"));
            Assert.That(Fails("class A:\n    def __len__(self):\n        return len(self)\nlen(A())").TypeName, Is.EqualTo("RecursionError"));
        }

        [Test]
        public void SpecialMethodsCannotWait()
        {
            var vm = new PyVm { Output = s => { } };
            vm.RegisterModule("game", m => m.Native("wait", (ctx, a) => { ctx.Wait(1); return Value.None; }, 1, 1));
            var task = vm.Start("import game\nclass A:\n    def __eq__(self, o):\n        game.wait(1)\n        return True\nA() == A()");
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Error));
            Assert.That(task.Error.Problem, Does.Contain("cannot wait"));
        }

        [Test]
        public void HashErrors()
        {
            Assert.That(Message("class A:\n    def __eq__(self, o):\n        return True\nd = {}\nd[A()] = 1"), Does.Contain("unhashable type: 'A'"));
            Assert.That(Message("class A:\n    def __hash__(self):\n        return 1.5\nhash(A())"), Is.EqualTo("__hash__ method should return an integer"));
            Assert.That(Message("hash([])"), Is.EqualTo("unhashable type: 'list'"));
        }

        [Test]
        public void EqualityDecidesWhichDictKeysAreTheSame()
        {
            const string source =
                "class K:\n    def __init__(self, n):\n        self.n = n\n    def __eq__(self, o):\n        return self.n == o.n\n    def __hash__(self):\n        return 0\n" +
                "d = {}\nfor n in (1, 2, 1, 3, 2):\n    d[K(n)] = n * 10\nprint(len(d), sorted(d.values()), d[K(2)])";
            Assert.That(Out(source), Is.EqualTo("3 [10, 20, 30] 20"));
        }

        [Test]
        public void TruthinessAndLengthErrors()
        {
            Assert.That(Message("class A:\n    def __bool__(self):\n        return None\nbool(A())"), Is.EqualTo("__bool__ should return bool, returned NoneType"));
            Assert.That(Message("class A:\n    pass\nlen(A())"), Is.EqualTo("object of type 'A' has no len()"));
            Assert.That(Message("class A:\n    def __len__(self):\n        return 1.5\nlen(A())"), Is.EqualTo("'float' object cannot be interpreted as an integer"));
            Assert.That(Message("class A:\n    def __len__(self):\n        return -2\nbool(A())"), Is.EqualTo("__len__() should return >= 0"));
        }

        [Test]
        public void SortingUsesLessThan()
        {
            Assert.That(Out("class V:\n    def __init__(self, n):\n        self.n = n\n    def __lt__(self, o):\n        return self.n < o.n\nprint([v.n for v in sorted([V(3), V(1), V(2)])], max([V(1), V(5)]).n if False else 'ok')"), Is.EqualTo("[1, 2, 3] ok"));
            Assert.That(Message("class V:\n    pass\nsorted([V(), V()])"), Is.EqualTo("'<' not supported between instances of 'V' and 'V'"));
        }

        [Test]
        public void NotImplementedIsAValueOfItsOwnType()
        {
            Assert.That(Out("x = NotImplemented\nprint(x is NotImplemented, x == NotImplemented, bool(x), type(x).__name__, isinstance(x, type(NotImplemented)))"),
                Is.EqualTo("True True True NotImplementedType True"));
        }

        [Test]
        public void HostOutsideAScriptSeesIdentityEquality()
        {
            var vm = new PyVm { Output = s => { } };
            var task = vm.Start("class A:\n    def __eq__(self, o):\n        return True\na = A()\nb = A()");
            task.RunUntilDone();
            Assert.That(task.Globals["a"].Equals(task.Globals["a"]), Is.True);
            Assert.That(task.Globals["a"].Equals(task.Globals["b"]), Is.False);
        }

        [Test]
        public void RestoredDictionaryOfScriptKeysIsIndexedWhenTheScriptRuns()
        {
            const string source =
                "class K:\n    def __init__(self, n):\n        self.n = n\n    def __eq__(self, o):\n        return self.n == o.n\n    def __hash__(self):\n        return self.n\n" +
                "d = {K(1): 'a', K(2): 'b'}\nhold = 1\nprint(d[K(2)], len(d))";
            var sb = new System.Text.StringBuilder();
            var vm = new PyVm(new VmOptions { EnableSnapshots = true, StepsPerSlice = 1 }) { Output = s => sb.Append(s) };
            var script = vm.Compile(source);
            var task = vm.Start(script);
            // Stop after the dictionary exists but before the lookup.
            while (task.State != TaskState.Finished && !task.Globals.ContainsKey("hold")) task.Run();
            var restored = vm.Restore(script, task.Save());

            // Without a running script there is nothing to run __hash__ with, but the entries can still be counted and read.
            var dict = restored.Globals["d"].AsDict();
            Assert.That(dict.Count, Is.EqualTo(2));
            Assert.Throws<System.InvalidOperationException>(() => dict.ContainsKey(Value.FromInt(1)));

            Assert.That(restored.RunUntilDone(), Is.EqualTo(TaskState.Finished));
            Assert.That(sb.ToString(), Is.EqualTo("b 2\n"));
        }
    }
}
