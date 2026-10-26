using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class BitwiseTests
    {
        private static string Message(string source) => Fails(source).Problem;

        [Test]
        public void ShiftsRejectNegativeCounts()
        {
            Assert.That(Fails("1 << -1").TypeName, Is.EqualTo("ValueError"));
            Assert.That(Message("1 << -1"), Is.EqualTo("negative shift count"));
            Assert.That(Message("1 >> -2"), Is.EqualTo("negative shift count"));
        }

        [Test]
        public void LeftShiftsThatLeave64BitsBecomeBigInts()
        {
            Assert.That(Out("print(1 << 63, 1 << 64, 3 << 62, -3 << 62)"), Is.EqualTo("9223372036854775808 18446744073709551616 13835058055282163712 -13835058055282163712"));
            Assert.That(Out("print(0 << 100000, 1 << 62, -1 << 63, -2 << 62)"), Is.EqualTo("0 4611686018427387904 -9223372036854775808 -9223372036854775808"));
        }

        [Test]
        public void OperandErrorsNameTheOperator()
        {
            Assert.That(Message("1.5 & 1"), Is.EqualTo("unsupported operand type(s) for &: 'float' and 'int'"));
            Assert.That(Message("'a' | 1"), Is.EqualTo("unsupported operand type(s) for |: 'str' and 'int'"));
            Assert.That(Message("1 ^ None"), Is.EqualTo("unsupported operand type(s) for ^: 'int' and 'NoneType'"));
            Assert.That(Message("1 << 1.0"), Is.EqualTo("unsupported operand type(s) for <<: 'int' and 'float'"));
            Assert.That(Message("[] >> 1"), Is.EqualTo("unsupported operand type(s) for >>: 'list' and 'int'"));
            Assert.That(Message("~1.5"), Is.EqualTo("bad operand type for unary ~: 'float'"));
        }

        [TestCase("+", "int", "'a'", "str")]
        [TestCase("-", "int", "'a'", "str")]
        [TestCase("*", "float", "None", "NoneType")]
        [TestCase("/", "int", "'a'", "str")]
        [TestCase("//", "int", "[]", "list")]
        [TestCase("%", "int", "{}", "dict")]
        [TestCase("**", "int", "'a'", "str")]
        [TestCase("&", "int", "1.5", "float")]
        [TestCase("|", "int", "'a'", "str")]
        [TestCase("^", "int", "None", "NoneType")]
        [TestCase("<<", "int", "'a'", "str")]
        [TestCase(">>", "int", "[]", "list")]
        public void AugmentedAssignmentErrorsUseTheAssignmentSymbol(string op, string left, string rhs, string right)
        {
            string init = left == "float" ? "1.5" : "1";
            Assert.That(Message("x = " + init + "\nx " + op + "= " + rhs),
                Is.EqualTo("unsupported operand type(s) for " + op + "=: '" + left + "' and '" + right + "'"));
        }

        [Test]
        public void AugmentedAssignmentOnASetWithANonSetFails()
        {
            Assert.That(Message("s = {1}\ns |= [2]"), Is.EqualTo("unsupported operand type(s) for |=: 'set' and 'list'"));
            Assert.That(Message("s = {1}\ns -= [2]"), Is.EqualTo("unsupported operand type(s) for -=: 'set' and 'list'"));
        }

        [Test]
        public void RightHandErrorsInsideDunderMethodsAreNotRewritten()
        {
            Assert.That(Message("class A:\n    def __ior__(self, o):\n        return 1 | 'x'\na = A()\na |= 1"),
                Is.EqualTo("unsupported operand type(s) for |: 'int' and 'str'"));
        }

        [Test]
        public void PrecedenceFollowsPython()
        {
            Assert.That(Out("print(1 | 2 ^ 3 & 4 << 1 + 1, ~-1 | 0, 2 ** 3 << 1, -2 >> 1, 6 & 3 == 2)"), Is.EqualTo("3 0 16 -1 True"));
        }

        [Test]
        public void BitwiseOnBoolsKeepsBoolOnlyForBoolPairs()
        {
            Assert.That(Out("print(True & True, True | 0, ~True, True << 1)"), Is.EqualTo("True 1 -2 2"));
        }

        [Test]
        public void SetsGainOperators()
        {
            Assert.That(Out("a = {1, 2}\nb = a | {3}\nb |= {4}\nprint(a, b, a & b, a ^ b, b - a)"),
                Is.EqualTo("{1, 2} {1, 2, 3, 4} {1, 2} {3, 4} {3, 4}"));
        }

        [Test]
        public void WorksInsideEventHandlers()
        {
            var sb = new System.Text.StringBuilder();
            var vm = new PyVm { Output = s => sb.Append(s) };
            var task = vm.Start("flags = 0\ndef on_flag(bit):\n    global flags\n    flags ^= 1 << bit\n    print(flags, ~flags & 7)\n");
            task.RunUntilDone();
            task.QueueEvent("on_flag", Value.FromInt(1));
            task.QueueEvent("on_flag", Value.FromInt(2));
            task.QueueEvent("on_flag", Value.FromInt(1));
            task.RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("2 5\n6 1\n4 3\n"));
        }
    }
}
