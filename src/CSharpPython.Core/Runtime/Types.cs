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
        public static readonly PyType NotImplementedType = Define("NotImplementedType", ValueKind.NotImplemented);
        public static readonly PyType Bool = Define("bool", ValueKind.Bool);
        public static readonly PyType Int = Define("int", ValueKind.Int);
        public static readonly PyType Float = Define("float", ValueKind.Float);
        public static readonly PyType Str = Define("str", ValueKind.Str);
        public static readonly PyType List = Define("list", ValueKind.List);
        public static readonly PyType Tuple = Define("tuple", ValueKind.Tuple);
        public static readonly PyType Dict = Define("dict", ValueKind.Dict);
        public static readonly PyType Set = Define("set", ValueKind.Set);
        public static readonly PyType FrozenSet = Define("frozenset", ValueKind.Set);
        public static readonly PyType Range = Define("range", ValueKind.Range);
        public static readonly PyType Slice = Define("slice", ValueKind.Slice);
        public static readonly PyType Function = Define("function", ValueKind.Function);
        public static readonly PyType Native = Define("builtin_function_or_method", ValueKind.Native);
        public static readonly PyType Method = Define("method", ValueKind.BoundMethod);
        public static readonly PyType Module = Define("module", ValueKind.Module);
        public static readonly PyType Iterator = Define("iterator", ValueKind.Iterator);
        public static readonly PyType Generator = Define("generator", ValueKind.Iterator);
        public static readonly PyType Handle = Define("handle", ValueKind.Handle);
        public static readonly PyType Type = Define("type", null);

        /// <summary>The root of every class; only useful as a base class or in <c>isinstance</c>.</summary>
        public static readonly PyType Object = Define("object", null);

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
                case ValueKind.Instance: return Value.FromClass(((PyInstance)v.RawObject).Class);
                case ValueKind.Iterator when v.RawObject is PyGenerator: return Value.FromType(Generator);
                case ValueKind.Set: return Value.FromType(((PySet)v.RawObject).Frozen ? FrozenSet : Set);
                case ValueKind.ExceptionType:
                case ValueKind.Class:
                case ValueKind.Type:
                    return Value.FromType(Type);
            }
            var kind = KindOf(v);
            foreach (var t in ByName.Values)
                if (t.Kind == kind) return Value.FromType(t);
            throw new PyError("TypeError", "type() cannot describe this value");
        }

        // Bound methods of script classes and of built-in types are both of type "method".
        private static ValueKind KindOf(Value v) => v.Kind == ValueKind.ScriptMethod ? ValueKind.BoundMethod : v.Kind;

        private static bool IsClass(Value v) =>
            v.Kind == ValueKind.Type || v.Kind == ValueKind.ExceptionType || v.Kind == ValueKind.Class;

        /// <summary><c>isinstance(value, cls)</c>, where cls is a type, an exception class or a tuple of those.</summary>
        public static bool IsInstance(Value value, Value cls)
        {
            switch (cls.Kind)
            {
                case ValueKind.Type:
                {
                    var t = (PyType)cls.RawObject;
                    if (t == Object) return true;
                    if (t == Type) return IsClass(value);
                    if (t == Int && value.Kind == ValueKind.Bool) return true; // bool is a kind of int
                    if (t.Kind == ValueKind.Set && value.Kind == ValueKind.Set) return ((PySet)value.RawObject).Frozen == (t == FrozenSet);
                    return t.Kind == KindOf(value);
                }
                case ValueKind.Class:
                    return value.Kind == ValueKind.Instance &&
                           ((PyInstance)value.RawObject).Class.IsSubclassOf((PyClass)cls.RawObject);
                case ValueKind.ExceptionType:
                    if (value.Kind == ValueKind.Instance)
                        return ((PyInstance)value.RawObject).Class.ExceptionRoot?.IsSubclassOf((PyExceptionType)cls.RawObject) == true;
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
                    var b = (PyType)cls.RawObject;
                    if (b == Object) return true;
                    if (sub.Kind != ValueKind.Type) return false;
                    var a = (PyType)sub.RawObject;
                    return a == b || (a == Bool && b == Int);
                }
                case ValueKind.Class:
                    return sub.Kind == ValueKind.Class &&
                           ((PyClass)sub.RawObject).IsSubclassOf((PyClass)cls.RawObject);
                case ValueKind.ExceptionType:
                    if (sub.Kind == ValueKind.Class)
                        return ((PyClass)sub.RawObject).ExceptionRoot?.IsSubclassOf((PyExceptionType)cls.RawObject) == true;
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
                case ValueKind.ScriptMethod:
                case ValueKind.Class:
                case ValueKind.Type:
                case ValueKind.ExceptionType:
                    return true;
                case ValueKind.Instance:
                    return ((PyInstance)v.RawObject).Class.TryFind("__call__", out Value call) && call.Kind == ValueKind.Function;
                default:
                    return false;
            }
        }
    }
}
