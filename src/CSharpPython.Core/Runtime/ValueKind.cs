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
    }
}
