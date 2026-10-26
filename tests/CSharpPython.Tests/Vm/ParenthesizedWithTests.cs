using CSharpPython.Syntax;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class ParenthesizedWithTests
    {
        [Test]
        public void EmptyParenthesesAreAnEmptyTupleContext()
        {
            Assert.That(Fails("with ():\n    pass").TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void UnterminatedItemListIsASyntaxError()
        {
            Assert.Throws<PySyntaxError>(() => Run("class R:\n    def __enter__(self): return 1\n    def __exit__(self, *a): return False\nwith (R() as a, R() as b:\n    pass"));
        }

        [Test]
        public void ParenthesizedItemsCannotBeFollowedByAs()
        {
            Assert.Throws<PySyntaxError>(() => Run("with (a as b, c as d) as e:\n    pass"));
        }

        [Test]
        public void ParenthesizedFirstExpressionStillWorks()
        {
            Assert.That(Out("class R:\n    def __enter__(self): return 5\n    def __exit__(self, *a): return False\nwith (R()) as x:\n    print(x)"), Is.EqualTo("5"));
        }

        [Test]
        public void DictUnionErrorsAndInPlaceUpdate()
        {
            Assert.That(Fails("{1: 1} | 5").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Out("d = {1: 1}\nalias = d\nd |= [(2, 2)]\nprint(alias, {1: 1} | {1: 2})"), Is.EqualTo("{1: 1, 2: 2} {1: 2}"));
        }
    }
}
