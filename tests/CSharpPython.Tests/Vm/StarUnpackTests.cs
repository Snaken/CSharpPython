using CSharpPython.Syntax;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class StarUnpackTests
    {
        private static string Problem(string source) => Assert.Throws<PySyntaxError>(() => new PyVm().Compile(source)).Problem;

        [TestCase("*a = [1]", "starred assignment target must be in a list or tuple")]
        [TestCase("a, *b, *c = [1, 2, 3]", "multiple starred expressions in assignment")]
        [TestCase("[*a, *b] = [1, 2]", "multiple starred expressions in assignment")]
        [TestCase("x = *a", "cannot use starred expression here")]
        [TestCase("*a", "cannot use starred expression here")]
        [TestCase("print((*a))", "cannot use starred expression here")]
        [TestCase("a, **b = 1, 2", "")]
        [TestCase("for *a in [[1]]:\n    pass", "starred assignment target must be in a list or tuple")]
        [TestCase("{*a: 1}", "cannot use a starred expression as a dictionary key")]
        public void MisplacedStarsAreSyntaxErrors(string source, string expected)
        {
            string problem = Problem(source);
            if (expected.Length > 0) Assert.That(problem, Is.EqualTo(expected));
        }

        [Test]
        public void StarsInComprehensionHeadsAreRejected()
        {
            Assert.That(Problem("[*a for a in b]"), Is.Not.Empty);
            Assert.That(Problem("{*a for a in b}"), Is.Not.Empty);
            Assert.That(Problem("{**a for a in b}"), Is.Not.Empty);
        }

        [Test]
        public void RuntimeErrorMessagesMatchCPython()
        {
            Assert.That(Fails("[*5]").Problem, Is.EqualTo("Value after * must be an iterable, not int"));
            Assert.That(Fails("(*None,)").Problem, Is.EqualTo("Value after * must be an iterable, not NoneType"));
            Assert.That(Fails("{*3}").Problem, Is.EqualTo("Value after * must be an iterable, not int"));
            Assert.That(Fails("{**[1]}").Problem, Is.EqualTo("'list' object is not a mapping"));
            Assert.That(Fails("a, *b, c = [1]").Problem, Is.EqualTo("not enough values to unpack (expected at least 2, got 1)"));
            Assert.That(Fails("a, *b = 5").Problem, Is.EqualTo("cannot unpack non-iterable int object"));
            Assert.That(Fails("def f(*a):\n    pass\nf(*5)").Problem, Is.EqualTo("argument after * must be an iterable, not int"));
        }

        [Test]
        public void UnhashableItemsInSetsAndDictsFail()
        {
            Assert.That(Fails("{*[[1]]}").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("{**{}, [1]: 2}").TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void DictMergeCopiesSoLaterChangesDoNotLeak()
        {
            Assert.That(Out("a = {'x': [1]}\nb = {**a}\nb['x'].append(2)\nb['y'] = 0\nprint(a, b)"), Is.EqualTo("{'x': [1, 2]} {'x': [1, 2], 'y': 0}"));
        }

        [Test]
        public void MergingADictIntoItselfWhileBuildingIsSafe()
        {
            Assert.That(Out("a = {1: 2}\na = {**a, **a, 3: 4}\nprint(a)"), Is.EqualTo("{1: 2, 3: 4}"));
        }

        [Test]
        public void StarTargetsMayBeSubscriptsAndSlices()
        {
            Assert.That(Out("xs = [0, 0, 0, 0]\nxs[0], *xs[1:3], xs[3] = 'a', 'b', 'c', 'd'\nprint(xs)"), Is.EqualTo("['a', 'b', 'c', 'd']"));
        }

        [Test]
        public void ParserBuildsStarNodes()
        {
            Assert.That(Syntax.AstPrinter.Module("a, *b = c\n"), Does.Contain("(* b)"));
            Assert.That(Syntax.AstPrinter.Module("x = {**a, 'k': 1}\n"), Does.Contain("**a"));
        }
    }
}
