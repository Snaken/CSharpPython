using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class ArithmeticDunderTests
    {
        private static string Message(string source) => Fails(source).Problem;

        [Test]
        public void UnsupportedOperandsNameBothTypes()
        {
            Assert.That(Message("class A:\n    pass\nA() + 1"), Is.EqualTo("unsupported operand type(s) for +: 'A' and 'int'"));
            Assert.That(Message("class A:\n    pass\n1 - A()"), Is.EqualTo("unsupported operand type(s) for -: 'int' and 'A'"));
            Assert.That(Message("class A:\n    pass\nA() ** A()"), Does.Contain("A' and 'A'"));
            Assert.That(Message("class A:\n    pass\n-A()"), Is.EqualTo("bad operand type for unary -: 'A'"));
            Assert.That(Message("class A:\n    pass\nabs(A())"), Is.EqualTo("bad operand type for abs(): 'A'"));
        }

        [Test]
        public void AugmentedAssignmentOnInstancesWorksOnEveryTargetKind()
        {
            const string source =
                "class V:\n    def __init__(self, n):\n        self.n = n\n    def __add__(self, o):\n        return V(self.n + o)\n" +
                "a = V(1)\na += 1\nclass H:\n    pass\nh = H()\nh.v = V(10)\nh.v += 5\nxs = [V(100)]\nxs[0] += 7\nd = {'k': V(0)}\nd['k'] += 3\n" +
                "print(a.n, h.v.n, xs[0].n, d['k'].n)";
            Assert.That(Out(source), Is.EqualTo("2 15 107 3"));
        }

        [Test]
        public void AugmentedAssignmentOnPlainValuesIsUnchanged()
        {
            Assert.That(Out("x = 1\nx += 2\nx *= 3\ny = 'a'\ny += 'b'\nz = [1]\nz += [2]\nw = 7\nw //= 2\nw **= 2\nw %= 5\nw /= 2\nprint(x, y, z, w)"), Is.EqualTo("9 ab [1, 2] 2.0"));
        }

        [Test]
        public void ArithmeticMethodsCanRaiseAndCannotWait()
        {
            Assert.That(Out("class V:\n    def __add__(self, o):\n        raise KeyError('k')\ntry:\n    V() + 1\nexcept KeyError:\n    print('caught')"), Is.EqualTo("caught"));
            var vm = new PyVm { Output = s => { } };
            vm.RegisterModule("game", m => m.Native("wait", (ctx, a) => { ctx.Wait(1); return Value.None; }, 1, 1));
            var task = vm.Start("import game\nclass V:\n    def __add__(self, o):\n        game.wait(1)\n        return self\nV() + 1");
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Error));
            Assert.That(task.Error.Problem, Does.Contain("cannot wait"));
        }

        [Test]
        public void ConversionResultsAreChecked()
        {
            Assert.That(Message("class A:\n    def __int__(self):\n        return 1.5\nint(A())"), Is.EqualTo("__int__ returned non-int (type float)"));
            Assert.That(Message("class A:\n    pass\nint(A())"), Is.EqualTo("int() argument must be a string or a number, not 'A'"));
            Assert.That(Message("class A:\n    pass\nfloat(A())"), Is.EqualTo("float() argument must be a string or a number, not 'A'"));
        }

        [Test]
        public void SumStartsFromIntegerZeroSoItNeedsRadd()
        {
            Assert.That(Message("class A:\n    def __add__(self, o):\n        return self\nsum([A(), A()])"), Does.Contain("unsupported operand type(s) for +: 'int' and 'A'"));
        }
    }
}
