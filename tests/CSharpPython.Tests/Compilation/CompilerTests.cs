using System.Linq;
using CSharpPython.Compilation;
using CSharpPython.Syntax;

namespace CSharpPython.Tests.Compilation
{
    public class CompilerTests
    {
        private static Code Compile(string src) => Compiler.Compile(Parser.ParseModule(src), "test");

        // The module listing without instruction numbers, one instruction per line.
        private static string Ops(string src)
        {
            return string.Join("\n", Compile(src).Disassemble().Split('\n', System.StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Substring(l.IndexOf(' ') + 1)));
        }

        private static string Listing(Code code)
        {
            return string.Join("\n", code.Disassemble().Split('\n', System.StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Substring(l.IndexOf(' ') + 1)));
        }

        private static string Body(string src)
        {
            // Drops the trailing 'LoadConst None / Return' every module ends with.
            string[] lines = Ops(src).Split('\n');
            return string.Join("\n", lines.Take(lines.Length - 2));
        }

        [Test]
        public void EmptyModuleReturnsNone()
        {
            Assert.That(Ops(""), Is.EqualTo("LoadConst None\nReturn"));
        }

        [Test]
        public void ModuleMetadata()
        {
            var code = Compile("x = 1");
            Assert.That(code.IsModule, Is.True);
            Assert.That(code.Name, Is.EqualTo("<module>"));
            Assert.That(code.SourceName, Is.EqualTo("test"));
            Assert.That(code.NumLocals, Is.EqualTo(0));
            Assert.That(code.Lines.Length, Is.EqualTo(code.Instructions.Length));
        }

        [Test]
        public void GlobalAssignmentAtModuleLevel()
        {
            Assert.That(Body("x = 1 + 2\n"), Is.EqualTo("LoadConst 1\nLoadConst 2\nAdd\nStoreGlobal x"));
        }

        [Test]
        public void ConstantsAreDeduplicated()
        {
            var code = Compile("a = 1\nb = 1\nc = 'x'\nd = 'x'\ne = 1.5\nf = 1.5\ng = True\nh = None\ni = None\n");
            Assert.That(code.Constants.Length, Is.EqualTo(5)); // 1, 'x', 1.5, True, None
        }

        [Test]
        public void NegativeZeroAndZeroAreDistinctConstants()
        {
            var code = Compile("a = 0.0\nb = -0.0\n");
            Assert.That(code.Constants.Count(c => c.Kind == ValueKind.Float), Is.EqualTo(2));
        }

        [Test]
        public void ChainedAssignmentDuplicatesTheValue()
        {
            Assert.That(Body("a = b = 3\n"), Is.EqualTo("LoadConst 3\nDup\nStoreGlobal a\nStoreGlobal b"));
        }

        [Test]
        public void TupleUnpackingUsesUnpackSequence()
        {
            Assert.That(Body("a, b = c\n"), Is.EqualTo("LoadGlobal c\nUnpackSequence 2\nStoreGlobal a\nStoreGlobal b"));
            Assert.That(Body("[a, b] = c\n"), Is.EqualTo("LoadGlobal c\nUnpackSequence 2\nStoreGlobal a\nStoreGlobal b"));
        }

        [Test]
        public void SubscriptAndAttributeStores()
        {
            Assert.That(Body("a[0] = 1\n"), Is.EqualTo("LoadConst 1\nLoadGlobal a\nLoadConst 0\nStoreIndex"));
            Assert.That(Body("a.b = 1\n"), Is.EqualTo("LoadConst 1\nLoadGlobal a\nStoreAttr b"));
        }

        [Test]
        public void AugmentedAssignmentForms()
        {
            Assert.That(Body("x += 1\n"), Is.EqualTo("LoadGlobal x\nLoadConst 1\nAdd\nStoreGlobal x"));
            Assert.That(Body("a[i] -= 1\n"), Is.EqualTo("LoadGlobal a\nLoadGlobal i\nDup2\nLoadIndex\nLoadConst 1\nSub\nRot3\nStoreIndex"));
            Assert.That(Body("a.b *= 2\n"), Is.EqualTo("LoadGlobal a\nDup\nLoadAttr b\nLoadConst 2\nMul\nRot2\nStoreAttr b"));
        }

        [TestCase("+", "Add")]
        [TestCase("-", "Sub")]
        [TestCase("*", "Mul")]
        [TestCase("/", "Div")]
        [TestCase("//", "FloorDiv")]
        [TestCase("%", "Mod")]
        [TestCase("**", "Pow")]
        public void BinaryOperators(string op, string name)
        {
            Assert.That(Body("a " + op + " b\n"), Does.Contain(name));
            Assert.That(Body("a " + op + "= b\n"), Does.Contain(name));
        }

        [TestCase("==", "Eq")]
        [TestCase("!=", "Ne")]
        [TestCase("<", "Lt")]
        [TestCase("<=", "Le")]
        [TestCase(">", "Gt")]
        [TestCase(">=", "Ge")]
        [TestCase("is", "Is")]
        [TestCase("is not", "IsNot")]
        [TestCase("in", "In")]
        [TestCase("not in", "NotIn")]
        public void ComparisonOperators(string op, string name)
        {
            Assert.That(Body("a " + op + " b\n"), Does.Contain("\n" + name + "\n"));
        }

        [Test]
        public void UnaryOperators()
        {
            Assert.That(Body("-a\n"), Does.Contain("Negate"));
            Assert.That(Body("+a\n"), Does.Contain("Plus"));
            Assert.That(Body("not a\n"), Does.Contain("Not"));
        }

        [Test]
        public void ChainedComparisonEvaluatesMiddleOperandsOnce()
        {
            string ops = Body("a < b < c\n");
            Assert.That(ops, Does.Contain("Dup\nRot3\nLt\nJumpIfFalseOrPop"));
            Assert.That(ops, Does.Contain("Rot2\nPop"));
            Assert.That(ops.Split('\n').Count(l => l == "LoadGlobal b"), Is.EqualTo(1));
        }

        [Test]
        public void ShortCircuitOperators()
        {
            Assert.That(Body("a and b\n"), Does.Contain("JumpIfFalseOrPop"));
            Assert.That(Body("a or b\n"), Does.Contain("JumpIfTrueOrPop"));
        }

        [Test]
        public void JumpTargetsAreResolved()
        {
            var code = Compile("if a:\n    x = 1\nelse:\n    x = 2\ny = 3\n");
            foreach (var ins in code.Instructions)
            {
                if (ins.Op == Op.Jump || ins.Op == Op.JumpIfFalse)
                    Assert.That(ins.Arg, Is.InRange(1, code.Instructions.Length));
            }
        }

        [Test]
        public void WhileLoopJumpsBackToTheTest()
        {
            var code = Compile("while a:\n    b = 1\n");
            var back = code.Instructions.First(i => i.Op == Op.Jump);
            Assert.That(back.Arg, Is.EqualTo(0));
        }

        [Test]
        public void ForLoopUsesIterators()
        {
            string ops = Body("for i in x:\n    pass\n");
            Assert.That(ops, Does.Contain("GetIter\nForIter"));
        }

        [Test]
        public void BreakInForLoopDiscardsTheIterator()
        {
            string ops = Body("for i in x:\n    break\n");
            Assert.That(ops, Does.Contain("Pop\nJump"));
        }

        [Test]
        public void BreakInWhileDoesNotPop()
        {
            var code = Compile("while a:\n    break\n");
            int jump = code.Instructions.Select((ins, i) => (ins, i)).First(t => t.ins.Op == Op.Jump).i;
            Assert.That(code.Instructions[jump - 1].Op, Is.Not.EqualTo(Op.Pop));
        }

        [Test]
        public void ContinueJumpsToLoopTop()
        {
            var code = Compile("while a:\n    continue\n");
            var jumps = code.Instructions.Where(i => i.Op == Op.Jump).ToList();
            Assert.That(jumps.All(j => j.Arg == 0), Is.True);
        }

        [Test]
        public void BreakAndContinueInsideTryPopTheHandlers()
        {
            var code = Compile("while a:\n    try:\n        break\n    except:\n        pass\n");
            Assert.That(code.Instructions.Count(i => i.Op == Op.PopTry), Is.EqualTo(2)); // one for break, one for normal exit
            var code2 = Compile("while a:\n    try:\n        continue\n    except:\n        pass\n");
            Assert.That(code2.Instructions.Count(i => i.Op == Op.PopTry), Is.EqualTo(2));
        }

        [Test]
        public void FunctionDefinitionCreatesNestedCode()
        {
            var code = Compile("def f(a, b=2):\n    c = a\n    return c\n");
            Assert.That(code.Functions.Length, Is.EqualTo(1));
            var f = code.Functions[0];
            Assert.That(f.Name, Is.EqualTo("f"));
            Assert.That(f.IsModule, Is.False);
            Assert.That(f.NumParams, Is.EqualTo(2));
            Assert.That(f.NumDefaults, Is.EqualTo(1));
            Assert.That(f.NumLocals, Is.EqualTo(3));
            Assert.That(f.ParamNames, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(Listing(f), Does.Contain("LoadLocal 0\nStoreLocal 2"));
            Assert.That(Body("def f(a, b=2):\n    pass\n"), Is.EqualTo("LoadConst 2\nMakeFunction f\nStoreGlobal f"));
        }

        [Test]
        public void FunctionsReadGlobalsWithLoadGlobalAndWriteThemWithDeclaration()
        {
            var code = Compile("def f():\n    global n\n    n = n + 1\n    return m\n");
            string d = code.Functions[0].Disassemble();
            Assert.That(d, Does.Contain("LoadGlobal n"));
            Assert.That(d, Does.Contain("StoreGlobal n"));
            Assert.That(d, Does.Contain("LoadGlobal m"));
        }

        [Test]
        public void FunctionEndsWithImplicitReturnNone()
        {
            var d = Listing(Compile("def f():\n    pass\n").Functions[0]);
            Assert.That(d, Does.Contain("LoadConst None\nReturn"));
        }

        [Test]
        public void ReturnWithoutValueReturnsNone()
        {
            var d = Compile("def f():\n    return\n").Functions[0].Disassemble();
            Assert.That(d.Split('\n')[0], Does.Contain("LoadConst None"));
        }

        [Test]
        public void MethodCallsUseLoadMethod()
        {
            Assert.That(Body("a.b(1)\n"), Is.EqualTo("LoadGlobal a\nLoadMethod b\nLoadConst 1\nCallMethod 1\nPop"));
            Assert.That(Body("f(1, 2)\n"), Is.EqualTo("LoadGlobal f\nLoadConst 1\nLoadConst 2\nCall 2\nPop"));
        }

        [Test]
        public void KeywordCallsPassNamesAsATuple()
        {
            string ops = Body("f(1, x=2)\n");
            Assert.That(ops, Does.Contain("CallKw 2"));
            Assert.That(ops, Does.Contain("LoadConst ('x',)"));
            Assert.That(Body("a.f(x=2)\n"), Does.Contain("CallMethodKw 1"));
        }

        [Test]
        public void SlicesAndSubscripts()
        {
            Assert.That(Body("a[1:2]\n"), Does.Contain("BuildSlice\nLoadIndex"));
            Assert.That(Body("a[::2]\n"), Does.Contain("LoadConst None\nLoadConst None\nLoadConst 2\nBuildSlice"));
            Assert.That(Body("a[i]\n"), Is.EqualTo("LoadGlobal a\nLoadGlobal i\nLoadIndex\nPop"));
            Assert.That(Body("a[1:2] = x\n"), Does.Contain("BuildSlice\nStoreIndex"));
        }

        [Test]
        public void CollectionLiterals()
        {
            Assert.That(Body("[1, 2]\n"), Does.Contain("BuildList 2"));
            Assert.That(Body("(1, 2)\n"), Does.Contain("BuildTuple 2"));
            Assert.That(Body("{1: 2, 3: 4}\n"), Does.Contain("BuildDict 2"));
        }

        [Test]
        public void ConditionalExpression()
        {
            Assert.That(Body("a if b else c\n"), Does.Contain("JumpIfFalse"));
        }

        [Test]
        public void FStringsCompileToFormatAndConcat()
        {
            string ops = Body("f'a{x}b{y!r:>5}'\n");
            Assert.That(ops, Does.Contain("FormatValue " + (int)'\0'));
            Assert.That(ops, Does.Contain("FormatValue " + ((int)'r' | 0x100)));
            Assert.That(ops, Does.Contain("BuildString 4"));
            Assert.That(Body("f''\n"), Does.Contain("LoadConst ''"));
        }

        [Test]
        public void TryExceptStructure()
        {
            string ops = Body("try:\n    a\nexcept ValueError as e:\n    b\n");
            Assert.That(ops, Does.Contain("SetupTry"));
            Assert.That(ops, Does.Contain("PopTry"));
            Assert.That(ops, Does.Contain("MatchException"));
            Assert.That(ops, Does.Contain("Reraise"));
            Assert.That(ops, Does.Contain("StoreGlobal e"));
        }

        [Test]
        public void BareExceptHasNoMatchAndNoReraise()
        {
            string ops = Body("try:\n    a\nexcept:\n    b\n");
            Assert.That(ops, Does.Not.Contain("MatchException"));
            Assert.That(ops, Does.Not.Contain("Reraise"));
            Assert.That(ops, Does.Contain("Pop"));
        }

        [Test]
        public void RaiseForms()
        {
            Assert.That(Body("raise E()\n"), Does.Contain("Raise"));
            Assert.That(Body("try:\n    a\nexcept:\n    raise\n"), Does.Contain("RaiseCurrent"));
        }

        [Test]
        public void BareRaiseOutsideExceptIsACompileError()
        {
            var ex = Assert.Throws<PySyntaxError>(() => Compile("raise\n"));
            Assert.That(ex.Problem, Does.Contain("bare 'raise'"));
            Assert.Throws<PySyntaxError>(() => Compile("try:\n    raise\nexcept:\n    pass\n"));
        }

        [Test]
        public void ImportForms()
        {
            Assert.That(Body("import game\n"), Is.EqualTo("Import game\nStoreGlobal game"));
            Assert.That(Body("import game as g\n"), Is.EqualTo("Import game\nStoreGlobal g"));
            Assert.That(Body("from game import a, b as c\n"), Is.EqualTo("Import game\nImportFrom a\nStoreGlobal a\nImportFrom b\nStoreGlobal c\nPop"));
        }

        [Test]
        public void ImportInsideFunctionStoresLocally()
        {
            Assert.That(Compile("def f():\n    import game\n").Functions[0].Disassemble(), Does.Contain("StoreLocal 0"));
        }

        [Test]
        public void LinesAreRecordedPerInstruction()
        {
            var code = Compile("a = 1\n\nb = 2\n");
            Assert.That(code.Lines[0], Is.EqualTo(1));
            Assert.That(code.Lines.Contains(3), Is.True);
        }

        [Test]
        public void FunctionBodyLinesPointIntoTheBody()
        {
            var f = Compile("def f():\n    x = 1\n    return x\n").Functions[0];
            Assert.That(f.Lines.Max(), Is.EqualTo(3));
        }

        [Test]
        public void DisassemblyShowsEveryOperandKind()
        {
            var code = Compile("def f(a):\n    return a\nx = f(1)\nimport game\nz = game.y\n");
            string d = code.Disassemble();
            Assert.That(d, Does.Contain("MakeFunction f"));
            Assert.That(d, Does.Contain("LoadAttr y"));
            Assert.That(d, Does.Contain("Call 1"));
        }

        [Test]
        public void InstructionToString()
        {
            Assert.That(new Instruction(Op.Jump, 5).ToString(), Is.EqualTo("Jump 5"));
        }

        [Test]
        public void ResolverErrorsSurfaceFromCompile()
        {
            Assert.Throws<PySyntaxError>(() => Compile("return 1\n"));
        }

        [Test]
        public void WhileWithStatementsAfterIt()
        {
            var code = Compile("while a:\n    b = 1\nc = 2\n");
            var exit = code.Instructions.First(i => i.Op == Op.JumpIfFalse).Arg;
            Assert.That(code.Instructions[exit].Op, Is.EqualTo(Op.LoadConst));
        }

        [Test]
        public void ForLoopTupleTarget()
        {
            Assert.That(Body("for k, v in d:\n    pass\n"), Does.Contain("ForIter"));
            Assert.That(Body("for k, v in d:\n    pass\n"), Does.Contain("UnpackSequence 2"));
        }

        [Test]
        public void IfWithoutElseHasSingleJump()
        {
            var code = Compile("if a:\n    b = 1\n");
            Assert.That(code.Instructions.Count(i => i.Op == Op.Jump), Is.EqualTo(0));
        }
    }
}
