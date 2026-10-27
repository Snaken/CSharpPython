using System;
using System.Collections.Generic;

namespace CSharpPython
{
    /// <summary>
    /// The built-in functions a <c>match</c> statement is rewritten into calls of (see the comment in
    /// <c>ParserMatch.cs</c>). Their names start with a dot, so no script can write or replace them.
    /// </summary>
    internal static class MatchHelpers
    {
        public static IEnumerable<NativeFunction> Functions()
        {
            yield return new NativeFunction(".matchseq", (c, a) => Value.FromBool(IsSequence(a[0])), 1, 1);
            yield return new NativeFunction(".matchmap", (c, a) => Value.FromBool(a[0].Kind == ValueKind.Dict), 1, 1);
            yield return new NativeFunction(".maprest", MapRest, 1, -1);
            yield return new NativeFunction(".matchclass", MatchClass, 3, -1);
        }

        // A list, tuple, deque or range, but not a string.
        private static bool IsSequence(Value v) =>
            v.Kind == ValueKind.List || v.Kind == ValueKind.Tuple || v.Kind == ValueKind.Deque || v.Kind == ValueKind.Range;

        // The mapping without the keys that were matched by name: {**rest}.
        private static Value MapRest(CallContext ctx, ReadOnlySpan<Value> a)
        {
            var rest = new PyDict();
            foreach (var pair in ((PyDict)a[0].RawObject).Pairs())
            {
                bool matched = false;
                for (int i = 1; i < a.Length && !matched; i++) matched = Ops.Equal(pair.Key, a[i]);
                if (!matched) rest.Set(pair.Key, pair.Value);
            }
            return Value.FromDict(rest);
        }

        // .matchclass(subject, cls, positional count, keyword names...): the attributes the sub-patterns look at, positional
        // ones first, or None if the subject is not an instance of cls or lacks one of them.
        private static Value MatchClass(CallContext ctx, ReadOnlySpan<Value> a)
        {
            Value subject = a[0], cls = a[1];
            if (cls.Kind != ValueKind.Type && cls.Kind != ValueKind.Class && cls.Kind != ValueKind.ExceptionType)
                throw Ops.TypeError("called match pattern must be a class");
            if (!PyTypes.IsInstance(subject, cls)) return Value.None;

            int positional = (int)a[2].AsInt();
            var values = new Value[positional + a.Length - 3];
            if (positional > 0)
            {
                string[] names = PositionalNames(cls, positional, out bool selfMatching);
                if (selfMatching) values[0] = subject;
                else
                {
                    for (int i = 0; i < positional; i++)
                    {
                        if (!TryAttribute(subject, names[i], out values[i])) return Value.None;
                    }
                }
            }
            for (int i = 3; i < a.Length; i++)
            {
                if (!TryAttribute(subject, (string)a[i].RawObject, out values[positional + i - 3])) return Value.None;
            }
            return Value.Tuple(values);
        }

        // The attribute names that positional sub-patterns stand for: __match_args__ of a class, the fields of a named tuple.
        // The built-in types int, float, str, bool, list, dict, tuple, set, frozenset and bytes take one that matches the subject itself.
        private static string[] PositionalNames(Value cls, int positional, out bool selfMatching)
        {
            selfMatching = false;
            string name = cls.Kind == ValueKind.Class ? ((PyClass)cls.RawObject).Name : cls.Kind == ValueKind.Type ? ((PyType)cls.RawObject).Name : ((PyExceptionType)cls.RawObject).Name;
            string[] names = null;
            if (cls.Kind == ValueKind.Class)
            {
                if (((PyClass)cls.RawObject).TryFind("__match_args__", out Value declared) && declared.Kind == ValueKind.Tuple)
                {
                    var items = ((PyTuple)declared.RawObject).Items;
                    names = new string[items.Length];
                    for (int i = 0; i < items.Length; i++)
                    {
                        if (items[i].Kind != ValueKind.Str) throw Ops.TypeError("__match_args__ elements must be strings");
                        names[i] = (string)items[i].RawObject;
                    }
                }
            }
            else if (cls.Kind == ValueKind.Type)
            {
                var type = (PyType)cls.RawObject;
                if (type.IsNamedTuple) names = type.Fields;
                else if (IsSelfMatching(type))
                {
                    selfMatching = true;
                    if (positional > 1) throw Ops.TypeError(name + "() accepts 1 positional sub-pattern (" + positional + " given)");
                    return null;
                }
            }
            int accepted = names?.Length ?? 0;
            if (positional > accepted)
                throw Ops.TypeError(name + "() accepts " + accepted + " positional sub-pattern" + (accepted == 1 ? "" : "s") + " (" + positional + " given)");
            return names;
        }

        private static bool IsSelfMatching(PyType type)
        {
            switch (type.Name)
            {
                case "int": case "float": case "str": case "bool": case "list": case "dict": case "tuple": case "set": case "frozenset": case "bytes":
                case "bytearray": case "defaultdict": case "Counter":
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryAttribute(Value subject, string name, out Value value)
        {
            try
            {
                value = Ops.GetAttr(subject, name);
                return true;
            }
            catch (Exception e) when (PyTask.IsAttributeError(e))
            {
                value = default;
                return false;
            }
        }
    }
}
