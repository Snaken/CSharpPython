using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class SliceAssignTests
    {
        [Test]
        public void ErrorMessagesNameTheProblem()
        {
            Assert.That(Out("xs = list(range(6))\ntry:\n    xs[::2] = [1, 2]\nexcept ValueError as e:\n    print(e)"),
                Is.EqualTo("attempt to assign sequence of size 2 to extended slice of size 3"));
            Assert.That(Out("try:\n    [1][0:1] = 5\nexcept TypeError as e:\n    print(e)"), Is.EqualTo("can only assign an iterable"));
            Assert.That(Out("try:\n    (1, 2)[0:1] = [1]\nexcept TypeError as e:\n    print(e)"), Is.EqualTo("'tuple' object does not support item assignment"));
        }

        [Test]
        public void DictsRejectSliceKeysAsUnhashable()
        {
            Assert.That(Out("d = {}\ntry:\n    d[1:2] = 3\nexcept TypeError as e:\n    print('TypeError')"), Is.EqualTo("TypeError"));
        }

        [Test]
        public void SourceThatFailsMidWayLeavesTheListUntouched()
        {
            Assert.That(Out("def g():\n    yield 1\n    raise ValueError('boom')\nxs = [1, 2, 3]\ntry:\n    xs[0:2] = g()\nexcept ValueError:\n    pass\nprint(xs)"), Is.EqualTo("[1, 2, 3]"));
        }

        [Test]
        public void SliceAssignmentHappensAtRunTimeInsideLoops()
        {
            Assert.That(Out("xs = [0] * 6\nfor i in range(3):\n    xs[i * 2:i * 2 + 2] = [i, i]\nprint(xs)"), Is.EqualTo("[0, 0, 1, 1, 2, 2]"));
        }
    }
}
