using System.Collections.Generic;
using System.Globalization;
using CSharpPython.Syntax;

namespace CSharpPython.Compilation
{
    /// <summary>Turns a resolved syntax tree into bytecode.</summary>
    internal sealed class Compiler
    {
        private sealed class Loop
        {
            public int ContinueTarget;
            public readonly List<int> BreakJumps = new List<int>();
            public bool IsFor;
            public int TryDepth;
            public int FinallyLevel;
        }

        // A try statement with a finally block that the code being compiled is currently inside.
        private sealed class Guard
        {
            public List<Stmt> Body;
            // The number of active handlers once this guard's own handler is set up.
            public int Depth;
        }

        // The code being generated for one module or function.
        private sealed class Unit
        {
            public readonly Scope Scope;
            public readonly string Name;
            public readonly List<Instruction> Instructions = new List<Instruction>();
            public readonly List<int> Lines = new List<int>();
            public readonly List<Value> Constants = new List<Value>();
            public readonly Dictionary<string, int> ConstantIndex = new Dictionary<string, int>();
            public readonly List<string> Names = new List<string>();
            public readonly Dictionary<string, int> NameIndex = new Dictionary<string, int>();
            public readonly List<Code> Functions = new List<Code>();
            public readonly List<Loop> Loops = new List<Loop>();
            public readonly List<Guard> Guards = new List<Guard>();
            // Greater than zero while compiling a copy of a finally block.
            public int InFinally;
            public int TryDepth;
            public int HandlerDepth;
            public int Line = 1;

            public Unit(Scope scope, string name)
            {
                Scope = scope;
                Name = name;
            }
        }

        private readonly ResolvedModule _resolved;
        private readonly string _sourceName;
        private Unit _unit;

        private Compiler(ResolvedModule resolved, string sourceName)
        {
            _resolved = resolved;
            _sourceName = sourceName;
        }

        public static Code Compile(ModuleNode module, string sourceName)
        {
            var resolved = Resolver.Resolve(module);
            var compiler = new Compiler(resolved, sourceName);
            return compiler.CompileModule();
        }

        private Code CompileModule()
        {
            _unit = new Unit(_resolved.ModuleScope, "<module>");
            CompileBody(_resolved.Module.Body);
            Emit(Op.LoadConst, Constant(Value.None));
            Emit(Op.Return);
            return Finish(true, null, 0, 0);
        }

        private Code Finish(bool isModule, DefStmt def, int numDefaults, int numPosDefaults)
        {
            var u = _unit;
            var code = new Code
            {
                Name = u.Name,
                SourceName = _sourceName,
                Instructions = u.Instructions.ToArray(),
                Lines = u.Lines.ToArray(),
                Constants = u.Constants.ToArray(),
                Names = u.Names.ToArray(),
                Functions = u.Functions.ToArray(),
                NumLocals = u.Scope.LocalCount,
                LocalNames = u.Scope.Locals.ToArray(),
                FreeNames = u.Scope.FreeVars.ToArray(),
                IsModule = isModule,
                IsGenerator = u.Scope.IsGenerator,
                NumDefaults = numDefaults,
                NumPosDefaults = numPosDefaults,
            };
            if (def != null)
            {
                code.NumParams = def.Params.Count;
                code.NumKwOnly = def.KeywordOnly.Count;
                code.HasVarArgs = def.VarArgs != null;
                code.HasVarKeywords = def.VarKeywords != null;
                code.ParamNames = new string[def.Params.Count + def.KeywordOnly.Count];
                for (int i = 0; i < def.Params.Count; i++) code.ParamNames[i] = def.Params[i].Name;
                code.KwDefaultIndex = new int[def.KeywordOnly.Count];
                int kwDefaults = 0;
                for (int i = 0; i < def.KeywordOnly.Count; i++)
                {
                    code.ParamNames[def.Params.Count + i] = def.KeywordOnly[i].Name;
                    code.KwDefaultIndex[i] = def.KeywordOnly[i].Default != null ? kwDefaults++ : -1;
                }
            }
            else
            {
                code.ParamNames = System.Array.Empty<string>();
                code.KwDefaultIndex = System.Array.Empty<int>();
            }
            return code;
        }

        // ---- Emission helpers -----------------------------------------------------------------------------------

        private int Emit(Op op, int arg = 0)
        {
            _unit.Instructions.Add(new Instruction(op, arg));
            _unit.Lines.Add(_unit.Line);
            return _unit.Instructions.Count - 1;
        }

        private int Here => _unit.Instructions.Count;

        private void Patch(int at)
        {
            _unit.Instructions[at] = new Instruction(_unit.Instructions[at].Op, Here);
        }

        private void SetLine(Node node)
        {
            if (node.Line > 0) _unit.Line = node.Line;
        }

        private int Constant(Value v)
        {
            string key;
            switch (v.Kind)
            {
                case ValueKind.None: key = "n"; break;
                case ValueKind.Bool: key = "b" + v.RawInt; break;
                case ValueKind.Int: key = "i" + v.RawInt.ToString(CultureInfo.InvariantCulture); break;
                case ValueKind.Float: key = "f" + System.BitConverter.DoubleToInt64Bits(v.RawFloat).ToString(CultureInfo.InvariantCulture); break;
                case ValueKind.Str: key = "s" + (string)v.RawObject; break;
                default: key = null; break;
            }
            if (key != null && _unit.ConstantIndex.TryGetValue(key, out int existing)) return existing;
            _unit.Constants.Add(v);
            int index = _unit.Constants.Count - 1;
            if (key != null) _unit.ConstantIndex[key] = index;
            return index;
        }

        private int Name(string name)
        {
            if (_unit.NameIndex.TryGetValue(name, out int index)) return index;
            _unit.Names.Add(name);
            index = _unit.Names.Count - 1;
            _unit.NameIndex[name] = index;
            return index;
        }

        private void LoadName(string name)
        {
            var scope = _unit.Scope;
            if (scope.TryGetLocal(name, out int slot)) Emit(scope.IsCell(name) ? Op.LoadCell : Op.LoadLocal, slot);
            else if (scope.TryGetFree(name, out int free)) Emit(Op.LoadFree, free);
            else Emit(Op.LoadGlobal, Name(name));
        }

        private void StoreName(string name)
        {
            var scope = _unit.Scope;
            if (scope.TryGetLocal(name, out int slot)) Emit(scope.IsCell(name) ? Op.StoreCell : Op.StoreLocal, slot);
            else if (scope.TryGetFree(name, out int free)) Emit(Op.StoreFree, free);
            else Emit(Op.StoreGlobal, Name(name));
        }

        private void DeleteName(string name)
        {
            var scope = _unit.Scope;
            if (scope.TryGetLocal(name, out int slot)) Emit(scope.IsCell(name) ? Op.DeleteCell : Op.DeleteLocal, slot);
            else if (scope.TryGetFree(name, out int free)) Emit(Op.DeleteFree, free);
            else Emit(Op.DeleteGlobal, Name(name));
        }

        private void CompileDelete(DelStmt d)
        {
            foreach (var target in d.Targets)
            {
                switch (target)
                {
                    case NameExpr n:
                        SetLine(n);
                        DeleteName(n.Name);
                        break;
                    case AttributeExpr a:
                        CompileExpr(a.Target);
                        SetLine(a);
                        Emit(Op.DeleteAttr, Name(a.Name));
                        break;
                    case SubscriptExpr s:
                        CompileExpr(s.Target);
                        CompileIndex(s.Index);
                        SetLine(s);
                        Emit(Op.DeleteIndex);
                        break;
                }
            }
        }

        private static PySyntaxError Error(string message, Node at) => new PySyntaxError(message, at.Line, at.Column);

        // ---- Statements -----------------------------------------------------------------------------------------

        private void CompileBody(List<Stmt> body)
        {
            foreach (var s in body) CompileStmt(s);
        }

        private void CompileStmt(Stmt stmt)
        {
            SetLine(stmt);
            switch (stmt)
            {
                case ExprStmt e:
                    CompileExpr(e.Value);
                    Emit(Op.Pop);
                    break;
                case AssignStmt a: CompileAssign(a); break;
                case AugAssignStmt a: CompileAugAssign(a); break;
                case DelStmt d: CompileDelete(d); break;
                case IfStmt i: CompileIf(i); break;
                case WhileStmt w: CompileWhile(w); break;
                case ForStmt f: CompileFor(f); break;
                case DefStmt d: CompileDef(d); break;
                case ClassStmt c: CompileClass(c); break;
                case ReturnStmt r:
                    if (_unit.InFinally > 0) throw Error("'return' inside a 'finally' block is not supported", r);
                    if (r.Value != null) CompileExpr(r.Value);
                    else Emit(Op.LoadConst, Constant(Value.None));
                    SetLine(stmt);
                    RunGuards(0, false);
                    Emit(Op.Return);
                    break;
                case BreakStmt b: CompileBreak(b); break;
                case ContinueStmt c: CompileContinue(c); break;
                case TryStmt t: CompileTry(t); break;
                case RaiseStmt r: CompileRaise(r); break;
                case ImportStmt i:
                    Emit(Op.Import, Name(i.Module));
                    StoreName(i.Alias ?? i.Module);
                    break;
                case ImportFromStmt i:
                    Emit(Op.Import, Name(i.Module));
                    for (int k = 0; k < i.Names.Count; k++)
                    {
                        Emit(Op.ImportFrom, Name(i.Names[k]));
                        StoreName(i.Aliases[k] ?? i.Names[k]);
                    }
                    Emit(Op.Pop);
                    break;
                case PassStmt _:
                case GlobalStmt _:
                case NonlocalStmt _:
                    break;
            }
        }

        private void CompileAssign(AssignStmt a)
        {
            CompileExpr(a.Value);
            for (int i = 0; i < a.Targets.Count; i++)
            {
                if (i < a.Targets.Count - 1) Emit(Op.Dup);
                CompileStore(a.Targets[i]);
            }
        }

        // Stores the value on top of the stack into the target.
        private void CompileStore(Expr target)
        {
            switch (target)
            {
                case NameExpr n:
                    StoreName(n.Name);
                    break;
                case AttributeExpr a:
                    CompileExpr(a.Target);
                    SetLine(a);
                    Emit(Op.StoreAttr, Name(a.Name));
                    break;
                case SubscriptExpr s:
                    CompileExpr(s.Target);
                    CompileIndex(s.Index);
                    SetLine(s);
                    Emit(Op.StoreIndex);
                    break;
                case TupleExpr t:
                    Emit(Op.UnpackSequence, t.Items.Count);
                    foreach (var item in t.Items) CompileStore(item);
                    break;
                case ListExpr l:
                    Emit(Op.UnpackSequence, l.Items.Count);
                    foreach (var item in l.Items) CompileStore(item);
                    break;
            }
        }

        private void CompileAugAssign(AugAssignStmt a)
        {
            switch (a.Target)
            {
                case NameExpr n:
                    LoadName(n.Name);
                    CompileExpr(a.Value);
                    SetLine(a);
                    Emit(BinaryOpCode(a.Op), 1); // an augmented assignment tries the in-place method first
                    StoreName(n.Name);
                    break;
                case AttributeExpr attr:
                    CompileExpr(attr.Target);
                    Emit(Op.Dup);
                    SetLine(attr);
                    Emit(Op.LoadAttr, Name(attr.Name));
                    CompileExpr(a.Value);
                    SetLine(a);
                    Emit(BinaryOpCode(a.Op), 1); // an augmented assignment tries the in-place method first
                    Emit(Op.Rot2);
                    Emit(Op.StoreAttr, Name(attr.Name));
                    break;
                case SubscriptExpr sub:
                    CompileExpr(sub.Target);
                    CompileIndex(sub.Index);
                    Emit(Op.Dup2);
                    SetLine(sub);
                    Emit(Op.LoadIndex);
                    CompileExpr(a.Value);
                    SetLine(a);
                    Emit(BinaryOpCode(a.Op), 1); // an augmented assignment tries the in-place method first
                    Emit(Op.Rot3);
                    Emit(Op.StoreIndex);
                    break;
            }
        }

        private void CompileIf(IfStmt i)
        {
            CompileExpr(i.Test);
            int toElse = Emit(Op.JumpIfFalse);
            CompileBody(i.Body);
            if (i.Else.Count == 0)
            {
                Patch(toElse);
                return;
            }
            int toEnd = Emit(Op.Jump);
            Patch(toElse);
            CompileBody(i.Else);
            Patch(toEnd);
        }

        private void CompileWhile(WhileStmt w)
        {
            int top = Here;
            CompileExpr(w.Test);
            int toEnd = Emit(Op.JumpIfFalse);
            var loop = new Loop { ContinueTarget = top, TryDepth = _unit.TryDepth, FinallyLevel = _unit.InFinally };
            _unit.Loops.Add(loop);
            CompileBody(w.Body);
            _unit.Loops.RemoveAt(_unit.Loops.Count - 1);
            Emit(Op.Jump, top);
            Patch(toEnd);
            CompileBody(w.Else);
            foreach (int b in loop.BreakJumps) Patch(b);
        }

        private void CompileFor(ForStmt f)
        {
            CompileExpr(f.Iterable);
            SetLine(f);
            Emit(Op.GetIter);
            int top = Here;
            int forIter = Emit(Op.ForIter);
            CompileStore(f.Target);
            var loop = new Loop { ContinueTarget = top, IsFor = true, TryDepth = _unit.TryDepth, FinallyLevel = _unit.InFinally };
            _unit.Loops.Add(loop);
            CompileBody(f.Body);
            _unit.Loops.RemoveAt(_unit.Loops.Count - 1);
            Emit(Op.Jump, top);
            Patch(forIter);
            CompileBody(f.Else);
            foreach (int b in loop.BreakJumps) Patch(b);
        }

        private void CompileYield(YieldExpr y)
        {
            if (!y.IsFrom)
            {
                if (y.Value != null) CompileExpr(y.Value);
                else Emit(Op.LoadConst, Constant(Value.None));
                SetLine(y);
                Emit(Op.Yield);
                return;
            }
            // yield from: hand on every item of the iterable, then the value it returned is the result.
            CompileExpr(y.Value);
            SetLine(y);
            Emit(Op.GetIter);
            int top = Here;
            int forIter = Emit(Op.ForIter);
            Emit(Op.Yield);
            Emit(Op.Pop); // what send() delivered; not passed on to the inner iterator
            Emit(Op.Jump, top);
            Patch(forIter);
            Emit(Op.GenReturn);
        }

        private void CompileBreak(Stmt at)
        {
            var loop = _unit.Loops[_unit.Loops.Count - 1];
            if (loop.FinallyLevel != _unit.InFinally) throw Error("'break' cannot leave a 'finally' block", at);
            RunGuards(loop.TryDepth, true);
            if (loop.IsFor) Emit(Op.Pop); // discard the iterator
            loop.BreakJumps.Add(Emit(Op.Jump));
        }

        private void CompileContinue(Stmt at)
        {
            var loop = _unit.Loops[_unit.Loops.Count - 1];
            if (loop.FinallyLevel != _unit.InFinally) throw Error("'continue' cannot leave a 'finally' block", at);
            RunGuards(loop.TryDepth, true);
            Emit(Op.Jump, loop.ContinueTarget);
        }

        private void CompileDef(DefStmt d)
        {
            CompileFunction(d);
            StoreName(d.Name);
        }

        // name, [base], then the class body run as a function that returns its namespace, then BuildClass.
        private void CompileClass(ClassStmt c)
        {
            if (c.Bases.Count > 1) throw Error("multiple inheritance is not supported", c.Bases[1]);
            Emit(Op.LoadConst, Constant(Value.FromString(c.Name)));
            foreach (var b in c.Bases) CompileExpr(b);
            CompileFunction(c.Body, true);
            Emit(Op.Call, 0);
            SetLine(c);
            Emit(Op.BuildClass, c.Bases.Count);
            StoreName(c.Name);
        }

        // Compiles a def or lambda and leaves the new function on the stack.
        private void CompileFunction(DefStmt d, bool isClassBody = false)
        {
            var scope = _resolved.Functions[d];
            int numDefaults = 0;
            foreach (var p in d.Params)
            {
                if (p.Default == null) continue;
                CompileExpr(p.Default);
                numDefaults++;
            }
            int numPosDefaults = numDefaults;
            foreach (var p in d.KeywordOnly)
            {
                if (p.Default == null) continue;
                CompileExpr(p.Default);
                numDefaults++;
            }

            // The cells of the enclosing function's variables that this function uses.
            foreach (var name in scope.FreeVars)
            {
                if (!_unit.Scope.IsClass && _unit.Scope.TryGetLocal(name, out int slot)) Emit(Op.LoadCellRef, slot);
                else
                {
                    _unit.Scope.TryGetFree(name, out int free);
                    Emit(Op.LoadFreeRef, free);
                }
            }

            var outer = _unit;
            _unit = new Unit(scope, d.Name) { Line = d.Line };
            foreach (var name in scope.CellVars)
            {
                scope.TryGetLocal(name, out int slot);
                Emit(Op.MakeCell, slot);
            }
            CompileBody(d.Body);
            if (isClassBody) Emit(Op.ClassNamespace);
            else Emit(Op.LoadConst, Constant(Value.None));
            Emit(Op.Return);
            var code = Finish(false, d, numDefaults, numPosDefaults);
            _unit = outer;

            outer.Functions.Add(code);
            SetLine(d);
            Emit(Op.MakeFunction, outer.Functions.Count - 1);
        }

        // Leaves the handlers above depth `target` before a jump or return, running the finally block of every
        // guarded try statement that is being left. A copy of each finally block is compiled right here, outside
        // its own guard, so an exception in it reaches the enclosing handlers.
        private void RunGuards(int target, bool popRest)
        {
            int depth = _unit.TryDepth;
            var guards = new List<Guard>(_unit.Guards);
            for (int k = guards.Count - 1; k >= 0 && guards[k].Depth > target; k--)
            {
                var g = guards[k];
                for (; depth >= g.Depth; depth--) Emit(Op.PopTry);
                int savedDepth = _unit.TryDepth;
                _unit.TryDepth = depth;
                _unit.Guards.RemoveRange(k, _unit.Guards.Count - k);
                _unit.InFinally++;
                CompileBody(g.Body);
                _unit.InFinally--;
                _unit.Guards.AddRange(guards.GetRange(k, guards.Count - k));
                _unit.TryDepth = savedDepth;
            }
            if (popRest)
                for (; depth > target; depth--) Emit(Op.PopTry);
        }

        private void CompileTry(TryStmt t)
        {
            if (t.Finally == null)
            {
                CompileTryExcept(t);
                return;
            }

            int setup = Emit(Op.SetupTry);
            _unit.TryDepth++;
            var guard = new Guard { Body = t.Finally, Depth = _unit.TryDepth };
            _unit.Guards.Add(guard);
            CompileTryExcept(t);
            _unit.Guards.RemoveAt(_unit.Guards.Count - 1);
            _unit.TryDepth--;
            Emit(Op.PopTry);
            _unit.InFinally++;
            CompileBody(t.Finally);
            var toEnd = Emit(Op.Jump);

            // An exception got past the body and handlers: it is on the stack. Run the finally block, then raise it again.
            Patch(setup);
            CompileBody(t.Finally);
            _unit.InFinally--;
            Emit(Op.Reraise);
            Patch(toEnd);
        }

        private void CompileTryExcept(TryStmt t)
        {
            if (t.Handlers.Count == 0)
            {
                CompileBody(t.Body);
                return;
            }
            int setup = Emit(Op.SetupTry);
            _unit.TryDepth++;
            CompileBody(t.Body);
            _unit.TryDepth--;
            Emit(Op.PopTry);
            CompileBody(t.Else);
            var toEnd = new List<int> { Emit(Op.Jump) };

            Patch(setup);
            // The exception is on the stack. Try each handler in turn.
            _unit.HandlerDepth++;
            foreach (var h in t.Handlers)
            {
                int next = -1;
                if (h.Type != null)
                {
                    if (h.Line > 0) _unit.Line = h.Line;
                    CompileExpr(h.Type);
                    Emit(Op.MatchException);
                    next = Emit(Op.JumpIfFalse);
                }
                if (h.Name != null) StoreName(h.Name);
                else Emit(Op.Pop);
                CompileBody(h.Body);
                toEnd.Add(Emit(Op.Jump));
                if (next >= 0) Patch(next);
            }
            _unit.HandlerDepth--;
            if (t.Handlers[t.Handlers.Count - 1].Type != null) Emit(Op.Reraise);
            foreach (int j in toEnd) Patch(j);
        }

        private void CompileRaise(RaiseStmt r)
        {
            if (r.Value == null)
            {
                if (_unit.HandlerDepth == 0)
                    throw Error("a bare 'raise' is only allowed inside an 'except' block", r);
                Emit(Op.RaiseCurrent);
                return;
            }
            CompileExpr(r.Value);
            SetLine(r);
            Emit(Op.Raise);
        }

        // ---- Expressions ----------------------------------------------------------------------------------------

        private static Op BinaryOpCode(BinaryOp op)
        {
            switch (op)
            {
                case BinaryOp.Add: return Op.Add;
                case BinaryOp.Subtract: return Op.Sub;
                case BinaryOp.Multiply: return Op.Mul;
                case BinaryOp.Divide: return Op.Div;
                case BinaryOp.FloorDivide: return Op.FloorDiv;
                case BinaryOp.Modulo: return Op.Mod;
                case BinaryOp.BitOr: return Op.BitOr;
                case BinaryOp.BitXor: return Op.BitXor;
                case BinaryOp.BitAnd: return Op.BitAnd;
                case BinaryOp.LeftShift: return Op.LShift;
                case BinaryOp.RightShift: return Op.RShift;
                default: return Op.Pow;
            }
        }

        private static Op CompareOpCode(CompareOp op)
        {
            switch (op)
            {
                case CompareOp.Eq: return Op.Eq;
                case CompareOp.NotEq: return Op.Ne;
                case CompareOp.Lt: return Op.Lt;
                case CompareOp.Le: return Op.Le;
                case CompareOp.Gt: return Op.Gt;
                case CompareOp.Ge: return Op.Ge;
                case CompareOp.Is: return Op.Is;
                case CompareOp.IsNot: return Op.IsNot;
                case CompareOp.In: return Op.In;
                default: return Op.NotIn;
            }
        }

        private void CompileExpr(Expr expr)
        {
            switch (expr)
            {
                case ConstantExpr c:
                    Emit(Op.LoadConst, Constant(ConstantValue(c.Value)));
                    break;
                case NameExpr n:
                    SetLine(n);
                    LoadName(n.Name);
                    break;
                case LambdaExpr l:
                    CompileFunction(l.Function);
                    break;
                case UnaryExpr u:
                    CompileExpr(u.Operand);
                    SetLine(u);
                    Emit(u.Op == UnaryOp.Negate ? Op.Negate : u.Op == UnaryOp.Plus ? Op.Plus : u.Op == UnaryOp.Invert ? Op.Invert : Op.Not);
                    break;
                case BinaryExpr b:
                    CompileExpr(b.Left);
                    CompileExpr(b.Right);
                    SetLine(b);
                    Emit(BinaryOpCode(b.Op));
                    break;
                case BoolOpExpr b:
                {
                    CompileExpr(b.Left);
                    int jump = Emit(b.IsAnd ? Op.JumpIfFalseOrPop : Op.JumpIfTrueOrPop);
                    CompileExpr(b.Right);
                    Patch(jump);
                    break;
                }
                case CompareExpr c: CompileCompare(c); break;
                case CallExpr c: CompileCall(c); break;
                case AttributeExpr a when IsSuperCall(a.Target):
                    CompileSuperAttr((CallExpr)a.Target, a);
                    break;
                case AttributeExpr a:
                    CompileExpr(a.Target);
                    SetLine(a);
                    Emit(Op.LoadAttr, Name(a.Name));
                    break;
                case SubscriptExpr s:
                    CompileExpr(s.Target);
                    CompileIndex(s.Index);
                    SetLine(s);
                    Emit(Op.LoadIndex);
                    break;
                case ListExpr l:
                    foreach (var item in l.Items) CompileExpr(item);
                    Emit(Op.BuildList, l.Items.Count);
                    break;
                case TupleExpr t:
                    foreach (var item in t.Items) CompileExpr(item);
                    Emit(Op.BuildTuple, t.Items.Count);
                    break;
                case YieldExpr y:
                    CompileYield(y);
                    break;
                case SetExpr se:
                    foreach (var item in se.Items) CompileExpr(item);
                    Emit(Op.BuildSet, se.Items.Count);
                    break;
                case DictExpr d:
                    for (int i = 0; i < d.Keys.Count; i++)
                    {
                        CompileExpr(d.Keys[i]);
                        CompileExpr(d.Values[i]);
                    }
                    Emit(Op.BuildDict, d.Keys.Count);
                    break;
                case IfExpr i:
                {
                    CompileExpr(i.Test);
                    int toElse = Emit(Op.JumpIfFalse);
                    CompileExpr(i.Then);
                    int toEnd = Emit(Op.Jump);
                    Patch(toElse);
                    CompileExpr(i.Else);
                    Patch(toEnd);
                    break;
                }
                case FStringExpr f: CompileFString(f); break;
            }
        }

        private static Value ConstantValue(object o)
        {
            switch (o)
            {
                case null: return Value.None;
                case bool b: return Value.FromBool(b);
                case long l: return Value.FromInt(l);
                case double d: return Value.FromFloat(d);
                default: return Value.FromString((string)o);
            }
        }

        private void CompileIndex(Expr index)
        {
            if (index is SliceExpr s) CompileSliceParts(s);
            else CompileExpr(index);
        }

        private void CompileSliceParts(SliceExpr s)
        {
            foreach (var part in new[] { s.Lower, s.Upper, s.Step })
            {
                if (part == null) Emit(Op.LoadConst, Constant(Value.None));
                else CompileExpr(part);
            }
            SetLine(s);
            Emit(Op.BuildSlice);
        }

        private void CompileCompare(CompareExpr c)
        {
            CompileExpr(c.First);
            if (c.Ops.Count == 1)
            {
                CompileExpr(c.Rest[0]);
                SetLine(c);
                Emit(CompareOpCode(c.Ops[0]));
                return;
            }

            // a < b < c  ->  evaluate each operand once, stop at the first false comparison.
            var exits = new List<int>();
            for (int i = 0; i < c.Ops.Count; i++)
            {
                CompileExpr(c.Rest[i]);
                bool last = i == c.Ops.Count - 1;
                if (!last)
                {
                    Emit(Op.Dup);
                    Emit(Op.Rot3);
                }
                SetLine(c);
                Emit(CompareOpCode(c.Ops[i]));
                if (!last) exits.Add(Emit(Op.JumpIfFalseOrPop));
            }
            int done = Emit(Op.Jump);
            foreach (int e in exits) Patch(e);
            Emit(Op.Rot2);
            Emit(Op.Pop);
            Patch(done);
        }

        private static bool IsSuperCall(Expr e) => e is CallExpr c && c.Func is NameExpr n && n.Name == "super";

        // super().name or super(Class, obj).name: the lookup starts in the parent of the class, so it is one instruction.
        private void CompileSuperAttr(CallExpr call, AttributeExpr attr)
        {
            if (call.KeywordNames.Count > 0 || call.Args.Exists(a => a is StarExpr))
                throw Error("super() takes no keyword or starred arguments", call);
            if (call.Args.Count == 0)
            {
                SetLine(attr);
                Emit(Op.LoadSuper, Name(attr.Name));
            }
            else if (call.Args.Count == 2)
            {
                CompileExpr(call.Args[0]);
                CompileExpr(call.Args[1]);
                SetLine(attr);
                Emit(Op.LoadSuper2, Name(attr.Name));
            }
            else
            {
                throw Error("super() takes either no arguments or two", call);
            }
        }

        private void CompileCall(CallExpr c)
        {
            if (IsSuperCall(c)) throw Error("super() is only supported as super().name", c);
            bool method = c.Func is AttributeExpr && !IsSuperCall(((AttributeExpr)c.Func).Target);
            if (c.Func is AttributeExpr sup && IsSuperCall(sup.Target))
            {
                CompileSuperAttr((CallExpr)sup.Target, sup);
            }
            else if (method)
            {
                var attr = (AttributeExpr)c.Func;
                CompileExpr(attr.Target);
                SetLine(attr);
                Emit(Op.LoadMethod, Name(attr.Name));
            }
            else
            {
                CompileExpr(c.Func);
            }

            if (c.Args.Exists(a => a is StarExpr) || c.KeywordNames.Contains(null))
            {
                CompileSpreadCall(c, method);
                return;
            }

            foreach (var a in c.Args) CompileExpr(a);
            int argc = c.Args.Count + c.KeywordValues.Count;
            if (c.KeywordNames.Count > 0)
            {
                foreach (var v in c.KeywordValues) CompileExpr(v);
                var names = new Value[c.KeywordNames.Count];
                for (int i = 0; i < names.Length; i++) names[i] = Value.FromString(c.KeywordNames[i]);
                Emit(Op.LoadConst, Constant(Value.Tuple(names)));
                SetLine(c);
                Emit(method ? Op.CallMethodKw : Op.CallKw, argc);
            }
            else
            {
                SetLine(c);
                Emit(method ? Op.CallMethod : Op.Call, argc);
            }
        }

        // A call with *iterable or **mapping arguments, with the callee already on the stack.
        private void CompileSpreadCall(CallExpr c, bool method)
        {
            Emit(Op.BuildList, 0);
            foreach (var a in c.Args)
            {
                if (a is StarExpr star)
                {
                    CompileExpr(star.Value);
                    SetLine(star);
                    Emit(Op.ListExtend);
                }
                else
                {
                    CompileExpr(a);
                    Emit(Op.ListAppend);
                }
            }

            bool keywords = c.KeywordNames.Count > 0;
            if (keywords)
            {
                Emit(Op.BuildDict, 0);
                for (int i = 0; i < c.KeywordNames.Count; i++)
                {
                    if (c.KeywordNames[i] != null)
                    {
                        Emit(Op.LoadConst, Constant(Value.FromString(c.KeywordNames[i])));
                        CompileExpr(c.KeywordValues[i]);
                        SetLine(c);
                        Emit(Op.DictSetUnique);
                    }
                    else
                    {
                        CompileExpr(c.KeywordValues[i]);
                        SetLine(c);
                        Emit(Op.DictMerge);
                    }
                }
            }

            SetLine(c);
            Emit(Op.CallEx, (keywords ? 1 : 0) | (method ? 2 : 0));
        }

        private void CompileFString(FStringExpr f)
        {
            int count = 0;
            foreach (var part in f.Parts)
            {
                if (part.Value == null)
                {
                    Emit(Op.LoadConst, Constant(Value.FromString(part.Literal)));
                }
                else
                {
                    CompileExpr(part.Value);
                    int arg = part.Conversion;
                    if (part.FormatSpec != null)
                    {
                        Emit(Op.LoadConst, Constant(Value.FromString(part.FormatSpec)));
                        arg |= 0x100;
                    }
                    SetLine(f);
                    Emit(Op.FormatValue, arg);
                }
                count++;
            }
            if (count == 0) Emit(Op.LoadConst, Constant(Value.FromString("")));
            else Emit(Op.BuildString, count);
        }
    }
}
