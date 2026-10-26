using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class DictViewTests
    {
        [Test]
        public void IsInstanceDistinguishesTheThreeViews()
        {
            Assert.That(Out("d = {1: 2}\nprint(isinstance(d.keys(), type(d.keys())), isinstance(d.items(), type(d.keys())), isinstance(d.keys(), list), isinstance(d.values(), object))"),
                Is.EqualTo("True False False True"));
        }

        [Test]
        public void ViewsFromDifferentDictsDoNotCompareAsValues()
        {
            Assert.That(Out("a = {1: 2}\nb = {1: 2}\nv = a.values()\nprint(a.values() == b.values(), v == v, a.keys() == b.values(), a.keys() == a.items())"),
                Is.EqualTo("False True False False"));
        }

        [Test]
        public void KeysViewIteratorDetectsGrowth()
        {
            Assert.That(Fails("d = {1: 2}\nfor k in d.keys():\n    d[k + 1] = 0").TypeName, Is.EqualTo("RuntimeError"));
        }

        [Test]
        public void ViewAcceptsStarUnpackingAndAssignment()
        {
            Assert.That(Out("d = {1: 'a', 2: 'b'}\nx, y = d.keys()\nprint(x, y, [*d.values()], (*d.items(),))"), Is.EqualTo("1 2 ['a', 'b'] ((1, 'a'), (2, 'b'))"));
        }

        [Test]
        public void ViewOfItemsContainingUnhashableValuesCannotBeAnOperand()
        {
            Assert.That(Fails("d = {1: [2]}\nd.items() | {(1, 2)}").TypeName, Is.EqualTo("TypeError"));
        }
    }
}
