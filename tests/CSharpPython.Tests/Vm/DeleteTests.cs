using CSharpPython.Syntax;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class DeleteTests
    {
        private static string Message(string source) => Fails(source).Problem;

        private static string SyntaxProblem(string source)
        {
            var e = Assert.Throws<PySyntaxError>(() => new PyVm().Compile(source), source);
            return e.Problem;
        }

        [TestCase("del 1", "cannot delete literal")]
        [TestCase("del f()", "cannot delete function call")]
        [TestCase("del a + b", "cannot delete expression")]
        [TestCase("del a, 1", "cannot delete literal")]
        [TestCase("del (a, f())", "cannot delete function call")]
        [TestCase("del [a, {}]", "cannot delete dict literal")]
        [TestCase("del {1}", "cannot delete set display")]
        [TestCase("del 'x'", "cannot delete literal")]
        [TestCase("del a == b", "cannot delete comparison")]
        [TestCase("del", "unexpected")]
        public void BadTargetsAreSyntaxErrors(string source, string message)
        {
            Assert.That(SyntaxProblem(source), Does.Contain(message));
        }

        [Test]
        public void UnboundErrorsMatchTheOtherUnboundReads()
        {
            Assert.That(Fails("def f():\n    x = 1\n    del x\n    return x\nf()").TypeName, Is.EqualTo("UnboundLocalError"));
            Assert.That(Message("def f():\n    x = 1\n    del x\n    return x\nf()"), Is.EqualTo("local variable 'x' referenced before assignment"));
            Assert.That(Message("def f():\n    del x\nf()"), Is.EqualTo("local variable 'x' referenced before assignment"));
            Assert.That(Message("del nothing"), Is.EqualTo("name 'nothing' is not defined"));
        }

        [Test]
        public void DeletingAGlobalThatShadowsABuiltinRevealsTheBuiltin()
        {
            Assert.That(Out("len = 5\ndel len\nprint(len([1, 2]))"), Is.EqualTo("2"));
            Assert.That(Message("del len"), Is.EqualTo("name 'len' is not defined"));
        }

        [Test]
        public void DelMakesAFunctionVariableLocal()
        {
            Assert.That(Message("x = 1\ndef f():\n    del x\nf()"), Is.EqualTo("local variable 'x' referenced before assignment"));
            Assert.That(Out("x = 1\ndef f():\n    global x\n    del x\nf()\ntry:\n    x\nexcept NameError:\n    print('gone')"), Is.EqualTo("gone"));
        }

        [Test]
        public void ClassBodyNamesCanBeDeleted()
        {
            Assert.That(Out("class A:\n    a = 1\n    b = 2\n    del a\nprint(hasattr(A, 'a'), A.b)"), Is.EqualTo("False 2"));
        }

        [Test]
        public void AClosureSeesADeletedVariableAsUnbound()
        {
            Assert.That(Out("def f():\n    x = 1\n    def g():\n        return x\n    del x\n    try:\n        g()\n    except NameError as e:\n        print(e)\n    x = 7\n    return g()\nprint(f())"),
                Is.EqualTo("free variable 'x' referenced before assignment in enclosing scope\n7"));
            Assert.That(Message("def f():\n    x = 1\n    def g():\n        nonlocal x\n        del x\n        del x\n    g()\nf()"),
                Is.EqualTo("free variable 'x' referenced before assignment in enclosing scope"));
            Assert.That(Message("def f():\n    x = 1\n    def g():\n        return x\n    del x\n    del x\nf()"),
                Is.EqualTo("local variable 'x' referenced before assignment"));
        }

        [Test]
        public void ModuleAttributesCannotBeDeleted()
        {
            Assert.That(Message("import math\ndel math.pi"), Is.EqualTo("module attributes are read-only"));
        }

        [Test]
        public void ListSliceDeletionFollowsPythonsRules()
        {
            Assert.That(Out("a = list(range(8))\ndel a[1:3], a[-1]\nprint(a)\ndel a[::3]\nprint(a)\ndel a[::-1]\nprint(a)"), Is.EqualTo("[0, 3, 4, 5, 6]\n[3, 4, 6]\n[]"));
            Assert.That(Message("a = [1]\ndel a[::0]"), Is.EqualTo("slice step cannot be zero"));
        }

        [Test]
        public void DunderDelitemReceivesTheKeyAndErrorsPropagate()
        {
            Assert.That(Out("class A:\n    def __delitem__(self, k):\n        print('del', k)\nA().__delitem__(1) if False else None\na = A()\ndel a[1], a['x'], a[1:2]"), Does.StartWith("del 1\ndel x\n"));
            Assert.That(Message("class A:\n    def __delitem__(self, k):\n        raise ValueError('nope')\ndel A()[0]"), Is.EqualTo("nope"));
        }

        [Test]
        public void DeletingWorksInsideEventHandlers()
        {
            var sb = new System.Text.StringBuilder();
            var vm = new PyVm { Output = s => sb.Append(s) };
            var task = vm.Start("targets = {1: 'a', 2: 'b'}\ndef on_lost(n):\n    del targets[n]\n    print(sorted(targets))\n");
            task.RunUntilDone();
            task.QueueEvent("on_lost", Value.FromInt(1));
            task.QueueEvent("on_lost", Value.FromInt(1));
            task.RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("[2]\n"));
            Assert.That(task.State, Is.EqualTo(TaskState.Error));
            Assert.That(task.Error.TypeName, Is.EqualTo("KeyError"));
        }
    }
}
