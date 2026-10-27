namespace CSharpPython.Compilation
{
    /// <summary>
    /// Combines common runs of instructions into one, which saves a trip round the interpreter loop for each
    /// instruction dropped. The result is a copy of the code in which the first instruction of a run is replaced
    /// and the others are left where they were, so every address (jumps, handlers, lines, saved positions) still
    /// means what it did and a jump into the middle of a run still runs the original instructions.
    /// </summary>
    internal static class Peephole
    {
        public static Instruction[] Combine(Instruction[] code)
        {
            var fast = (Instruction[])code.Clone();
            int i = 0;
            while (i < code.Length)
            {
                int length = TryCombine(code, fast, i);
                i += length;
            }
            return fast;
        }

        /// <summary>Whether every instruction is one that <c>PyTask.TryLeaf</c> can run.</summary>
        public static bool IsLeaf(Instruction[] code)
        {
            if (code.Length > 64) return false;
            for (int i = 0; i < code.Length; i++)
            {
                switch (code[i].Op)
                {
                    case Op.LoadLocal: case Op.LoadConst: case Op.LoadFree: case Op.LoadGlobal: case Op.LoadAttr: case Op.LoadIndex:
                    case Op.Add: case Op.Sub: case Op.Mul: case Op.Div: case Op.Mod: case Op.FloorDiv: case Op.Negate: case Op.Not:
                    case Op.Eq: case Op.Ne: case Op.Lt: case Op.Le: case Op.Gt: case Op.Ge: case Op.Return:
                        break;
                    case Op.Jump: case Op.JumpIfFalse: case Op.JumpIfFalseOrPop: case Op.JumpIfTrueOrPop:
                        if (code[i].Arg <= i) return false; // only forward, so the function always ends within a few steps
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        // Replaces the run starting at i if there is one; returns how many instructions it covers (1 if none).
        private static int TryCombine(Instruction[] code, Instruction[] fast, int i)
        {
            Instruction a = code[i];
            Instruction b = i + 1 < code.Length ? code[i + 1] : default;
            Instruction c = i + 2 < code.Length ? code[i + 2] : default;
            Instruction d = i + 3 < code.Length ? code[i + 3] : default;
            bool hasB = i + 1 < code.Length, hasC = i + 2 < code.Length, hasD = i + 3 < code.Length;

            if (a.Op == Op.LoadLocal && hasB && b.Op == Op.LoadConst)
            {
                if (hasC && hasD && IsComparison(c.Op) && d.Op == Op.JumpIfFalse)
                {
                    fast[i] = new Instruction(Op.LocalConstCmpJump, a.Arg);
                    return 4;
                }
                if (hasC && IsArithmetic(c.Op))
                {
                    fast[i] = new Instruction(Op.LocalConstArith, a.Arg);
                    return 3;
                }
                fast[i] = new Instruction(Op.LoadLocalConst, a.Arg);
                return 2;
            }
            if (a.Op == Op.LoadLocal && hasB && b.Op == Op.LoadLocal)
            {
                // Better to leave the first one alone when the second starts a longer run.
                if (hasC && c.Op == Op.LoadConst && hasD && (IsArithmetic(d.Op) || (i + 4 < code.Length && IsComparison(d.Op) && code[i + 4].Op == Op.JumpIfFalse))) return 1;
                fast[i] = new Instruction(Op.LoadLocal2, a.Arg);
                return 2;
            }
            if (a.Op == Op.LoadLocal && hasB && b.Op == Op.LoadAttr)
            {
                fast[i] = new Instruction(Op.LoadLocalAttr, a.Arg);
                return 2;
            }
            if (a.Op == Op.LoadConst && hasB && IsArithmetic(b.Op))
            {
                fast[i] = new Instruction(Op.ConstArith, a.Arg);
                return 2;
            }
            if (IsComparison(a.Op) && hasB && b.Op == Op.JumpIfFalse)
            {
                fast[i] = new Instruction(Op.CmpJump, (int)a.Op);
                return 2;
            }
            return 1;
        }

        private static bool IsComparison(Op op) =>
            op == Op.Eq || op == Op.Ne || op == Op.Lt || op == Op.Le || op == Op.Gt || op == Op.Ge;

        private static bool IsArithmetic(Op op) =>
            op == Op.Add || op == Op.Sub || op == Op.Mul || op == Op.Mod || op == Op.FloorDiv;
    }
}
