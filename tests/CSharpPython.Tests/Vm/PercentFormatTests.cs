using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class PercentFormatTests
    {
        [Test]
        public void AstralCharacterFromCodePoint()
        {
            Assert.That(Out("print(len('%c' % 0x1F600))"), Is.EqualTo("2"));
        }

        [Test]
        public void CodePointOutOfRange()
        {
            Assert.That(Fails("'%c' % 0x110000").TypeName, Is.EqualTo("OverflowError"));
        }

        [Test]
        public void HugeWidthAndPrecisionAreRejected()
        {
            Assert.That(Fails("'%99999999999d' % 1").TypeName, Is.EqualTo("ValueError"));
            Assert.That(Fails("'%.99999d' % 1").TypeName, Is.EqualTo("ValueError"));
        }

        [Test]
        public void StarArgumentMustBeInt()
        {
            Assert.That(Fails("'%*d' % (1.5, 2)").Problem, Does.Contain("* wants int"));
        }

        [Test]
        public void NegativeStarPrecisionIsZero()
        {
            Assert.That(Out("print('%.*f' % (-1, 2.5))"), Is.EqualTo("2"));
        }

        [Test]
        public void LengthModifiersAreIgnored()
        {
            Assert.That(Out("print('%ld %hd %Lf' % (1, 2, 3.0))"), Is.EqualTo("1 2 3.000000"));
        }

        [Test]
        public void MappingKeyWithNestedParentheses()
        {
            Assert.That(Out("print('%(a(b))s' % {'a(b)': 5})"), Is.EqualTo("5"));
        }

        [Test]
        public void AlternateFloatKeepsThePoint()
        {
            Assert.That(Out("print(format(3.0, '#.0f'), format(3.0, '#.0e'), '%#.0f' % 2.0)"), Is.EqualTo("3. 3.e+00 2."));
        }

        [Test]
        public void ErrorsCarryTheRightType()
        {
            Assert.That(Fails("'%s %s' % (1,)").Problem, Is.EqualTo("not enough arguments for format string"));
            Assert.That(Fails("'%s' % (1, 2)").Problem, Is.EqualTo("not all arguments converted during string formatting"));
            Assert.That(Fails("'%(a)s' % 5").Problem, Is.EqualTo("format requires a mapping"));
        }
    }
}
