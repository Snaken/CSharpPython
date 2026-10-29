using System;

namespace CSharpPython.Tests.Runtime
{
    public class PinCompatibilityTests
    {
        private static PinType P(string text) => PinType.Parse(text);

        [TestCase("int", "int")]
        [TestCase("str", "str")]
        [TestCase("bool", "bool")]
        [TestCase("float", "float")]
        [TestCase("int", "float")]
        [TestCase("Entity", "Entity")]
        [TestCase("list[int]", "list[int]")]
        [TestCase("list[list[str]]", "list[list[str]]")]
        public void MatchingTypesFit(string from, string to)
        {
            Assert.That(PinCompatibility.CanConnect(P(from), P(to)), Is.True);
        }

        [TestCase("any", "int")]
        [TestCase("int", "any")]
        [TestCase("any", "Entity")]
        [TestCase("Entity", "any")]
        [TestCase("list[int]", "any")]
        [TestCase("any", "list[str]")]
        [TestCase("list[int]", "list[any]")]
        [TestCase("list[any]", "list[str]")]
        public void AnyFitsOnEitherSide(string from, string to)
        {
            Assert.That(PinCompatibility.CanConnect(P(from), P(to)), Is.True);
        }

        [TestCase("float", "int")]
        [TestCase("bool", "int")]
        [TestCase("int", "bool")]
        [TestCase("int", "str")]
        [TestCase("str", "int")]
        [TestCase("Entity", "Character")]
        [TestCase("Entity", "int")]
        [TestCase("int", "Entity")]
        [TestCase("int", "list[int]")]
        [TestCase("list[int]", "int")]
        [TestCase("list[int]", "list[float]")]
        [TestCase("list[int]", "list[str]")]
        [TestCase("list[list[int]]", "list[list[str]]")]
        public void EveryOtherMismatchIsRefusedWithAReason(string from, string to)
        {
            var check = PinCompatibility.Check(P(from), P(to));
            Assert.That(check.Allowed, Is.False);
            Assert.That(check.Reason, Does.Contain(P(to).ToString()));
        }

        [Test]
        public void AllowedWiresHaveNoReason()
        {
            Assert.That(PinCompatibility.Check(PinType.Int, PinType.Float).Reason, Is.Null);
        }

        [Test]
        public void NoneHasNoValueToWire()
        {
            Assert.That(PinCompatibility.CanConnect(PinType.None, PinType.Any), Is.False);
            Assert.That(PinCompatibility.CanConnect(PinType.None, PinType.None), Is.False);
            Assert.That(PinCompatibility.Check(PinType.None, PinType.Int).Reason, Does.Contain("none"));
        }

        [Test]
        public void ReasonNamesTheListElementTypes()
        {
            var reason = PinCompatibility.Check(P("list[int]"), P("list[float]")).Reason;
            Assert.That(reason, Does.Contain("list[int]").And.Contain("list[float]"));
        }

        [Test]
        public void NullTypesAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => PinCompatibility.Check(null, PinType.Int));
            Assert.Throws<ArgumentNullException>(() => PinCompatibility.Check(PinType.Int, null));
        }
    }
}
