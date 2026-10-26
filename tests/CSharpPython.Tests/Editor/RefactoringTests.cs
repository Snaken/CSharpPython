using System.Linq;
using CSharpPython.Editor;

namespace CSharpPython.Tests.Editor
{
    public class RefactoringTests
    {
        private static int[] Refs(string marked)
        {
            int cursor = marked.IndexOf('|');
            return Refactoring.FindReferences(marked.Remove(cursor, 1), cursor).Select(r => r.Offset).ToArray();
        }

        private static RenameResult Rename(string marked, string name)
        {
            int cursor = marked.IndexOf('|');
            return Refactoring.Rename(marked.Remove(cursor, 1), cursor, name);
        }

        private static string Renamed(string marked, string name)
        {
            var r = Rename(marked, name);
            Assert.That(r.Error, Is.Null);
            return r.NewSource;
        }

        [Test]
        public void FindsEveryUseOfAVariableIncludingReassignments()
        {
            string src = "x = 1\nx = x + 1\nprint(x)\n";
            var refs = Refactoring.FindReferences(src, 0);
            Assert.That(refs.Select(r => r.Offset), Is.EqualTo(new[] { 0, 6, 10, 22 }));
            Assert.That(refs.Select(r => r.IsDefinition), Is.EqualTo(new[] { true, true, false, false }));
            Assert.That(refs[2].ToString(), Is.EqualTo("2:5"));
            Assert.That(refs[0].ToString(), Is.EqualTo("1:1 (definition)"));
            Assert.That((refs[1].Line, refs[1].Column, refs[1].Length), Is.EqualTo((2, 1, 1)));
        }

        [Test]
        public void ReferencesFromAUseAndFromTheDefinitionAgree()
        {
            Assert.That(Refs("def go():\n    pass\ng|o()\ngo()\n"), Is.EqualTo(new[] { 4, 19, 24 }));
            Assert.That(Refs("def g|o():\n    pass\ngo()\ngo()\n"), Is.EqualTo(new[] { 4, 19, 24 }));
        }

        [Test]
        public void LocalsAreKeptApartFromGlobalsOfTheSameName()
        {
            string src = "x = 1\ndef f():\n    x = 2\n    return x\nprint(x)\n";
            Assert.That(Refactoring.FindReferences(src, 0).Select(r => r.Offset), Is.EqualTo(new[] { 0, src.LastIndexOf('x') }));
            int inner = src.IndexOf("x = 2");
            Assert.That(Refactoring.FindReferences(src, inner).Count, Is.EqualTo(2));
        }

        [Test]
        public void ParametersIncludeKeywordArgumentsAtCallSites()
        {
            string src = "def f(a, b=2):\n    return b\nf(1, b=3)\ng(b=4)\no.f(b=5)\n";
            var r = Refactoring.Rename(src, src.IndexOf("b=2"), "count");
            Assert.That(r.Error, Is.Null);
            Assert.That(r.NewSource, Is.EqualTo("def f(a, count=2):\n    return count\nf(1, count=3)\ng(b=4)\no.f(b=5)\n"));
            Assert.That(r.Edits.Count, Is.EqualTo(3));
        }

        [Test]
        public void AttributesAndKeywordArgumentsOfOtherCallsAreLeftAlone()
        {
            string src = "x = 1\nobj.x = 2\nprint(x)\nf(x=3)\n";
            Assert.That(Renamed("x| = 1\nobj.x = 2\nprint(x)\nf(x=3)\n", "y"), Is.EqualTo("y = 1\nobj.x = 2\nprint(y)\nf(x=3)\n"));
            Assert.That(Refactoring.FindReferences(src, src.IndexOf("f(x=") + 2), Is.Empty);
        }

        [Test]
        public void ImportLinesAreNotTouchedByAVariableRename()
        {
            Assert.That(Renamed("from m import a as b\nimport math\na| = 1\nprint(a)\n", "z"), Is.EqualTo("from m import a as b\nimport math\nz = 1\nprint(z)\n"));
        }

        [Test]
        public void RenamesFunctionsAndClasses()
        {
            Assert.That(Renamed("class Enemy:\n    pass\ne = Ene|my()\n", "Foe"), Is.EqualTo("class Foe:\n    pass\ne = Foe()\n"));
            Assert.That(Renamed("def f():\n    return f|\n", "g"), Is.EqualTo("def g():\n    return g\n"));
        }

        [Test]
        public void RenamingToTheSameNameChangesNothing()
        {
            var r = Rename("x| = 1\n", "x");
            Assert.That(r.Success, Is.True);
            Assert.That(r.NewSource, Is.EqualTo("x = 1\n"));
            Assert.That(r.Edits, Is.Empty);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("1x")]
        [TestCase("a b")]
        [TestCase("class")]
        [TestCase("a.b")]
        [TestCase("a\"")]
        public void InvalidNewNamesAreRefused(string name)
        {
            var r = Rename("x| = 1\n", name);
            Assert.That(r.Success, Is.False);
            Assert.That(r.Error, Does.Contain("not a valid name"));
            Assert.That(r.NewSource, Is.EqualTo("x = 1\n"));
        }

        [Test]
        public void NamesAlreadyInUseAreRefused()
        {
            Assert.That(Rename("x| = 1\ny = 2\n", "y").Error, Does.Contain("already in use"));
            Assert.That(Rename("x| = 1\nprint(x)\n", "print").Error, Does.Contain("already in use"));
            Assert.That(Rename("x| = 1\ndef f():\n    y = 1\n", "y").Error, Does.Contain("already in use"));
            Assert.That(Rename("def f(a|):\n    b = 1\n", "b").Error, Does.Contain("already in use"));
            Assert.That(Rename("x| = 1\nf(y=2)\n", "y").Error, Is.Null);
            Assert.That(Rename("x| = 1\no.y = 2\n", "y").Error, Is.Null);
        }

        [Test]
        public void ImportsMethodsAndAttributesAreRefused()
        {
            Assert.That(Rename("import ma|th\n", "m").Error, Does.Contain("imported"));
            Assert.That(Rename("from m import a|\n", "b").Error, Does.Contain("imported"));
            Assert.That(Rename("class A:\n    def run|(self):\n        pass\n", "go").Error, Does.Contain("Methods"));
            Assert.That(Rename("class A:\n    hp| = 3\n", "life").Error, Does.Contain("Methods"));
        }

        [Test]
        public void MethodParametersAreRenamedUnlessUsedAsKeywords()
        {
            Assert.That(Renamed("class A:\n    def run(self, n|):\n        return n\n", "m"), Is.EqualTo("class A:\n    def run(self, m):\n        return m\n"));
            Assert.That(Rename("class A:\n    def run(self, n|):\n        return n\nA().run(n=1)\n", "m").Error, Does.Contain("keyword"));
        }

        [Test]
        public void GlobalAndNonlocalDeclarationsAreRefused()
        {
            Assert.That(Rename("x| = 1\ndef f():\n    global x\n    x = 2\n", "y").Error, Does.Contain("global"));
            Assert.That(Rename("def f():\n    x| = 1\n    def g():\n        nonlocal x\n        x = 2\n", "y").Error, Does.Contain("nonlocal"));
            Assert.That(Rename("x| = 1\ndef f():\n    global z\n", "y").Error, Is.Null);
        }

        [Test]
        public void NothingToRenameIsExplained()
        {
            foreach (var marked in new[] { "|\n", "x = 1 |\n", "print|(1)\n", "o.na|me = 1\n", "f(na|me=1)\n", "undefined_name| + 1\n" })
            {
                var r = Rename(marked, "y");
                Assert.That(r.Success, Is.False, marked);
                Assert.That(r.Error, Does.Contain("no renamable"), marked);
            }
        }

        [Test]
        public void HandlesNullAndOutOfRangeInput()
        {
            Assert.That(Refactoring.FindReferences(null, 5), Is.Empty);
            Assert.That(Refactoring.FindReferences("x = 1", -5).Count, Is.EqualTo(1));
            Assert.That(Refactoring.Rename(null, 0, "y").Success, Is.False);
            Assert.That(Refactoring.FindReferences("x = 1", 500), Is.Empty);
        }

        [Test]
        public void WorksOnHalfTypedCode()
        {
            Assert.That(Renamed("x| = (1 +\nprint(x", "y"), Is.EqualTo("y = (1 +\nprint(y"));
        }

        [Test]
        public void RenamedScriptsStillCompileAndRunTheSame()
        {
            string src = "total = 0\ndef add(amount, times=1):\n    global_total = total\n    for i in range(times):\n        global_total += amount\n    return global_total\nresult = add(5, times=2)\nprint(result)\n";
            string before = Vm.Py.Run(src);
            string renamed = Renamed(src.Replace("add(amount", "a|dd(amount"), "plus");
            renamed = Renamed(renamed.Replace("amount, times", "am|ount, times"), "step");
            renamed = Renamed(renamed.Replace("result =", "res|ult ="), "answer");
            Assert.That(renamed, Does.Contain("def plus(step, times=1)").And.Contain("answer = plus(5, times=2)").And.Contain("print(answer)"));
            Assert.That(Vm.Py.Run(renamed), Is.EqualTo(before));
        }
    }
}
