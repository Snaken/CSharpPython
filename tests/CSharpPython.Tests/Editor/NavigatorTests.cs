using CSharpPython.Editor;

namespace CSharpPython.Tests.Editor
{
    public class NavigatorTests
    {
        // The text with '|' marking the cursor.
        private static DefinitionLocation At(string marked)
        {
            int cursor = marked.IndexOf('|');
            return Navigator.FindDefinition(marked.Remove(cursor, 1), cursor);
        }

        [Test]
        public void FindsAFunction()
        {
            var d = At("def move(a):\n    pass\nmo|ve(1)");
            Assert.That(d.Name, Is.EqualTo("move"));
            Assert.That(d.Kind, Is.EqualTo(DefinitionKind.Function));
            Assert.That(d.Offset, Is.EqualTo(4));
            Assert.That(d.Length, Is.EqualTo(4));
            Assert.That((d.Line, d.Column), Is.EqualTo((1, 5)));
            Assert.That(d.ToString(), Is.EqualTo("move (Function) at 1:5"));
        }

        [Test]
        public void CursorMayBeAnywhereOnTheWord()
        {
            string src = "x = 1\nprint(x)\n";
            int use = src.IndexOf("x)");
            foreach (int offset in new[] { use, use + 1 })
                Assert.That(Navigator.FindDefinition(src, offset).Offset, Is.EqualTo(0), "offset " + offset);
        }

        [Test]
        public void FindsVariablesAndTheLatestAssignmentBeforeTheUse()
        {
            var d = At("x = 1\nx = 2\nprint(x|)\nx = 3\n");
            Assert.That(d.Kind, Is.EqualTo(DefinitionKind.Variable));
            Assert.That(d.Line, Is.EqualTo(2));
            Assert.That(At("print(x|)\nx = 3\nx = 4\n").Line, Is.EqualTo(2));
        }

        [Test]
        public void FindsParametersIncludingStarOnes()
        {
            var d = At("def f(a, *rest, **opts):\n    return rest|\n");
            Assert.That(d.Kind, Is.EqualTo(DefinitionKind.Parameter));
            Assert.That(d.Offset, Is.EqualTo(10));
            Assert.That(At("def f(a, *rest, **opts):\n    return opts|\n").Kind, Is.EqualTo(DefinitionKind.Parameter));
            Assert.That(At("def f(a, b):\n    return b|\n").Line, Is.EqualTo(1));
        }

        [Test]
        public void ParametersShadowGlobals()
        {
            var d = At("x = 1\ndef f(x):\n    return x|\n");
            Assert.That(d.Kind, Is.EqualTo(DefinitionKind.Parameter));
            Assert.That(d.Line, Is.EqualTo(2));
            Assert.That(At("x = 1\ndef f(x):\n    pass\nprint(x|)\n").Kind, Is.EqualTo(DefinitionKind.Variable));
        }

        [Test]
        public void LocalsShadowOuterNames()
        {
            var d = At("v = 1\ndef f():\n    v = 2\n    return v|\n");
            Assert.That(d.Line, Is.EqualTo(3));
        }

        [Test]
        public void FindsLoopVariablesAndExceptionNames()
        {
            Assert.That(At("for i in range(3):\n    print(i|)\n").Line, Is.EqualTo(1));
            Assert.That(At("try:\n    pass\nexcept ValueError as err:\n    print(err|)\n").Line, Is.EqualTo(3));
        }

        [Test]
        public void FindsImports()
        {
            var m = At("import math\nprint(math|.pi)\n");
            Assert.That(m.Kind, Is.EqualTo(DefinitionKind.Module));
            Assert.That(m.Line, Is.EqualTo(1));
            var alias = At("import math as m\nprint(m|.pi)\n");
            Assert.That(alias.Name, Is.EqualTo("m"));
            Assert.That(alias.Offset, Is.EqualTo(15));
            var imported = At("from math import sqrt\nsqrt|(4)\n");
            Assert.That(imported.Kind, Is.EqualTo(DefinitionKind.ImportedName));
        }

        [Test]
        public void NestedFunctionsAreFoundFromInsideOnly()
        {
            Assert.That(At("def outer():\n    def inner():\n        pass\n    inner|()\n").Line, Is.EqualTo(2));
            Assert.That(At("def outer():\n    def inner():\n        pass\ninner|()\n"), Is.Null);
        }

        [Test]
        public void DefinitionOfADefinitionIsItself()
        {
            var d = At("def fo|o():\n    pass\n");
            Assert.That(d.Offset, Is.EqualTo(4));
        }

        [Test]
        public void ReturnsNullWhenThereIsNothingToFind()
        {
            Assert.That(At("print|(1)\n"), Is.Null);              // built-in
            Assert.That(At("import math\nmath.sq|rt(4)\n"), Is.Null); // module member
            Assert.That(At("s = 'a'\ns.up|per()\n"), Is.Null);      // method
            Assert.That(At("x = 1\n|\n"), Is.Null);                // not on a name
            Assert.That(At("x = |1\n"), Is.Null);                   // a number
            Assert.That(At("unknown|\n"), Is.Null);                 // never defined
            Assert.That(At("x = 'ab|c'\n"), Is.Null);               // inside a string
            Assert.That(At("def f(a):\n    pass\nf(a|=1)\n"), Is.Null); // keyword argument label
            Assert.That(At("a = 1\nf(x, b|=1)\n"), Is.Null);
            Assert.That(At("a = 1\nf([1, b|=1])\n"), Is.Null);
        }

        [Test]
        public void DefaultsInDefHeadersAreNotKeywordArguments()
        {
            Assert.That(At("def f(a|=1):\n    pass\n").Kind, Is.EqualTo(DefinitionKind.Parameter));
        }

        [Test]
        public void KeywordArgumentsInNestedCallsAreNotDefinitions()
        {
            Assert.That(At("n = 1\nprint(f(1), n|=2)\n"), Is.Null);
            Assert.That(At("n = 1\nx = [f(g(1), n|=2)]\n"), Is.Null);
            Assert.That(At("n = 1\nx = (n|=2)\n"), Is.Not.Null);
        }

        [Test]
        public void NeverThrowsOnOddInput()
        {
            foreach (var text in new[] { "", "(", "def", "def f(", "x =", "'abc", "f(a=", "import", "from", "a.b.c", "a.", "def f(*", "for", "except" })
            {
                for (int i = -1; i <= text.Length + 1; i++)
                    Assert.DoesNotThrow(() => Navigator.FindDefinition(text, i), text + "@" + i);
            }
            Assert.DoesNotThrow(() => Navigator.FindDefinition(null, 0));
        }
    }
}
