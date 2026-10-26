using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpPython
{
    /// <summary>
    /// <c>collections.namedtuple</c>. A named tuple type is a <see cref="PyType"/> of tuples that has field names; its
    /// instances are ordinary <see cref="PyTuple"/>s that point to the type, so they unpack, index, hash and compare
    /// like tuples.
    /// </summary>
    internal static class NamedTuples
    {
        private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "False", "None", "True", "and", "as", "assert", "async", "await", "break", "class", "continue", "def", "del",
            "elif", "else", "except", "finally", "for", "from", "global", "if", "import", "in", "is", "lambda", "nonlocal",
            "not", "or", "pass", "raise", "return", "try", "while", "with", "yield",
        };

        private static readonly Dictionary<string, NativeFunction> InstanceMethods = new Dictionary<string, NativeFunction>();

        /// <summary>The method that makes an instance from an iterable, reached through the type (<c>Point._make</c>).</summary>
        internal static readonly NativeFunction MakeMethod;

        static NamedTuples()
        {
            void Add(string name, int min, int max, NativeFn fn, bool keywords = false) =>
                InstanceMethods[name] = new NativeFunction("namedtuple." + name, fn, min + 1, max < 0 ? -1 : max + 1, keywords);
            Add("_asdict", 0, 0, (c, a) =>
            {
                var tuple = (PyTuple)a[0].RawObject;
                var result = new PyDict();
                for (int i = 0; i < tuple.Items.Length; i++) result.Set(Value.FromString(tuple.Named.Fields[i]), tuple.Items[i]);
                return Value.FromDict(result);
            });
            Add("_replace", 0, 0, (c, a) =>
            {
                var tuple = (PyTuple)a[0].RawObject;
                var items = (Value[])tuple.Items.Clone();
                var unknown = new List<string>();
                for (int i = 0; i < c.KeywordNames.Length; i++)
                {
                    int at = Array.IndexOf(tuple.Named.Fields, c.KeywordNames[i]);
                    if (at < 0) unknown.Add("'" + c.KeywordNames[i] + "'");
                    else items[at] = a[c.PositionalCount + i];
                }
                 if (unknown.Count > 0) throw Ops.TypeError("Got unexpected field names: [" + string.Join(", ", unknown) + "]");
                return Instance(tuple.Named, items);
            }, true);
            MakeMethod = new NativeFunction("namedtuple._make", (c, a) =>
            {
                var type = (PyType)a[0].RawObject;
                var items = Ops.ToList(a[1]);
                if (items.Count != type.Fields.Length) throw Ops.TypeError("Expected " + type.Fields.Length + " arguments, got " + items.Count);
                return Instance(type, items.ToArray());
            }, 2, 2);
        }

        public static IEnumerable<string> MethodNames => InstanceMethods.Keys;

        public static NativeFunction FindMethod(string name) => InstanceMethods.TryGetValue(name, out var fn) ? fn : null;

        public static Value Instance(PyType type, Value[] items) =>
            Value.FromTuple(new PyTuple(items) { Named = type });

        /// <summary>Makes the type for the given validated fields. The last <c>defaults.Length</c> fields have defaults.</summary>
        public static PyType CreateType(string name, string[] fields, Value[] defaults)
        {
            var type = new PyType(name, ValueKind.Tuple) { Module = "__main__", Fields = fields, FieldDefaults = defaults };
            type.Constructor = MakeConstructor(type);
            return type;
        }

        /// <summary>Attaches the constructor to a type restored from a snapshot.</summary>
        public static void AttachConstructor(PyType type) => type.Constructor = MakeConstructor(type);

        private static NativeFunction MakeConstructor(PyType type) =>
            new NativeFunction(type.Name, (ctx, a) => Construct(type, ctx, a), 0, -1, true);

        private static Value Construct(PyType type, CallContext ctx, ReadOnlySpan<Value> a)
        {
            int n = type.Fields.Length;
            string who = type.Name + ".__new__()";
            if (ctx.PositionalCount > n)
                throw Ops.TypeError(who + " takes " + (n + 1) + " positional argument" + (n + 1 == 1 ? "" : "s") + " but " + (ctx.PositionalCount + 1) + (ctx.PositionalCount + 1 == 1 ? " was" : " were") + " given");
            var items = new Value[n];
            for (int i = 0; i < n; i++) items[i] = Value.Unbound;
            for (int i = 0; i < ctx.PositionalCount; i++) items[i] = a[i];
            for (int k = 0; k < ctx.KeywordNames.Length; k++)
            {
                int at = Array.IndexOf(type.Fields, ctx.KeywordNames[k]);
                if (at < 0) throw Ops.TypeError(who + " got an unexpected keyword argument '" + ctx.KeywordNames[k] + "'");
                if (items[at].Kind != ValueKind.Unbound) throw Ops.TypeError(who + " got multiple values for argument '" + ctx.KeywordNames[k] + "'");
                items[at] = a[ctx.PositionalCount + k];
            }
            var missing = new List<string>();
            int firstDefault = n - type.FieldDefaults.Length;
            for (int i = 0; i < n; i++)
            {
                if (items[i].Kind != ValueKind.Unbound) continue;
                if (i >= firstDefault) items[i] = type.FieldDefaults[i - firstDefault];
                else missing.Add("'" + type.Fields[i] + "'");
            }
            if (missing.Count > 0)
                throw Ops.TypeError(who + " missing " + missing.Count + " required positional argument" + (missing.Count == 1 ? "" : "s") + ": " + JoinNames(missing));
            return Instance(type, items);
        }

        private static string JoinNames(List<string> names)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) sb.Append(names.Count == 2 ? " and " : i == names.Count - 1 ? ", and " : ", ");
                sb.Append(names[i]);
            }
            return sb.ToString();
        }

        private static bool IsIdentifier(string s)
        {
            if (s.Length == 0 || !(char.IsLetter(s[0]) || s[0] == '_')) return false;
            foreach (char c in s) if (!(char.IsLetterOrDigit(c) || c == '_')) return false;
            return true;
        }

        /// <summary><c>namedtuple(typename, field_names, *, rename=False, defaults=None, module=None)</c>.</summary>
        public static Value Make(CallContext ctx, ReadOnlySpan<Value> a)
        {
            Value typenameArg = Value.Unbound, fieldsArg = Value.Unbound, renameArg = Value.Unbound, defaultsArg = Value.Unbound;
            if (ctx.PositionalCount > 2) throw Ops.TypeError("namedtuple() takes 2 positional arguments but " + ctx.PositionalCount + " were given");
            if (ctx.PositionalCount > 0) typenameArg = a[0];
            if (ctx.PositionalCount > 1) fieldsArg = a[1];
            for (int i = 0; i < ctx.KeywordNames.Length; i++)
            {
                Value v = a[ctx.PositionalCount + i];
                switch (ctx.KeywordNames[i])
                {
                    case "typename" when typenameArg.Kind == ValueKind.Unbound: typenameArg = v; break;
                    case "field_names" when fieldsArg.Kind == ValueKind.Unbound: fieldsArg = v; break;
                    case "rename": renameArg = v; break;
                    case "defaults": defaultsArg = v; break;
                    case "module": break;
                    default: throw Ops.TypeError("namedtuple() got an unexpected keyword argument '" + ctx.KeywordNames[i] + "'");
                }
            }
            if (typenameArg.Kind == ValueKind.Unbound || fieldsArg.Kind == ValueKind.Unbound)
                throw Ops.TypeError("namedtuple() missing required arguments 'typename' and 'field_names'");

            var names = new List<string>();
            if (fieldsArg.Kind == ValueKind.Str)
                names.AddRange(fieldsArg.AsString().Replace(',', ' ').Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
            else
                foreach (var item in Ops.ToList(fieldsArg)) names.Add(Ops.Str(item));   // like CPython, other values are converted to text first

            bool rename = renameArg.Kind != ValueKind.Unbound && renameArg.IsTruthy;
            string typename = Ops.Str(typenameArg);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                bool bad = !IsIdentifier(name) || Keywords.Contains(name) || name.StartsWith("_", StringComparison.Ordinal) || seen.Contains(name);
                if (bad && rename) names[i] = name = "_" + i;
                else if (bad)
                {
                    if (!IsIdentifier(name)) throw Ops.ValueError("Type names and field names must be valid identifiers: " + Ops.Repr(Value.FromString(name)));
                    if (Keywords.Contains(name)) throw Ops.ValueError("Type names and field names cannot be a keyword: " + Ops.Repr(Value.FromString(name)));
                    if (name.StartsWith("_", StringComparison.Ordinal)) throw Ops.ValueError("Field names cannot start with an underscore: " + Ops.Repr(Value.FromString(name)));
                    throw Ops.ValueError("Encountered duplicate field name: " + Ops.Repr(Value.FromString(name)));
                }
                seen.Add(name);
            }
            if (!IsIdentifier(typename)) throw Ops.ValueError("Type names and field names must be valid identifiers: " + Ops.Repr(Value.FromString(typename)));
            if (Keywords.Contains(typename)) throw Ops.ValueError("Type names and field names cannot be a keyword: " + Ops.Repr(Value.FromString(typename)));

            var defaults = new Value[0];
            if (defaultsArg.Kind != ValueKind.Unbound && !defaultsArg.IsNone)
            {
                defaults = Ops.ToList(defaultsArg).ToArray();
                if (defaults.Length > names.Count) throw Ops.TypeError("Got more default values than field names");
            }
            return Value.FromType(CreateType(typename, names.ToArray(), defaults));
        }
    }
}
