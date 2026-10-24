using CSharpPython.Syntax;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class ClosureTests
    {
        private static string Problem(string source) => Assert.Throws<PySyntaxError>(() => new PyVm().Compile(source)).Problem;

        [Test]
        public void NonlocalAtModuleLevelIsRejected()
        {
            Assert.That(Problem("nonlocal x\n"), Does.Contain("not allowed at module level"));
        }

        [Test]
        public void NonlocalWithoutAnEnclosingBindingIsRejected()
        {
            Assert.That(Problem("def f():\n    nonlocal x\n    x = 1\n"), Is.EqualTo("no binding for nonlocal 'x' found"));
        }

        [Test]
        public void NonlocalDoesNotReachModuleGlobals()
        {
            Assert.That(Problem("x = 1\ndef f():\n    def g():\n        nonlocal x\n        x = 2\n"), Does.Contain("no binding for nonlocal"));
        }

        [Test]
        public void NonlocalAfterAssignmentIsRejected()
        {
            Assert.That(Problem("def f():\n    x = 1\n    def g():\n        x = 2\n        nonlocal x\n"), Does.Contain("assigned to before nonlocal"));
        }

        [Test]
        public void NonlocalAfterUseIsRejected()
        {
            Assert.That(Problem("def f():\n    x = 1\n    def g():\n        print(x)\n        nonlocal x\n"), Does.Contain("used prior to nonlocal"));
        }

        [Test]
        public void NonlocalAndGlobalConflict()
        {
            Assert.That(Problem("def f():\n    x = 1\n    def g():\n        global x\n        nonlocal x\n"), Does.Contain("nonlocal and global"));
            Assert.That(Problem("def f():\n    x = 1\n    def g():\n        nonlocal x\n        global x\n"), Does.Contain("nonlocal and global"));
        }

        [Test]
        public void NonlocalOfAParameterIsRejected()
        {
            Assert.That(Problem("def f():\n    x = 1\n    def g(x):\n        nonlocal x\n"), Does.Contain("parameter and nonlocal"));
        }

        [Test]
        public void ReadingAFreeVariableBeforeItIsAssignedRaisesNameError()
        {
            var e = Fails("def f():\n    def g():\n        return y\n    r = g()\n    y = 1\n    return r\nf()");
            Assert.That(e.Message, Does.Contain("free variable 'y'"));
        }

        [Test]
        public void ReadingACellVariableBeforeItIsAssignedRaisesUnboundLocalError()
        {
            var e = Fails("def f():\n    print(y)\n    y = 1\n    def g():\n        return y\nf()");
            Assert.That(e.Message, Does.Contain("'y'"));
        }

        [Test]
        public void AClosureCanBeCalledFromNativeCallbacksAndStillSeesItsCells()
        {
            Assert.That(Out("def f():\n    n = 0\n    def k(v):\n        nonlocal n\n        n += 1\n        return -v\n    sorted([1, 2, 3], key=k)\n    return n\nprint(f())"), Is.EqualTo("3"));
        }

        [Test]
        public void DeepRecursionOfAClosureStillHitsTheCallDepthLimit()
        {
            var e = Fails("def f():\n    def g(n):\n        return g(n + 1)\n    return g(0)\nf()");
            Assert.That(e.Message, Does.Contain("recursion"));
        }

        [Test]
        public void LambdaParsesWithDefaultsAndNoParameters()
        {
            Assert.That(Out("print((lambda: 1)(), (lambda a, b=2: a + b)(1))"), Is.EqualTo("1 3"));
        }

        [Test]
        public void LambdaBodyIsAnExpressionOnly()
        {
            Assert.Throws<PySyntaxError>(() => new PyVm().Compile("f = lambda: pass\n"));
        }

        [Test]
        public void LambdaFunctionHasAnAngleBracketName()
        {
            Assert.That(Out("f = lambda: 1\nprint(f)"), Does.Contain("<lambda>"));
        }

        [Test]
        public void ClosureDisassemblyUsesTheCellInstructions()
        {
            var code = new PyVm().Compile("def f():\n    n = 0\n    def g():\n        nonlocal n\n        n += 1\n    return g\n");
            string text = code.Disassemble();
            Assert.That(code.ToString(), Is.Not.Null);
            Assert.That(text, Does.Contain("MakeFunction"));
        }
    }
}
