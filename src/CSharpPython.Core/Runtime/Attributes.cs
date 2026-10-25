namespace CSharpPython
{
    internal static partial class Ops
    {
        private static PyError NoAttribute(Value obj, string name) =>
            new PyError("AttributeError", "'" + TypeName(obj) + "' object has no attribute '" + name + "'");

        /// <summary>Finds a built-in method (such as <c>list.append</c>) for a value, if its type has one.</summary>
        public static bool TryGetMethod(Value obj, string name, out NativeFunction method)
        {
            method = Methods.Find(obj.Kind, name);
            return method != null;
        }

        /// <summary>The method an instance's class defines under this name, ready to be called with the instance in front.</summary>
        public static bool TryGetScriptMethod(Value obj, string name, out PyFunction function)
        {
            function = null;
            var inst = (PyInstance)obj.RawObject;
            if (inst.Fields.ContainsKey(Value.FromString(name))) return false;
            if (!inst.Class.TryFind(name, out Value member) || member.Kind != ValueKind.Function) return false;
            function = (PyFunction)member.RawObject;
            return true;
        }

        /// <summary>Looks <paramref name="name"/> up on <paramref name="self"/> starting at the parent of <paramref name="owner"/>.</summary>
        public static Value SuperAttr(PyClass owner, Value self, string name)
        {
            if (self.Kind != ValueKind.Instance || !((PyInstance)self.RawObject).Class.IsSubclassOf(owner))
                throw new PyError("TypeError", "super(type, obj): obj must be an instance or subtype of type");
            var key = Value.FromString(name);
            for (var c = owner.Base; c != null; c = c.Base)
                if (c.Namespace.TryGetValue(key, out Value member))
                    return member.Kind == ValueKind.Function
                        ? Value.FromScriptMethod(new ScriptMethod(self, (PyFunction)member.RawObject))
                        : member;

            // Past the last script class is object.
            var instance = (PyInstance)self.RawObject;
            switch (name)
            {
                case "__init__":
                    return Value.FromNative(ObjectInit);
                case "__repr__":
                case "__str__":
                    return Value.FromNative(new NativeFunction("object." + name,
                        (ctx, args) => Value.FromString(instance.DefaultRepr()), 0, 0));
            }
            throw new PyError("AttributeError", "'super' object has no attribute '" + name + "'");
        }

        private static readonly NativeFunction ObjectInit = new NativeFunction("object.__init__", (ctx, args) =>
        {
            if (args.Length > 0)
                throw new PyError("TypeError", "object.__init__() takes exactly one argument (the instance to initialize)");
            return Value.None;
        });

        public static Value GetAttr(Value obj, string name)
        {
            if (name == "__class__") return PyTypes.TypeOf(obj);
            switch (obj.Kind)
            {
                case ValueKind.Instance:
                {
                    var inst = (PyInstance)obj.RawObject;
                    if (inst.Fields.TryGetValue(Value.FromString(name), out Value field)) return field;
                    if (inst.Class.TryFind(name, out Value member))
                        return member.Kind == ValueKind.Function
                            ? Value.FromScriptMethod(new ScriptMethod(obj, (PyFunction)member.RawObject))
                            : member;
                    throw NoAttribute(obj, name);
                }
                case ValueKind.Class:
                {
                    var cls = (PyClass)obj.RawObject;
                    if (name == "__name__") return Value.FromString(cls.Name);
                    if (cls.TryFind(name, out Value member)) return member;
                    throw new PyError("AttributeError", "type object '" + cls.Name + "' has no attribute '" + name + "'");
                }
                case ValueKind.Type:
                    if (name == "__name__") return Value.FromString(((PyType)obj.RawObject).Name);
                    break;
                case ValueKind.ExceptionType:
                    if (name == "__name__") return Value.FromString(((PyExceptionType)obj.RawObject).Name);
                    break;
                case ValueKind.Module:
                {
                    var module = (PyModule)obj.RawObject;
                    if (module.Members.TryGetValue(name, out Value member)) return member;
                    throw new PyError("AttributeError", "module '" + module.Name + "' has no attribute '" + name + "'");
                }
                case ValueKind.Exception:
                    if (name == "args")
                    {
                        string message = ((PyExceptionValue)obj.RawObject).Message;
                        return message.Length == 0 ? Value.Tuple() : Value.Tuple(Value.FromString(message));
                    }
                    break;
            }
            if (Methods.Find(obj.Kind, name) is NativeFunction method)
                return Value.FromBound(new BoundMethod(obj, method));
            throw NoAttribute(obj, name);
        }

        public static void SetAttr(Value obj, string name, Value value)
        {
            if (obj.Kind == ValueKind.Instance)
            {
                ((PyInstance)obj.RawObject).Fields.Set(Value.FromString(name), value);
                return;
            }
            if (obj.Kind == ValueKind.Class)
            {
                ((PyClass)obj.RawObject).Namespace.Set(Value.FromString(name), value);
                return;
            }
            if (obj.Kind == ValueKind.Module)
                throw new PyError("AttributeError", "module attributes are read-only");
            throw new PyError("AttributeError", "'" + TypeName(obj) + "' object has no attribute '" + name + "' and does not support assignment");
        }
    }
}
