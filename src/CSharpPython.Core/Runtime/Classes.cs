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

        public PyClass(string name, PyClass baseClass, PyDict ns)
        {
            Name = name;
            Base = baseClass;
            Namespace = ns;
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

    /// <summary>An object created by calling a script class.</summary>
    internal sealed class PyInstance
    {
        public PyClass Class;

        /// <summary>The instance attributes, assigned with <c>self.name = value</c>.</summary>
        public readonly PyDict Fields = new PyDict();

        /// <summary>The traceback of an exception instance that has been raised, kept so a re-raise continues it.</summary>
        public System.Collections.Generic.List<TraceFrame> Trace;

        public PyInstance(PyClass cls)
        {
            Class = cls;
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
