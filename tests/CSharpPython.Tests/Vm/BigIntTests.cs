using System.Text;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class BigIntTests
    {
        // ---- The size limit (Python has none; this keeps one instruction from stalling the game) ------------------

        [TestCase("2 ** 100000")]
        [TestCase("2 ** (2 ** 70)")]
        [TestCase("(-2) ** (10 ** 20)")]
        [TestCase("1 << 100000")]
        [TestCase("1 << (2 ** 70)")]
        [TestCase("(2 ** 9000) * (2 ** 9000)")]
        [TestCase("10 ** 5000")]
        public void ResultsOverTheBitLimitRaiseOverflowError(string expr)
        {
            Assert.That(Fails("print(" + expr + ")").TypeName, Is.EqualTo("OverflowError"));
        }

        [Test]
        public void TheLimitIsCatchableAndTheScriptGoesOn()
        {
            Assert.That(Out("try:\n    x = 2 ** 100000\nexcept OverflowError as e:\n    print('too big')\nprint(2 ** 100)"), Is.EqualTo("too big\n1267650600228229401496703205376"));
        }

        [Test]
        public void OneZeroAndMinusOneRaisedToHugePowersNeedNoWork()
        {
            Assert.That(Out("print(1 ** (10 ** 100), 0 ** (10 ** 100), (-1) ** (10 ** 100), (-1) ** (10 ** 100 + 1), 0 ** 0, (-1) ** 0)"), Is.EqualTo("1 0 1 -1 1 1"));
        }

        [Test]
        public void TheLimitIsAnOption()
        {
            var vm = new PyVm(new VmOptions { MaxIntBits = 128 });
            var sb = new StringBuilder();
            vm.Output = s => sb.Append(s);
            vm.Start("print(2 ** 127)\nprint(-2 ** 127)\ntry:\n    print(2 ** 128)\nexcept OverflowError:\n    print('limit')\nx = 2 ** 64\nprint(x * x if False else 'ok')\ntry:\n    print(x * x * 2)\nexcept OverflowError:\n    print('limit again')").RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("170141183460469231731687303715884105728\n-170141183460469231731687303715884105728\nlimit\nok\nlimit again\n"));
        }

        [Test]
        public void ConvertingHugeIntegersToTextIsLimitedLikeCpython()
        {
            Assert.That(Fails("str(2 ** 16000)").TypeName, Is.EqualTo("ValueError"));
            Assert.That(Fails("print(2 ** 15000)").TypeName, Is.EqualTo("ValueError"));
            Assert.That(Fails("int('9' * 5000)").TypeName, Is.EqualTo("ValueError"));
            Assert.That(Fails("int('9' * 5000, 7)").TypeName, Is.EqualTo("ValueError"));
            Assert.That(Out("print(len(str(2 ** 14000)), len(hex(2 ** 16000)), len(str(int('1' * 4300))), int('1' + '0' * 4000, 16) > 0)"), Is.EqualTo("4215 4003 4300 True"));
        }

        [Test]
        public void IntegersJustUnderTheStringLimitConvert()
        {
            Assert.That(Out("print(len(str(10 ** 4299)), len(str(-(10 ** 4299))), len(repr(10 ** 4299 * 9)))"), Is.EqualTo("4300 4301 4300"));
            Assert.That(Fails("str(10 ** 4300)").TypeName, Is.EqualTo("ValueError"));
        }

        // ---- Host interop -------------------------------------------------------------------------------------------

        [Test]
        public void HostFunctionsReadingALongSeeOverflowForBigInts()
        {
            var vm = new PyVm();
            vm.RegisterModule("host", m => m
                .Function("half", x => x.AsInt() / 2)
                .Function("echo", x => x));
            var sb = new StringBuilder();
            vm.Output = s => sb.Append(s);
            var task = vm.Start("import host\nprint(host.half(2 ** 40))\nprint(host.echo(2 ** 80))\nhost.half(2 ** 80)");
            task.RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("549755813888\n1208925819614629174706176\n"));
            Assert.That(task.State, Is.EqualTo(TaskState.Error));
            Assert.That(task.Error.Message, Does.Contain("too large"));
        }

        [Test]
        public void BigIntsAreReadableThroughGlobals()
        {
            var vm = new PyVm();
            var task = vm.Start("x = 2 ** 70\ny = 5 ** 2");
            task.RunUntilDone();
            Assert.That(task.Globals["x"].ToString(), Is.EqualTo("1180591620717411303424"));
            Assert.That(task.Globals["x"].TypeName, Is.EqualTo("int"));
            Assert.That(task.Globals["x"].IsNumber, Is.True);
            Assert.That(task.Globals["x"].AsFloat(), Is.EqualTo(1180591620717411303424.0));
            Assert.That(task.Globals["y"].AsInt(), Is.EqualTo(25));
            Assert.That(task.Globals["x"].AsBigInteger(), Is.EqualTo(System.Numerics.BigInteger.Pow(2, 70)));
            Assert.That(task.Globals["y"].AsBigInteger(), Is.EqualTo(new System.Numerics.BigInteger(25)));
            Assert.That(Value.FromBigInteger(System.Numerics.BigInteger.Pow(2, 70)).ToString(), Is.EqualTo("1180591620717411303424"));
            Assert.That(Value.FromBigInteger(7).Kind, Is.EqualTo(ValueKind.Int));
            Assert.Throws<PyError>(() => Value.FromString("a").AsBigInteger());
            var e = Assert.Throws<PyError>(() => task.Globals["x"].AsInt());
            Assert.That(e.TypeName, Is.EqualTo("OverflowError"));
        }

        // ---- Details ------------------------------------------------------------------------------------------------

        [Test]
        public void OrderingWithNanIsFalse()
        {
            Assert.That(Out("n = float('nan')\nprint(2 ** 70 < n, 2 ** 70 >= n, 2 ** 70 == n, 2 ** 70 != n, n < 2 ** 70, 5 < n)"), Is.EqualTo("False False False True False False"));
        }

        [Test]
        public void ComparisonsWithInfinity()
        {
            Assert.That(Out("inf = float('inf')\nprint(2 ** 70 < inf, 2 ** 70 > -inf, -2 ** 70 > -inf, 2 ** 70 == inf, 10 ** 300 < inf)"), Is.EqualTo("True True True False True"));
        }

        [Test]
        public void IntegersAreExactAgainstFloatsBeyond53Bits()
        {
            Assert.That(Out("print(2 ** 53 + 1 == float(2 ** 53), 2 ** 53 + 1 > float(2 ** 53), 2 ** 63 - 1 < 2.0 ** 63, 9007199254740993 > 9007199254740992.0)"), Is.EqualTo("False True True True"));
        }

        [Test]
        public void RangeCannotTakeBigBounds()
        {
            Assert.That(Fails("range(2 ** 70)").TypeName, Is.EqualTo("OverflowError"));
            Assert.That(Fails("range(0, 5, 2 ** 70)").TypeName, Is.EqualTo("OverflowError"));
        }

        [Test]
        public void BigIntsWorkAsSetMembersAndDictKeysWithFloatTwins()
        {
            Assert.That(Out("d = {2 ** 80: 'a'}\nd[2.0 ** 80] = 'b'\nprint(d, len({2 ** 64, 2.0 ** 64, 18446744073709551616}))"), Is.EqualTo("{1208925819614629174706176: 'b'} 1"));
        }

        [Test]
        public void HashDunderMayReturnABigInt()
        {
            Assert.That(Out("class K:\n    def __hash__(self):\n        return 2 ** 70\n    def __eq__(self, o):\n        return True\nprint(len({K(), K()}))"), Is.EqualTo("1"));
        }

        [Test]
        public void LenDunderReturningABigIntIsAnError()
        {
            Assert.That(Fails("class K:\n    def __len__(self):\n        return 2 ** 70\nlen(K())").TypeName, Is.EqualTo("OverflowError"));
        }

        [Test]
        public void IndexDunderMayReturnABigInt()
        {
            Assert.That(Fails("class K:\n    def __index__(self):\n        return 2 ** 70\n[1][K()]").TypeName, Is.EqualTo("IndexError"));
            Assert.That(Out("class K:\n    def __index__(self):\n        return 2 ** 70\nprint(hex(K()), [1, 2][:K()])"), Is.EqualTo("0x400000000000000000 [1, 2]"));
        }

        [Test]
        public void ClassesCanConvertToBigInts()
        {
            Assert.That(Out("class K:\n    def __int__(self):\n        return 2 ** 70\nprint(int(K()), int(K()) + 1)"), Is.EqualTo("1180591620717411303424 1180591620717411303425"));
        }

        [Test]
        public void ConstantsAreSharedAndCompared()
        {
            Assert.That(Out("x = 2 ** 64\nprint(x is x, x == 18446744073709551616, 18446744073709551616 is 18446744073709551616)"), Does.StartWith("True True"));
        }

        [Test]
        public void NegativeZeroFromFloatModuloAndBigDivision()
        {
            Assert.That(Out("print(4.0 % -2, 0 % -2.5, 0 / -2 ** 70, -0.0 + 0 / 2 ** 70)"), Is.EqualTo("-0.0 -0.0 -0.0 0.0"));
        }

        [Test]
        public void ShiftsAndBitwiseOnBigNegatives()
        {
            Assert.That(Out("x = -2 ** 70 - 1\nprint(x >> 3, x >> 200, x << 3, x & 0xFF, x | 1, x ^ -1, ~x, 5 >> 2 ** 70, -5 >> 2 ** 70)"), Is.EqualTo("-147573952589676412929 -1 -9444732965739290427400 255 -1180591620717411303425 1180591620717411303424 1180591620717411303424 0 -1"));
        }

        [Test]
        public void NegativeShiftCountsRaise()
        {
            Assert.That(Fails("2 ** 70 << -1").TypeName, Is.EqualTo("ValueError"));
            Assert.That(Fails("2 ** 70 >> -1").TypeName, Is.EqualTo("ValueError"));
            Assert.That(Fails("2 ** 70 << -(2 ** 70)").TypeName, Is.EqualTo("ValueError"));
            Assert.That(Fails("2 ** 70 >> -(2 ** 70)").TypeName, Is.EqualTo("ValueError"));
            Assert.That(Out("print(0 << 2 ** 70, 2 ** 70 >> 2 ** 62)"), Is.EqualTo("0 0"));
        }

        [Test]
        public void MultiplicationAndPowerBoundaries()
        {
            Assert.That(Out("print(3037000500 * 3037000500, -3037000500 * 3037000500, 4611686018427387904 * 2, 4611686018427387904 * -2, (-2) ** 63, (-2) ** 64, 2 ** 62 * 2 ** 62)"),
                Is.EqualTo("9223372037000250000 -9223372037000250000 9223372036854775808 -9223372036854775808 -9223372036854775808 18446744073709551616 21267647932558653966460912964485513216"));
        }

        [Test]
        public void ParseIntKeepsLongFastPathAndBigPath()
        {
            Assert.That(Out("print(int('999999999999'), int('-1000000000000'), int('zzzzzzzzzzzz', 36), int('zzzzzzzzzzzzz', 36), int('-1000000000000000000000'))"),
                Is.EqualTo("999999999999 -1000000000000 4738381338321616895 170581728179578208255 -1000000000000000000000"));
        }

        [Test]
        public void GroupingSeparatorsFollowTheBase()
        {
            Assert.That(Out("print(format(255, '_b'), format(2 ** 70, '_o'), format(0xabcdef, '#_x'), format(2 ** 70, '_d'), format(1234567, ','))"),
                Is.EqualTo("1111_1111 2000_0000_0000_0000_0000_0000 0xab_cdef 1_180_591_620_717_411_303_424 1,234,567"));
            Assert.That(Fails("format(255, ',x')").Problem, Is.EqualTo("Cannot specify ',' with 'x'."));
            Assert.That(Fails("format(255, '_n')").Problem, Is.EqualTo("Cannot specify '_' with 'n'."));
            Assert.That(Fails("format(255, ',n')").TypeName, Is.EqualTo("ValueError"));
        }

        [Test]
        public void FormattingBigIntsHonoursSpecs()
        {
            Assert.That(Out("print(format(2 ** 70, ','), format(2 ** 70, '_x'), format(-2 ** 70, '#o'), format(2 ** 70, '>30'), format(2 ** 70, '^+30'), format(2 ** 70, '.3e'), format(2 ** 70, 'f'), '%020d|%-25x|' % (2 ** 70, 2 ** 70))"),
                Is.EqualTo("1,180,591,620,717,411,303,424 40_0000_0000_0000_0000 -0o200000000000000000000000         1180591620717411303424    +1180591620717411303424     1.181e+21 1180591620717411303424.000000 1180591620717411303424|400000000000000000       |"));
        }
    }
}
