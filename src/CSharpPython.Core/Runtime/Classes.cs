using System;
using System.Runtime.CompilerServices;

namespace CSharpPython
{
    /// <summary>A class defined by the script with <c>class</c>. Calling it creates a <see cref="PyInstance"/>.</summary>
    internal sealed class PyClass
    {
        public string Name;

        /// <summary>The parent class, or null for a class that derives from <c>object</c>.</summary>
        public PyClass Base;

        /// <summary>The attributes defined in the class body: methods and class variables.</summary>
        public readonly PyDict Namespace;

        /// <summary>The built-in exception type a class derives from directly (<c>class E(ValueError)</c>), else null.</summary>
        public PyExceptionType RootType;

        /// <summary>The built-in exception type at the end of the base chain, or null if this is not an exception class.</summary>
        public PyExceptionType ExceptionRoot
        {
            get
            {
                for (var c = this; c != null; c = c.Base)
                    if (c.RootType != null) return c.RootType;
                return null;
            }
        }

        /// <summary>
        /// Set once any class defines <c>__getattr__</c>, <c>__setattr__</c> or <c>__delattr__</c>, so attribute access
        /// only pays for looking the hooks up when one could exist. It never goes back to false.
        /// </summary>
        public static bool HooksExist;

        public static bool IsHookName(string name) => name == "__getattr__" || name == "__setattr__" || name == "__delattr__";

        /// <summary>Records that this class defines an attribute hook, if its namespace holds one.</summary>
        public void NoteHooks()
        {
            foreach (var kv in Namespace.Pairs())
                if (kv.Key.Kind == ValueKind.Str && IsHookName((string)kv.Key.RawObject)) HooksExist = true;
        }

        /// <summary>The shape of an instance of this class that has no attributes yet (see <see cref="Shape"/>).</summary>
        public Shape RootShape => _rootShape ?? (_rootShape = new Shape());
        private Shape _rootShape;

        /// <summary>The most attributes an instance of this class has had, so a new instance makes room for them all at once.</summary>
        public int SlotHint;

        public PyClass(string name, PyClass baseClass, PyDict ns)
        {
            Name = name;
            Base = baseClass;
            Namespace = ns;
        }

        /// <summary>Raised whenever a class attribute is assigned or deleted, so cached lookups on classes know to look again.</summary>
        public static int Epoch;

        private int _initEpoch = -1;
        private PyFunction _plainInit;

        /// <summary>
        /// The <c>__init__</c> of a class whose construction needs nothing special (not an exception, not a generator),
        /// or null. Cached until any class attribute changes.
        /// </summary>
        public PyFunction PlainInit
        {
            get
            {
                if (_initEpoch != Epoch)
                {
                    _plainInit = null;
                    if (ExceptionRoot == null && TryFind("__init__", out Value init) && init.Kind == ValueKind.Function
                        && !((PyFunction)init.RawObject).Code.IsGenerator)
                        _plainInit = (PyFunction)init.RawObject;
                    _initEpoch = Epoch;
                }
                return _plainInit;
            }
        }

        /// <summary>Looks a name up in this class and then its parents.</summary>
        public bool TryFind(string name, out Value value)
        {
            var key = Value.FromString(name);
            for (var c = this; c != null; c = c.Base)
                if (c.Namespace.TryGetValue(key, out value)) return true;
            value = default;
            return false;
        }

        /// <summary>Marks the functions in the namespace as methods of this class, so that <c>super()</c> knows where to start.</summary>
        public void AdoptMethods()
        {
            NoteHooks();
            foreach (var kv in Namespace.Pairs())
            {
                if (kv.Value.Kind == ValueKind.Function) Adopt(kv.Value);
                else if (kv.Value.Kind == ValueKind.Descriptor)
                {
                    var d = (PyDescriptor)kv.Value.RawObject;
                    Adopt(d.Function);
                    Adopt(d.Getter);
                    Adopt(d.Setter);
                    Adopt(d.Deleter);
                }
            }
        }

        private void Adopt(Value function)
        {
            if (function.Kind != ValueKind.Function) return;
            var f = (PyFunction)function.RawObject;
            if (f.Owner == null) f.Owner = this;
        }

        public bool IsSubclassOf(PyClass other)
        {
            for (var c = this; c != null; c = c.Base)
                if (c == other) return true;
            return false;
        }

        /// <summary>The number of classes from this one up to the root, used to stop a cyclic base chain.</summary>
        public int Depth
        {
            get
            {
                int n = 0;
                for (var c = this; c != null; c = c.Base) n++;
                return n;
            }
        }
    }

    /// <summary>
    /// The attribute names an instance has, in the order they were first assigned. Instances that were given the same
    /// names in the same order share one shape, and each keeps only its values (in an array), where a dictionary of its
    /// own would cost four objects. A shape is never changed: adding a name leads to the next shape.
    /// </summary>
    internal sealed class Shape
    {
        private static readonly string[] NoNames = new string[0];
        private static readonly Shape[] NoShapes = new Shape[0];

        public readonly string[] Names;
        private Shape[] _next = NoShapes;

        public Shape() { Names = NoNames; }

        private Shape(string[] names) { Names = names; }

        public int Count => Names.Length;

        public int IndexOf(string name)
        {
            string[] names = Names;
            for (int i = 0; i < names.Length; i++)
            {
                string s = names[i];
                if ((object)s == name || (s.Length == name.Length && s == name)) return i;
            }
            return -1;
        }

        /// <summary>The shape with <paramref name="name"/> added after these names.</summary>
        public Shape Add(string name)
        {
            foreach (Shape shape in _next)
                if (shape.Names[shape.Names.Length - 1] == name) return shape;
            var names = new string[Names.Length + 1];
            Array.Copy(Names, names, Names.Length);
            names[Names.Length] = name;
            var child = new Shape(names);
            Array.Resize(ref _next, _next.Length + 1);
            _next[_next.Length - 1] = child;
            return child;
        }
    }

    /// <summary>An object created by calling a script class.</summary>
    internal sealed class PyInstance
    {
        // More attributes than this and an instance keeps them in a dictionary, so finding one stays quick.
        private const int MaxSlots = 32;

        public PyClass Class;

        // The attributes assigned with self.name = value live either in _slots, named by _shape, or, in dictionary mode
        // (_shape is null), in _fields. An instance goes to dictionary mode for good when a script deletes an attribute,
        // has too many, or when something asks for the dictionary itself (Fields).
        private PyDict _fields;
        private Shape _shape;
        private Value[] _slots;

        /// <summary>The traceback of an exception instance that has been raised, kept so a re-raise continues it.</summary>
        public System.Collections.Generic.List<TraceFrame> Trace;

        public PyInstance(PyClass cls)
        {
            Class = cls;
            // A class that is not known yet (an instance being restored from a snapshot) starts with a dictionary.
            if (cls == null) _fields = new PyDict();
            else _shape = cls.RootShape;
        }

        /// <summary>The instance attributes as a dictionary. Asking for it moves the instance to dictionary mode for good; use <see cref="FieldsView"/> to read them.</summary>
        public PyDict Fields
        {
            get
            {
                if (_fields == null) ToDictionary();
                return _fields;
            }
        }

        /// <summary>The attributes in a dictionary for reading: the live one in dictionary mode, otherwise a copy that leaves the instance as it is.</summary>
        public PyDict FieldsView()
        {
            if (_fields != null) return _fields;
            var copy = new PyDict();
            for (int i = 0; i < _shape.Count; i++) copy.Set(Value.FromString(_shape.Names[i]), _slots[i]);
            return copy;
        }

        /// <summary>True while the attributes are in slots (not a dictionary).</summary>
        public bool InSlots => _shape != null;

        public int SlotCount => _shape.Count;

        public Value SlotAt(int index) => _slots[index];

        private void ToDictionary()
        {
            _fields = FieldsView();
            _shape = null;
            _slots = null;
        }

        public bool TryGetField(string name, out Value value)
        {
            if (_shape != null)
            {
                int i = _shape.IndexOf(name);
                if (i >= 0)
                {
                    value = _slots[i];
                    return true;
                }
                value = default;
                return false;
            }
            return _fields.TryGetStr(name, out value);
        }

        public bool HasField(string name) =>
            _shape != null ? _shape.IndexOf(name) >= 0 : _fields.ContainsKey(Value.FromString(name));

        public void SetField(string name, Value value)
        {
            if (_shape != null)
            {
                int i = _shape.IndexOf(name);
                if (i >= 0)
                {
                    _slots[i] = value;
                    return;
                }
                if (_shape.Count < MaxSlots)
                {
                    AddSlot(name, value);
                    return;
                }
                ToDictionary();
            }
            _fields.Set(Value.FromString(name), value);
        }

        private void AddSlot(string name, Value value)
        {
            int n = _shape.Count;
            if (_slots == null) _slots = new Value[Math.Max(Class.SlotHint, 1)];
            else if (n == _slots.Length) Array.Resize(ref _slots, n * 2);
            _shape = _shape.Add(name);
            _slots[n] = value;
            if (n + 1 > Class.SlotHint) Class.SlotHint = n + 1;
        }

        public bool RemoveField(string name)
        {
            if (_shape != null)
            {
                if (_shape.IndexOf(name) < 0) return false;
                ToDictionary();
            }
            return _fields.Remove(Value.FromString(name));
        }

        /// <summary>The text <c>repr</c> gives for an object that does not define <c>__repr__</c>.</summary>
        public string DefaultRepr() =>
            "<__main__." + Class.Name + " object at 0x" + RuntimeHelpers.GetHashCode(this).ToString("x8") + ">";
    }

    /// <summary>A script method bound to the instance it was looked up on, e.g. <c>obj.update</c>.</summary>
    internal sealed class ScriptMethod
    {
        public readonly Value Self;
        public readonly PyFunction Function;

        public ScriptMethod(Value self, PyFunction function)
        {
            Self = self;
            Function = function;
        }
    }
}
