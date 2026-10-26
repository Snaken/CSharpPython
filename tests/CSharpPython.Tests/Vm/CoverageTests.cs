using System.Collections.Generic;
using System.Linq;
using CSharpPython.Compilation;
using CSharpPython.Syntax;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class CoverageTests
    {
        [Test]
        public void ChainedComparisonStopsAtFirstFalseLink()
        {
            Assert.That(Out(@"
calls = []
def f():
    calls.append(1)
    return 5
print(3 < 1 < f(), len(calls))
print(1 < 2 < f() < 4, len(calls))"), Is.EqualTo("False 0\nFalse 1"));
        }

        [Test]
        public void EventPendingWhenTheMainScriptEndsIsHandledThen()
        {
            var sb = new System.Text.StringBuilder();
            var vm = new PyVm { Output = s => sb.Append(s) };
            var task = vm.Start("def on_x():\n    print('handler')\nprint('main')");
            task.QueueEvent("on_x");
            Assert.That(task.Run(), Is.EqualTo(TaskState.Finished));
            Assert.That(sb.ToString(), Is.EqualTo("main\nhandler\n"));
        }

        [Test]
        public void UnknownExceptionAttributeIsAnError()
        {
            Assert.That(Fails("try:\n    1 / 0\nexcept Exception as e:\n    e.nothing").TypeName, Is.EqualTo("AttributeError"));
            Assert.That(Out("try:\n    1 / 0\nexcept Exception as e:\n    print(e.args)"), Is.EqualTo("('division by zero',)"));
            Assert.That(Out("e = ValueError()\nprint(e.args)"), Is.EqualTo("()"));
        }

        [Test]
        public void ConversionsOfValuesThatAlreadyHaveTheType()
        {
            Assert.That(Eval("int(5)"), Is.EqualTo("5"));
            Assert.That(Eval("int(True)"), Is.EqualTo("1"));
            Assert.That(Eval("float(2.5)"), Is.EqualTo("2.5"));
        }

        [Test]
        public void RoundOverflowIsReported()
        {
            Assert.That(Fails("round(1.7976931348623157e308, -308)").TypeName, Is.EqualTo("OverflowError"));
        }

        [Test]
        public void MathLogarithms()
        {
            Assert.That(Out("import math\nprint(math.log(8, 2), math.log2(8), math.log10(1000), round(math.log(math.e), 6))"), Is.EqualTo("3.0 3.0 3.0 1.0"));
        }

        [Test]
        public void DictCompactsAfterManyDeletions()
        {
            Assert.That(Out(@"
d = {}
for i in range(200):
    d[i] = i
for i in range(150):
    d.pop(i)
d['new'] = 1
keys = list(d)
print(len(d), keys[0], keys[-1], d[199], d['new'])"), Is.EqualTo("51 150 new 199 1"));
        }

        [Test]
        public void ChangingADictWhileIteratingIsAnError()
        {
            var e = Fails("d = {1: 1, 2: 2}\nfor k in d:\n    d[k + 10] = 0");
            Assert.That(e.TypeName, Is.EqualTo("RuntimeError"));
            Assert.That(e.Problem, Is.EqualTo("dictionary changed size during iteration"));
            Assert.That(Fails("d = {1: 1, 2: 2}\nfor k in d:\n    d.pop(k)").TypeName, Is.EqualTo("RuntimeError"));
            Assert.That(Fails("d = {1: 1, 2: 2}\nfor k in d:\n    d.clear()").Problem, Does.Contain("changed"));
            Assert.That(Out("d = {1: 1, 2: 2}\nfor k in d:\n    d[k] = 0\nprint(d)"), Is.EqualTo("{1: 0, 2: 0}"));
        }

        [Test]
        public void TuplesAreIterable()
        {
            Assert.That(Out("for x in (1, 2, 3):\n    print(x)"), Is.EqualTo("1\n2\n3"));
            Assert.That(Out("a, b = (1, 2)\nprint(a + b)\nprint(list(zip((1, 2), (3, 4))))"), Is.EqualTo("3\n[(1, 3), (2, 4)]"));
        }

        [Test]
        public void ReprsOfNonDataValues()
        {
            Assert.That(Eval("len"), Is.EqualTo("<built-in function len>"));
            Assert.That(Eval("[].append"), Is.EqualTo("<built-in method list.append>"));
            Assert.That(Eval("enumerate([1])"), Is.EqualTo("<iterator>"));
            Assert.That(Eval("ValueError"), Is.EqualTo("<class 'ValueError'>"));
            Assert.That(Eval("type_ if False else 1"), Is.EqualTo("1"));
            Assert.That(Value.Unbound.Repr(), Is.EqualTo("<unbound>"));
            Assert.That(Ops.MakeSlice(1, Value.None, 2).Repr(), Is.EqualTo("slice(1, None, 2)"));
            Assert.That(new NativeFunction("f", (c, a) => Value.None).ToString(), Is.EqualTo("<built-in function f>"));
        }

        [Test]
        public void IdentityComparison()
        {
            Assert.That(Eval("1 is 1"), Is.EqualTo("True"));
            Assert.That(Eval("True is True"), Is.EqualTo("True"));
            Assert.That(Eval("1.5 is 1.5"), Is.EqualTo("True"));
            Assert.That(Eval("1 is 1.0"), Is.EqualTo("False"));
            Assert.That(Eval("'a' is 'a'"), Is.EqualTo("True"));
            Assert.That(Eval("[1] is [1]"), Is.EqualTo("False"));
            Assert.That(Eval("None is not None"), Is.EqualTo("False"));
            Assert.That(Ops.Is(Value.FromHandle(1, 2), Value.FromHandle(1, 2)), Is.True);
            Assert.That(Ops.Is(Value.FromHandle(1, 2), Value.FromHandle(2, 2)), Is.False);
            Assert.That(Ops.Is(Value.Unbound, Value.Unbound), Is.True);
        }

        [Test]
        public void RangesCompareByContent()
        {
            Assert.That(Eval("range(3) == range(3)"), Is.EqualTo("True"));
            Assert.That(Eval("range(3) == range(0, 3, 1)"), Is.EqualTo("True"));
            Assert.That(Eval("range(3) == range(4)"), Is.EqualTo("False"));
            Assert.That(Eval("range(0) == range(5, 5)"), Is.EqualTo("True"));
            Assert.That(Eval("range(1) == range(0, 1, 3)"), Is.EqualTo("True"));
            Assert.That(Eval("range(0, 6, 2) == range(0, 6, 3)"), Is.EqualTo("False"));
            Assert.That(Eval("range(3) == [0, 1, 2]"), Is.EqualTo("False"));
        }

        [Test]
        public void FloatOrderingAndMinusOneDivision()
        {
            Assert.That(Eval("1.5 <= 2.5"), Is.EqualTo("True"));
            Assert.That(Eval("2.5 >= 2.5"), Is.EqualTo("True"));
            Assert.That(Eval("float('nan') <= 1"), Is.EqualTo("False"));
            Assert.That(Eval("5 // -1"), Is.EqualTo("-5"));
            Assert.That(Eval("5 % -1"), Is.EqualTo("0"));
            Assert.That(Eval("(-9223372036854775807 - 1) // -1"), Is.EqualTo("9223372036854775808"));
        }

        [TestCase("'{:8}'.format(1.5) + '|'", "     1.5|")]
        [TestCase("'{:,}'.format(1234567.891)", "1,234,567.891")]
        [TestCase("'{:,.2f}'.format(1234567.891)", "1,234,567.89")]
        [TestCase("'{:g}'.format(1234.5)", "1234.5")]
        [TestCase("'{:g}'.format(100000.0)", "100000")]
        [TestCase("'{:g}'.format(1e-5)", "1e-05")]
        [TestCase("'{:.3g}'.format(0.0001234)", "0.000123")]
        [TestCase("'{:f}'.format(5)", "5.000000")]
        [TestCase("'{:e}'.format(5)", "5.000000e+00")]
        [TestCase("'{:.1%}'.format(1)", "100.0%")]
        [TestCase("'{:+f}'.format(float('nan'))", "+nan")]
        [TestCase("'{:F}'.format(float('inf'))", "INF")]
        [TestCase("'{:E}'.format(12345.678)", "1.234568E+04")]
        [TestCase("'{:G}'.format(1e-10)", "1E-10")]
        public void FloatFormatting(string expr, string expected) => Assert.That(Eval(expr), Is.EqualTo(expected));

        [Test]
        public void FormatErrors()
        {
            Assert.That(Fails("format([1], 'x')").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("'{:d}'.format(1.5)").TypeName, Is.EqualTo("ValueError"));
            Assert.That(Fails("'{:z}'.format(1.5)").TypeName, Is.EqualTo("ValueError"));
            Assert.That(Fails("f'{1:q}'").TypeName, Is.EqualTo("ValueError"));
        }

        [Test]
        public void FunctionsCompareByIdentity()
        {
            Assert.That(Out("def f():\n    pass\ndef g():\n    pass\nprint(f == f, f == g, len == len, len == abs)"), Is.EqualTo("True False True False"));
            Assert.That(Out("xs = []\nprint(type_name(xs.append)) if False else print(1)"), Is.EqualTo("1"));
            Assert.That(Ops.GetAttr(Value.FromString("a"), "upper").TypeName, Is.EqualTo("method"));
        }

        [Test]
        public void MethodNamesAreListedForAutoComplete()
        {
            Assert.That(Methods.NamesOf(ValueKind.Str), Does.Contain("upper"));
            Assert.That(Methods.NamesOf(ValueKind.List), Does.Contain("append"));
            Assert.That(Methods.NamesOf(ValueKind.Dict), Does.Contain("get"));
            Assert.That(Methods.NamesOf(ValueKind.Tuple), Does.Contain("count"));
            Assert.That(Methods.NamesOf(ValueKind.Int), Is.Empty);
        }

        [Test]
        public void PyListHasAnEmptyConstructor()
        {
            var list = new PyList();
            list.Items.Add(1);
            Assert.That(Value.FromList(list).Repr(), Is.EqualTo("[1]"));
        }

        [Test]
        public void SubscriptingNonSequencesWithSlicesFails()
        {
            Assert.That(Fails("5[1:2]").Problem, Is.EqualTo("'int' object is not subscriptable"));
            Assert.That(Fails("None[::2]").TypeName, Is.EqualTo("TypeError"));
        }

        private static Script Handmade(params Instruction[] instructions)
        {
            var code = new Code
            {
                Name = "<module>",
                SourceName = "<hand>",
                Instructions = instructions,
                Lines = instructions.Select(_ => 1).ToArray(),
                Constants = new[] { Value.None },
                Names = new string[0],
                Functions = new Code[0],
                LocalNames = new string[0],
                ParamNames = new string[0],
                IsModule = true,
            };
            return new Script(code);
        }

        [Test]
        public void BareRaiseWithoutAnActiveExceptionIsARuntimeError()
        {
            var vm = new PyVm();
            var task = vm.Start(Handmade(new Instruction(Op.RaiseCurrent, 0)));
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Error));
            Assert.That(task.Error.TypeName, Is.EqualTo("RuntimeError"));
            Assert.That(task.Error.Problem, Is.EqualTo("No active exception to reraise"));
        }

        [Test]
        public void HandBuiltCodeRunsToTheEnd()
        {
            var vm = new PyVm();
            var task = vm.Start(Handmade(new Instruction(Op.LoadConst, 0), new Instruction(Op.Return, 0)));
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Finished));
        }
    }

    public class LexerErrorPathTests
    {
        private static string FirstProblem(string source)
        {
            Lexer.TokenizeTolerant(source, out List<Diagnostic> diagnostics);
            Assert.That(diagnostics, Is.Not.Empty, source);
            return diagnostics[0].Message;
        }

        [Test]
        public void CharacterAfterLineContinuation() =>
            Assert.That(FirstProblem("x = 1 \\ 2"), Does.Contain("unexpected character after line continuation"));

        [Test]
        public void RawFStringsAndBytesAreRejected()
        {
            Assert.That(FirstProblem("x = rf'a'"), Does.Contain("raw f-strings are not supported"));
            Assert.That(FirstProblem("x = b'a'"), Does.Contain("bytes literals are not supported"));
            Assert.That(FirstProblem("x = rb'a'"), Does.Contain("bytes literals are not supported"));
        }

        [Test]
        public void LoneBangIsInvalid() =>
            Assert.That(FirstProblem("x = !y"), Does.Contain("unexpected character '!'"));

        [TestCase("@")]
        public void MatrixOperatorIsUnsupported(string op) =>
            Assert.That(FirstProblem("x = 1 " + op + " 2"), Does.Contain("not supported yet"));

        [Test]
        public void UnbalancedBrackets()
        {
            Assert.That(FirstProblem("x = 1)"), Does.Contain("unmatched ')'"));
            Assert.That(FirstProblem("x = (1]"), Does.Contain("does not match opening '(' on line 1"));
        }

        [Test]
        public void HugeIntegerLiteral() =>
            Assert.That(FirstProblem("x = " + new string('1', 4301)), Does.Contain("too large"));
    }

    public class ParserErrorPathTests
    {
        [Test]
        public void FStringExpressionWithTrailingTokens()
        {
            var e = Assert.Throws<PySyntaxError>(() => Parser.ParseModule("x = f'{1 2}'"));
            Assert.That(e.Problem, Does.Contain("expected"));
        }

        [TestCase("x = (1 +\n")]
        [TestCase("def f(:\n    pass")]
        [TestCase("x = [1, 2")]
        [TestCase("if x:\npass")]
        [TestCase("x = 1 +")]
        [TestCase("else:\n    pass")]
        [TestCase("  x = 1")]
        [TestCase("x = $")]
        public void BrokenSourceReportsAnError(string source)
        {
            Assert.Throws<PySyntaxError>(() => Parser.ParseModule(source));
        }

        [TestCase("1 = x", "assign")]
        [TestCase("f() = 1", "assign")]
        [TestCase("x + 1 = 2", "assign")]
        [TestCase("-x = 1", "assign")]
        [TestCase("{} = 1", "assign")]
        [TestCase("(a if b else c) = 1", "assign")]
        public void InvalidAssignmentTargetsAreDescribed(string source, string word)
        {
            var e = Assert.Throws<PySyntaxError>(() => Parser.ParseModule(source));
            Assert.That(e.Problem, Does.Contain(word));
        }
    }
}
