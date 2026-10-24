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
            return Finish(true, null, 0);
        }

        private Code Finish(bool isModule, DefStmt def, int numDefaults)
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
                IsModule = isModule,
                NumDefaults = numDefaults,
            };
            if (def != null)
            {
                code.NumParams = def.Params.Count;
                code.ParamNames = new string[def.Params.Count];
                for (int i = 0; i < def.Params.Count; i++) code.ParamNames[i] = def.Params[i].Name;
            }
            else
            {
                code.ParamNames = System.Array.Empty<string>();
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
            if (_unit.Scope.TryGetLocal(name, out int slot)) Emit(Op.LoadLocal, slot);
            else Emit(Op.LoadGlobal, Name(name));
        }

        private void StoreName(string name)
        {
            if (_unit.Scope.TryGetLocal(name, out int slot)) Emit(Op.StoreLocal, slot);
            else Emit(Op.StoreGlobal, Name(name));
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
                case IfStmt i: CompileIf(i); break;
                case WhileStmt w: CompileWhile(w); break;
                case ForStmt f: CompileFor(f); break;
                case DefStmt d: CompileDef(d); break;
                case ReturnStmt r:
                    if (r.Value != null) CompileExpr(r.Value);
                    else Emit(Op.LoadConst, Constant(Value.None));
                    SetLine(stmt);
                    Emit(Op.Return);
                    break;
                case BreakStmt _: CompileBreak(); break;
                case ContinueStmt _: CompileContinue(); break;
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
                    Emit(BinaryOpCode(a.Op));
                    StoreName(n.Name);
                    break;
                case AttributeExpr attr:
                    CompileExpr(attr.Target);
                    Emit(Op.Dup);
                    SetLine(attr);
                    Emit(Op.LoadAttr, Name(attr.Name));
                    CompileExpr(a.Value);
                    SetLine(a);
                    Emit(BinaryOpCode(a.Op));
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
                    Emit(BinaryOpCode(a.Op));
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
            var loop = new Loop { ContinueTarget = top, TryDepth = _unit.TryDepth };
            _unit.Loops.Add(loop);
            CompileBody(w.Body);
            _unit.Loops.RemoveAt(_unit.Loops.Count - 1);
            Emit(Op.Jump, top);
            Patch(toEnd);
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
            var loop = new Loop { ContinueTarget = top, IsFor = true, TryDepth = _unit.TryDepth };
            _unit.Loops.Add(loop);
            CompileBody(f.Body);
            _unit.Loops.RemoveAt(_unit.Loops.Count - 1);
            Emit(Op.Jump, top);
            Patch(forIter);
            foreach (int b in loop.BreakJumps) Patch(b);
        }

        private void CompileBreak()
        {
            var loop = _unit.Loops[_unit.Loops.Count - 1];
            for (int i = _unit.TryDepth; i > loop.TryDepth; i--) Emit(Op.PopTry);
            if (loop.IsFor) Emit(Op.Pop); // discard the iterator
            loop.BreakJumps.Add(Emit(Op.Jump));
        }

        private void CompileContinue()
        {
            var loop = _unit.Loops[_unit.Loops.Count - 1];
            for (int i = _unit.TryDepth; i > loop.TryDepth; i--) Emit(Op.PopTry);
            Emit(Op.Jump, loop.ContinueTarget);
        }

        private void CompileDef(DefStmt d)
        {
            var scope = _resolved.Functions[d];
            int numDefaults = 0;
            foreach (var p in d.Params)
            {
                if (p.Default == null) continue;
                CompileExpr(p.Default);
                numDefaults++;
            }

            var outer = _unit;
            _unit = new Unit(scope, d.Name) { Line = d.Line };
            CompileBody(d.Body);
            Emit(Op.LoadConst, Constant(Value.None));
            Emit(Op.Return);
            var code = Finish(false, d, numDefaults);
            _unit = outer;

            outer.Functions.Add(code);
            SetLine(d);
            Emit(Op.MakeFunction, outer.Functions.Count - 1);
            StoreName(d.Name);
        }

        private void CompileTry(TryStmt t)
        {
            int setup = Emit(Op.SetupTry);
            _unit.TryDepth++;
            CompileBody(t.Body);
            _unit.TryDepth--;
            Emit(Op.PopTry);
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
                case UnaryExpr u:
                    CompileExpr(u.Operand);
                    SetLine(u);
                    Emit(u.Op == UnaryOp.Negate ? Op.Negate : u.Op == UnaryOp.Plus ? Op.Plus : Op.Not);
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

        private void CompileCall(CallExpr c)
        {
            bool method = c.Func is AttributeExpr;
            if (method)
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
