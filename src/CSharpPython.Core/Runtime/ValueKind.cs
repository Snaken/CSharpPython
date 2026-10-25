namespace CSharpPython
{
    /// <summary>The runtime type tag of a <see cref="Value"/>.</summary>
    public enum ValueKind : byte
    {
        None = 0,
        Bool,
        Int,
        Float,
        Str,
        List,
        Tuple,
        Dict,
        Range,
        Slice,
        Function,
        Native,
        BoundMethod,
        Module,
        Iterator,
        Exception,
        ExceptionType,
        /// <summary>A reference to a host (game) object: a host-defined type id plus an object id.</summary>
        Handle,
        /// <summary>Marks a local variable that has not been assigned yet. Never visible to scripts.</summary>
        Unbound,
        /// <summary>A variable shared between a function and the functions defined inside it. Never visible to scripts.</summary>
        Cell,
        /// <summary>A built-in type such as <c>int</c>, see <see cref="PyType"/>.</summary>
        Type,
        /// <summary>A class defined by the script, see <see cref="PyClass"/>.</summary>
        Class,
        /// <summary>An object created from a script class.</summary>
        Instance,
        /// <summary>A script method bound to its instance.</summary>
        ScriptMethod,
        /// <summary>The <c>NotImplemented</c> value a special method returns to decline an operation.</summary>
        NotImplemented,
        /// <summary>A <c>set</c>, see <see cref="PySet"/>.</summary>
        Set,
        /// <summary>A <c>staticmethod</c>, <c>classmethod</c> or <c>property</c>, see <see cref="PyDescriptor"/>.</summary>
        Descriptor,
        /// <summary>The <c>Ellipsis</c> value, written <c>...</c>.</summary>
        Ellipsis,
    }
}
