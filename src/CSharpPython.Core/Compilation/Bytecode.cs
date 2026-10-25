using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CSharpPython.Compilation
{
    /// <summary>
    /// The VM instruction set. It is a stack machine: operands are popped from and results pushed to the value
    /// stack. 'arg' is an index, a count or a jump target depending on the instruction.
    /// </summary>
    internal enum Op : byte
    {
        Pop,
        Dup,          // a -> a a
        Dup2,         // a b -> a b a b
        Rot2,         // a b -> b a
        Rot3,         // a b c -> c a b

        LoadConst,    // arg = constant index
        LoadLocal,    // arg = slot
        StoreLocal,
        LoadGlobal,   // arg = name index
        StoreGlobal,
        MakeCell,     // arg = slot; wraps the slot's value in a cell shared with inner functions
        LoadCell,     // arg = slot holding a cell
        StoreCell,
        LoadFree,     // arg = index into the function's closure
        StoreFree,
        LoadCellRef,  // -> cell (pushed as a value to build a closure); arg = slot holding a cell
        LoadFreeRef,  // -> cell; arg = index into the function's closure

        LoadAttr,     // obj -> value; arg = name index
        StoreAttr,    // value obj -> ; arg = name index
        LoadMethod,   // obj -> callable self-or-unbound; arg = name index
        LoadIndex,    // obj index -> value
        StoreIndex,   // value obj index ->
        BuildSlice,   // start stop step -> slice

        BuildList,    // arg = count
        BuildTuple,
        BuildDict,    // arg = number of pairs
        BuildSet,     // arg = count
        BuildString,  // arg = count of strings to concatenate
        FormatValue,  // value [spec] -> str; arg = conversion char | 0x100 when a spec is on the stack

        Negate,
        Plus,
        Not,
        Add,
        Sub,
        Mul,
        Div,
        FloorDiv,
        Mod,
        Pow,
        BitOr,
        BitXor,
        BitAnd,
        LShift,
        RShift,
        Invert,

        Eq,
        Ne,
        Lt,
        Le,
        Gt,
        Ge,
        Is,
        IsNot,
        In,
        NotIn,

        Jump,             // arg = target
        JumpIfFalse,      // pops the condition
        JumpIfFalseOrPop, // keeps the condition when jumping, pops it otherwise
        JumpIfTrueOrPop,

        GetIter,
        ForIter,      // iterator -> iterator value, or pops the iterator and jumps to arg when exhausted

        Call,         // callable args... -> result; arg = argument count
        CallKw,       // callable args... kwvalues... names -> result; arg = total argument count
        CallMethod,   // callable self-or-unbound args... -> result; arg = argument count (without self)
        CallMethodKw,
        Return,
        MakeFunction, // defaults... cells... -> function; arg = index into Code.Functions

        UnpackSequence, // seq -> items (first item on top); arg = expected count

        SetupTry,     // arg = handler address
        PopTry,
        Raise,        // value -> (raises)
        Reraise,      // exception -> (raises it unchanged)
        RaiseCurrent, // bare 'raise' inside an except block
        MatchException, // exc type -> exc bool

        Import,       // -> module; arg = name index
        ImportFrom,   // module -> module value; arg = name index

        // Calls with *iterable and **mapping arguments: the positional arguments are collected in a list and the
        // keyword arguments in a dict, then CallEx spreads them out and calls as usual.
        ListAppend,   // list value -> list
        ListExtend,   // list iterable -> list
        DictSetUnique, // dict key value -> dict; refuses a repeated key
        DictMerge,    // dict mapping -> dict; refuses repeated keys
        CallEx,       // callable [self] args-list [kwargs-dict] -> result; arg bit 0 = has kwargs dict, bit 1 = method call

        ClassNamespace, // -> dict of the class body's assigned names; ends a class body
        BuildClass,   // name [base] namespace -> class; arg = number of bases (0 or 1)
        DeleteLocal,  // arg = slot; the variable becomes unbound
        DeleteCell,   // arg = slot holding a cell
        DeleteFree,   // arg = index into the function's closure
        DeleteGlobal, // arg = name index
        DeleteAttr,   // obj -> ; arg = name index
        DeleteIndex,  // obj index ->
        LoadSuper,    // -> super().names[arg], bound to the first argument of the running method
        LoadSuper2,   // class obj -> super(class, obj).names[arg]
        Yield,        // value -> sent; suspends the generator, giving value to whoever resumed it
        GenReturn,    // -> the value the last exhausted generator returned (None for other iterators); ends 'yield from'
    }

    internal readonly struct Instruction
    {
        public readonly Op Op;
        public readonly int Arg;

        public Instruction(Op op, int arg)
        {
            Op = op;
            Arg = arg;
        }

        public override string ToString() => Op + " " + Arg;
    }

    /// <summary>The compiled form of a module or function.</summary>
    internal sealed class Code
    {
        public string Name;
        public string SourceName;
        public Instruction[] Instructions;

        /// <summary>The source line of each instruction.</summary>
        public int[] Lines;
        public Value[] Constants;
        public string[] Names;

        /// <summary>The functions defined directly inside this code, referenced by <see cref="Op.MakeFunction"/>.</summary>
        public Code[] Functions;
        public int NumLocals;
        public int NumParams;

        /// <summary>The number of default values, positional ones first and then those of keyword-only parameters.</summary>
        public int NumDefaults;

        /// <summary>How many of <see cref="NumDefaults"/> belong to positional parameters.</summary>
        public int NumPosDefaults;

        /// <summary>
        /// Keyword-only parameters follow the positional ones in the local slots, then the *args slot, then the
        /// **kwargs slot.
        /// </summary>
        public int NumKwOnly;
        public bool HasVarArgs;
        public bool HasVarKeywords;

        /// <summary>For each keyword-only parameter, the index of its default among the keyword-only defaults, or -1.</summary>
        public int[] KwDefaultIndex;

        /// <summary>Whether calls need the general argument binding rather than the positional fast path.</summary>
        public bool IsComplex => NumKwOnly > 0 || HasVarArgs || HasVarKeywords;

        /// <summary>The names of the positional and keyword-only parameters, in slot order.</summary>
        public string[] ParamNames;

        /// <summary>The name of each local slot, used in error messages.</summary>
        public string[] LocalNames;

        /// <summary>The names of the enclosing functions' variables this function uses, in closure order.</summary>
        public string[] FreeNames;
        public bool IsModule;

        /// <summary>Calling a generator function binds the arguments and returns a generator without running the body.</summary>
        public bool IsGenerator;

        /// <summary>A readable listing, used by tests and for debugging.</summary>
        public string Disassemble()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Instructions.Length; i++)
            {
                var ins = Instructions[i];
                sb.Append(i.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(ins.Op);
                switch (ins.Op)
                {
                    case Op.LoadConst: sb.Append(' ').Append(Constants[ins.Arg].Repr()); break;
                    case Op.LoadGlobal:
                    case Op.StoreGlobal:
                    case Op.LoadAttr:
                    case Op.StoreAttr:
                    case Op.LoadMethod:
                    case Op.DeleteGlobal:
                    case Op.DeleteAttr:
                    case Op.LoadSuper:
                    case Op.LoadSuper2:
                    case Op.Import:
                    case Op.ImportFrom: sb.Append(' ').Append(Names[ins.Arg]); break;
                    case Op.LoadLocal:
                    case Op.StoreLocal: sb.Append(' ').Append(ins.Arg); break;
                    case Op.DeleteLocal:
                    case Op.MakeCell:
                    case Op.DeleteCell:
                    case Op.LoadCell:
                    case Op.StoreCell:
                    case Op.LoadCellRef: sb.Append(' ').Append(LocalNames[ins.Arg]); break;
                    case Op.DeleteFree:
                    case Op.LoadFree:
                    case Op.StoreFree:
                    case Op.LoadFreeRef: sb.Append(' ').Append(FreeNames[ins.Arg]); break;
                    case Op.MakeFunction: sb.Append(' ').Append(Functions[ins.Arg].Name); break;
                    case Op.Pop:
                    case Op.Dup:
                    case Op.Dup2:
                    case Op.Rot2:
                    case Op.Rot3:
                    case Op.Negate:
                    case Op.Plus:
                    case Op.Not:
                    case Op.Add:
                    case Op.Sub:
                    case Op.Mul:
                    case Op.Div:
                    case Op.FloorDiv:
                    case Op.Mod:
                    case Op.Pow:
                    case Op.BitOr:
                    case Op.BitXor:
                    case Op.BitAnd:
                    case Op.LShift:
                    case Op.RShift:
                    case Op.Invert:
                    case Op.Eq:
                    case Op.Ne:
                    case Op.Lt:
                    case Op.Le:
                    case Op.Gt:
                    case Op.Ge:
                    case Op.Is:
                    case Op.IsNot:
                    case Op.In:
                    case Op.NotIn:
                    case Op.LoadIndex:
                    case Op.StoreIndex:
                    case Op.DeleteIndex:
                    case Op.BuildSlice:
                    case Op.GetIter:
                    case Op.ListAppend:
                    case Op.ListExtend:
                    case Op.DictSetUnique:
                    case Op.DictMerge:
                    case Op.Return:
                    case Op.Yield:
                    case Op.GenReturn:
                    case Op.PopTry:
                    case Op.Raise:
                    case Op.Reraise:
                    case Op.RaiseCurrent:
                    case Op.MatchException:
                    case Op.ClassNamespace:
                        break;
                    default: sb.Append(' ').Append(ins.Arg); break;
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
