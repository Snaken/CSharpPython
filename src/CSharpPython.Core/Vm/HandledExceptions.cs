using System;

namespace CSharpPython
{
    /// <summary>
    /// The exceptions a frame is handling. Each <c>except</c> block (and the exceptional path of a <c>finally</c>) has a
    /// depth, the number of handler bodies it is inside, and records its exception in the slot of that depth, so that
    /// leaving an inner block brings back the exception of the outer one. The slots are kept as one tuple (or None when
    /// nothing is being handled), which is what snapshots already know how to save. A snapshot from before the slots
    /// holds the exception itself, which counts as the first slot.
    /// </summary>
    internal static class HandledExceptions
    {
        /// <summary>The exception the innermost handler body is handling, or None.</summary>
        public static Value Current(Value info)
        {
            if (info.Kind != ValueKind.Tuple) return info.Kind == ValueKind.Exception || info.Kind == ValueKind.Instance ? info : Value.None;
            var items = ((PyTuple)info.RawObject).Items;
            for (int i = items.Length - 1; i >= 0; i--)
                if (items[i].Kind != ValueKind.None) return items[i];
            return Value.None;
        }

        /// <summary>Records the exception handled at this depth (counting from 1), forgetting anything deeper.</summary>
        public static Value Enter(Value info, int depth, Value exception)
        {
            var slots = new Value[depth];
            for (int i = 0; i < depth; i++) slots[i] = Value.None;
            Copy(info, slots, depth - 1);
            slots[depth - 1] = exception;
            return Value.Tuple(slots);
        }

        /// <summary>Forgets the exceptions handled deeper than <paramref name="depth"/>.</summary>
        public static Value Leave(Value info, int depth)
        {
            if (depth <= 0) return Value.None;
            var slots = new Value[depth];
            for (int i = 0; i < depth; i++) slots[i] = Value.None;
            Copy(info, slots, depth);
            return Current(Value.Tuple(slots)).Kind == ValueKind.None ? Value.None : Value.Tuple(slots);
        }

        // Copies the first `count` slots of info.
        private static void Copy(Value info, Value[] into, int count)
        {
            if (info.Kind == ValueKind.Tuple)
            {
                var items = ((PyTuple)info.RawObject).Items;
                Array.Copy(items, into, Math.Min(Math.Min(count, items.Length), into.Length));
            }
            else if (count > 0 && Current(info).Kind != ValueKind.None) into[0] = info;
        }
    }
}
