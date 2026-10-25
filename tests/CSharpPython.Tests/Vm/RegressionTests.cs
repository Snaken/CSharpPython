using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    /// <summary>One test per bug found by the CPython comparison tests, plus behaviour that deliberately differs.</summary>
    public class RegressionTests
    {
        [Test]
        public void SortingMixedTypesRaisesTypeErrorNotHostError()
        {
            var e = Fails("sorted([1, 'a'])");
            Assert.That(e.TypeName, Is.EqualTo("TypeError"));
            Assert.That(e.Problem, Does.Contain("not supported between"));
            Assert.That(Fails("xs = [2, None]\nxs.sort()").TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void UnhashableKeysAreRejectedByEmptyDicts()
        {
            Assert.That(Fails("{}[[]]").Problem, Is.EqualTo("unhashable type: 'list'"));
            Assert.That(Fails("[] in {}").Problem, Is.EqualTo("unhashable type: 'list'"));
            Assert.That(Fails("{}.get([])").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("{}.pop([])").TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void RoundUsesExactBinaryValue()
        {
            Assert.That(Eval("round(2.675, 2)"), Is.EqualTo("2.67"));
            Assert.That(Eval("round(1.005, 2)"), Is.EqualTo("1.0"));
            Assert.That(Eval("round(0.5 + 1e-16, 0)"), Is.EqualTo("1.0"));
            Assert.That(Eval("round(-0.4, 0)"), Is.EqualTo("-0.0"));
            Assert.That(Eval("round(1234.5678, -2)"), Is.EqualTo("1200.0"));
            Assert.That(Eval("round(5e-324, 400)"), Is.EqualTo("5e-324"));
            Assert.That(Eval("round(1.5, -400)"), Is.EqualTo("0.0"));
            Assert.That(Eval("round(1e-300, 20)"), Is.EqualTo("0.0"));
        }

        [Test]
        public void RoundIntegersWithNegativeDigits()
        {
            Assert.That(Eval("round(1234, -2)"), Is.EqualTo("1200"));
            Assert.That(Eval("round(1250, -2)"), Is.EqualTo("1200"));
            Assert.That(Eval("round(1350, -2)"), Is.EqualTo("1400"));
            Assert.That(Eval("round(-1250, -2)"), Is.EqualTo("-1200"));
            Assert.That(Eval("round(5, -1)"), Is.EqualTo("0"));
            Assert.That(Eval("round(15, -1)"), Is.EqualTo("20"));
            Assert.That(Eval("round(7, 3)"), Is.EqualTo("7"));
            Assert.That(Eval("round(7, -30)"), Is.EqualTo("0"));
            Assert.That(Fails("round(9223372036854775807, -1)").TypeName, Is.EqualTo("OverflowError"));
            Assert.That(Fails("round(5, 'a')").TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void IntWithBaseZeroDetectsThePrefix()
        {
            Assert.That(Eval("int('0xff', 0)"), Is.EqualTo("255"));
            Assert.That(Eval("int('0b11', 0)"), Is.EqualTo("3"));
            Assert.That(Eval("int('0o17', 0)"), Is.EqualTo("15"));
            Assert.That(Eval("int('-0x10', 0)"), Is.EqualTo("-16"));
            Assert.That(Eval("int('42', 0)"), Is.EqualTo("42"));
            Assert.That(Fails("int('zz', 0)").Problem, Is.EqualTo("invalid literal for int() with base 0: 'zz'"));
            Assert.That(Fails("int('1', 1)").Problem, Does.Contain("base must be"));
        }

        [Test]
        public void ThreeArgumentPow()
        {
            Assert.That(Eval("pow(2, 10, 1000)"), Is.EqualTo("24"));
            Assert.That(Eval("pow(-2, 3, 5)"), Is.EqualTo("2"));
            Assert.That(Eval("pow(2, 3, -5)"), Is.EqualTo("-2"));
            Assert.That(Eval("pow(5, 0, 1)"), Is.EqualTo("0"));
            Assert.That(Eval("pow(3, 200, 1000000007)"), Is.EqualTo("136318165"));
            Assert.That(Fails("pow(2, -1, 5)").Problem, Does.Contain("cannot be negative"));
            Assert.That(Fails("pow(2, 3, 0)").Problem, Does.Contain("cannot be 0"));
            Assert.That(Fails("pow(2.0, 3, 5)").TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void FloatOverflowInPowerRaises()
        {
            Assert.That(Fails("2.0 ** 1024").TypeName, Is.EqualTo("OverflowError"));
            Assert.That(Fails("10.0 ** 400").Problem, Does.Contain("out of range"));
            Assert.That(Eval("float('inf') ** 2"), Is.EqualTo("inf"));
            Assert.That(Eval("1e308 * 10"), Is.EqualTo("inf"));
        }

        [Test]
        public void MathFunctionsRaiseLikeCPython()
        {
            Assert.That(Fails("import math\nmath.exp(1000)").TypeName, Is.EqualTo("OverflowError"));
            Assert.That(Fails("import math\nmath.sin(float('inf'))").TypeName, Is.EqualTo("ValueError"));
            Assert.That(Fails("import math\nmath.cos(float('inf'))").TypeName, Is.EqualTo("ValueError"));
            Assert.That(Fails("import math\nmath.tan(float('-inf'))").TypeName, Is.EqualTo("ValueError"));
            Assert.That(Fails("import math\nmath.log(5, 1)").TypeName, Is.EqualTo("ZeroDivisionError"));
            Assert.That(Fails("import math\nmath.pow(0, -1)").TypeName, Is.EqualTo("ValueError"));
            Assert.That(Fails("import math\nmath.pow(10, 400)").TypeName, Is.EqualTo("OverflowError"));
            Assert.That(Fails("import math\nmath.pow(-1, 0.5)").TypeName, Is.EqualTo("ValueError"));
            Assert.That(Out("import math\nprint(math.exp(float('-inf')), math.pow(2, 0.5), math.pow(float('inf'), 2))"), Is.EqualTo("0.0 1.4142135623730951 inf"));
        }

        [Test]
        public void ExceptTupleMustNotNest()
        {
            var e = Fails("try:\n    1 / 0\nexcept ((ValueError,), ZeroDivisionError):\n    pass");
            Assert.That(e.TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void SplitMaxsplitMustBeAnInteger()
        {
            Assert.That(Fails("'a,b'.split(',', None)").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("'a,b'.split(maxsplit=None)").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Eval("'a b c'.split(None, 1)"), Is.EqualTo("['a', 'b c']"));
        }

        [Test]
        public void PrintAcceptsFlush()
        {
            Assert.That(Run("print('x', flush=True)"), Is.EqualTo("x\n"));
        }

        [Test]
        public void SplitlinesKeepEnds()
        {
            Assert.That(Eval("'a\\nb\\r\\nc'.splitlines(True)"), Is.EqualTo("['a\\n', 'b\\r\\n', 'c']"));
            Assert.That(Eval("'a\\r'.splitlines(True)"), Is.EqualTo("['a\\r']"));
            Assert.That(Eval("'a\\nb'.splitlines(False)"), Is.EqualTo("['a', 'b']"));
        }

        [Test]
        public void RangeReprAlwaysShowsStart()
        {
            Assert.That(Eval("range(5)"), Is.EqualTo("range(0, 5)"));
            Assert.That(Eval("range(2, 5)"), Is.EqualTo("range(2, 5)"));
            Assert.That(Eval("range(0, 10, 2)"), Is.EqualTo("range(0, 10, 2)"));
        }

        [Test]
        public void IndexTakesStartAndEnd()
        {
            Assert.That(Eval("[1, 2, 1, 2].index(2, 2)"), Is.EqualTo("3"));
            Assert.That(Eval("[1, 2, 1, 2].index(1, -2)"), Is.EqualTo("2"));
            Assert.That(Eval("(1, 2, 1).index(1, 1)"), Is.EqualTo("2"));
            Assert.That(Eval("[1, 2, 3].index(2, 0, 10)"), Is.EqualTo("1"));
            Assert.That(Fails("[1, 2, 3].index(1, 1)").TypeName, Is.EqualTo("ValueError"));
            Assert.That(Fails("[1, 2, 3].index(3, 0, 2)").TypeName, Is.EqualTo("ValueError"));
            Assert.That(Fails("[1].index(1, 5)").TypeName, Is.EqualTo("ValueError"));
        }

        [Test]
        public void ReversedAcceptsDicts()
        {
            Assert.That(Eval("list(reversed({'a': 1, 'b': 2}))"), Is.EqualTo("['b', 'a']"));
            Assert.That(Fails("reversed(5)").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("reversed(enumerate([1]))").TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void FormatNActsLikeD()
        {
            Assert.That(Eval("'{:n}'.format(1234)"), Is.EqualTo("1234"));
        }

        // ---- Deliberate differences from CPython (listed as unsupported in the README) ----

        [TestCase("2 ** 63")]
        [TestCase("9223372036854775807 + 1")]
        [TestCase("-9223372036854775807 - 2")]
        [TestCase("9223372036854775807 * 2")]
        [TestCase("10 ** 19")]
        [TestCase("3 ** 40")]
        [TestCase("int('9223372036854775808')")]
        [TestCase("int('-9223372036854775809')")]
        [TestCase("int(1e19)")]
        [TestCase("int(float('1e30'))")]
        [TestCase("round(1e20)")]
        [TestCase("abs(-9223372036854775807 - 1)")]
        [TestCase("-(-9223372036854775807 - 1)")]
        [TestCase("(-9223372036854775807 - 1) // -1")]
        public void IntegersAreSixtyFourBitAndOverflowRaises(string expr)
        {
            Assert.That(Fails("print(" + expr + ")").TypeName, Is.EqualTo("OverflowError"));
        }

        [TestCase("9223372036854775808")]
        [TestCase("99999999999999999999")]
        [TestCase("0xFFFFFFFFFFFFFFFFF")]
        public void HugeLiteralsAreSyntaxErrors(string literal)
        {
            var vm = new PyVm();
            Assert.Throws<CSharpPython.Syntax.PySyntaxError>(() => vm.Compile("x = " + literal));
        }

        [TestCase("(-8) ** 0.5")]
        [TestCase("(-2.0) ** 0.5")]
        public void NegativeBaseWithFractionalPowerIsAValueErrorNotComplex(string expr)
        {
            Assert.That(Fails("print(" + expr + ")").TypeName, Is.EqualTo("ValueError"));
        }

        [Test]
        public void DictViewsAreLists()
        {
            Assert.That(Eval("{'a': 1}.keys()"), Is.EqualTo("['a']"));
            Assert.That(Eval("{'a': 1}.values()"), Is.EqualTo("[1]"));
            Assert.That(Eval("{'a': 1}.items()"), Is.EqualTo("[('a', 1)]"));
        }

        [Test]
        public void UnsupportedSyntaxIsReportedClearly()
        {
            var vm = new PyVm();
            foreach (var source in new[] { "x = 1 @ 2", "await x" })
            {
                var e = Assert.Throws<CSharpPython.Syntax.PySyntaxError>(() => vm.Compile(source), source);
                Assert.That(e.Problem, Does.Contain("not supported"), source);
            }
        }

        [Test]
        public void SliceAssignmentOnTuplesAndStringsStillFails()
        {
            Assert.That(Fails("x = (1,)\nx[0:1] = [2]").Problem, Is.EqualTo("'tuple' object does not support item assignment"));
        }

        [Test]
        public void ModuleAndFunctionAttributesAreReadOnly()
        {
            Assert.That(Fails("import math\nmath.pi = 3").TypeName, Is.EqualTo("AttributeError"));
            Assert.That(Fails("def f():\n    pass\nf.x = 1").TypeName, Is.EqualTo("AttributeError"));
        }

        [Test]
        public void LambdaAndInvertAreAllowedAfterACommaInATuple()
        {
            Assert.That(Out("def f():\n    return 1, lambda: 2, ~3\nt = f()\nprint(t[0], t[1](), t[2])"), Is.EqualTo("1 2 -4"));
            Assert.That(Out("x = 1, ~2, lambda: 3\nprint(x[1], x[2]())"), Is.EqualTo("-3 3"));
            Assert.That(Out("def g():\n    yield 1, lambda: 2\nprint(next(g())[1]())"), Is.EqualTo("2"));
        }

        [Test]
        public void EllipsisIsAValueWithIdentityAndNoOtherBehaviour()
        {
            Assert.That(Out("print(... is Ellipsis, ... == ..., repr(...), type(...).__name__)"), Is.EqualTo("True True Ellipsis ellipsis"));
            Assert.That(Fails("... + 1").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("len(...)").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("...[0]").TypeName, Is.EqualTo("TypeError"));
            Assert.Throws<CSharpPython.Syntax.PySyntaxError>(() => new PyVm().Compile("x = .."));
            Assert.Throws<CSharpPython.Syntax.PySyntaxError>(() => new PyVm().Compile("x = ....a b"));
        }
    }
}
