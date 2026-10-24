using CSharpPython.Syntax;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class VarArgsTests
    {
        private static string Message(string source) => Fails(source).Problem;

        [Test]
        public void SpreadErrorsNameTheProblem()
        {
            Assert.That(Message("def f(*a):\n    pass\nf(*5)"), Is.EqualTo("argument after * must be an iterable, not int"));
            Assert.That(Message("def f(**k):\n    pass\nf(**[1])"), Is.EqualTo("argument after ** must be a mapping, not list"));
            Assert.That(Message("def f(**k):\n    pass\nf(**{1: 2})"), Is.EqualTo("keywords must be strings"));
            Assert.That(Message("def f(**k):\n    pass\nf(a=1, **{'a': 2})"), Is.EqualTo("got multiple values for keyword argument 'a'"));
            Assert.That(Message("def f(**k):\n    pass\nf(**{'a': 1}, **{'a': 2})"), Is.EqualTo("got multiple values for keyword argument 'a'"));
        }

        [Test]
        public void BindingErrorsMatchCpython()
        {
            Assert.That(Message("def f(a, *r):\n    pass\nf()"), Is.EqualTo("f() missing 1 required positional argument: 'a'"));
            Assert.That(Message("def f(*, a):\n    pass\nf()"), Is.EqualTo("f() missing 1 required keyword-only argument: 'a'"));
            Assert.That(Message("def f(*, a, b):\n    pass\nf()"), Is.EqualTo("f() missing 2 required keyword-only arguments: 'a' and 'b'"));
            Assert.That(Message("def f(*, a):\n    pass\nf(1, a=2)"), Is.EqualTo("f() takes 0 positional arguments but 1 was given"));
            Assert.That(Message("def f(*a):\n    pass\nf(x=1)"), Is.EqualTo("f() got an unexpected keyword argument 'x'"));
            Assert.That(Message("def f(a, **k):\n    pass\nf(1, a=2)"), Is.EqualTo("f() got multiple values for argument 'a'"));
            Assert.That(Message("def f(*, a):\n    pass\nf(a=1, a=2)\n".Replace("f(a=1, a=2)", "f(**{'a': 1}, a=2)")), Does.Contain("multiple values"));
        }

        [Test]
        public void DuplicateParameterNamesAreRejected()
        {
            Assert.That(Assert.Throws<PySyntaxError>(() => new PyVm().Compile("def f(a, *a):\n    pass\n")).Problem, Does.Contain("duplicate argument"));
            Assert.That(Assert.Throws<PySyntaxError>(() => new PyVm().Compile("def f(*a, **a):\n    pass\n")).Problem, Does.Contain("duplicate argument"));
            Assert.That(Assert.Throws<PySyntaxError>(() => new PyVm().Compile("def f(a, *, a):\n    pass\n")).Problem, Does.Contain("duplicate argument"));
        }

        [Test]
        public void ArgsAndKwargsAreAssignableLocals()
        {
            Assert.That(Out("def f(*a, **k):\n    a = 1\n    k = 2\n    return a, k\nprint(f(5, x=6))"), Is.EqualTo("(1, 2)"));
        }

        [Test]
        public void NonlocalOfVariableParametersIsRejected()
        {
            Assert.That(Assert.Throws<PySyntaxError>(() => new PyVm().Compile("def f():\n    x = 1\n    def g(*x):\n        nonlocal x\n")).Problem, Does.Contain("parameter and nonlocal"));
        }

        [Test]
        public void EventHandlersMayTakeVariableArguments()
        {
            var vm = new PyVm { Output = null };
            var sb = new System.Text.StringBuilder();
            vm.Output = s => sb.Append(s);
            var task = vm.Start("def on_hit(*a, **k):\n    print(a, k)\n");
            task.Update();
            task.QueueEvent("on_hit", Value.FromInt(1), Value.FromInt(2));
            task.Update();
            Assert.That(sb.ToString().TrimEnd(), Is.EqualTo("(1, 2) {}"));
        }

        [Test]
        public void VariableArgumentFunctionsWorkAsNativeCallbacks()
        {
            Assert.That(Out("def k(*a, **kw):\n    return -a[0]\nprint(sorted([1, 3, 2], key=k))"), Is.EqualTo("[3, 2, 1]"));
        }

        [Test]
        public void SpreadCallsInsideTryFinallyUnwindCorrectly()
        {
            Assert.That(Out("def f(*a):\n    try:\n        return sum(a)\n    finally:\n        print('done')\nprint(f(*[1, 2, 3]))"), Is.EqualTo("done\n6"));
        }

        [Test]
        public void DisassemblyShowsSpreadInstructions()
        {
            var text = new PyVm().Compile("f(*a, x=1, **b)\n").Disassemble();
            Assert.That(text, Does.Contain("ListExtend").And.Contain("DictSetUnique").And.Contain("DictMerge").And.Contain("CallEx"));
            Assert.That(new PyVm().Compile("o.m(1, *a)\n").Disassemble(), Does.Contain("ListAppend"));
        }
    }
}
