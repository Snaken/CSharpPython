using CSharpPython.Editor;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class AnnotationTests
    {
        [Test]
        public void AnnotationsAreNeverEvaluated()
        {
            Assert.That(Out("x: undefined_name = 1\ndef f(a: nope, b: 1 / 0 = 2) -> also_nope:\n    return a + b\nprint(x, f(1))"), Is.EqualTo("1 3"));
        }

        [Test]
        public void ArrowIsAToken()
        {
            Assert.That(Out("def f() -> int: return 1\nprint(f())"), Is.EqualTo("1"));
            Assert.That(Out("f = lambda: 1\nprint(f())"), Is.EqualTo("1"));
        }

        [Test]
        public void MinusFollowedByGreaterThanStillSubtractsAndCompares()
        {
            Assert.That(Out("a = 3\nprint(a - -1, a-1 > 1)"), Is.EqualTo("4 True"));
        }

        [Test]
        public void ValueOfAnAnnotatedAssignmentMayBeAYieldOrATuple()
        {
            Assert.That(Out("def g():\n    x: int = yield 1\n    yield x\nit = g()\nprint(next(it), it.send(7))"), Is.EqualTo("1 7"));
        }

        [Test]
        public void ErrorsInTheValueStillRaise()
        {
            Assert.That(Fails("x: int = 1 // 0").TypeName, Is.EqualTo("ZeroDivisionError"));
            Assert.That(Out("d = {}\nd['a']: int\nprint(d)"), Is.EqualTo("{}"));
        }

        [Test]
        public void AnnotationOnlyTargetsEvaluateTheirObject()
        {
            Assert.That(Fails("missing.x: int").TypeName, Is.EqualTo("NameError"));
            Assert.That(Fails("missing[0]: int").TypeName, Is.EqualTo("NameError"));
        }

        [Test]
        public void EditorToolsHandleAnnotatedCode()
        {
            string src = "def move(speed: float, name: str = 'a') -> None:\n    pass\nmove(";
            var sig = SignatureHelper.Get(src, src.Length, new PyVm());
            Assert.That(sig, Is.Not.Null);
            Assert.That(sig.Label, Does.Contain("move(speed"));
            Assert.That(Analyzer.Analyze("x: int = 1\ndef f(a: int) -> int:\n    return a"), Is.Empty);
            var h = Hover.Get("def move(speed: float):\n    pass\nmove", 36, new PyVm());
            Assert.That(h.Label, Does.StartWith("def move(speed"));
        }
    }
}
