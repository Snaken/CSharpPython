using System;
using System.Collections.Generic;
using System.Linq;
using CSharpPython.Editor;

namespace CSharpPython.Tests.Editor
{
    public class HighlighterTests
    {
        private static string Show(string source, PyVm vm = null) =>
            string.Join(" ", SyntaxHighlighter.Highlight(source, vm).Select(s => source.Substring(s.Start, s.Length).Replace("\n", "\\n") + ":" + s.Kind));

        [Test]
        public void ClassifiesTheBasicTokens()
        {
            Assert.That(Show("x = 1 + 2.5"), Is.EqualTo("x:Identifier =:Operator 1:Number +:Operator 2.5:Number"));
            Assert.That(Show("s = 'hi' + f\"{x}\""), Is.EqualTo("s:Identifier =:Operator 'hi':String +:Operator f\"{x}\":String"));
            Assert.That(Show("if x == None:"), Is.EqualTo("if:Keyword x:Identifier ==:Operator None:Constant ::Punctuation"));
            Assert.That(Show("a = [1, (2, 3)]"), Does.Contain("[:Punctuation").And.Contain(",:Punctuation"));
        }

        [Test]
        public void FunctionNamesMembersAndModulesGetTheirOwnKinds()
        {
            Assert.That(Show("def fly(a, b):\n    return a"),
                Does.StartWith("def:Keyword fly:FunctionName (:Punctuation a:Identifier").And.Contain("return:Keyword"));
            Assert.That(Show("xs.append(1)"), Is.EqualTo("xs:Identifier .:Punctuation append:Member (:Punctuation 1:Number ):Punctuation"));
            Assert.That(Show("import math"), Is.EqualTo("import:Keyword math:Module"));
            Assert.That(Show("import game as g"), Is.EqualTo("import:Keyword game:Module as:Keyword g:Identifier"));
            Assert.That(Show("from game import add, sub"), Is.EqualTo("from:Keyword game:Module import:Keyword add:Identifier ,:Punctuation sub:Identifier"));
        }

        [Test]
        public void ModuleHighlightingEndsWithTheStatement()
        {
            Assert.That(Show("import math\nx = 1"), Does.Contain("x:Identifier"));
            Assert.That(Show("import math\nmath.sqrt(4)"), Does.Contain("math:Identifier").And.Contain("sqrt:Member"));
        }

        [Test]
        public void BuiltinsComeFromTheVm()
        {
            Assert.That(Show("print(len(x))"), Is.EqualTo("print:Builtin (:Punctuation len:Builtin (:Punctuation x:Identifier ):Punctuation ):Punctuation"));
            Assert.That(Show("raise ValueError('x')"), Does.Contain("ValueError:Builtin"));

            var vm = new PyVm();
            vm.SetBuiltin("spawn", Value.FromNative(new NativeFunction("spawn", (c, a) => Value.None)));
            Assert.That(Show("spawn()", vm), Does.StartWith("spawn:Builtin"));
            Assert.That(Show("spawn()", new PyVm()), Does.StartWith("spawn:Identifier"));
        }

        [Test]
        public void AFunctionDefinedByTheScriptHidesTheBuiltinOfTheSameName()
        {
            Assert.That(Show("def print(x):\n    pass\nprint(1)"), Does.Contain("print:FunctionName").And.Contain("print:Identifier"));
        }

        [Test]
        public void CommentsAreFoundEvenThoughTheLexerSkipsThem()
        {
            Assert.That(Show("x = 1  # set x"), Is.EqualTo("x:Identifier =:Operator 1:Number # set x:Comment"));
            Assert.That(Show("# top\nx = 1"), Is.EqualTo("# top:Comment x:Identifier =:Operator 1:Number"));
            Assert.That(Show("if a:\n    # inside\n    b = 1\n# after"), Does.Contain("# inside:Comment").And.Contain("# after:Comment"));
            Assert.That(Show("x = '#not a comment'"), Is.EqualTo("x:Identifier =:Operator '#not a comment':String"));
            Assert.That(Show("x = 1 #a\n#b\n\n#c"), Is.EqualTo("x:Identifier =:Operator 1:Number #a:Comment #b:Comment #c:Comment"));
        }

        [Test]
        public void UnsupportedKeywordsAndBadTextAreFlagged()
        {
            Assert.That(Show("await a"), Does.StartWith("await:UnsupportedKeyword"));
            Assert.That(Show("x = 'abc"), Does.Contain(":Error"));
            Assert.That(Show("x = 5 $ 3"), Does.Contain("$:Error"));
        }

        [Test]
        public void ClassNamesAreHighlightedLikeFunctionNames()
        {
            Assert.That(Show("class Enemy(Base):"), Does.StartWith("class:Keyword Enemy:ClassName (:Punctuation Base:Identifier"));
            Assert.That(Show("class A:\n    pass\nx = A()"), Does.Contain("A:ClassName").And.Contain("x:Identifier =:Operator A:Identifier"));
        }

        [Test]
        public void EmptyAndNullSourceGiveNoSpans()
        {
            Assert.That(SyntaxHighlighter.Highlight(""), Is.Empty);
            Assert.That(SyntaxHighlighter.Highlight(null), Is.Empty);
            Assert.That(SyntaxHighlighter.Highlight("   \n\n  "), Is.Empty);
        }

        public const string Noise = "#'\"\\\n\t ()[]{}:,.=+*$@é0aZ_";

        public static IEnumerable<string> Samples()
        {
            yield return "x = 1\nprint(x)\n";
            yield return "def f(a, b=2):\n    # note\n    return a * b  # trailing\n\nprint(f(3))\n";
            yield return "import game\nfor i in range(3):\n    game.wait(2)\n    s = f'{i:03d}' + \"q\"\n";
            yield return "try:\n    x = 1 / 0\nexcept ZeroDivisionError as e:\n    print(e)\n";
            yield return "xs = [1, 2,\n      3]\nd = {'a': (1, 2)}\nt = '''multi\nline'''\n";
            yield return "class A:\n    pass\nlambda x: x\n";
            yield return "x = 'unterminated\ny = 2 $ 3 @ 4\n\tz = 5\n";
        }

        [TestCaseSource(nameof(Samples))]
        public void SpansAreOrderedDisjointAndInsideTheTextForEveryPrefix(string source)
        {
            for (int length = 0; length <= source.Length; length++)
                CheckSpans(source.Substring(0, length));
        }

        [Test]
        public void SpansHoldOnRandomlyDamagedSource()
        {
            var random = new Random(7);
            var samples = Samples().ToArray();
            for (int i = 0; i < 2000; i++)
            {
                var chars = samples[random.Next(samples.Length)].ToCharArray();
                int edits = 1 + random.Next(4);
                for (int e = 0; e < edits; e++) chars[random.Next(chars.Length)] = Noise[random.Next(Noise.Length)];
                CheckSpans(new string(chars));
            }
        }

        private static void CheckSpans(string source)
        {
            var spans = SyntaxHighlighter.Highlight(source);
            int end = 0;
            foreach (var s in spans)
            {
                Assert.That(s.Length, Is.GreaterThan(0), source);
                Assert.That(s.Start, Is.GreaterThanOrEqualTo(end), "overlap in: " + source);
                Assert.That(s.End, Is.LessThanOrEqualTo(source.Length), source);
                end = s.End;
            }
        }

        [Test]
        public void EveryVisibleCharacterIsCovered()
        {
            foreach (var source in Samples())
            {
                var covered = new bool[source.Length];
                foreach (var s in SyntaxHighlighter.Highlight(source))
                    for (int i = s.Start; i < s.End; i++) covered[i] = true;
                for (int i = 0; i < source.Length; i++)
                    if (!covered[i]) Assert.That(char.IsWhiteSpace(source[i]), Is.True, "uncovered '" + source[i] + "' at " + i + " in: " + source);
            }
        }
    }
}
