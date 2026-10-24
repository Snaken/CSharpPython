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

        public static Value GetAttr(Value obj, string name)
        {
            switch (obj.Kind)
            {
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
            if (obj.Kind == ValueKind.Module)
                throw new PyError("AttributeError", "module attributes are read-only");
            throw new PyError("AttributeError", "'" + TypeName(obj) + "' object has no attribute '" + name + "' and does not support assignment");
        }
    }
}
