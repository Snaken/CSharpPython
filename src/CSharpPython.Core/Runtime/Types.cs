using System.Collections.Generic;

namespace CSharpPython
{
    /// <summary>
    /// A built-in type such as <c>int</c> or <c>list</c>, the value of those names and of <c>type(x)</c>. Calling it
    /// runs its constructor. (Exception classes are <see cref="PyExceptionType"/>.)
    /// </summary>
    public sealed class PyType
    {
        public readonly string Name;

        /// <summary>The kind of the values of this type; null for <c>type</c> itself.</summary>
        public readonly ValueKind? Kind;

        /// <summary>What calling the type does, or null when scripts cannot create instances.</summary>
        internal NativeFunction Constructor;

        internal PyType(string name, ValueKind? kind)
        {
            Name = name;
            Kind = kind;
        }

        public override string ToString() => "<class '" + Name + "'>";
    }

    /// <summary>The built-in types and the checks behind <c>type</c>, <c>isinstance</c> and <c>issubclass</c>.</summary>
    internal static class PyTypes
    {
        private static readonly Dictionary<string, PyType> ByName = new Dictionary<string, PyType>();

        public static readonly PyType None = Define("NoneType", ValueKind.None);
        public static readonly PyType Bool = Define("bool", ValueKind.Bool);
        public static readonly PyType Int = Define("int", ValueKind.Int);
        public static readonly PyType Float = Define("float", ValueKind.Float);
        public static readonly PyType Str = Define("str", ValueKind.Str);
        public static readonly PyType List = Define("list", ValueKind.List);
        public static readonly PyType Tuple = Define("tuple", ValueKind.Tuple);
        public static readonly PyType Dict = Define("dict", ValueKind.Dict);
        public static readonly PyType Range = Define("range", ValueKind.Range);
        public static readonly PyType Slice = Define("slice", ValueKind.Slice);
        public static readonly PyType Function = Define("function", ValueKind.Function);
        public static readonly PyType Native = Define("builtin_function_or_method", ValueKind.Native);
        public static readonly PyType Method = Define("method", ValueKind.BoundMethod);
        public static readonly PyType Module = Define("module", ValueKind.Module);
        public static readonly PyType Iterator = Define("iterator", ValueKind.Iterator);
        public static readonly PyType Handle = Define("handle", ValueKind.Handle);
        public static readonly PyType Type = Define("type", null);

        private static PyType Define(string name, ValueKind? kind)
        {
            var t = new PyType(name, kind);
            ByName[name] = t;
            return t;
        }

        public static bool TryGet(string name, out PyType type) => ByName.TryGetValue(name, out type);

        /// <summary>The type of a value, as a value (what <c>type(x)</c> returns).</summary>
        public static Value TypeOf(Value v)
        {
            switch (v.Kind)
            {
                case ValueKind.Exception: return Value.FromExceptionType(((PyExceptionValue)v.RawObject).Type);
                case ValueKind.ExceptionType:
                case ValueKind.Type:
                    return Value.FromType(Type);
            }
            foreach (var t in ByName.Values)
                if (t.Kind == v.Kind) return Value.FromType(t);
            throw new PyError("TypeError", "type() cannot describe this value");
        }

        private static bool IsClass(Value v) => v.Kind == ValueKind.Type || v.Kind == ValueKind.ExceptionType;

        /// <summary><c>isinstance(value, cls)</c>, where cls is a type, an exception class or a tuple of those.</summary>
        public static bool IsInstance(Value value, Value cls)
        {
            switch (cls.Kind)
            {
                case ValueKind.Type:
                {
                    var t = (PyType)cls.RawObject;
                    if (t == Type) return IsClass(value);
                    if (t == Int && value.Kind == ValueKind.Bool) return true; // bool is a kind of int
                    return t.Kind == value.Kind;
                }
                case ValueKind.ExceptionType:
                    return value.Kind == ValueKind.Exception &&
                           ((PyExceptionValue)value.RawObject).Type.IsSubclassOf((PyExceptionType)cls.RawObject);
                case ValueKind.Tuple:
                    foreach (var item in ((PyTuple)cls.RawObject).Items)
                        if (IsInstance(value, item)) return true;
                    return false;
                default:
                    throw new PyError("TypeError", "isinstance() arg 2 must be a type or a tuple of types");
            }
        }

        /// <summary><c>issubclass(sub, cls)</c> for built-in types and exception classes.</summary>
        public static bool IsSubclass(Value sub, Value cls)
        {
            if (!IsClass(sub)) throw new PyError("TypeError", "issubclass() arg 1 must be a class");
            switch (cls.Kind)
            {
                case ValueKind.Type:
                {
                    if (sub.Kind != ValueKind.Type) return false;
                    var a = (PyType)sub.RawObject;
                    var b = (PyType)cls.RawObject;
                    return a == b || (a == Bool && b == Int);
                }
                case ValueKind.ExceptionType:
                    return sub.Kind == ValueKind.ExceptionType &&
                           ((PyExceptionType)sub.RawObject).IsSubclassOf((PyExceptionType)cls.RawObject);
                case ValueKind.Tuple:
                    foreach (var item in ((PyTuple)cls.RawObject).Items)
                        if (IsSubclass(sub, item)) return true;
                    return false;
                default:
                    throw new PyError("TypeError", "issubclass() arg 2 must be a class or a tuple of classes");
            }
        }

        /// <summary><c>callable(value)</c>.</summary>
        public static bool IsCallable(Value v)
        {
            switch (v.Kind)
            {
                case ValueKind.Function:
                case ValueKind.Native:
                case ValueKind.BoundMethod:
                case ValueKind.Type:
                case ValueKind.ExceptionType:
                    return true;
                default:
                    return false;
            }
        }
    }
}
