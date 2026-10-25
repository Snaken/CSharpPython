using CSharpPython.Syntax;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class SuperTests
    {
        private static string Message(string source) => Fails(source).Problem;

        private static string SyntaxProblem(string source)
        {
            var vm = new PyVm();
            return Assert.Throws<PySyntaxError>(() => vm.Compile(source), source).Problem;
        }

        private const string Base = "class A:\n    def f(self):\n        return 'A'\n";

        [Test]
        public void SuperOnlyWorksAsAttributeAccess()
        {
            Assert.That(SyntaxProblem(Base + "class B(A):\n    def f(self):\n        s = super()\n        return s.f()"),
                Is.EqualTo("super() is only supported as super().name"));
            Assert.That(SyntaxProblem(Base + "class B(A):\n    def f(self):\n        super().x = 1"),
                Is.EqualTo("super() is only supported as super().name"));
            Assert.That(SyntaxProblem("class B:\n    def f(self):\n        return super(1).x"), Does.Contain("either no arguments or two"));
            Assert.That(SyntaxProblem("class B:\n    def f(self):\n        return super(1, 2, 3).x"), Does.Contain("either no arguments or two"));
            Assert.That(SyntaxProblem("class B:\n    def f(self):\n        return super(a=1).x"), Does.Contain("no keyword or starred"));
            Assert.That(SyntaxProblem("class B:\n    def f(self, *a):\n        return super(*a).x"), Does.Contain("no keyword or starred"));
        }

        [Test]
        public void SuperOutsideAMethodFails()
        {
            Assert.That(Message("def f():\n    return super().x\nf()"), Is.EqualTo("super(): no arguments"));
            Assert.That(Message("super().x"), Is.EqualTo("super(): no arguments"));
            Assert.That(Message("class A:\n    def f():\n        return super().x\nA.f()"), Is.EqualTo("super(): no arguments"));
            // A lambda or inner function is not itself a method.
            Assert.That(Message(Base + "class B(A):\n    def f(self):\n        return (lambda: super().f())()\nB().f()"),
                Is.EqualTo("super(): no arguments"));
        }

        [Test]
        public void SuperChecksItsArguments()
        {
            Assert.That(Message(Base + "class B(A):\n    def f(self):\n        return super(B, 5).f()\nB().f()"),
                Is.EqualTo("super(type, obj): obj must be an instance or subtype of type"));
            Assert.That(Message(Base + "class B(A):\n    pass\nsuper(B, A()).f()"),
                Is.EqualTo("super(type, obj): obj must be an instance or subtype of type"));
            Assert.That(Message("super(5, 5).f()"), Is.EqualTo("super() argument 1 must be a class, not int"));
            Assert.That(Message(Base + "class B(A):\n    def f(self):\n        return super().nope\nB().f()"),
                Is.EqualTo("'super' object has no attribute 'nope'"));
        }

        [Test]
        public void SuperToObjectKnowsInitAndTheDefaultTexts()
        {
            Assert.That(Out("class A:\n    def __init__(self):\n        print(super().__init__())\nA()"), Is.EqualTo("None"));
            Assert.That(Message("class A:\n    def __init__(self):\n        super().__init__(1)\nA()"),
                Is.EqualTo("object.__init__() takes exactly one argument (the instance to initialize)"));
            Assert.That(Out("class A:\n    def __str__(self):\n        return super().__str__()[:12]\nprint(A())"),
                Is.EqualTo("<__main__.A "));
        }

        [Test]
        public void SuperIsResolvedFromTheClassTheMethodWasDefinedIn()
        {
            // The same method inherited by a subclass still continues from where it was written.
            const string source =
                "class A:\n    def f(self):\n        return 'A'\n" +
                "class B(A):\n    def f(self):\n        return 'B' + super().f()\n" +
                "class C(B):\n    pass\nprint(C().f())";
            Assert.That(Out(source), Is.EqualTo("BA"));
        }

        [Test]
        public void SuperWorksWithABoundMethodCapturedEarly()
        {
            Assert.That(Out(Base + "class B(A):\n    def f(self):\n        return 'B' + super().f()\nm = B().f\nprint(m(), A.f(B()))"),
                Is.EqualTo("BA A"));
        }

        [Test]
        public void SuperInInitCanWaitInTheParentConstructor()
        {
            var vm = new PyVm { Output = s => { } };
            vm.RegisterModule("game", m => m.Native("wait", (ctx, a) => { ctx.Wait((int)a[0].AsInt()); return Value.None; }, 1, 1));
            var sb = new System.Text.StringBuilder();
            vm.Output = s => sb.Append(s);
            var task = vm.Start("import game\nclass A:\n    def __init__(self):\n        game.wait(2)\n        self.a = 1\n" +
                                "class B(A):\n    def __init__(self):\n        super().__init__()\n        self.b = 2\nb = B()\nprint(b.a, b.b)");
            Assert.That(task.Run(), Is.EqualTo(TaskState.Waiting));
            for (int i = 0; i < 5 && task.State != TaskState.Finished; i++) { task.Tick(); task.Run(); }
            Assert.That(sb.ToString(), Is.EqualTo("1 2\n"));
        }

        [Test]
        public void StrAndReprMustReturnStrings()
        {
            Assert.That(Message("class A:\n    def __str__(self):\n        return 1\nstr(A())"), Is.EqualTo("__str__ returned non-string (type int)"));
            Assert.That(Message("class A:\n    def __repr__(self):\n        return [1]\nrepr(A())"), Is.EqualTo("__repr__ returned non-string (type list)"));
            Assert.That(Message("class A:\n    def __repr__(self):\n        return 1\nprint([A()])"), Is.EqualTo("__repr__ returned non-string (type int)"));
        }

        [Test]
        public void StrAndReprCannotWait()
        {
            var vm = new PyVm { Output = s => { } };
            vm.RegisterModule("game", m => m.Native("wait", (ctx, a) => { ctx.Wait(1); return Value.None; }, 1, 1));
            var task = vm.Start("import game\nclass A:\n    def __str__(self):\n        game.wait(1)\n        return 'x'\nprint(A())");
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Error));
            Assert.That(task.Error.Problem, Does.Contain("cannot wait"));
        }

        [Test]
        public void RecursiveReprIsStoppedByTheCallLimit()
        {
            var error = Fails("class A:\n    def __repr__(self):\n        return repr(self)\nrepr(A())");
            Assert.That(error.TypeName, Is.EqualTo("RecursionError"));
        }

        [Test]
        public void StrFallsBackToReprAndDefaultsOutsideARunningScript()
        {
            Assert.That(Out("class A:\n    def __repr__(self):\n        return 'R'\nprint(A(), str(A()), repr(A()))"), Is.EqualTo("R R R"));

            // The host printing a value has no task to run __str__ on, so it sees the default text.
            var vm = new PyVm { Output = s => { } };
            var task = vm.Start("class A:\n    def __repr__(self):\n        return 'R'\na = A()");
            task.RunUntilDone();
            Assert.That(task.Globals["a"].Repr(), Does.StartWith("<__main__.A object at 0x"));
        }

        [Test]
        public void StrAndReprKeepWorkingInsideEventsAndCallbacks()
        {
            var sb = new System.Text.StringBuilder();
            var vm = new PyVm { Output = s => sb.Append(s) };
            var task = vm.Start("class A:\n    def __str__(self):\n        return 'a'\ndef on_hit():\n    print(A(), sorted([3, 1], key=lambda v: len(str(A())) * v))\n");
            task.RunUntilDone();
            task.QueueEvent("on_hit");
            task.RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("a [1, 3]\n"));
        }
    }
}
