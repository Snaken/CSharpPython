using System.Linq;
using CSharpPython.Compilation;
using CSharpPython.Syntax;

namespace CSharpPython.Tests.Compilation
{
    public class ResolverTests
    {
        private static ResolvedModule Resolve(string src) => Resolver.Resolve(Parser.ParseModule(src));

        private static Scope FunctionScope(string src, string name = "f")
        {
            var resolved = Resolve(src);
            return resolved.Functions.First(kv => kv.Key.Name == name).Value;
        }

        private static PySyntaxError Fails(string src) => Assert.Throws<PySyntaxError>(() => Resolve(src));

        [Test]
        public void ModuleScopeHasNoLocals()
        {
            var r = Resolve("x = 1\nfor i in y:\n    z = i\n");
            Assert.That(r.ModuleScope.IsModule, Is.True);
            Assert.That(r.ModuleScope.LocalCount, Is.EqualTo(0));
        }

        [Test]
        public void ParametersAreLocalsInOrder()
        {
            var s = FunctionScope("def f(a, b, c=1):\n    pass\n");
            Assert.That(s.Locals, Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(s.IsModule, Is.False);
            Assert.That(s.TryGetLocal("b", out int slot) && slot == 1, Is.True);
        }

        [Test]
        public void AssignedNamesBecomeLocals()
        {
            var s = FunctionScope("def f(a):\n    b = 1\n    a = 2\n    c, d = 1, 2\n    [e, g] = 1, 2\n");
            Assert.That(s.Locals, Is.EqualTo(new[] { "a", "b", "c", "d", "e", "g" }));
        }

        [Test]
        public void ReadOnlyNamesStayGlobal()
        {
            var s = FunctionScope("def f():\n    return x + y\n");
            Assert.That(s.IsLocal("x"), Is.False);
            Assert.That(s.LocalCount, Is.EqualTo(0));
        }

        [Test]
        public void ReadBeforeAssignmentIsStillLocal()
        {
            var s = FunctionScope("def f():\n    print(x)\n    x = 1\n");
            Assert.That(s.IsLocal("x"), Is.True);
        }

        [Test]
        public void ForTargetsAndExceptNamesAndImportsAreLocals()
        {
            var s = FunctionScope("def f():\n    for i in x:\n        pass\n    try:\n        pass\n    except E as err:\n        pass\n    import game\n    import game as g\n    from game import a, b as c\n");
            Assert.That(s.Locals, Is.EqualTo(new[] { "i", "err", "game", "g", "a", "c" }));
        }

        [Test]
        public void AugmentedAssignmentMakesNameLocal()
        {
            var s = FunctionScope("def f():\n    n += 1\n");
            Assert.That(s.IsLocal("n"), Is.True);
        }

        [Test]
        public void AttributeAndSubscriptTargetsDoNotCreateLocals()
        {
            var s = FunctionScope("def f():\n    a[i] = 1\n    b.x = 2\n    c[d.e], g.h = 1, 2\n");
            Assert.That(s.LocalCount, Is.EqualTo(0));
        }

        [Test]
        public void GlobalDeclarationKeepsNameGlobal()
        {
            var s = FunctionScope("def f():\n    global n\n    n = 1\n    n += 1\n    for n in x:\n        pass\n");
            Assert.That(s.IsLocal("n"), Is.False);
            Assert.That(s.DeclaredGlobals, Does.Contain("n"));
        }

        [Test]
        public void GlobalAtModuleLevelIsANoOp()
        {
            Assert.DoesNotThrow(() => Resolve("global x\nx = 1\n"));
        }

        [Test]
        public void GlobalAfterAssignmentIsAnError()
        {
            Assert.That(Fails("def f():\n    n = 1\n    global n\n").Problem, Does.Contain("assigned to before global"));
        }

        [Test]
        public void GlobalAfterParameterIsAnError()
        {
            Assert.That(Fails("def f(n):\n    global n\n").Problem, Does.Contain("assigned to before global"));
        }

        [Test]
        public void GlobalAfterUseIsAnError()
        {
            Assert.That(Fails("def f():\n    print(n)\n    global n\n").Problem, Does.Contain("used prior to global"));
        }

        [Test]
        public void GlobalAfterUseInOtherFunctionIsFine()
        {
            Assert.DoesNotThrow(() => Resolve("def g():\n    print(n)\ndef f():\n    global n\n    n = 1\n"));
        }

        [Test]
        public void DuplicateParametersAreAnError()
        {
            var ex = Fails("def f(a, b, a):\n    pass\n");
            Assert.That(ex.Problem, Does.Contain("duplicate argument 'a'"));
            Assert.That(ex.Line, Is.EqualTo(1));
        }

        [Test]
        public void NestedFunctionsAreResolved()
        {
            Assert.DoesNotThrow(() => Resolve("def f():\n    def g():\n        pass\n"));
        }

        [Test]
        public void FunctionInsideLoopInsideFunctionIsResolved()
        {
            Assert.DoesNotThrow(() => Resolve("def f():\n    while x:\n        def g():\n            pass\n"));
        }

        [Test]
        public void FunctionsMayBeDefinedConditionallyAtModuleLevel()
        {
            var r = Resolve("if x:\n    def f():\n        pass\nelse:\n    def f():\n        return 1\n");
            Assert.That(r.Functions.Count, Is.EqualTo(2));
        }

        [Test]
        public void FunctionDefinitionBindsItsNameInFunctionlessScope()
        {
            var r = Resolve("def f():\n    pass\n");
            Assert.That(r.ModuleScope.LocalCount, Is.EqualTo(0));
        }

        [Test]
        public void DefaultsAreResolvedInTheEnclosingScope()
        {
            // 'x' is read in the default expression, which runs at module level; this must not trip the global check.
            Assert.DoesNotThrow(() => Resolve("x = 1\ndef f(a=x):\n    global x\n    x = 2\n"));
        }

        [Test]
        public void ReturnOutsideFunctionIsAnError()
        {
            Assert.That(Fails("return 1\n").Problem, Does.Contain("'return' outside function"));
        }

        [Test]
        public void BreakAndContinueOutsideLoopsAreErrors()
        {
            Assert.That(Fails("break\n").Problem, Does.Contain("'break' outside loop"));
            Assert.That(Fails("continue\n").Problem, Does.Contain("'continue'"));
            Assert.That(Fails("if x:\n    break\n").Problem, Does.Contain("'break' outside loop"));
        }

        [Test]
        public void BreakInsideFunctionInsideLoopIsAnError()
        {
            Assert.That(Fails("while x:\n    def f():\n        break\n").Problem, Does.Contain("outside loop"));
        }

        [Test]
        public void BreakAndContinueInsideLoopsAreFine()
        {
            Assert.DoesNotThrow(() => Resolve("while x:\n    if y:\n        break\n    continue\n"));
            Assert.DoesNotThrow(() => Resolve("for i in x:\n    try:\n        break\n    except:\n        continue\n"));
        }

        [Test]
        public void LoopDepthIsRestoredAfterFunction()
        {
            Assert.That(Fails("def f():\n    while x:\n        pass\n    break\n").Problem, Does.Contain("outside loop"));
        }

        [Test]
        public void ReturnInsideFunctionIsFine()
        {
            Assert.DoesNotThrow(() => Resolve("def f():\n    return\ndef g():\n    return 1\n"));
        }

        [Test]
        public void EveryExpressionKindIsWalked()
        {
            // Walks each expression node type so that globals used inside them are tracked for the 'global' check.
            string[] exprs =
            {
                "-n", "n + 1", "1 + n", "n and 1", "1 or n", "n < 1", "1 < n", "f(n)", "f(a=n)", "n.x", "n[0]", "a[n]", "a[n:1]",
                "a[1:n]", "a[1:2:n]", "[n]", "(n, 1)", "{n: 1}", "{1: n}", "1 if n else 2", "n if 1 else 2", "1 if 1 else n", "f'{n}'",
            };
            foreach (var e in exprs)
            {
                var src = "def f():\n    " + e + "\n    global n\n";
                Assert.That(Fails(src).Problem, Does.Contain("used prior to global"), e);
            }
        }

        [Test]
        public void StatementKindsAreWalked()
        {
            string[] stmts =
            {
                "n = 1", "x = n", "n[0] = 1", "x[n] = 1", "x.y = n", "if n:\n        pass", "while n:\n        break",
                "for i in n:\n        pass", "return n", "raise n", "try:\n        pass\n    except n:\n        pass",
                "n += 1", "x += n", "x, y = n, 1",
            };
            foreach (var s in stmts)
            {
                var src = "def f():\n    " + s + "\n    global n\n";
                Assert.That(Fails(src), Is.Not.Null, s);
            }
        }

        [Test]
        public void ScopeAddLocalIsIdempotent()
        {
            var scope = new Scope(null);
            Assert.That(scope.AddLocal("a"), Is.EqualTo(0));
            Assert.That(scope.AddLocal("b"), Is.EqualTo(1));
            Assert.That(scope.AddLocal("a"), Is.EqualTo(0));
            Assert.That(scope.TryGetLocal("zzz", out _), Is.False);
        }

        [Test]
        public void BareRaiseAndReturnWithoutValueAreWalked()
        {
            Assert.DoesNotThrow(() => Resolve("def f():\n    try:\n        pass\n    except:\n        raise\n    return\n"));
        }
    }
}
