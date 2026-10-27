using System;

namespace CSharpPython
{
    internal static partial class Ops
    {
        private static PyError NoAttribute(Value obj, string name) =>
            new PyError("AttributeError", "'" + TypeName(obj) + "' object has no attribute '" + name + "'");

        /// <summary>Finds a built-in method (such as <c>list.append</c>) for a value, if its type has one.</summary>
        public static bool TryGetMethod(Value obj, string name, out NativeFunction method)
        {
            method = Methods.FindFor(obj, name);
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

        /// <summary>
        /// Looks <paramref name="name"/> up on <paramref name="self"/> (an instance, or the class itself inside a
        /// classmethod) starting at the parent of <paramref name="owner"/>.
        /// </summary>
        public static Value SuperAttr(PyClass owner, Value self, string name)
        {
            PyClass selfClass = self.Kind == ValueKind.Instance ? ((PyInstance)self.RawObject).Class
                : self.Kind == ValueKind.Class ? (PyClass)self.RawObject : null;
            if (selfClass == null || !selfClass.IsSubclassOf(owner))
                throw new PyError("TypeError", "super(type, obj): obj must be an instance or subtype of type");
            var key = Value.FromString(name);
            for (var c = owner.Base; c != null; c = c.Base)
                if (c.Namespace.TryGetValue(key, out Value member))
                    return BindMember(member, self, selfClass, name);

            // Past the last script class is object.
            var builtin = self.Kind == ValueKind.Instance ? Methods.Find(ValueKind.Instance, name) : null;
            if (builtin != null) return Value.FromBound(new BoundMethod(self, builtin));
            throw new PyError("AttributeError", "'super' object has no attribute '" + name + "'");
        }

        private static bool IsProperty(Value member) =>
            member.Kind == ValueKind.Descriptor && ((PyDescriptor)member.RawObject).Kind == DescriptorKind.Property;

        // What a class member becomes when it is looked up on an instance (or on the class itself, when self is the
        // class): a function is bound to its instance, a classmethod to the class, a staticmethod is unwrapped, and a
        // property runs its getter.
        private static Value BindMember(Value member, Value self, PyClass cls, string name)
        {
            bool onInstance = self.Kind == ValueKind.Instance;
            switch (member.Kind)
            {
                case ValueKind.Function:
                    return onInstance ? Value.FromScriptMethod(new ScriptMethod(self, (PyFunction)member.RawObject)) : member;
                case ValueKind.Descriptor:
                {
                    var d = (PyDescriptor)member.RawObject;
                    switch (d.Kind)
                    {
                        case DescriptorKind.StaticMethod: return d.Function;
                        case DescriptorKind.ClassMethod:
                            return Value.FromScriptMethod(new ScriptMethod(Value.FromClass(cls), (PyFunction)d.Function.RawObject));
                        default:
                            if (!onInstance) return member;
                            if (d.Getter.Kind == ValueKind.None) throw NoAccessor(cls, name, "getter");
                            return CallAccessor(d.Getter, self, null);
                    }
                }
                default: return member;
            }
        }

        private static PyError NoAccessor(PyClass cls, string name, string which) =>
            new PyError("AttributeError", "property '" + name + "' of '" + cls.Name + "' object has no " + which);

        private static Value CallAccessor(Value function, Value self, Value? argument)
        {
            var task = PyTask.Current;
            if (task == null) throw new PyError("RuntimeError", "a property can only run inside a script");
            return task.CallSync(function, argument.HasValue ? new[] { self, argument.Value } : new[] { self });
        }

        public static string AttrName(Value v, string function)
        {
            if (v.Kind != ValueKind.Str) throw new PyError("TypeError", function + "(): attribute name must be string");
            return (string)v.RawObject;
        }

        // The __getattr__, __setattr__ or __delattr__ function of an instance's class, when a script is running to call it.
        private static bool TryHook(Value obj, string hook, out Value method)
        {
            method = default;
            if (!PyClass.HooksExist || PyTask.Current == null) return false;
            var cls = ((PyInstance)obj.RawObject).Class;
            if (!cls.TryFind(hook, out Value member) || member.Kind != ValueKind.Function) return false;
            method = Value.FromScriptMethod(new ScriptMethod(obj, (PyFunction)member.RawObject));
            return true;
        }

        public static Value GetAttr(Value obj, string name)
        {
            if (obj.Kind != ValueKind.Instance || !PyClass.HooksExist || name == "__class__") return GetAttrCore(obj, name);
            try
            {
                return GetAttrCore(obj, name);
            }
            catch (Exception e) when (PyTask.IsAttributeError(e) && TryHook(obj, "__getattr__", out _))
            {
                TryHook(obj, "__getattr__", out Value hook);
                return PyTask.Current.CallSync(hook, new[] { Value.FromString(name) });
            }
        }

        private static Value GetAttrCore(Value obj, string name)
        {
            if (name == "__class__") return PyTypes.TypeOf(obj);
            switch (obj.Kind)
            {
                case ValueKind.Instance:
                {
                    var inst = (PyInstance)obj.RawObject;
                    if (PyDescriptor.PropertiesExist && inst.Class.TryFind(name, out Value prop) && IsProperty(prop))
                        return BindMember(prop, obj, inst.Class, name);
                    if (inst.Fields.TryGetValue(Value.FromString(name), out Value field)) return field;
                    if (inst.Class.TryFind(name, out Value member)) return BindMember(member, obj, inst.Class, name);
                    throw NoAttribute(obj, name);
                }
                case ValueKind.Class:
                {
                    var cls = (PyClass)obj.RawObject;
                    if (name == "__name__") return Value.FromString(cls.Name);
                    if (cls.TryFind(name, out Value member)) return BindMember(member, obj, cls, name);
                    throw new PyError("AttributeError", "type object '" + cls.Name + "' has no attribute '" + name + "'");
                }
                case ValueKind.Type:
                    if (name == "__name__") return Value.FromString(((PyType)obj.RawObject).Name);
                    if (((PyType)obj.RawObject).IsNamedTuple)
                    {
                        var named = (PyType)obj.RawObject;
                        if (name == "_fields") return Value.FromTuple(new PyTuple(Array.ConvertAll(named.Fields, f => Value.FromString(f))));
                        if (name == "_make") return Value.FromBound(new BoundMethod(obj, NamedTuples.MakeMethod));
                        if (name == "_field_defaults")
                        {
                            var defaults = new PyDict();
                            int first = named.Fields.Length - named.FieldDefaults.Length;
                            for (int i = 0; i < named.FieldDefaults.Length; i++) defaults.Set(Value.FromString(named.Fields[first + i]), named.FieldDefaults[i]);
                            return Value.FromDict(defaults);
                        }
                    }
                    // object.__setattr__(self, name, value) and the like, for a class's own hooks to call.
                    if ((PyType)obj.RawObject == PyTypes.Object && Methods.Find(ValueKind.Instance, name) is NativeFunction objectMethod)
                        return Value.FromNative(objectMethod);
                    // str.lower, dict.fromkeys and the like: the method of the type, with the instance as its first argument.
                    if (Methods.FindOnType((PyType)obj.RawObject, name) is NativeFunction typeMethod)
                        return Value.FromNative(typeMethod);
                    break;
                case ValueKind.Function:
                    if (name == "__name__") return Value.FromString(((PyFunction)obj.RawObject).Name);
                    break;
                case ValueKind.ScriptMethod:
                    if (name == "__name__") return Value.FromString(((ScriptMethod)obj.RawObject).Function.Name);
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
                case ValueKind.Slice:
                {
                    var slice = (PySlice)obj.RawObject;
                    if (name == "start") return slice.Start;
                    if (name == "stop") return slice.Stop;
                    if (name == "step") return slice.Step;
                    break;
                }
                case ValueKind.Exception:
                {
                    var exc = (PyExceptionValue)obj.RawObject;
                    if (name == "args")
                    {
                        if (exc.Payload.Kind != ValueKind.None) return Value.Tuple(exc.Payload);
                        return exc.Message.Length == 0 ? Value.Tuple() : Value.Tuple(Value.FromString(exc.Message));
                    }
                    if (name == "value" && exc.Type.IsSubclassOf(ExceptionTypes.StopIteration))
                        return exc.Payload.Kind != ValueKind.None ? exc.Payload : exc.Message.Length == 0 ? Value.None : Value.FromString(exc.Message);
                    break;
                }
            }
            if (obj.Kind == ValueKind.Dict && name == "default_factory" && ((PyDict)obj.RawObject).Flavor == DictFlavor.Default)
                return ((PyDict)obj.RawObject).Factory;
            if (obj.Kind == ValueKind.Tuple && ((PyTuple)obj.RawObject).Named != null)
            {
                var tuple = (PyTuple)obj.RawObject;
                int field = Array.IndexOf(tuple.Named.Fields, name);
                if (field >= 0) return tuple.Items[field];
                if (name == "_fields") return GetAttrCore(Value.FromType(tuple.Named), name);
                if (name == "_field_defaults") return GetAttrCore(Value.FromType(tuple.Named), name);
            }
            if (obj.Kind == ValueKind.Deque && name == "maxlen")
                return ((PyDeque)obj.RawObject).MaxLen < 0 ? Value.None : Value.FromInt(((PyDeque)obj.RawObject).MaxLen);
            if (obj.Kind == ValueKind.Native && name == "from_iterable" && obj.RawObject == ItertoolsModule.Chain)
                return Value.FromNative(ItertoolsModule.ChainFromIterable);
            if (Methods.FindFor(obj, name) is NativeFunction method)
                return Value.FromBound(new BoundMethod(obj, method));
            throw NoAttribute(obj, name);
        }

        public static void DelAttr(Value obj, string name)
        {
            if (obj.Kind == ValueKind.Instance && TryHook(obj, "__delattr__", out Value hook))
            {
                PyTask.Current.CallSync(hook, new[] { Value.FromString(name) });
                return;
            }
            DelAttrCore(obj, name);
        }

        internal static void DelAttrCore(Value obj, string name)
        {
            var key = Value.FromString(name);
            if (obj.Kind == ValueKind.Instance)
            {
                var inst = (PyInstance)obj.RawObject;
                if (PyDescriptor.PropertiesExist && inst.Class.TryFind(name, out Value prop) && IsProperty(prop))
                {
                    var deleter = ((PyDescriptor)prop.RawObject).Deleter;
                    if (deleter.Kind == ValueKind.None) throw NoAccessor(inst.Class, name, "deleter");
                    CallAccessor(deleter, obj, null);
                    return;
                }
                if (!inst.Fields.Remove(key)) throw NoAttribute(obj, name);
                return;
            }
            if (obj.Kind == ValueKind.Class)
            {
                var cls = (PyClass)obj.RawObject;
                if (!cls.Namespace.Remove(key)) throw new PyError("AttributeError", "type object '" + cls.Name + "' has no attribute '" + name + "'");
                return;
            }
            if (obj.Kind == ValueKind.Module)
                throw new PyError("AttributeError", "module attributes are read-only");
            throw new PyError("AttributeError", "'" + TypeName(obj) + "' object has no attribute '" + name + "' and no __dict__ for setting new attributes");
        }

        public static void SetAttr(Value obj, string name, Value value)
        {
            if (obj.Kind == ValueKind.Instance && TryHook(obj, "__setattr__", out Value hook))
            {
                PyTask.Current.CallSync(hook, new[] { Value.FromString(name), value });
                return;
            }
            SetAttrCore(obj, name, value);
        }

        internal static void SetAttrCore(Value obj, string name, Value value)
        {
            if (obj.Kind == ValueKind.Instance)
            {
                var inst = (PyInstance)obj.RawObject;
                if (PyDescriptor.PropertiesExist && inst.Class.TryFind(name, out Value prop) && IsProperty(prop))
                {
                    var setter = ((PyDescriptor)prop.RawObject).Setter;
                    if (setter.Kind == ValueKind.None) throw NoAccessor(inst.Class, name, "setter");
                    CallAccessor(setter, obj, value);
                    return;
                }
                inst.Fields.Set(Value.FromString(name), value);
                return;
            }
            if (obj.Kind == ValueKind.Class)
            {
                ((PyClass)obj.RawObject).Namespace.Set(Value.FromString(name), value);
                if (PyClass.IsHookName(name)) PyClass.HooksExist = true;
                return;
            }
            if (obj.Kind == ValueKind.Module)
                throw new PyError("AttributeError", "module attributes are read-only");
            if (obj.Kind == ValueKind.Dict && name == "default_factory" && ((PyDict)obj.RawObject).Flavor == DictFlavor.Default)
            {
                ((PyDict)obj.RawObject).Factory = value;
                return;
            }
            throw new PyError("AttributeError", "'" + TypeName(obj) + "' object has no attribute '" + name + "' and no __dict__ for setting new attributes");
        }
    }
}
