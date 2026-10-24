using System;

namespace CSharpPython
{
    /// <summary>
    /// Thrown by the VM and by host functions to raise a Python exception, such as
    /// <c>throw new PyError("ValueError", "bad direction")</c>. Scripts can catch it with <c>try/except</c>.
    /// </summary>
    public sealed class PyError : Exception
    {
        /// <summary>The Python exception type name, for example <c>"ValueError"</c>.</summary>
        public string TypeName { get; }

        /// <summary>The exception message without the type name.</summary>
        public string Problem { get; }

        public PyError(string typeName, string message) : base(typeName + ": " + message)
        {
            TypeName = typeName;
            Problem = message;
        }
    }
}
