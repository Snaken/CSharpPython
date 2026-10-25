using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class SpecialMethodBatchTests
    {
        [Test]
        public void ImportOfSeveralModulesBindsEachOne()
        {
            Assert.That(Out("import math, math as m\nprint(math.pi == m.pi)"), Is.EqualTo("True"));
            Assert.That(Out("import math as a, math as b; print(a is b)"), Is.EqualTo("True"));
            Assert.That(Out("def f():\n    import math, math as m\n    return m is math\nprint(f())"), Is.EqualTo("True"));
        }

        [Test]
        public void ImportOfSeveralModulesFailsOnTheMissingOne()
        {
            var e = Fails("import math, nothing_here");
            Assert.That(e.TypeName, Is.EqualTo("ImportError"));
        }

        [Test]
        public void ImportOfSeveralModulesParsesToSeveralStatements()
        {
            var module = CSharpPython.Syntax.Parser.ParseModule("import a, b.c as d, e\nx = 1\n");
            Assert.That(module.Body.Count, Is.EqualTo(4));
            Assert.That(((CSharpPython.Syntax.ImportStmt)module.Body[1]).Module, Is.EqualTo("b.c"));
            Assert.That(((CSharpPython.Syntax.ImportStmt)module.Body[1]).Alias, Is.EqualTo("d"));
        }

        [Test]
        public void FormatDunderGetsTheSpecAndTheConversionsStillApply()
        {
            const string src = "class A:\n    def __format__(self, spec): return '[' + spec + ']'\n    def __repr__(self): return 'A()'\na = A()\nprint(f'{a:x>4}', f'{a!r}', f'{a!r:>6}', '{:q}'.format(a), format(a))";
            Assert.That(Out(src), Is.EqualTo("[x>4] A()    A() [q] []"));
        }

        [Test]
        public void FormatDunderMayCallFormatOnItsParts()
        {
            Assert.That(Out("class A:\n    def __format__(self, spec): return format(3.14159, spec)\nprint(f'{A():.2f}')"), Is.EqualTo("3.14"));
        }

        [Test]
        public void ObjectsWithoutFormatRejectASpec()
        {
            var e = Fails("class A: pass\nformat(A(), '>4')");
            Assert.That(e.TypeName, Is.EqualTo("TypeError"));
            Assert.That(e.Problem, Does.Contain("unsupported format string passed to A.__format__"));
        }

        [Test]
        public void IndexDunderWorksInSliceAssignmentAndStep()
        {
            const string src = "class I:\n    def __init__(self, n): self.n = n\n    def __index__(self): return self.n\nxs = [0, 1, 2, 3, 4, 5]\nxs[I(1):I(3)] = ['a']\nprint(xs)\nxs[::I(2)] = [9, 9, 9]\nprint(xs)\nprint('abcdef'[I(1):I(4)], (1, 2, 3, 4)[I(-2):])";
            Assert.That(Out(src), Is.EqualTo("[0, 'a', 3, 4, 5]\n[9, 'a', 9, 4, 9]\nbcd (3, 4)"));
        }

        [Test]
        public void IndexDunderIsNotUsedForFloatsOrDictKeys()
        {
            Assert.That(Fails("[1, 2][1.0]").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Out("class K:\n    def __index__(self): return 1\nk = K()\nd = {k: 'v'}\nprint(d[k])"), Is.EqualTo("v"));
        }

        [Test]
        public void IntFallsBackToIndexButPrefersInt()
        {
            Assert.That(Out("class A:\n    def __index__(self): return 4\nprint(int(A()))"), Is.EqualTo("4"));
            Assert.That(Out("class B:\n    def __int__(self): return 1\n    def __index__(self): return 2\nprint(int(B()))"), Is.EqualTo("1"));
        }

        [Test]
        public void ReversedWithoutTheDunderStillUsesTheSequence()
        {
            Assert.That(Out("class S:\n    def __iter__(self): return iter([1, 2, 3])\nprint(list(reversed(S())))"), Is.EqualTo("[3, 2, 1]"));
        }

        [Test]
        public void ReversedDunderMayReturnAnythingIterable()
        {
            Assert.That(Out("class R:\n    def __reversed__(self): return 'abc'\nprint(list(reversed(R())))"), Is.EqualTo("['a', 'b', 'c']"));
        }

        [Test]
        public void RoundDunderIsPassedNdigitsOnlyWhenGiven()
        {
            const string src = "class N:\n    def __round__(self, *a): return len(a)\nn = N()\nprint(round(n), round(n, 3), round(n, None))";
            Assert.That(Out(src), Is.EqualTo("0 1 0"));
        }

        [Test]
        public void RoundOfAnObjectWithoutTheDunderRaises()
        {
            var e = Fails("class A: pass\nround(A())");
            Assert.That(e.Problem, Is.EqualTo("type A doesn't define __round__ method"));
        }
    }
}
