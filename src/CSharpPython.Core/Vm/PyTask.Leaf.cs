using System;
using System.Runtime.CompilerServices;
using CSharpPython.Compilation;

namespace CSharpPython
{
    public sealed partial class PyTask
    {
        // Slots of operand stack a leaf function may use.
        private const int LeafStackSize = 16;

        /// <summary>
        /// Runs a small function that only does arithmetic, comparisons and reads (a key function such as
        /// <c>lambda x: -x</c>) without pushing a frame, for a call made by native code. It gives up (returning false)
        /// as soon as anything is not plain and cheap, before any effect, and the caller then makes the ordinary
        /// call, which also reports any error in full.
        /// </summary>
        private bool TryLeaf(PyFunction fn, Value arg0, Value arg1, int argc, out Value result)
        {
            result = default;
            Code c = fn.Code;
            if (!c.IsLeaf || argc != c.NumParams) return false;

            int bas = _sp;
            int numLocals = c.NumLocals;
            if (bas + numLocals + LeafStackSize + 8 > _stack.Length) EnsureStack(bas + numLocals + LeafStackSize + 8);
            Value[] stack = _stack;
            if (argc > 0) stack[bas] = arg0;
            if (argc > 1) stack[bas + 1] = arg1;
            for (int i = argc; i < numLocals; i++) stack[i + bas] = Value.Unbound;
            int sp = bas + numLocals;
            int limit = sp + LeafStackSize;

            // Keep the slots in use, in case a lookup runs script code (a dictionary key's __eq__, say), which pushes at _sp.
            _sp = limit;
            try
            {
                return RunLeaf(fn, c, bas, sp, limit, out result);
            }
            finally
            {
                _sp = bas;
            }
        }

        private bool RunLeaf(PyFunction fn, Code c, int bas, int sp, int limit, out Value result)
        {
            Value[] stack = _stack;
            Instruction[] code = c.Instructions;
            result = default;
            int pc = 0;
            long steps = 0;
            while (true)
            {
                Instruction ins = code[pc++];
                steps++;
                if (sp >= limit) return false;
                switch (ins.Op)
                {
                    case Op.LoadLocal:
                    {
                        Value v = stack[bas + ins.Arg];
                        if (v.Kind == ValueKind.Unbound) return false;
                        stack[sp++] = v;
                        break;
                    }
                    case Op.LoadConst:
                        stack[sp++] = c.Constants[ins.Arg];
                        break;
                    case Op.LoadFree:
                    {
                        Value v = fn.Closure[ins.Arg].Value;
                        if (v.Kind == ValueKind.Unbound) return false;
                        stack[sp++] = v;
                        break;
                    }
                    case Op.LoadGlobal:
                    {
                        string name = c.Names[ins.Arg];
                        if (!Globals.TryGetValue(name, out Value v) && !Vm.Builtins.TryGetValue(name, out v)) return false;
                        stack[sp++] = v;
                        break;
                    }
                    case Op.LoadAttr:
                    {
                        Value obj = stack[sp - 1];
                        string name = c.Names[ins.Arg];
                        if (obj.Kind == ValueKind.Instance && !PyClass.HooksExist && !PyDescriptor.PropertiesExist && !(name.Length == 9 && name == "__class__"))
                        {
                            if (!((PyInstance)obj.RawObject).Fields.TryGetStr(name, out stack[sp - 1])) return false;
                        }
                        else if (obj.Kind == ValueKind.Module)
                        {
                            if (!((PyModule)obj.RawObject).Members.TryGetValue(name, out stack[sp - 1])) return false;
                        }
                        else return false;
                        break;
                    }
                    case Op.LoadIndex:
                    {
                        Value index = stack[--sp];
                        if (!LeafIndex(stack[sp - 1], index, out Value item)) return false;
                        stack = _stack; // a lookup that ran script code may have grown the stack
                        stack[sp - 1] = item;
                        break;
                    }
                    case Op.Add:
                    case Op.Sub:
                    case Op.Mul:
                    case Op.Div:
                    case Op.Mod:
                    case Op.FloorDiv:
                    {
                        Value right = stack[--sp];
                        if (!LeafArithmetic(ins.Op, stack[sp - 1], right, out stack[sp - 1])) return false;
                        break;
                    }
                    case Op.Negate:
                    {
                        Value v = stack[sp - 1];
                        if (v.Kind == ValueKind.Int && v.RawInt != long.MinValue) stack[sp - 1] = Value.FromInt(-v.RawInt);
                        else if (v.Kind == ValueKind.Float) stack[sp - 1] = Value.FromFloat(-v.RawFloat);
                        else return false;
                        break;
                    }
                    case Op.Not:
                    {
                        if (!LeafTruthy(stack[sp - 1], out bool truth)) return false;
                        stack[sp - 1] = Value.FromBool(!truth);
                        break;
                    }
                    case Op.Eq:
                    case Op.Ne:
                    case Op.Lt:
                    case Op.Le:
                    case Op.Gt:
                    case Op.Ge:
                    {
                        Value right = stack[--sp];
                        if (!LeafCompare(ins.Op, stack[sp - 1], right, out bool outcome)) return false;
                        stack[sp - 1] = Value.FromBool(outcome);
                        break;
                    }
                    case Op.Jump:
                        pc = ins.Arg;
                        break;
                    case Op.JumpIfFalse:
                    {
                        if (!LeafTruthy(stack[--sp], out bool truth)) return false;
                        if (!truth) pc = ins.Arg;
                        break;
                    }
                    case Op.JumpIfFalseOrPop:
                    {
                        if (!LeafTruthy(stack[sp - 1], out bool truth)) return false;
                        if (!truth) pc = ins.Arg;
                        else sp--;
                        break;
                    }
                    case Op.JumpIfTrueOrPop:
                    {
                        if (!LeafTruthy(stack[sp - 1], out bool truth)) return false;
                        if (truth) pc = ins.Arg;
                        else sp--;
                        break;
                    }
                    case Op.Return:
                        result = stack[sp - 1];
                        Charge(steps);
                        return true;
                    default:
                        return false;
                }
            }
        }

        private const long ExactDouble = 1L << 53;

        private static bool LeafTruthy(Value v, out bool truth)
        {
            switch (v.Kind)
            {
                case ValueKind.None: truth = false; return true;
                case ValueKind.Bool:
                case ValueKind.Int: truth = v.RawInt != 0; return true;
                case ValueKind.Float: truth = v.RawFloat != 0.0; return true;
                case ValueKind.Str: truth = ((string)v.RawObject).Length != 0; return true;
                default: truth = false; return false;
            }
        }

        private static bool LeafArithmetic(Op op, Value a, Value b, out Value result)
        {
            result = default;
            if (a.Kind == ValueKind.Int && b.Kind == ValueKind.Int)
            {
                long x = a.RawInt, y = b.RawInt;
                switch (op)
                {
                    case Op.Add:
                    {
                        long sum = unchecked(x + y);
                        if (((x ^ sum) & (y ^ sum)) < 0) return false;
                        result = Value.FromInt(sum);
                        return true;
                    }
                    case Op.Sub:
                    {
                        long difference = unchecked(x - y);
                        if (((x ^ y) & (x ^ difference)) < 0) return false;
                        result = Value.FromInt(difference);
                        return true;
                    }
                    case Op.Mul:
                    {
                        if (x > int.MaxValue || x < int.MinValue || y > int.MaxValue || y < int.MinValue) return false; // cannot overflow below
                        result = Value.FromInt(x * y);
                        return true;
                    }
                    case Op.Div:
                        if (y == 0 || x > ExactDouble || x < -ExactDouble || y > ExactDouble || y < -ExactDouble) return false;
                        result = Value.FromFloat((double)x / y);
                        return true;
                    case Op.FloorDiv:
                    {
                        if (y <= 0) return false;
                        long q = x / y;
                        if (x % y != 0 && x < 0) q--;
                        result = Value.FromInt(q);
                        return true;
                    }
                    default: // Mod
                    {
                        if (y <= 0) return false;
                        long r = x % y;
                        if (r < 0) r += y;
                        result = Value.FromInt(r);
                        return true;
                    }
                }
            }
            if (!LeafNumber(a, out double fx, out bool aFloat) || !LeafNumber(b, out double fy, out bool bFloat) || !(aFloat || bFloat)) return false;
            switch (op)
            {
                case Op.Add: result = Value.FromFloat(fx + fy); return true;
                case Op.Sub: result = Value.FromFloat(fx - fy); return true;
                case Op.Mul: result = Value.FromFloat(fx * fy); return true;
                case Op.Div:
                    if (fy == 0.0) return false;
                    result = Value.FromFloat(fx / fy);
                    return true;
                default: return false;
            }
        }

        // The value as a double when it is an int small enough to convert exactly, or a float.
        private static bool LeafNumber(Value v, out double d, out bool isFloat)
        {
            isFloat = v.Kind == ValueKind.Float;
            if (isFloat)
            {
                d = v.RawFloat;
                return true;
            }
            if (v.Kind == ValueKind.Int && v.RawInt <= ExactDouble && v.RawInt >= -ExactDouble)
            {
                d = v.RawInt;
                return true;
            }
            d = 0;
            return false;
        }

        private static bool LeafCompare(Op op, Value a, Value b, out bool result)
        {
            result = false;
            if (a.Kind == ValueKind.Int && b.Kind == ValueKind.Int)
            {
                long x = a.RawInt, y = b.RawInt;
                result = op == Op.Lt ? x < y : op == Op.Le ? x <= y : op == Op.Gt ? x > y : op == Op.Ge ? x >= y : op == Op.Eq ? x == y : x != y;
                return true;
            }
            if (LeafNumber(a, out double fx, out _) && LeafNumber(b, out double fy, out _))
            {
                result = op == Op.Lt ? fx < fy : op == Op.Le ? fx <= fy : op == Op.Gt ? fx > fy : op == Op.Ge ? fx >= fy : op == Op.Eq ? FloatsEqual(fx, fy) : !FloatsEqual(fx, fy);
                return true;
            }
            if (a.Kind == ValueKind.Str && b.Kind == ValueKind.Str && (op == Op.Eq || op == Op.Ne))
            {
                result = ((string)a.RawObject == (string)b.RawObject) == (op == Op.Eq);
                return true;
            }
            return false;
        }

        // Equal as numbers; NaN is equal to nothing. Written without == so that it reads as intended.
        private static bool FloatsEqual(double x, double y) => !double.IsNaN(x) && !double.IsNaN(y) && !(x < y) && !(x > y);

        private static bool LeafIndex(Value container, Value index, out Value result)
        {
            result = default;
            if (container.Kind == ValueKind.List && index.Kind == ValueKind.Int)
            {
                var items = ((PyList)container.RawObject).Items;
                long i = index.RawInt;
                if (i < 0) i += items.Count;
                if (i < 0 || i >= items.Count) return false;
                result = items[(int)i];
                return true;
            }
            if (container.Kind == ValueKind.Tuple && index.Kind == ValueKind.Int)
            {
                var items = ((PyTuple)container.RawObject).Items;
                long i = index.RawInt;
                if (i < 0) i += items.Length;
                if (i < 0 || i >= items.Length) return false;
                result = items[(int)i];
                return true;
            }
            if (container.Kind == ValueKind.Dict && (index.Kind == ValueKind.Int || index.Kind == ValueKind.Str))
                return ((PyDict)container.RawObject).TryGetValue(index, out result);
            return false;
        }
    }
}
