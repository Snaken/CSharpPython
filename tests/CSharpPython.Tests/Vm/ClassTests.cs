using CSharpPython.Syntax;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class ClassTests
    {
        private static string Message(string source) => Fails(source).Problem;

        private static string SyntaxProblem(string source)
        {
            var vm = new PyVm();
            return Assert.Throws<PySyntaxError>(() => vm.Compile(source), source).Problem;
        }

        [Test]
        public void ErrorMessagesNameTheProblem()
        {
            Assert.That(Message("class A: pass\nA(1)"), Is.EqualTo("A() takes no arguments"));
            Assert.That(Message("class A: pass\nA().x"), Is.EqualTo("'A' object has no attribute 'x'"));
            Assert.That(Message("class A: pass\nA.x"), Is.EqualTo("type object 'A' has no attribute 'x'"));
            Assert.That(Message("class A:\n    def __init__(self):\n        return 5\nA()"), Is.EqualTo("__init__() should return None, not 'int'"));
            Assert.That(Message("class A:\n    def __init__(self, x): pass\nA()"), Does.Contain("missing 1 required positional argument: 'x'"));
            Assert.That(Message("class A:\n    def f(self): pass\nA().f(1)"), Does.Contain("takes 1 positional argument"));
            Assert.That(Message("class A:\n    __init__ = 5\nA()"), Is.EqualTo("__init__ must be a function"));
            Assert.That(Message("class A(5): pass"), Is.EqualTo("bases must be classes, not 'int'"));
            Assert.That(Message("class A(int): pass"), Is.EqualTo("cannot inherit from the built-in type 'int' yet"));
            Assert.That(Message("class A(str): pass"), Is.EqualTo("cannot inherit from the built-in type 'str' yet"));
            Assert.That(Message("object()"), Is.EqualTo("cannot create 'object' instances"));
            Assert.That(Message("getattr(1, 2)"), Is.EqualTo("getattr(): attribute name must be string"));
            Assert.That(Message("setattr(1, 'x', 2)"), Does.Contain("has no attribute 'x'"));
        }

        [Test]
        public void SyntaxErrorsInClassStatements()
        {
            Assert.That(SyntaxProblem("class A(B, C): pass"), Is.EqualTo("multiple inheritance is not supported"));
            Assert.That(SyntaxProblem("class A(metaclass=B): pass"), Does.Contain("metaclass"));
            Assert.That(SyntaxProblem("class A:\n    return 1"), Is.EqualTo("'return' outside function"));
            Assert.That(SyntaxProblem("class : pass"), Does.StartWith("expected a class name"));
            Assert.That(SyntaxProblem("class A pass"), Does.StartWith("expected ':'"));
            Assert.That(SyntaxProblem("class A(B:\n    pass"), Does.Contain("never closed"));
            Assert.That(SyntaxProblem("class A:\npass"), Does.Contain("expected an indented block after class definition"));
            Assert.That(SyntaxProblem("class A:\n    break"), Does.Contain("'break' outside loop"));
        }

        [Test]
        public void ClassBodyNamesAreNotVisibleToMethods()
        {
            Assert.That(Message("class K:\n    x = 1\n    def f(self):\n        return x\nK().f()"), Is.EqualTo("name 'x' is not defined"));
            Assert.That(Out("x = 'g'\nclass K:\n    x = 'c'\n    def f(self):\n        return x\nprint(K().f())"), Is.EqualTo("g"));
            Assert.That(Out("def f():\n    x = 'outer'\n    class K:\n        x = 'inner'\n        def g(self):\n            return x\n    return K().g()\nprint(f())"), Is.EqualTo("outer"));
        }

        [Test]
        public void ClassBodyReadsBeforeAssignmentAreUnbound()
        {
            // Class bodies keep their names in fixed slots, so unlike Python they do not fall back to a global.
            Assert.That(Fails("x = 1\nclass K:\n    x = x + 1").TypeName, Is.EqualTo("UnboundLocalError"));
        }

        [Test]
        public void MethodsOfAClassInsideAFunctionCanShareTheFunctionsVariables()
        {
            Assert.That(Out("def f():\n    n = 1\n    class K:\n        def inc(self):\n            nonlocal n\n            n += 1\n            return n\n    k = K()\n    k.inc()\n    return k.inc(), n\nprint(f())"), Is.EqualTo("(3, 3)"));
        }

        [Test]
        public void ClassInsideClassInsideFunctionPassesVariablesThrough()
        {
            Assert.That(Out("def f(a):\n    class A:\n        class B:\n            def g(self):\n                return a\n    return A.B().g()\nprint(f(9))"), Is.EqualTo("9"));
        }

        [Test]
        public void ClassVariableWithTheSameNameAsAnOuterVariableUsedByAMethod()
        {
            Assert.That(Out("def f():\n    x = 1\n    class K:\n        x = 2\n        def g(self):\n            return x\n    return K.x, K().g()\nprint(f())"), Is.EqualTo("(2, 1)"));
        }

        [Test]
        public void InstancesAreHashedAndComparedByIdentity()
        {
            Assert.That(Out("class A: pass\na = A()\nb = A()\nprint({a: 1}.get(b), {a: 1}[a], a == b, a == a)"), Is.EqualTo("None 1 False True"));
        }

        [Test]
        public void ClassesPrintLikeCpythonExceptForAddresses()
        {
            Assert.That(Out("class A: pass\nprint(A)"), Is.EqualTo("<class '__main__.A'>"));
            Assert.That(Out("class A: pass\nprint(repr(A()))"), Does.Match("^<__main__.A object at 0x[0-9a-f]{8}>$"));
            Assert.That(Out("class A:\n    def f(self): pass\nprint(A().f)"), Does.Match("^<bound method f of <__main__.A object at 0x[0-9a-f]{8}>>$"));
            Assert.That(Out("class A:\n    def f(self): pass\nprint(A.f)"), Is.EqualTo("<function f>"));
        }

        [Test]
        public void InitCanWaitWithoutLosingTheInstance()
        {
            var vm = new PyVm();
            var sb = new System.Text.StringBuilder();
            vm.Output = s => sb.Append(s);
            vm.RegisterModule("game", m => m.Native("wait", (ctx, a) => { ctx.Wait((int)a[0].AsInt()); return Value.None; }, 1, 1));
            var task = vm.Start("import game\nclass A:\n    def __init__(self):\n        self.x = 1\n        game.wait(2)\n        self.x = 2\na = A()\nprint(a.x)");
            Assert.That(task.Update(), Is.EqualTo(TaskState.Waiting));
            Assert.That(sb.ToString(), Is.Empty);
            task.Update();
            task.Update();
            Assert.That(task.Update(), Is.EqualTo(TaskState.Finished));
            Assert.That(sb.ToString().Trim(), Is.EqualTo("2"));
        }

        [Test]
        public void HostCanInspectInstancesAndClasses()
        {
            var vm = new PyVm();
            var task = vm.Start("class A:\n    pass\na = A()\nm = a.__class__\nf = a.__class__.__name__");
            task.RunUntilDone();
            Assert.That(task.Globals["a"].Kind, Is.EqualTo(ValueKind.Instance));
            Assert.That(task.Globals["a"].TypeName, Is.EqualTo("A"));
            Assert.That(task.Globals["A"].Kind, Is.EqualTo(ValueKind.Class));
            Assert.That(task.Globals["A"].TypeName, Is.EqualTo("type"));
            Assert.That(task.Globals["f"].AsString(), Is.EqualTo("A"));
            Assert.That(task.Globals["a"].IsTruthy, Is.True);
        }

        [Test]
        public void ScriptCallbacksCanBeMethodsAndClasses()
        {
            Assert.That(Out("class K:\n    def __init__(self, v):\n        self.v = v\nxs = sorted([K(3), K(1), K(2)], key=lambda k: k.v)\nprint([k.v for k in xs])"), Is.EqualTo("[1, 2, 3]"));
            // The class is called for every element; the instances it makes cannot be ordered, as in Python.
            Assert.That(Message("class K:\n    def __init__(self, v):\n        self.v = v\nsorted([3, 1, 2], key=K)"),
                Is.EqualTo("'<' not supported between instances of 'K' and 'K'"));
            Assert.That(Out("class K:\n    n = 0\n    def __init__(self, v):\n        K.n += 1\nsorted([3, 1, 2], key=lambda v: K(v) and v)\nprint(K.n)"), Is.EqualTo("3"));
            Assert.That(Out("class K:\n    def key(self, x):\n        return -x\nprint(sorted([1, 3, 2], key=K().key))"), Is.EqualTo("[3, 2, 1]"));
        }

        [Test]
        public void ExceptionInInitShowsAUsefulTraceback()
        {
            var e = Fails("class A:\n    def __init__(self):\n        raise ValueError('bad')\nA()");
            Assert.That(e.TypeName, Is.EqualTo("ValueError"));
            Assert.That(e.Trace[0].Function, Is.EqualTo("__init__"));
            Assert.That(e.Trace[0].Line, Is.EqualTo(3));
        }

        [Test]
        public void ClassStatementsBindTheNameLikeDef()
        {
            Assert.That(Out("def f():\n    class K:\n        v = 1\n    return K\nprint(f().v)"), Is.EqualTo("1"));
            Assert.That(Out("class K:\n    pass\nK = 5\nprint(K)"), Is.EqualTo("5"));
            Assert.That(Message("def f():\n    print(K)\n    class K: pass\nf()"), Does.Contain("K"));
        }

        [Test]
        public void ObjectIsABaseAndInstanceCheckTarget()
        {
            Assert.That(Out("print(isinstance(1, object), isinstance(int, object), issubclass(int, object), issubclass(bool, object))"), Is.EqualTo("True True True True"));
            Assert.That(Out("print(object, type(object))"), Is.EqualTo("<class 'object'> <class 'type'>"));
        }

        [Test]
        public void SetattrRequiresAnAssignableTarget()
        {
            Assert.That(Message("setattr([], 'x', 1)"), Does.Contain("object has no attribute 'x'"));
            Assert.That(Message("import math\nsetattr(math, 'x', 1)"), Is.EqualTo("module attributes are read-only"));
        }
    }
}
