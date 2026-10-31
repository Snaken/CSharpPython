using System.Collections.Generic;

namespace CSharpPython.Blocks
{
    /// <summary>
    /// Pin types of the built-in blocks, written like a host function description with the pin names as parameter
    /// names. <c>$T</c> is a wildcard: every pin that mentions it takes its type from the wires of the node
    /// (<see cref="BlockTypeChecker"/>). <c>*item</c> stands for the numbered pins <c>item0</c>, <c>item1</c>, ...
    /// A block not listed has <c>any</c> inputs and an <c>any</c> output. The operators (<c>add</c>, <c>sub</c>, ...,
    /// <c>neg</c>) and the <c>list</c> block take <c>any</c> and the checker works out their result, and refuses impossible pairs, from the wires.
    /// </summary>
    internal static class BlockTypes
    {
        private static readonly Dictionary<string, string> Source = new Dictionary<string, string>
        {
            { "eq", "a, b -> bool" }, { "ne", "a, b -> bool" }, { "lt", "a, b -> bool" },
            { "le", "a, b -> bool" }, { "gt", "a, b -> bool" }, { "ge", "a, b -> bool" },
            { "not", "a -> bool" },
            { "contains", "a, b -> bool" },
            { "is_none", "a -> bool" },
            { "choose", "cond, a: $T, b: $T -> $T" },
            { "index", "list: list[$T], index: int -> $T" },
            { "list", "*item -> list[any]" },
            { "set_index", "list: list[$T], index: int, value: $T" },
            { "append", "list: list[$T], value: $T" },
            { "remove_at", "list: list[any], index: int" },
            { "repeat", "count: int" },
            { "for_range", "start: int, stop: int, step: int" },
        };

        private static readonly Dictionary<string, HostSignature> Parsed = Build();

        private static Dictionary<string, HostSignature> Build()
        {
            var parsed = new Dictionary<string, HostSignature>();
            foreach (var kv in Source) parsed[kv.Key] = HostSignature.Parse(kv.Value);
            return parsed;
        }

        /// <summary>The typed description of a built-in block, or null when all its pins are <c>any</c>.</summary>
        public static HostSignature Find(string block) => Parsed.TryGetValue(block, out var s) ? s : null;
    }
}
