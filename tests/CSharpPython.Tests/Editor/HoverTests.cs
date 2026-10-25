using CSharpPython.Editor;

namespace CSharpPython.Tests.Editor
{
    public class HoverTests
    {
        private static HoverInfo At(string marked, PyVm vm = null)
        {
            int cursor = marked.IndexOf('|');
            return Hover.Get(marked.Remove(cursor, 1), cursor, vm);
        }

        [Test]
        public void DescribesAFunctionWithItsDocstring()
        {
            var h = At("def move(target, speed=1.0):\n    \"\"\"Moves the thing.\n\n    Fast.\n    \"\"\"\n    pass\nmo|ve(1)\n");
            Assert.That(h.Label, Is.EqualTo("def move(target, speed=1.0)"));
            Assert.That(h.Origin, Is.EqualTo("script"));
            Assert.That(h.Documentation, Is.EqualTo("Moves the thing.\n\nFast."));
            Assert.That(h.Text, Is.EqualTo("def move(target, speed=1.0)\n(script)\n\nMoves the thing.\n\nFast."));
            Assert.That(h.ToString(), Is.EqualTo(h.Label));
            Assert.That((h.Offset, h.Length), Is.EqualTo((h.Offset, 4)));
        }

        [Test]
        public void FunctionsWithoutDocstringsHaveNone()
        {
            Assert.That(At("def f():\n    x = \"not a doc\"\n    return x\nf|()\n").Documentation, Is.Null);
            Assert.That(At("def f():\n    \"doc\" + \"x\"\nf|()\n").Documentation, Is.Null);
            Assert.That(At("def f(): \"inline\"\nf|()\n").Documentation, Is.Null);
            var h = At("def f():\n    pass\nf|()\n");
            Assert.That(h.Documentation, Is.Null);
            Assert.That(h.Text, Is.EqualTo("def f()\n(script)"));
        }

        [Test]
        public void OneLineDocstringIsKept()
        {
            Assert.That(At("def f():\n    '''Short.'''\n    pass\nf|()\n").Documentation, Is.EqualTo("Short."));
        }

        [Test]
        public void DescribesClassesAndMethods()
        {
            var cls = At("class Enemy:\n    \"\"\"A foe.\"\"\"\n    def __init__(self, hp):\n        self.hp = hp\ne = Ene|my(3)\n");
            Assert.That(cls.Label, Is.EqualTo("class Enemy(hp)"));
            Assert.That(cls.Documentation, Is.EqualTo("A foe."));
            var method = At("class A:\n    def go|(self):\n        pass\n");
            Assert.That(method.Label, Is.EqualTo("def go(self)"));
            Assert.That(method.Origin, Is.EqualTo("method of A"));
        }

        [Test]
        public void DescribesVariablesAndParameters()
        {
            Assert.That(At("hp = 3\nprint(h|p)\n").Label, Is.EqualTo("hp: int"));
            Assert.That(At("hp = 3\nprint(h|p)\n").Origin, Is.EqualTo("variable"));
            Assert.That(At("x = 1\nx = \"a\"\nprint(x|)\n").Label, Is.EqualTo("x"));
            var inner = At("def f(a):\n    n = []\n    return n|\n");
            Assert.That(inner.Label, Is.EqualTo("n: list"));
            Assert.That(inner.Origin, Is.EqualTo("variable in f"));
            var p = At("def f(a, b):\n    return b|\n");
            Assert.That((p.Label, p.Origin), Is.EqualTo(("b", "parameter of f")));
        }

        [Test]
        public void DescribesImportsBuiltinsAndModuleMembers()
        {
            Assert.That(At("import math\nma|th.sqrt(4)\n").Label, Is.EqualTo("import math"));
            var sqrt = At("import math\nmath.sq|rt(4)\n");
            Assert.That(sqrt.Origin, Is.EqualTo("math"));
            Assert.That(sqrt.Label, Does.StartWith("sqrt("));
            var from = At("from math import floor\nfl|oor(1.5)\n");
            Assert.That(from.Origin, Is.EqualTo("math"));
            var len = At("print(le|n([1]))\n");
            Assert.That((len.Label, len.Origin), Is.EqualTo(("len(obj)", "built-in")));
            Assert.That(At("x = Value|Error(1)\n").Origin, Is.EqualTo("exception"));
        }

        [Test]
        public void DescribesMethodsOfKnownTypes()
        {
            var h = At("s = \"abc\"\ns.up|per()\n");
            Assert.That(h.Origin, Is.EqualTo("str method"));
            Assert.That(h.Label, Is.EqualTo("upper()"));
            Assert.That(At("names = []\nnames.app|end(1)\n").Origin, Is.EqualTo("list method"));
        }

        [Test]
        public void DescribesHostModuleFunctions()
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m => m.Function("spawn", (Value a, Value b) => Value.None).Describe("spawn", "kind, count=1"));
            var h = At("import game\ngame.sp|awn(\"orc\")\n", vm);
            Assert.That(h.Label, Is.EqualTo("spawn(kind, count=1)"));
            Assert.That(h.Origin, Is.EqualTo("game"));
            Assert.That(At("print(sp|awn)\n", vm), Is.Null);
        }

        [Test]
        public void UnknownThingsHaveNoHover()
        {
            foreach (var marked in new[] { "|\n", "x = 1 |\n", "if| True:\n    pass\n", "undefined_name|\n", "o.mystery|\n", "f(na|me=1)\n", "x = 12|3\n", "def |\n", "class |\n" })
                Assert.That(At(marked), Is.Null, marked);
        }

        [Test]
        public void HoverOnKeywordArgumentOfAKnownFunctionIsEmptyNotTheVariable()
        {
            Assert.That(At("name = 1\nf(na|me=2)\n"), Is.Null);
        }

        [Test]
        public void HandlesNullAndOutOfRangeInput()
        {
            Assert.That(Hover.Get(null, 3), Is.Null);
            Assert.That(Hover.Get("x = 1", -4), Is.Not.Null);
            Assert.That(Hover.Get("x = 1", 100), Is.Null);
        }

        [Test]
        public void WorksOnHalfTypedCode()
        {
            var h = At("def f(a:\n    pass\nf|(\n");
            Assert.That(h, Is.Not.Null);
        }

        [Test]
        public void DecoratedDefinitionsStillResolve()
        {
            Assert.That(At("def deco(f):\n    return f\n@deco\ndef move(a):\n    pass\nmo|ve(1)\n").Label, Is.EqualTo("def move(a)"));
            Assert.That(At("@prop|erty\ndef f(self): pass\n").Label, Does.StartWith("property"));
        }

        [Test]
        public void DottedImportsHoverAsTheirModule()
        {
            Assert.That(At("import game.ai as br|ain\n", Dotted()).Label, Is.EqualTo("import game.ai"));
            Assert.That(At("import game.ai\ngam|e.ai.think()\n", Dotted()).Label, Is.EqualTo("import game"));
        }

        private static PyVm Dotted()
        {
            var vm = new PyVm();
            vm.RegisterModule("game.ai", m => m.Function("think", () => Value.None));
            return vm;
        }
    }
}
