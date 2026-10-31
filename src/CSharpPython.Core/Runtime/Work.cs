using System;

namespace CSharpPython
{
    /// <summary>
    /// Counts the work a native operation does on a large value as steps, so <c>StepsPerSlice</c> and <c>MaxSteps</c> bound
    /// the time a script can take. Without it, <c>x in big_list</c> or <c>sorted(big_list)</c> is one step however long the list is.
    /// </summary>
    internal static class Work
    {
        /// <summary>How many items a native loop handles in the time of one VM step (measured: about 6 ns an item against about 100 ns a step).</summary>
        public const int ItemsPerStep = 16;

        /// <summary>The size of a built-in container or string, or 0 for anything else (no error, no call into script code).</summary>
        public static long SizeOf(Value v)
        {
            switch (v.Kind)
            {
                case ValueKind.Str: return ((string)v.RawObject).Length;
                case ValueKind.List: return ((PyList)v.RawObject).Count;
                case ValueKind.Tuple: return ((PyTuple)v.RawObject).Count;
                case ValueKind.Deque: return ((PyDeque)v.RawObject).Count;
                case ValueKind.Dict: return ((PyDict)v.RawObject).Count;
                case ValueKind.Set: return ((PySet)v.RawObject).Count;
                case ValueKind.Range: return ((PyRange)v.RawObject).Length;
                default: return 0;
            }
        }

        /// <summary>Charges the running task for walking or copying <paramref name="items"/> items.</summary>
        public static void Items(long items)
        {
            if (items >= ItemsPerStep) PyTask.Current?.Charge(items / ItemsPerStep);
        }

        /// <summary>Charges for sorting <paramref name="items"/> items (n log n comparisons).</summary>
        public static void Sort(long items)
        {
            if (items < 2) return;
            int log = 1;
            for (long n = items; n > 1; n >>= 1) log++;
            Items(items * log);
        }

        /// <summary>
        /// Wraps a native method that grows its receiver or builds a new container so that the receiver and the result
        /// stay within <see cref="VmOptions.MaxSequenceLength"/> (a catchable <c>MemoryError</c> otherwise).
        /// </summary>
        public static NativeFn Bounded(NativeFn fn) => (ctx, args) =>
        {
            var result = fn(ctx, args);
            if (args.Length > 0) Ops.CheckRoom(SizeOf(args[0]), "sequence");
            Ops.CheckRoom(SizeOf(result), "sequence");
            return result;
        };

        /// <summary>
        /// Wraps a native method or function so that each call is charged for the size of its receiver and of its
        /// arguments (a list to extend by, an iterable to join) before the work is done.
        /// </summary>
        public static NativeFn Metered(NativeFn fn, bool sorting = false) => (ctx, args) =>
        {
            long size = 0;
            for (int i = 0; i < args.Length; i++) size += SizeOf(args[i]);
            if (sorting) Sort(size); else Items(size);
            return fn(ctx, args);
        };
    }
}
