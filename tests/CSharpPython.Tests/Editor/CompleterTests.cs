using System;
using System.Collections.Generic;
using System.Linq;
using CSharpPython.Editor;

namespace CSharpPython.Tests.Editor
{
    public class CompleterTests
    {
        private static PyVm Vm()
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m => m
                .Function("spawn", (a, b) => a)
                .Function("player", () => Value.None)
                .Constant("MAX_HP", 100)
                .Constant("NAME", "tank"));
            return vm;
        }

        // The text with '|' marking the cursor.
        private static CompletionList At(string marked, PyVm vm = null)
        {
            int cursor = marked.IndexOf('|');
            return Completer.Complete(marked.Remove(cursor, 1), cursor, vm ?? Vm());
        }

        private static string[] Names(string marked, PyVm vm = null) => At(marked, vm).Items.Select(i => i.Text).ToArray();

        [Test]
        public void SuggestsClassesAndTheClassKeyword()
        {
            Assert.That(Names("cla|"), Does.Contain("class"));
            var items = At("class Enemy:\n    def __init__(self, hp, speed=2):\n        self.hp = hp\nx = Ene|").Items;
            var enemy = items.Single(i => i.Text == "Enemy");
            Assert.That(enemy.Kind, Is.EqualTo(CompletionKind.Class));
            Assert.That(enemy.Detail, Is.EqualTo("class Enemy(hp, speed=2)"));
            Assert.That(At("class Enemy:\n    pass\nx = Ene|").Items.Single(i => i.Text == "Enemy").Detail, Is.EqualTo("class Enemy"));
        }

        [Test]
        public void NothingIsSuggestedForANewClassName()
        {
            Assert.That(Names("class |"), Is.Empty);
        }

        [Test]
        public void ClassBodyNamesDoNotLeakIntoTheCodeAround()
        {
            Assert.That(Names("class A:\n    secret = 1\nx = sec|"), Does.Not.Contain("secret"));
        }

        [Test]
        public void SuggestsNonlocalAndLambdaKeywords()
        {
            Assert.That(Names("nonl|"), Does.Contain("nonlocal"));
            Assert.That(Names("f = lamb|"), Does.Contain("lambda"));
        }

        [Test]
        public void SuggestsBuiltinsByPrefix()
        {
            var list = At("pri|");
            Assert.That(list.Items.Select(i => i.Text), Does.Contain("print"));
            Assert.That(list.Items.All(i => i.Text.StartsWith("pri", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(list.ReplaceStart, Is.EqualTo(0));
            Assert.That(list.ReplaceLength, Is.EqualTo(3));
            Assert.That(At("x = len|").ReplaceStart, Is.EqualTo(4));
        }

        [Test]
        public void SuggestsNamesTheScriptDefines()
        {
            var names = Names("score = 1\ndef shoot(a, b=2):\n    pass\nfor sprite in []:\n    pass\ns|");
            Assert.That(names, Does.Contain("score").And.Contain("shoot").And.Contain("sprite").And.Contain("sorted").And.Contain("str"));
            Assert.That(names.ToList().IndexOf("score"), Is.LessThan(names.ToList().IndexOf("sorted")), "script names rank first");
        }

        [Test]
        public void TheWordBeingTypedDoesNotSuggestItself()
        {
            Assert.That(Names("zzz|"), Is.Empty);
            Assert.That(Names("zzz| = 5"), Is.Empty);
            Assert.That(Names("zzz = 5\nzz|"), Does.Contain("zzz"));
        }

        [Test]
        public void ParametersAreOnlyVisibleInsideTheirFunction()
        {
            const string code = "def f(alpha, beta):\n    return al|\nx = 1";
            Assert.That(Names(code), Does.Contain("alpha"));
            Assert.That(Names("def f(alpha):\n    pass\nal|"), Does.Not.Contain("alpha"));
            Assert.That(Names("def f(alpha):\n    gamma = 1\n    pass\ngam|"), Does.Not.Contain("gamma"));
            Assert.That(Names("def f(alpha):\n    gamma = 1\n    ga|"), Does.Contain("gamma"));
        }

        [Test]
        public void ImportsAndExceptAliasesAreNames()
        {
            Assert.That(Names("import math as mm\nm|"), Does.Contain("mm"));
            Assert.That(Names("from game import spawn as sp\nsp|"), Does.Contain("sp"));
            Assert.That(Names("try:\n    pass\nexcept ValueError as err:\n    er|"), Does.Contain("err"));
        }

        [Test]
        public void StatementPositionOffersStatementKeywords()
        {
            var atStart = Names("|");
            Assert.That(atStart, Does.Contain("def").And.Contain("while").And.Contain("print"));
            Assert.That(Names("x = 1\nw|"), Does.Contain("while"));
            Assert.That(Names("y = x i|"), Does.Contain("if").And.Contain("in").And.Contain("is"));
            Assert.That(Names("y = x w|"), Does.Not.Contain("while"));
        }

        [Test]
        public void ConstantsAreOffered()
        {
            Assert.That(Names("x = T|"), Does.Contain("True"));
            Assert.That(Names("x = No|"), Does.Contain("None"));
        }

        [Test]
        public void MethodsOfLiteralsAndObviousVariables()
        {
            Assert.That(Names("'abc'.up|"), Is.EqualTo(new[] { "upper" }));
            Assert.That(Names("xs = [1, 2]\nxs.app|"), Is.EqualTo(new[] { "append" }));
            Assert.That(Names("d = {'a': 1}\nd.ke|"), Is.EqualTo(new[] { "keys" }));
            Assert.That(Names("t = (1, 2)\nt.|"), Is.EqualTo(new[] { "count", "index" }));
            Assert.That(Names("s = str(5)\ns.sp|"), Is.EqualTo(new[] { "split", "splitlines" }));
            Assert.That(Names("n = sorted([3, 1])\nn.so|"), Is.EqualTo(new[] { "sort" }));
            Assert.That(Names("[1, 2].rev|"), Is.EqualTo(new[] { "reverse" }));
            Assert.That(Names("{'a': 1}.it|"), Is.EqualTo(new[] { "items" }));
            Assert.That(Names("list(range(3)).cl|"), Is.EqualTo(new[] { "clear" }));
            Assert.That(Names("(1, 2).cou|"), Is.EqualTo(new[] { "count" }));
            Assert.That(Names("x = 'a'\nx.|"), Does.Contain("upper").And.Not.Contain("append"));
        }

        [Test]
        public void MethodItemsDescribeTheirArguments()
        {
            var item = At("xs = []\nxs.ins|").Items.Single();
            Assert.That(item.Kind, Is.EqualTo(CompletionKind.Method));
            Assert.That(item.Detail, Is.EqualTo("list.insert(2 args)"));
            Assert.That(At("[].pop|").Items.Single().Detail, Is.EqualTo("list.pop(0-1 args)"));
            Assert.That(At("[].clear|").Items.Single().Detail, Is.EqualTo("list.clear()"));
            Assert.That(At("'x'.joi|").Items.Single().Detail, Is.EqualTo("str.join(1 arg)"));
        }

        [Test]
        public void WhenTheTypeIsUnknownEveryMethodIsOffered()
        {
            var names = Names("x = foo()\nx.|");
            Assert.That(names, Does.Contain("upper").And.Contain("append").And.Contain("keys").And.Contain("count"));
            Assert.That(names.Distinct().Count(), Is.EqualTo(names.Length));
            // Conflicting assignments are unknown too.
            var mixed = Names("x = 'a'\nx = [1]\nx.|");
            Assert.That(mixed, Does.Contain("upper").And.Contain("append"));
            // A subscript or a call result is unknown.
            Assert.That(Names("xs[0].up|"), Is.EqualTo(new[] { "update", "upper" }));
            Assert.That(Names("f(1).app|"), Is.EqualTo(new[] { "append" }));
            Assert.That(Names("a.b.c|"), Is.Empty);
            Assert.That(Names("a.b.|"), Is.Empty);
        }

        [Test]
        public void ModuleMembersAfterAnImport()
        {
            var names = Names("import game\ngame.|");
            Assert.That(names, Is.EquivalentTo(new[] { "spawn", "player", "MAX_HP", "NAME" }));
            Assert.That(Names("import game as g\ng.sp|"), Is.EqualTo(new[] { "spawn" }));
            Assert.That(Names("import math\nmath.sq|"), Is.EqualTo(new[] { "sqrt" }));
            Assert.That(Names("import math\nmath.p|"), Is.EquivalentTo(new[] { "pi", "pow" }));

            var items = At("import game\ngame.|").Items;
            Assert.That(items.Single(i => i.Text == "MAX_HP").Kind, Is.EqualTo(CompletionKind.Constant));
            Assert.That(items.Single(i => i.Text == "MAX_HP").Detail, Is.EqualTo("game.MAX_HP = 100"));
            Assert.That(items.Single(i => i.Text == "spawn").Kind, Is.EqualTo(CompletionKind.Function));
            Assert.That(items.Single(i => i.Text == "spawn").Detail, Is.EqualTo("game.spawn(2 args)"));
            Assert.That(items.Single(i => i.Text == "player").Detail, Is.EqualTo("game.player()"));
        }

        [Test]
        public void AModuleThatIsNotImportedHasNoMembers()
        {
            Assert.That(Names("game.|"), Does.Contain("upper")); // just an unknown variable
            Assert.That(Names("game.sp|"), Is.EqualTo(new[] { "split", "splitlines" })); // no module members
        }

        [Test]
        public void ImportStatementsCompleteModuleAndMemberNames()
        {
            Assert.That(Names("import |"), Is.EquivalentTo(new[] { "game", "math" }));
            Assert.That(Names("import ga|"), Is.EqualTo(new[] { "game" }));
            Assert.That(Names("from |"), Is.EquivalentTo(new[] { "game", "math" }));
            Assert.That(Names("from game |"), Is.EqualTo(new[] { "import" }));
            Assert.That(Names("from game import |"), Is.EquivalentTo(new[] { "spawn", "player", "MAX_HP", "NAME" }));
            Assert.That(Names("from game import spawn, pl|"), Is.EqualTo(new[] { "player" }));
            Assert.That(Names("from nosuch import |"), Is.Empty);
            Assert.That(Names("import game, |"), Is.EquivalentTo(new[] { "game", "math" }));
            Assert.That(Names("from game import spawn\nspa|"), Does.Contain("spawn"));
            var imported = At("from game import spawn, MAX_HP\nsp|").Items.Single(i => i.Text == "spawn");
            Assert.That(imported.Kind, Is.EqualTo(CompletionKind.Function));
            Assert.That(At("from game import spawn, MAX_HP\nMA|").Items.Single(i => i.Text == "MAX_HP").Kind, Is.EqualTo(CompletionKind.Constant));
        }

        [Test]
        public void NothingIsOfferedWhereANewNameIsBeingMade()
        {
            Assert.That(Names("def |"), Is.Empty);
            Assert.That(Names("def fo|"), Is.Empty);
            Assert.That(Names("for |"), Is.Empty);
            Assert.That(Names("import math as |"), Is.Empty);
        }

        [Test]
        public void NothingIsOfferedInsideStringsOrComments()
        {
            Assert.That(Names("x = 'pri|"), Is.Empty);
            Assert.That(Names("x = \"pri|"), Is.Empty);
            Assert.That(Names("x = 1  # pri|"), Is.Empty);
            Assert.That(Names("# |"), Is.Empty);
            Assert.That(Names("x = '''abc\npri|"), Is.Empty);
            Assert.That(Names("x = 'done' + pri|"), Does.Contain("print"));
            Assert.That(Names("x = 'abc'|"), Is.Empty);
            Assert.That(Names("y = f(1)|"), Is.Empty);
            Assert.That(Names("y = 12|"), Is.Empty);
            Assert.That(Names("y = 12ab|"), Is.Empty);
        }

        [Test]
        public void ResultsAreSortedAndUnique()
        {
            var items = At("pr|").Items;
            Assert.That(items.Select(i => i.Text).Distinct().Count(), Is.EqualTo(items.Count));
            var upper = At("PRI|").Items.Select(i => i.Text).ToArray();
            Assert.That(upper, Does.Contain("print"));
            var same = At("Tr|").Items.Select(i => i.Text).ToArray();
            Assert.That(same[0], Is.EqualTo("True"));
            Assert.That(Names("xs = []\nxs.|").First(), Is.EqualTo("append"));
        }

        [Test]
        public void HostBuiltinsAndExceptionClassesAreSuggested()
        {
            var vm = Vm();
            vm.SetBuiltin("teleport", Value.FromNative(new NativeFunction("teleport", (c, a) => Value.None, 1, 3)));
            vm.RegisterExceptionType("EditorGameError");
            vm.SetBuiltin("LIMIT", Value.FromInt(5));
            var tele = At("tele|", vm).Items.Single();
            Assert.That(tele.Kind, Is.EqualTo(CompletionKind.Function));
            Assert.That(tele.Detail, Is.EqualTo("built-in teleport(1-3 args)"));
            Assert.That(At("EditorG|", vm).Items.Single().Kind, Is.EqualTo(CompletionKind.Class));
            Assert.That(At("LIM|", vm).Items.Single().Kind, Is.EqualTo(CompletionKind.Constant));
            Assert.That(At("Value|", vm).Items.Select(i => i.Text), Does.Contain("ValueError"));
        }

        [Test]
        public void WorksWithoutAVmAndOutOfRangeOffsets()
        {
            Assert.That(Completer.Complete("pri", 3).Items.Select(i => i.Text), Does.Contain("print"));
            Assert.That(Completer.Complete("pri", 99).ReplaceStart, Is.EqualTo(0));
            Assert.That(Completer.Complete("pri", -5).Items, Is.Not.Empty);
            Assert.That(Completer.Complete(null, 0).Items, Is.Not.Empty);
        }

        [Test]
        public void NamesInsideTheFileAfterTheCursorAreKnownToo()
        {
            Assert.That(Names("va|\nvalue = 1"), Does.Contain("value"));
        }

        [Test]
        public void ADefinedFunctionShowsItsSignature()
        {
            var item = At("def move(dx, dy=0):\n    pass\nmo|").Items.Single(i => i.Text == "move");
            Assert.That(item.Detail, Is.EqualTo("def move(dx, dy=0)"));
            Assert.That(item.Kind, Is.EqualTo(CompletionKind.Function));
        }

        [Test]
        public void VariablesRememberTheirTypeInTheDetail()
        {
            Assert.That(At("name = 'bob'\nna|").Items.Single(i => i.Text == "name").Detail, Is.EqualTo("str"));
            Assert.That(At("zed = foo()\nze|").Items.Single(i => i.Text == "zed").Detail, Is.Null);
        }

        public static IEnumerable<string> Samples() => HighlighterTests.Samples();

        [TestCaseSource(nameof(Samples))]
        public void NeverThrowsAtAnyOffset(string source)
        {
            var vm = Vm();
            for (int i = 0; i <= source.Length; i++)
            {
                var list = Completer.Complete(source, i, vm);
                Assert.That(list.ReplaceStart, Is.InRange(0, source.Length));
                Assert.That(list.ReplaceStart + list.ReplaceLength, Is.LessThanOrEqualTo(source.Length));
            }
        }

        [Test]
        public void NeverThrowsOnRandomlyDamagedSource()
        {
            var random = new Random(99);
            var samples = HighlighterTests.Samples().ToArray();
            var vm = Vm();
            for (int i = 0; i < 1500; i++)
            {
                var chars = samples[random.Next(samples.Length)].ToCharArray();
                for (int e = 0; e < 1 + random.Next(4); e++) chars[random.Next(chars.Length)] = HighlighterTests.Noise[random.Next(HighlighterTests.Noise.Length)];
                string text = new string(chars);
                for (int k = 0; k < 6; k++) Completer.Complete(text, random.Next(text.Length + 1), vm);
            }
        }
    }
}
