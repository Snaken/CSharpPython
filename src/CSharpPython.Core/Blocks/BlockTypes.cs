using System.Collections.Generic;
using System.Linq;

namespace CSharpPython.Blocks
{
    /// <summary>
    /// Pin types of the built-in blocks, written like a host function description with the pin names as parameter
    /// names. <c>$T</c> is a wildcard: every pin that mentions it takes its type from the wires of the node
    /// (<see cref="BlockTypeChecker"/>). <c>*item</c> stands for the numbered pins <c>item0</c>, <c>item1</c>, ... (the <c>tuple</c>, <c>set_of</c> and
    /// <c>dict_of</c> blocks have numbered pins too and are typed by <see cref="BlockTypeChecker"/>, like <c>list</c>)
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
            { "contains", "item, collection -> bool" },
            { "is_none", "a -> bool" },
            { "choose", "cond, a: $T, b: $T -> $T" },
            { "index", "list: list[$T], index: int -> $T" },
            { "list", "*item -> list[any]" },
            { "set_index", "list: list[$T], index: int, value: $T" },
            { "append", "list: list[$T], value: $T" },
            { "pop", "list: list[$T], index: int -> $T" },
            { "list_of_size", "value: $T, count: int -> list[$T]" },
            { "remove_at", "list: list[any], index: int" },
            { "tuple_item", "tuple: tuple, index: int -> any" },
            { "set_add", "set: set, value" },
            { "set_discard", "set: set, value" },
            { "dict_get", "dict: dict, key, default -> any" },
            { "dict_set", "dict: dict, key, value" },
            { "dict_remove", "dict: dict, key" },
            { "sorted", "items, reverse: bool -> list[any]" },
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

        // The blocks that grow a numbered pin for each item: item0, item1, ... (a dictionary has key0 with value0, ...).
        public static bool IsVariadic(string block) => block == "list" || block == "tuple" || block == "set_of" || block == "dict_of";

        public static bool IsNumbered(string pin, string prefix) =>
            pin.Length > prefix.Length && pin.StartsWith(prefix, System.StringComparison.Ordinal) && pin.Substring(prefix.Length).All(c => c >= '0' && c <= '9');

        public static bool IsVariadicPin(string block, string pin) =>
            IsVariadic(block) && (block == "dict_of" ? IsNumbered(pin, "key") || IsNumbered(pin, "value") : IsNumbered(pin, "item"));

        /// <summary>The typed description of a built-in block, or null when all its pins are <c>any</c>.</summary>
        public static HostSignature Find(string block) => Parsed.TryGetValue(block, out var s) ? s : null;
    }
}
