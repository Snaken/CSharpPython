using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class ContainerDunderTests
    {
        private static string Message(string source) => Fails(source).Problem;

        [Test]
        public void MissingMethodsGiveTheUsualMessages()
        {
            Assert.That(Message("class A:\n    pass\nA()[0]"), Is.EqualTo("'A' object is not subscriptable"));
            Assert.That(Message("class A:\n    pass\nA()[0] = 1"), Is.EqualTo("'A' object does not support item assignment"));
            Assert.That(Message("class A:\n    pass\nfor x in A():\n    pass"), Is.EqualTo("'A' object is not iterable"));
            Assert.That(Message("class A:\n    pass\n1 in A()"), Is.EqualTo("argument of type 'A' is not a container or iterable"));
            Assert.That(Message("class A:\n    pass\na, b = A()"), Is.EqualTo("cannot unpack non-iterable A object"));
            Assert.That(Message("class A:\n    pass\nA()()"), Is.EqualTo("'A' object is not callable"));
        }

        [Test]
        public void IterAndNextCheckTheirArguments()
        {
            Assert.That(Message("next(5)"), Is.EqualTo("'int' object is not an iterator"));
            Assert.That(Message("class A:\n    pass\nnext(A())"), Is.EqualTo("'A' object is not an iterator"));
            Assert.That(Message("iter(5)"), Is.EqualTo("'int' object is not iterable"));
            Assert.That(Message("it = iter([])\nnext(it)"), Is.EqualTo(""));
            Assert.That(Fails("it = iter([])\nnext(it)").TypeName, Is.EqualTo("StopIteration"));
        }

        [Test]
        public void IteratorReturnedByIterMustHaveNext()
        {
            Assert.That(Message("class It:\n    pass\nclass A:\n    def __iter__(self):\n        return It()\nlist(A())"),
                Is.EqualTo("iter() returned non-iterator of type 'It'"));
        }

        [Test]
        public void StopIterationFromNextEndsTheLoopButOtherErrorsPropagate()
        {
            Assert.That(Out("class StopNow(StopIteration):\n    pass\nclass A:\n    def __iter__(self):\n        return self\n    def __next__(self):\n        raise StopNow()\nprint(list(A()))"), Is.EqualTo("[]"));
            Assert.That(Fails("class A:\n    def __iter__(self):\n        return self\n    def __next__(self):\n        raise KeyError('k')\nlist(A())").TypeName, Is.EqualTo("KeyError"));
        }

        [Test]
        public void CallCanWaitButNextCannot()
        {
            var vm = new PyVm { Output = s => { } };
            var sb = new System.Text.StringBuilder();
            vm.Output = s => sb.Append(s);
            vm.RegisterModule("game", m => m.Native("wait", (ctx, a) => { ctx.Wait(1); return Value.None; }, 1, 1));
            var ok = vm.Start("import game\nclass Job:\n    def __call__(self, n):\n        game.wait(1)\n        return n + 1\nj = Job()\nprint(j(1))");
            for (int i = 0; i < 10 && ok.State != TaskState.Finished; i++) { ok.Run(); ok.Tick(); }
            Assert.That(sb.ToString(), Is.EqualTo("2\n"));

            var bad = vm.Start("import game\nclass It:\n    def __iter__(self):\n        return self\n    def __next__(self):\n        game.wait(1)\n        raise StopIteration\nfor x in It():\n    pass");
            Assert.That(bad.RunUntilDone(), Is.EqualTo(TaskState.Error));
            Assert.That(bad.Error.Problem, Does.Contain("cannot wait"));
        }

        [Test]
        public void SpecialMethodsWorkInsideEventHandlersAndCallbacks()
        {
            var sb = new System.Text.StringBuilder();
            var vm = new PyVm { Output = s => sb.Append(s) };
            var task = vm.Start("class Bag:\n    def __init__(self):\n        self.items = [3, 1, 2]\n    def __iter__(self):\n        return iter(self.items)\n    def __call__(self, v):\n        return -v\n" +
                                "def on_ping():\n    b = Bag()\n    print(sorted(b), sorted(b, key=b), 2 in b)\n");
            task.RunUntilDone();
            task.QueueEvent("on_ping");
            task.RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("[1, 2, 3] [3, 2, 1] True\n"));
        }
    }
}
