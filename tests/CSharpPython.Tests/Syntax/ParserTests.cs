using CSharpPython.Syntax;

namespace CSharpPython.Tests.Syntax
{
    public class ParserTests
    {
        private static string E(string src) => AstPrinter.Expr(Parser.ParseExpression(src));

        private static string M(string src) => AstPrinter.Module(src);

        private static PySyntaxError Fails(string src) => Assert.Throws<PySyntaxError>(() => Parser.ParseModule(src));

        [Test]
        public void TypeAnnotationHasClearError()
        {
            Assert.That(Fails("x: int = 5").Problem, Does.Contain("type annotations are not supported"));
        }

        // ---- Literals and names -------------------------------------------------------------------------------

        [TestCase("1", "1")]
        [TestCase("1.5", "1.5")]
        [TestCase("2.0", "2.0")]
        [TestCase("'hi'", "'hi'")]
        [TestCase("True", "True")]
        [TestCase("False", "False")]
        [TestCase("None", "None")]
        [TestCase("name", "name")]
        public void Atoms(string src, string expected) => Assert.That(E(src), Is.EqualTo(expected));

        [Test]
        public void AdjacentStringsAreConcatenated() => Assert.That(E("'a' 'b' \"c\""), Is.EqualTo("'abc'"));

        [Test]
        public void ParenthesisedExpressionIsJustTheExpression() => Assert.That(E("(1)"), Is.EqualTo("1"));

        [Test]
        public void EmptyTupleAndTupleLiterals()
        {
            Assert.That(E("()"), Is.EqualTo("(tuple)"));
            Assert.That(E("(1,)"), Is.EqualTo("(tuple 1)"));
            Assert.That(E("(1, 2, 3)"), Is.EqualTo("(tuple 1 2 3)"));
            Assert.That(E("(1, 2,)"), Is.EqualTo("(tuple 1 2)"));
        }

        [Test]
        public void ListLiterals()
        {
            Assert.That(E("[]"), Is.EqualTo("[]"));
            Assert.That(E("[1, 2, 3]"), Is.EqualTo("[1 2 3]"));
            Assert.That(E("[1, 2,]"), Is.EqualTo("[1 2]"));
        }

        [Test]
        public void DictLiterals()
        {
            Assert.That(E("{}"), Is.EqualTo("{}"));
            Assert.That(E("{'a': 1, 'b': 2}"), Is.EqualTo("{'a':1 'b':2}"));
            Assert.That(E("{1: 2,}"), Is.EqualTo("{1:2}"));
        }

        // ---- Operators ----------------------------------------------------------------------------------------

        [Test]
        public void MultiplicationBindsTighterThanAddition() => Assert.That(E("1 + 2 * 3"), Is.EqualTo("(+ 1 (* 2 3))"));

        [Test]
        public void AdditionIsLeftAssociative() => Assert.That(E("1 - 2 - 3"), Is.EqualTo("(- (- 1 2) 3)"));

        [Test]
        public void PowerIsRightAssociative() => Assert.That(E("2 ** 3 ** 2"), Is.EqualTo("(** 2 (** 3 2))"));

        [Test]
        public void UnaryMinusBindsLooserThanPowerOnTheLeft() => Assert.That(E("-x ** 2"), Is.EqualTo("(neg (** x 2))"));

        [Test]
        public void PowerAcceptsNegativeExponent() => Assert.That(E("2 ** -1"), Is.EqualTo("(** 2 -1)"));

        [Test]
        public void NegativeNumericLiteralsAreFolded()
        {
            Assert.That(E("-5"), Is.EqualTo("-5"));
            Assert.That(E("-2.5"), Is.EqualTo("-2.5"));
        }

        [Test]
        public void UnaryOperatorsOnNames()
        {
            Assert.That(E("-x"), Is.EqualTo("(neg x)"));
            Assert.That(E("+x"), Is.EqualTo("(pos x)"));
            Assert.That(E("- -x"), Is.EqualTo("(neg (neg x))"));
        }

        [Test]
        public void NegativeLiteralInPowerIsNotFoldedIntoBase() => Assert.That(E("-2 ** 2"), Is.EqualTo("(neg (** 2 2))"));

        [Test]
        public void AllArithmeticOperators()
        {
            Assert.That(E("a / b // c % d"), Is.EqualTo("(% (// (/ a b) c) d)"));
        }

        [Test]
        public void ParenthesesOverridePrecedence() => Assert.That(E("(1 + 2) * 3"), Is.EqualTo("(* (+ 1 2) 3)"));

        [Test]
        public void ComparisonsAreLowerThanArithmetic() => Assert.That(E("a + 1 < b * 2"), Is.EqualTo("(cmp (+ a 1) < (* b 2))"));

        [Test]
        public void ChainedComparisonsShareOneNode() => Assert.That(E("a < b <= c"), Is.EqualTo("(cmp a < b <= c)"));

        [TestCase("a == b", "(cmp a == b)")]
        [TestCase("a != b", "(cmp a != b)")]
        [TestCase("a > b", "(cmp a > b)")]
        [TestCase("a >= b", "(cmp a >= b)")]
        [TestCase("a in b", "(cmp a in b)")]
        [TestCase("a not in b", "(cmp a not-in b)")]
        [TestCase("a is b", "(cmp a is b)")]
        [TestCase("a is not b", "(cmp a is-not b)")]
        public void ComparisonOperators(string src, string expected) => Assert.That(E(src), Is.EqualTo(expected));

        [Test]
        public void BooleanOperatorsHaveCorrectPrecedence()
        {
            Assert.That(E("a or b and c"), Is.EqualTo("(or a (and b c))"));
            Assert.That(E("not a and b"), Is.EqualTo("(and (not a) b)"));
            Assert.That(E("not not a"), Is.EqualTo("(not (not a))"));
            Assert.That(E("a or b or c"), Is.EqualTo("(or (or a b) c)"));
            Assert.That(E("a and b and c"), Is.EqualTo("(and (and a b) c)"));
        }

        [Test]
        public void NotBindsLooserThanComparison() => Assert.That(E("not a == b"), Is.EqualTo("(not (cmp a == b))"));

        [Test]
        public void ConditionalExpression()
        {
            Assert.That(E("a if c else b"), Is.EqualTo("(ifexp c a b)"));
            Assert.That(E("a if c else b if d else e"), Is.EqualTo("(ifexp c a (ifexp d b e))"));
        }

        // ---- Postfix ------------------------------------------------------------------------------------------

        [Test]
        public void Calls()
        {
            Assert.That(E("f()"), Is.EqualTo("(call f)"));
            Assert.That(E("f(1, 2)"), Is.EqualTo("(call f 1 2)"));
            Assert.That(E("f(1, 2,)"), Is.EqualTo("(call f 1 2)"));
            Assert.That(E("f(1)(2)"), Is.EqualTo("(call (call f 1) 2)"));
        }

        [Test]
        public void KeywordArguments()
        {
            Assert.That(E("f(1, x=2, y=3)"), Is.EqualTo("(call f 1 x=2 y=3)"));
            Assert.That(E("f(x=a == b)"), Is.EqualTo("(call f x=(cmp a == b))"));
        }

        [Test]
        public void AttributesAndMethodCalls()
        {
            Assert.That(E("a.b.c"), Is.EqualTo("(. (. a b) c)"));
            Assert.That(E("a.b(1)"), Is.EqualTo("(call (. a b) 1)"));
        }

        [Test]
        public void Subscripts()
        {
            Assert.That(E("a[0]"), Is.EqualTo("(idx a 0)"));
            Assert.That(E("a[0][1]"), Is.EqualTo("(idx (idx a 0) 1)"));
            Assert.That(E("a[1, 2]"), Is.EqualTo("(idx a (tuple 1 2))"));
            Assert.That(E("a[1,]"), Is.EqualTo("(idx a (tuple 1))"));
        }

        [TestCase("a[1:2]", "(idx a (slice 1 2 _))")]
        [TestCase("a[:2]", "(idx a (slice _ 2 _))")]
        [TestCase("a[1:]", "(idx a (slice 1 _ _))")]
        [TestCase("a[:]", "(idx a (slice _ _ _))")]
        [TestCase("a[::2]", "(idx a (slice _ _ 2))")]
        [TestCase("a[1:2:3]", "(idx a (slice 1 2 3))")]
        [TestCase("a[1::]", "(idx a (slice 1 _ _))")]
        [TestCase("a[::-1]", "(idx a (slice _ _ -1))")]
        [TestCase("a[1:2, 3]", "(idx a (tuple (slice 1 2 _) 3))")]
        [TestCase("a[1:,]", "(idx a (tuple (slice 1 _ _)))")]
        [TestCase("a[:,1]", "(idx a (tuple (slice _ _ _) 1))")]
        public void Slices(string src, string expected) => Assert.That(E(src), Is.EqualTo(expected));

        [Test]
        public void MixedPostfixChain() => Assert.That(E("a.b[1](2).c"), Is.EqualTo("(. (call (idx (. a b) 1) 2) c)"));

        // ---- F-strings ----------------------------------------------------------------------------------------

        [Test]
        public void FStringWithLiteralAndExpression() => Assert.That(E("f'a{x}b'"), Is.EqualTo("(f 'a' (val x) 'b')"));

        [Test]
        public void FStringWithoutPlaceholders() => Assert.That(E("f'plain'"), Is.EqualTo("(f 'plain')"));

        [Test]
        public void FStringWithEscapedBraces() => Assert.That(E("f'{{x}} {y}'"), Is.EqualTo("(f '{x} ' (val y))"));

        [Test]
        public void FStringDecodesEscapesInLiteralText() => Assert.That(E("f'a\\n{x}'"), Is.EqualTo("(f 'a\n' (val x))"));

        [Test]
        public void FStringWithExpressionsAndOperators() => Assert.That(E("f'{a + 1}'"), Is.EqualTo("(f (val (+ a 1)))"));

        [Test]
        public void FStringConversionsAndFormatSpecs()
        {
            Assert.That(E("f'{x!r}'"), Is.EqualTo("(f (val x !r))"));
            Assert.That(E("f'{x!s}'"), Is.EqualTo("(f (val x !s))"));
            Assert.That(E("f'{x!a}'"), Is.EqualTo("(f (val x !a))"));
            Assert.That(E("f'{x:>10}'"), Is.EqualTo("(f (val x :>10))"));
            Assert.That(E("f'{x!r:>10}'"), Is.EqualTo("(f (val x !r :>10))"));
            Assert.That(E("f'{x:.2f}'"), Is.EqualTo("(f (val x :.2f))"));
        }

        [Test]
        public void FStringExpressionMayContainNestedBracketsAndStrings()
        {
            Assert.That(E("f'{d[\"k\"]}'"), Is.EqualTo("(f (val (idx d 'k')))"));
            Assert.That(E("f'{ {1: 2}[1] }'"), Is.EqualTo("(f (val (idx {1:2} 1)))"));
            Assert.That(E("f'{f(1, 2)}'"), Is.EqualTo("(f (val (call f 1 2)))"));
            Assert.That(E("f'{\"a:b\"}'"), Is.EqualTo("(f (val 'a:b'))"));
            Assert.That(E("f'{\"a\\\"b\"}'"), Is.EqualTo("(f (val 'a\"b'))"));
        }

        [Test]
        public void FStringNotEqualsInsideExpressionIsNotAConversion() =>
            Assert.That(E("f'{a != b}'"), Is.EqualTo("(f (val (cmp a != b)))"));

        [Test]
        public void FStringComparisonOperatorsEndingInEqualsAreAllowed()
        {
            Assert.That(E("f'{a <= b}'"), Is.EqualTo("(f (val (cmp a <= b)))"));
            Assert.That(E("f'{a >= b}'"), Is.EqualTo("(f (val (cmp a >= b)))"));
            Assert.That(E("f'{a == b}'"), Is.EqualTo("(f (val (cmp a == b)))"));
        }

        [Test]
        public void FStringTupleExpression() => Assert.That(E("f'{a, b}'"), Is.EqualTo("(f (val (tuple a b)))"));

        [Test]
        public void FStringMultilineExpressionInTripleQuotes() =>
            Assert.That(E("f'''{a +\n b}'''"), Is.EqualTo("(f (val (+ a b)))"));

        [Test]
        public void AdjacentPlainAndFStringsMerge()
        {
            Assert.That(E("'a' f'{x}' 'b'"), Is.EqualTo("(f 'a' (val x) 'b')"));
            Assert.That(E("f'{x}' f'{y}'"), Is.EqualTo("(f (val x) (val y))"));
            Assert.That(E("f'a' f'b'"), Is.EqualTo("(f 'ab')"));
        }

        [Test]
        public void FStringErrors()
        {
            Assert.That(Assert.Throws<PySyntaxError>(() => E("f'}'")).Problem, Does.Contain("single '}'"));
            Assert.That(Assert.Throws<PySyntaxError>(() => E("f'{x'")).Problem, Does.Contain("expecting '}'"));
            Assert.That(Assert.Throws<PySyntaxError>(() => E("f'{}'")).Problem, Does.Contain("empty expression"));
            Assert.That(Assert.Throws<PySyntaxError>(() => E("f'{ }'")).Problem, Does.Contain("empty expression"));
            Assert.That(Assert.Throws<PySyntaxError>(() => E("f'{x=}'")).Problem, Does.Contain("'='"));
            Assert.That(Assert.Throws<PySyntaxError>(() => E("f'{x!z}'")).Problem, Does.Contain("conversion"));
            Assert.That(Assert.Throws<PySyntaxError>(() => E("f'{x!}'")).Problem, Does.Contain("conversion"));
            Assert.That(Assert.Throws<PySyntaxError>(() => E("f'{x!r'")).Problem, Does.Contain("expecting '}'"));
            Assert.That(Assert.Throws<PySyntaxError>(() => E("f'{x:{y}}'")).Problem, Does.Contain("nested"));
            Assert.That(Assert.Throws<PySyntaxError>(() => E("f'{x:>5'")).Problem, Does.Contain("expecting '}'"));
            Assert.That(Assert.Throws<PySyntaxError>(() => E("f'{x +}'")).Problem, Does.Contain("unexpected"));
            Assert.That(Assert.Throws<PySyntaxError>(() => E("f'{x $}'")).Problem, Does.Contain("unexpected character"));
            Assert.That(Assert.Throws<PySyntaxError>(() => E("f'{x y}'")).Problem, Does.Contain("expected"));
            Assert.That(Assert.Throws<PySyntaxError>(() => E("f'\\xZZ{x}'")).Problem, Does.Contain("escape"));
        }

        [Test]
        public void FStringErrorPositionsPointInsideTheString()
        {
            var ex = Fails("x = 1\ny = f'ab{1 +}'\n");
            Assert.That(ex.Line, Is.EqualTo(2));
            Assert.That(ex.Column, Is.GreaterThan(5));
        }

        [Test]
        public void FStringLexErrorsInsideExpressionAreShifted()
        {
            var ex = Fails("y = f'ab{$}'\n");
            Assert.That(ex.Line, Is.EqualTo(1));
            Assert.That(ex.Column, Is.EqualTo(10));
        }

        [Test]
        public void FStringErrorsOnLaterLinesOfTripleQuotedString()
        {
            var ex = Fails("y = f'''a\nbc{1 +}'''\n");
            Assert.That(ex.Line, Is.EqualTo(2));
        }

        [Test]
        public void ParseExpressionRejectsTrailingTokens()
        {
            Assert.Throws<PySyntaxError>(() => Parser.ParseExpression("1 2"));
            Assert.Throws<PySyntaxError>(() => Parser.ParseExpression("1 +"));
        }

        // ---- Statements ---------------------------------------------------------------------------------------

        [Test]
        public void ExpressionStatement() => Assert.That(M("f(1)\n"), Is.EqualTo("(expr (call f 1))"));

        [Test]
        public void SimpleAssignment() => Assert.That(M("x = 1 + 2\n"), Is.EqualTo("(assign x = (+ 1 2))"));

        [Test]
        public void ChainedAssignment() => Assert.That(M("a = b = 3\n"), Is.EqualTo("(assign a b = 3)"));

        [Test]
        public void TupleUnpackingAssignment()
        {
            Assert.That(M("a, b = 1, 2\n"), Is.EqualTo("(assign (tuple a b) = (tuple 1 2))"));
            Assert.That(M("(a, b) = c\n"), Is.EqualTo("(assign (tuple a b) = c)"));
            Assert.That(M("[a, b] = c\n"), Is.EqualTo("(assign [a b] = c)"));
            Assert.That(M("a, = c\n"), Is.EqualTo("(assign (tuple a) = c)"));
        }

        [Test]
        public void AssignmentToSubscriptAndAttribute()
        {
            Assert.That(M("a[0] = 1\n"), Is.EqualTo("(assign (idx a 0) = 1)"));
            Assert.That(M("a.b = 1\n"), Is.EqualTo("(assign (. a b) = 1)"));
            Assert.That(M("a[0], a.b = 1, 2\n"), Is.EqualTo("(assign (tuple (idx a 0) (. a b)) = (tuple 1 2))"));
        }

        [Test]
        public void TrailingCommaMakesATupleValue() => Assert.That(M("x = 1,\n"), Is.EqualTo("(assign x = (tuple 1))"));

        [TestCase("x += 1", "(aug += x 1)")]
        [TestCase("x -= 1", "(aug -= x 1)")]
        [TestCase("x *= 1", "(aug *= x 1)")]
        [TestCase("x /= 1", "(aug /= x 1)")]
        [TestCase("x //= 1", "(aug //= x 1)")]
        [TestCase("x %= 1", "(aug %= x 1)")]
        [TestCase("x **= 1", "(aug **= x 1)")]
        [TestCase("a[0] += 1", "(aug += (idx a 0) 1)")]
        [TestCase("a.b -= 1", "(aug -= (. a b) 1)")]
        public void AugmentedAssignment(string src, string expected) => Assert.That(M(src + "\n"), Is.EqualTo(expected));

        [Test]
        public void SimpleKeywordStatements()
        {
            Assert.That(M("pass\nbreak\ncontinue\n"), Is.EqualTo("pass\nbreak\ncontinue"));
        }

        [Test]
        public void ReturnStatements()
        {
            Assert.That(M("return\n"), Is.EqualTo("(return)"));
            Assert.That(M("return 1\n"), Is.EqualTo("(return 1)"));
            Assert.That(M("return 1, 2\n"), Is.EqualTo("(return (tuple 1 2))"));
        }

        [Test]
        public void GlobalStatement()
        {
            Assert.That(M("global a\n"), Is.EqualTo("(global a)"));
            Assert.That(M("global a, b, c\n"), Is.EqualTo("(global a b c)"));
        }

        [Test]
        public void SemicolonSeparatedStatements()
        {
            Assert.That(M("a = 1; b = 2\n"), Is.EqualTo("(assign a = 1)\n(assign b = 2)"));
            Assert.That(M("a = 1;\n"), Is.EqualTo("(assign a = 1)"));
        }

        [Test]
        public void IfElifElse()
        {
            Assert.That(M("if a:\n    x\nelif b:\n    y\nelse:\n    z\n"),
                Is.EqualTo("(if a [(expr x)] [(if b [(expr y)] [(expr z)])])"));
        }

        [Test]
        public void IfWithoutElse() => Assert.That(M("if a:\n    x\n    y\n"), Is.EqualTo("(if a [(expr x); (expr y)])"));

        [Test]
        public void IfWithInlineBody() => Assert.That(M("if a: x = 1; y = 2\n"), Is.EqualTo("(if a [(assign x = 1); (assign y = 2)])"));

        [Test]
        public void ElifChainWithoutFinalElse()
        {
            Assert.That(M("if a:\n    x\nelif b:\n    y\n"), Is.EqualTo("(if a [(expr x)] [(if b [(expr y)])])"));
        }

        [Test]
        public void WhileLoop() => Assert.That(M("while a < 3:\n    a += 1\n"), Is.EqualTo("(while (cmp a < 3) [(aug += a 1)])"));

        [Test]
        public void ForLoop() => Assert.That(M("for i in range(3):\n    f(i)\n"), Is.EqualTo("(for i (call range 3) [(expr (call f i))])"));

        [Test]
        public void ForLoopWithTupleTarget()
        {
            Assert.That(M("for k, v in d:\n    pass\n"), Is.EqualTo("(for (tuple k v) d [pass])"));
            Assert.That(M("for (k, v) in d:\n    pass\n"), Is.EqualTo("(for (tuple k v) d [pass])"));
        }

        [Test]
        public void ForLoopIterableMayBeATuple() => Assert.That(M("for x in 1, 2:\n    pass\n"), Is.EqualTo("(for x (tuple 1 2) [pass])"));

        [Test]
        public void InOperatorStillWorksInsideBracketsInForTarget() =>
            Assert.That(M("for x in a in b:\n    pass\n"), Is.EqualTo("(for x (cmp a in b) [pass])"));

        [Test]
        public void InOperatorInsideCallInsideForHeader() =>
            Assert.That(M("for x in f(a in b):\n    pass\n"), Is.EqualTo("(for x (call f (cmp a in b)) [pass])"));

        [Test]
        public void ForTargetCanBeSubscriptOrAttribute()
        {
            Assert.That(M("for a[0] in b:\n    pass\n"), Is.EqualTo("(for (idx a 0) b [pass])"));
            Assert.That(M("for a.x in b:\n    pass\n"), Is.EqualTo("(for (. a x) b [pass])"));
        }

        [Test]
        public void FunctionDefinition()
        {
            Assert.That(M("def f(a, b):\n    return a + b\n"), Is.EqualTo("(def f (a b) [(return (+ a b))])"));
            Assert.That(M("def f():\n    pass\n"), Is.EqualTo("(def f () [pass])"));
        }

        [Test]
        public void FunctionDefaults()
        {
            Assert.That(M("def f(a, b=2, c=[]):\n    pass\n"), Is.EqualTo("(def f (a b=2 c=[]) [pass])"));
            Assert.That(M("def f(a, b=2,):\n    pass\n"), Is.EqualTo("(def f (a b=2) [pass])"));
        }

        [Test]
        public void TryExcept()
        {
            Assert.That(M("try:\n    a\nexcept:\n    b\n"), Is.EqualTo("(try [(expr a)] (except _ [(expr b)]))"));
            Assert.That(M("try:\n    a\nexcept ValueError:\n    b\nexcept (A, B) as e:\n    c\nexcept:\n    d\n"),
                Is.EqualTo("(try [(expr a)] (except ValueError [(expr b)]) (except (tuple A B) as e [(expr c)]) (except _ [(expr d)]))"));
        }

        [Test]
        public void Raise()
        {
            Assert.That(M("raise\n"), Is.EqualTo("(raise)"));
            Assert.That(M("raise ValueError('x')\n"), Is.EqualTo("(raise (call ValueError 'x'))"));
        }

        [Test]
        public void Imports()
        {
            Assert.That(M("import game\n"), Is.EqualTo("(import game)"));
            Assert.That(M("import game as g\n"), Is.EqualTo("(import game as g)"));
            Assert.That(M("from game import move\n"), Is.EqualTo("(from game import move)"));
            Assert.That(M("from game import move as m, scan, look as l\n"), Is.EqualTo("(from game import move as m, scan, look as l)"));
        }

        [Test]
        public void NodesCarryPositions()
        {
            var module = Parser.ParseModule("x = 1\n  \nif x:\n    y = f(2)\n");
            var assign = module.Body[0];
            Assert.That((assign.Line, assign.Column), Is.EqualTo((1, 1)));
            var ifs = (IfStmt)module.Body[1];
            Assert.That((ifs.Line, ifs.Column), Is.EqualTo((3, 1)));
            var inner = (AssignStmt)ifs.Body[0];
            Assert.That((inner.Line, inner.Column), Is.EqualTo((4, 5)));
            var call = (CallExpr)inner.Value;
            Assert.That((call.Line, call.Column), Is.EqualTo((4, 10)));
            Assert.That((module.Line, module.Column), Is.EqualTo((1, 1)));
        }

        [Test]
        public void ParameterCarriesPosition()
        {
            var def = (DefStmt)Parser.ParseModule("def f(a, b):\n    pass\n").Body[0];
            Assert.That((def.Params[1].Line, def.Params[1].Column), Is.EqualTo((1, 10)));
        }

        [Test]
        public void EmptyAndBlankSourceParse()
        {
            Assert.That(Parser.ParseModule("").Body, Is.Empty);
            Assert.That(Parser.ParseModule("\n\n# hi\n").Body, Is.Empty);
        }

        [Test]
        public void BlockEndingAtEndOfFileWithoutNewline() => Assert.That(M("if a:\n    b"), Is.EqualTo("(if a [(expr b)])"));

        [Test]
        public void NestedBlocks()
        {
            Assert.That(M("def f():\n    for i in x:\n        if i:\n            break\n    return 1\n"),
                Is.EqualTo("(def f () [(for i x [(if i [break])]); (return 1)])"));
        }

        // ---- Errors -------------------------------------------------------------------------------------------

        [TestCase("await x", "'await' is not supported yet")]
        public void UnsupportedKeywordsAreRejected(string src, string message)
        {
            Assert.That(Fails(src + "\n").Problem, Is.EqualTo(message));
        }

        [TestCase("f(x for x in y, 1)", "generator expression must be parenthesized")]
        [TestCase("f(1, x for x in y)", "generator expression must be parenthesized")]
        [TestCase("f(x for x in y,)", "generator expression must be parenthesized")]
        [TestCase("[a, b for a in y]", "expected ']'")]
        [TestCase("[x for x in]", "unexpected")]
        [TestCase("[x for in y]", "unexpected")]
        [TestCase("[x for 1 in y]", "assign")]
        [TestCase("[x for x y]", "expected 'in'")]
        [TestCase("[x for x in y", "never closed")]
        [TestCase("{1 for x in y if}", "unexpected")]
        [TestCase("{1, 2 for x in y}", "expected '}'")]
        [TestCase("{1: 2, 3: 4 for x in y}", "expected '}'")]
        [TestCase("(x for x in y", "never closed")]
        public void BadComprehensionsAreRejected(string src, string message)
        {
            Assert.That(Fails(src + "\n").Problem, Does.Contain(message));
        }

        [Test]
        public void ComprehensionsBecomeCallsOfAnonymousFunctions()
        {
            Assert.DoesNotThrow(() => Parser.ParseModule("x = [i for i in y if i for j in i]\ny = {i: 1 for i in z}\nw = (i for i in z)\nf(i for i in z)\n"));
        }

        [Test]
        public void SetLiteralsParseAndMixedDisplaysAreRejected()
        {
            Assert.DoesNotThrow(() => Parser.ParseModule("x = {1, 2}\ny = {1}\nz = {1, 2,}\nw = {i for i in y if i}\n"));
            Assert.That(Fails("x = {1: 2, 3}\n").Problem, Does.Contain("expected ':'"));
            Assert.That(Fails("x = {1, 2: 3}\n").Problem, Does.Contain("makes no sense in a set"));
            Assert.That(Fails("{1, 2} = x\n").Problem, Does.Contain("cannot assign to set display"));
        }

        [Test]
        public void DictUnpackingParses()
        {
            Assert.That(M("x = {**a}\n"), Does.Contain("{**a}"));
            Assert.That(M("x = {'k': 1, **a, 'j': 2}\n"), Does.Contain("**a"));
            Assert.That(Fails("x = {**a: 1}\n").Problem, Does.Contain("':'").Or.Contain("expected"));
        }

        [TestCase("f(a=1, *b)", "iterable argument unpacking follows keyword argument")]
        [TestCase("f(**a, b)", "positional argument follows keyword argument")]
        [TestCase("f(a=1, a=2)", "keyword argument repeated: a")]
        [TestCase("def f(*): pass", "named arguments must follow bare *")]
        [TestCase("def f(*, **k): pass", "named arguments must follow bare *")]
        [TestCase("def f(*, a, *, b): pass", "* argument may appear only once")]
        [TestCase("def f(*a, *b): pass", "* argument may appear only once")]
        [TestCase("def f(**k, a): pass", "arguments cannot follow var-keyword argument")]
        [TestCase("def f(**k, **j): pass", "arguments cannot follow var-keyword argument")]
        [TestCase("def f(**k=1): pass", "var-keyword argument cannot have default value")]
        [TestCase("def f(*a=1): pass", "var-positional argument cannot have default value")]
        [TestCase("def f(a=1, b): pass", "non-default argument 'b' follows default argument")]
        [TestCase("def f(*, a=1, b): pass", null)]
        [TestCase("def f(*1): pass", "expected a parameter name")]
        [TestCase("def f(*a, a): pass", null)]
        public void BadVariableArgumentsAreRejected(string src, string message)
        {
            if (message == null)
            {
                // Legal syntax here (the second case is a duplicate caught by the resolver).
                Assert.DoesNotThrow(() => Parser.ParseModule(src + "\n"));
                return;
            }
            Assert.That(Fails(src + "\n").Problem, Does.Contain(message));
        }

        [Test]
        public void VariableArgumentsParse()
        {
            Assert.DoesNotThrow(() => Parser.ParseModule("def f(a, b=1, *args, c, d=2, **kw): pass\ndef g(*, a): pass\nh = lambda *a, **k: 0\nf(*x, y, *z, k=1, **w, **v)\nf(*(1, 2), **{})\n"));
        }

        [Test]
        public void ParameterAnnotationsAreRejected() => Assert.That(Fails("def f(a: int): pass\n").Problem, Does.Contain("annotations"));

        [Test]
        public void NonDefaultAfterDefaultIsRejected() => Assert.That(Fails("def f(a=1, b): pass\n").Problem, Does.Contain("non-default argument 'b'"));

        [Test]
        public void KeywordArgumentErrors()
        {
            Assert.That(Fails("f(a=1, 2)\n").Problem, Does.Contain("positional argument follows keyword"));
            Assert.That(Fails("f(a=1, a=2)\n").Problem, Does.Contain("repeated"));
        }

        [TestCase("1 = x", "cannot assign to literal")]
        [TestCase("f() = x", "cannot assign to function call")]
        [TestCase("a + b = x", "cannot assign to expression")]
        [TestCase("-a = x", "cannot assign to expression")]
        [TestCase("a and b = x", "cannot assign to expression")]
        [TestCase("a < b = x", "cannot assign to comparison")]
        [TestCase("{} = x", "cannot assign to dict literal")]
        [TestCase("f'{a}' = x", "cannot assign to f-string expression")]
        [TestCase("(a if b else c) = x", "cannot assign to conditional expression")]
        [TestCase("a, 1 = x", "cannot assign to literal")]
        [TestCase("[a, 1] = x", "cannot assign to literal")]
        [TestCase("a[0].b() = x", "cannot assign to function call")]
        public void InvalidAssignmentTargets(string src, string message) => Assert.That(Fails(src + "\n").Problem, Is.EqualTo(message));

        [Test]
        public void InvalidForTarget() => Assert.That(Fails("for 1 in x:\n    pass\n").Problem, Does.Contain("cannot assign"));

        [Test]
        public void InvalidAugmentedTargets()
        {
            Assert.That(Fails("a, b += 1\n").Problem, Does.Contain("augmented"));
            Assert.That(Fails("f() += 1\n").Problem, Does.Contain("augmented"));
        }

        [Test]
        public void MissingColonIsReported() => Assert.That(Fails("if a\n    b\n").Problem, Does.Contain("expected ':'"));

        [Test]
        public void MissingIndentedBlockIsReported()
        {
            Assert.That(Fails("if a:\nb\n").Problem, Does.Contain("expected an indented block after 'if' statement"));
            Assert.That(Fails("def f():\nb\n").Problem, Does.Contain("function definition"));
            Assert.That(Fails("if a:\n").Problem, Does.Contain("indented block"));
        }

        [Test]
        public void UnexpectedIndentIsReported()
        {
            Assert.That(Fails("x = 1\n    y = 2\n").Problem, Is.EqualTo("unexpected indent"));
            Assert.That(Fails("if a:\n    x\n        y\n").Problem, Is.EqualTo("unexpected indent"));
        }

        [Test]
        public void UnexpectedTokensAreDescribed()
        {
            Assert.That(Fails("x = )\n").Problem, Does.Contain("unmatched"));
            Assert.That(Fails("x = +\n").Problem, Is.EqualTo("unexpected end of line"));
            Assert.That(Fails("x = 1 2\n").Problem, Is.EqualTo("unexpected '2'"));
            Assert.That(Fails("x = 'a' 3\n").Problem, Is.EqualTo("unexpected '3'"));
            Assert.That(Fails("x = a 'b'\n").Problem, Is.EqualTo("unexpected string"));
            Assert.That(Fails("else:\n    pass\n").Problem, Is.EqualTo("unexpected 'else'"));
        }

        [Test]
        public void UnexpectedEndOfFile() => Assert.That(Fails("x = ").Problem, Is.EqualTo("unexpected end of line"));

        [Test]
        public void ConditionalExpressionNeedsElse() => Assert.That(Fails("x = a if b\n").Problem, Does.Contain("'else'"));

        [Test]
        public void LoopsParseTheirElseBlocks()
        {
            var w = (WhileStmt)Parser.ParseModule("while a:\n    pass\nelse:\n    x = 1\n").Body[0];
            Assert.That(w.Else.Count, Is.EqualTo(1));
            var f = (ForStmt)Parser.ParseModule("for a in b:\n    pass\nelse:\n    x = 1\n    y = 2\n").Body[0];
            Assert.That(f.Else.Count, Is.EqualTo(2));
            Assert.That(((ForStmt)Parser.ParseModule("for a in b:\n    pass\n").Body[0]).Else, Is.Empty);
            Assert.That(Fails("for a in b:\n    pass\nelse\n    pass\n").Problem, Does.Contain("':'"));
            Assert.That(Fails("while a:\n    pass\nelse:\npass\n").Problem, Does.Contain("indented block"));
        }

        [Test]
        public void TryErrors()
        {
            Assert.That(Fails("try:\n    a\n").Problem, Does.Contain("expected an 'except' or 'finally' block"));
            Assert.That(Fails("try:\n    a\nelse:\n    b\n").Problem, Does.Contain("'else' needs an 'except' block"));
            Assert.That(Fails("try:\n    a\nfinally\n").Problem, Does.Contain("':'"));
            Assert.That(Fails("try:\n    a\nexcept:\n    b\nelse\n").Problem, Does.Contain("':'"));
            Assert.That(Fails("try:\n    a\nexcept:\n    b\nexcept ValueError:\n    c\n").Problem, Does.Contain("default 'except:' must be last"));
        }

        [Test]
        public void ImportErrors()
        {
            Assert.That(Fails("import a.b\n").Problem, Does.Contain("dotted"));
            Assert.That(Fails("from a.b import c\n").Problem, Does.Contain("dotted"));
            Assert.That(Fails("import a, b\n").Problem, Does.Contain("several modules"));
            Assert.That(Fails("from a import *\n").Problem, Does.Contain("import *"));
            Assert.That(Fails("from a b\n").Problem, Does.Contain("expected 'import'"));
            Assert.That(Fails("import 1\n").Problem, Does.Contain("module name"));
        }

        [Test]
        public void RaiseFromIsRejected() => Assert.That(Fails("raise A from B\n").Problem, Does.Contain("raise ... from"));

        [Test]
        public void MissingClosersAreReported()
        {
            Assert.That(Fails("f(1\n").Problem, Does.Contain("never closed"));
            Assert.That(Fails("f(1 2)\n").Problem, Does.Contain("expected ')'"));
            Assert.That(Fails("a[1 2]\n").Problem, Does.Contain("expected ']'"));
            Assert.That(Fails("x = [1 2]\n").Problem, Does.Contain("expected ']'"));
            Assert.That(Fails("x = {1: 2 3: 4}\n").Problem, Does.Contain("expected '}'"));
            Assert.That(Fails("x = (1 2)\n").Problem, Does.Contain("expected ')'"));
            Assert.That(Fails("x = (1, 2 3)\n").Problem, Does.Contain("expected ')'"));
        }

        [Test]
        public void AttributeNeedsAName() => Assert.That(Fails("a.(1)\n").Problem, Does.Contain("expected an attribute name"));

        [Test]
        public void ErrorsCarryTokenPositions()
        {
            var ex = Fails("x = 1\ny = 2 +\n");
            Assert.That((ex.Line, ex.Column), Is.EqualTo((2, 8)));
        }

        [Test]
        public void LexerErrorsSurfaceFromTheParser() => Assert.That(Fails("x = $\n").Problem, Does.Contain("unexpected character"));

        [Test]
        public void StatementMustEndAtEndOfLine() => Assert.That(Fails("pass pass\n").Problem, Is.EqualTo("unexpected 'pass'"));

        [Test]
        public void NotWithoutInInComparisonPosition() => Assert.That(Fails("x = a not b\n").Problem, Is.EqualTo("unexpected 'not'"));

        [Test]
        public void ErrorTokenIsDescribed() => Assert.That(Fails("x = 1 \\ 2\n").Problem, Does.Contain("line continuation"));
    }
}
