using System.Linq;
using CSharpPython.Editor;

namespace CSharpPython.Tests.Editor
{
    public class SignatureHelpTests
    {
        private static PyVm Vm()
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m => m
                .Function("spawn", (a, b) => a)
                .Function("player", () => Value.None)
                .Native("fire", (ctx, a) => Value.None, 1, 3, true)
                .Describe("spawn", "kind, count=1")
                .Describe("missing", "ignored")
                .Describe("player", "")
                .Constant("MAX_HP", 100));
            return vm;
        }

        // The text with '|' marking the cursor.
        private static SignatureHelp At(string marked, PyVm vm = null)
        {
            int cursor = marked.IndexOf('|');
            return SignatureHelper.Get(marked.Remove(cursor, 1), cursor, vm ?? Vm());
        }

        [Test]
        public void ScriptFunctionShowsItsParameters()
        {
            var h = At("def move(target, speed=1.0, *rest, **opts):\n    pass\nmove(|");
            Assert.That(h.Label, Is.EqualTo("move(target, speed=1.0, *rest, **opts)"));
            Assert.That(h.Name, Is.EqualTo("move"));
            Assert.That(h.Origin, Is.EqualTo("script"));
            Assert.That(h.Parameters, Is.EqualTo(new[] { "target", "speed=1.0", "*rest", "**opts" }));
            Assert.That(h.ActiveParameter, Is.EqualTo(0));
        }

        [Test]
        public void ClassShowsTheParametersOfInitWithoutSelf()
        {
            var h = At("class Enemy:\n    def __init__(self, hp, speed=2):\n        pass\nEnemy(|");
            Assert.That(h.Label, Is.EqualTo("Enemy(hp, speed=2)"));
            Assert.That(h.Origin, Is.EqualTo("script"));
            Assert.That(h.Parameters, Is.EqualTo(new[] { "hp", "speed=2" }));
            Assert.That(At("class Enemy:\n    def __init__(self):\n        pass\nEnemy(|").Parameters, Is.Empty);
            Assert.That(At("class Enemy:\n    pass\nEnemy(|").Parameters, Is.Empty);
        }

        [Test]
        public void DefiningAClassShowsNoSignatureHelp()
        {
            Assert.That(SignatureHelper.Get("class A(B,", 10), Is.Null);
            Assert.That(At("class A(|"), Is.Null);
        }

        [TestCase("f(|", 0)]
        [TestCase("f(1|", 0)]
        [TestCase("f(1,|", 1)]
        [TestCase("f(1, |", 1)]
        [TestCase("f(1, 2, |", 2)]
        [TestCase("f((1, 2), [3, 4], |", 2)]
        [TestCase("f(g(1, 2), |", 1)]
        [TestCase("f([1, 2, |", 0)]
        [TestCase("f({1: 2, 3: |", 0)]
        [TestCase("f('a, b', |", 1)]
        [TestCase("f(1, 2, 3, 4|", -1)]
        public void CommasChooseTheActiveParameter(string call, int expected)
        {
            var h = At("def f(a, b, c):\n    pass\ndef g(x, y):\n    pass\n" + call);
            Assert.That(h.Name, Is.EqualTo("f"));
            Assert.That(h.ActiveParameter, Is.EqualTo(expected));
        }

        [Test]
        public void TheInnermostCallWins()
        {
            var h = At("def f(a, b, c):\n    pass\ndef g(x, y):\n    pass\nf(g(1, 2|");
            Assert.That(h.Name, Is.EqualTo("g"));
            Assert.That(h.ActiveParameter, Is.EqualTo(1));
        }

        [Test]
        public void VarArgsStayActiveForEveryExtraArgument()
        {
            var src = "def f(a, *rest, k=1):\n    pass\nf(1, 2, 3, 4, |";
            Assert.That(At(src).ActiveParameter, Is.EqualTo(1));
            Assert.That(At("def f(a, *rest, k=1):\n    pass\nf(|").ActiveParameter, Is.EqualTo(0));
        }

        [Test]
        public void KeywordArgumentsSelectTheirParameter()
        {
            string def = "def f(a, b=2, **extra):\n    pass\n";
            Assert.That(At(def + "f(1, b=|").ActiveParameter, Is.EqualTo(1));
            Assert.That(At(def + "f(b=1, a=|").ActiveParameter, Is.EqualTo(0));
            Assert.That(At(def + "f(zzz=|").ActiveParameter, Is.EqualTo(2));
            Assert.That(At("def f(a):\n    pass\nf(zzz=|").ActiveParameter, Is.EqualTo(-1));
        }

        [Test]
        public void BuiltinsUseTheSignatureTable()
        {
            var h = At("print(1, |");
            Assert.That(h.Label, Is.EqualTo("print(*values, sep=' ', end='\\n')"));
            Assert.That(h.Origin, Is.EqualTo("built-in"));
            Assert.That(h.ActiveParameter, Is.EqualTo(0));
            Assert.That(At("sorted([3, 1], |").Parameters, Is.EqualTo(new[] { "iterable", "*", "key=None", "reverse=False" }));
            Assert.That(At("range(1, |").Label, Is.EqualTo("range(start_or_stop, stop=None, step=1)"));
        }

        [Test]
        public void TypesShowTheirConstructorSignature()
        {
            Assert.That(At("int(|").Label, Is.EqualTo("int(x=0, base=10)"));
            Assert.That(At("isinstance(1, |").ActiveParameter, Is.EqualTo(1));
            Assert.That(At("type(|").Label, Is.EqualTo("type(obj)"));
        }

        [Test]
        public void ExceptionClassesTakeArguments()
        {
            var h = At("raise ValueError(|");
            Assert.That(h.Label, Is.EqualTo("ValueError(*args)"));
            Assert.That(h.Origin, Is.EqualTo("exception"));
        }

        [Test]
        public void ModuleFunctionsUseTheHostsDescription()
        {
            var h = At("import game\ngame.spawn(|");
            Assert.That(h.Label, Is.EqualTo("spawn(kind, count=1)"));
            Assert.That(h.Origin, Is.EqualTo("game"));
            Assert.That(At("from game import spawn\nspawn('a', |").ActiveParameter, Is.EqualTo(1));
            Assert.That(At("import game as g\ng.spawn(|").Label, Is.EqualTo("spawn(kind, count=1)"));
        }

        [Test]
        public void UndescribedHostFunctionsGetAMadeUpSignature()
        {
            Assert.That(At("import game\ngame.fire(|").Label, Is.EqualTo("fire(arg1, arg2=..., arg3=..., **kwargs)"));
            Assert.That(At("import game\ngame.player(|").Label, Is.EqualTo("player()"));
            Assert.That(At("import game\ngame.player(|").Parameters, Is.Empty);
            Assert.That(At("import game\ngame.player(|").ActiveParameter, Is.EqualTo(-1));
        }

        [Test]
        public void NativesWithoutADescriptionOrTableEntryDescribeTheirArgumentCounts()
        {
            var vm = new PyVm();
            vm.RegisterModule("m", b => b
                .Native("any_args", (c, a) => Value.None)
                .Native("two", (c, a) => Value.None, 2, 2));
            Assert.That(At("import m\nm.any_args(|", vm).Label, Is.EqualTo("any_args(*args)"));
            Assert.That(At("import m\nm.two(|", vm).Label, Is.EqualTo("two(arg1, arg2)"));
        }

        [Test]
        public void MethodsOfKnownTypesDropSelf()
        {
            Assert.That(At("s = 'abc'\ns.replace(|").Label, Is.EqualTo("replace(old, new, count=-1)"));
            Assert.That(At("s = 'abc'\ns.replace(|").Origin, Is.EqualTo("str method"));
            Assert.That(At("xs = [1]\nxs.insert(1, |").ActiveParameter, Is.EqualTo(1));
            Assert.That(At("'a,b'.split(|").Label, Is.EqualTo("split(sep=None, maxsplit=-1)"));
            Assert.That(At("[3, 1].sort(|").Label, Is.EqualTo("sort(*, key=None, reverse=False)"));
            Assert.That(At("{}.get(|").Label, Is.EqualTo("get(key, default=None)"));
            Assert.That(At("str(1).upper(|").Label, Is.EqualTo("upper()"));
        }

        [Test]
        public void MethodsOfUnknownTypesAreShownWhenOnlyOneTypeHasThem()
        {
            Assert.That(At("def f(x):\n    x.append(|").Label, Is.EqualTo("append(x)"));
            Assert.That(At("def f(x):\n    x.upper(|").Label, Is.EqualTo("upper()"));
            Assert.That(At("def f(x):\n    x.index(|"), Is.Null); // str, list and tuple all have 'index'
            Assert.That(At("def f(x):\n    x.nothing(|"), Is.Null);
        }

        [Test]
        public void NothingIsShownOutsideKnownCalls()
        {
            Assert.That(At("x = 1|"), Is.Null);
            Assert.That(At("f(1)|"), Is.Null);
            Assert.That(At("print(1)\n|"), Is.Null);
            Assert.That(At("unknown_fn(|"), Is.Null);
            Assert.That(At("x = 5\nx(|"), Is.Null);
            Assert.That(At("def f(|"), Is.Null);
            Assert.That(At("(|"), Is.Null);
            Assert.That(At("1 + (|"), Is.Null);
            Assert.That(At("import game\ngame.nope(|"), Is.Null);
            Assert.That(At("import game\ngame.MAX_HP(|"), Is.Null);
            Assert.That(At("import game\nx.game.spawn(|"), Is.Null);
            Assert.That(At("from game import nope\nnope(|"), Is.Null);
            Assert.That(At("import nomodule\nnomodule.f(|"), Is.Null);
            Assert.That(At("print(1)\nprint(2)|\n(|"), Is.Null);
        }

        [Test]
        public void ClosedCallsAndEarlierLinesAreIgnored()
        {
            Assert.That(At("print(1)\nlen(|").Name, Is.EqualTo("len"));
            Assert.That(At("print(len(x), |").Name, Is.EqualTo("print"));
            Assert.That(At("print(len(x)|").Name, Is.EqualTo("print"));
        }

        [Test]
        public void CallsSpanLines()
        {
            Assert.That(At("print(1,\n      2,\n      |").ActiveParameter, Is.EqualTo(0));
            Assert.That(At("def f(a, b):\n    pass\nf(1,\n  |").ActiveParameter, Is.EqualTo(1));
        }

        [Test]
        public void ShadowingPrefersTheScriptsOwnFunction()
        {
            var h = At("def len(a, b):\n    pass\nlen(|");
            Assert.That(h.Origin, Is.EqualTo("script"));
            Assert.That(h.Parameters, Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void NestedFunctionsAreFoundInsideTheirParent()
        {
            var src = "def outer():\n    def inner(p, q):\n        pass\n    inner(|";
            Assert.That(At(src).Parameters, Is.EqualTo(new[] { "p", "q" }));
            Assert.That(At("def outer():\n    def inner(p):\n        pass\ninner(|"), Is.Null);
        }

        [Test]
        public void NeverThrowsOnOddInput()
        {
            foreach (var text in new[] { "", "(", ")", "f(", "f(((((", "f(\"", "f('abc", "f(1,,,,", "f(#", "def", "def f(", "x = [", "{", "f(*", "f(**", "f(a=", "f(=" })
            {
                for (int i = 0; i <= text.Length; i++)
                    Assert.DoesNotThrow(() => SignatureHelper.Get(text, i, Vm()), text + "@" + i);
            }
            Assert.DoesNotThrow(() => SignatureHelper.Get(null, 5));
            Assert.That(SignatureHelper.Get("print(", -3), Is.Null);
            Assert.That(SignatureHelper.Get("print(", 1000).Name, Is.EqualTo("print"));
        }

        [Test]
        public void EveryBuiltinFunctionAndMethodHasATableEntry()
        {
            var vm = new PyVm();
            foreach (var kv in vm.Builtins)
                if (!kv.Key.StartsWith(".") && (kv.Value.Kind == ValueKind.Native || kv.Value.Kind == ValueKind.Type))
                    Assert.That(Signatures.Find(kv.Key), Is.Not.Null, "built-in " + kv.Key);
            foreach (var kv in vm.Modules["math"].Members)
                if (kv.Value.Kind == ValueKind.Native)
                    Assert.That(Signatures.Find("math." + kv.Key), Is.Not.Null, "math." + kv.Key);
            foreach (var kind in new[] { ValueKind.Str, ValueKind.List, ValueKind.Dict, ValueKind.Tuple })
                foreach (var name in Methods.NamesOf(kind))
                    Assert.That(Signatures.Find(kind.ToString().ToLowerInvariant() + "." + name), Is.Not.Null, kind + "." + name);
        }

        [Test]
        public void TableEntriesAgreeWithTheArgumentCounts()
        {
            // Every entry must be able to accept the number of positional arguments the function accepts.
            var vm = new PyVm();
            void Check(string key, NativeFunction fn, int self)
            {
                var parameters = SignatureHelper.SplitParameters(Signatures.Find(key));
                bool varargs = parameters.Any(p => p.StartsWith("*") && !p.StartsWith("**") && p.Length > 1);
                int star = parameters.IndexOf("*");
                int positional = star >= 0 ? star : parameters.Count(p => !p.StartsWith("*"));
                int required = parameters.Count(p => !p.Contains("=") && !p.StartsWith("*"));
                int max = fn.MaxArgs < 0 ? -1 : fn.MaxArgs - self;
                int min = fn.MinArgs - self;
                if (!varargs && max >= 0) Assert.That(positional, Is.GreaterThanOrEqualTo(max), key + " positional");
                if (max < 0) Assert.That(varargs, Is.True, key + " is variadic");
                Assert.That(required, Is.LessThanOrEqualTo(min), key + " required");
            }
            foreach (var kv in vm.Builtins)
            {
                if (kv.Key.StartsWith(".")) continue;
                if (kv.Value.Kind == ValueKind.Native) Check(kv.Key, (NativeFunction)kv.Value.RawObject, 0);
                if (kv.Value.Kind == ValueKind.Type && ((PyType)kv.Value.RawObject).Constructor != null)
                    Check(kv.Key, ((PyType)kv.Value.RawObject).Constructor, 0);
            }
            foreach (var kv in vm.Modules["math"].Members)
                if (kv.Value.Kind == ValueKind.Native) Check("math." + kv.Key, (NativeFunction)kv.Value.RawObject, 0);
            foreach (var kind in new[] { ValueKind.Str, ValueKind.List, ValueKind.Dict, ValueKind.Tuple })
                foreach (var name in Methods.NamesOf(kind))
                    Check(kind.ToString().ToLowerInvariant() + "." + name, Methods.Find(kind, name), 1);
        }

        [Test]
        public void SplitParametersRespectsBracketsAndStrings()
        {
            Assert.That(SignatureHelper.SplitParameters("a, b=(1, 2), c=[3, 4], d={1: 2}, e=', '"),
                Is.EqualTo(new[] { "a", "b=(1, 2)", "c=[3, 4]", "d={1: 2}", "e=', '" }));
            Assert.That(SignatureHelper.SplitParameters(""), Is.Empty);
            Assert.That(SignatureHelper.SplitParameters(null), Is.Empty);
            Assert.That(SignatureHelper.SplitParameters("a,"), Is.EqualTo(new[] { "a" }));
        }

        [Test]
        public void SourceModelSeesStarParameters()
        {
            var h = At("def f(*args, **kwargs):\n    return args|");
            Assert.That(h, Is.Null);
            var inside = Completer.Complete("def f(*args, **kwargs):\n    return ar", 38);
            Assert.That(inside.Items.Select(i => i.Text), Does.Contain("args"));
            Assert.That(Completer.Complete("def f(*args, **kwargs):\n    return kw", 38).Items.Select(i => i.Text), Does.Contain("kwargs"));
        }
    }
}
