using System;
using CSharpPython.Compilation;

namespace CSharpPython
{
    /// <summary>
    /// The signature of a function implemented in C#. <paramref name="args"/> holds the positional arguments
    /// followed by any keyword argument values (see <see cref="CallContext"/>).
    /// </summary>
    public delegate Value NativeFn(CallContext ctx, ReadOnlySpan<Value> args);

    /// <summary>A function implemented in C#, either a built-in or one registered by the host game.</summary>
    public sealed class NativeFunction
    {
        public readonly string Name;
        public readonly NativeFn Fn;
        public readonly int MinArgs;

        /// <summary>The maximum number of positional arguments, or -1 for any number.</summary>
        public readonly int MaxArgs;

        public readonly bool AcceptsKeywords;

        public NativeFunction(string name, NativeFn fn, int minArgs = 0, int maxArgs = -1, bool acceptsKeywords = false)
        {
            Name = name;
            Fn = fn;
            MinArgs = minArgs;
            MaxArgs = maxArgs;
            AcceptsKeywords = acceptsKeywords;
        }

        public override string ToString() => "<built-in function " + Name + ">";
    }

    /// <summary>A native method bound to the value it was looked up on, e.g. <c>xs.append</c>.</summary>
    internal sealed class BoundMethod
    {
        public readonly Value Self;
        public readonly NativeFunction Function;

        public BoundMethod(Value self, NativeFunction function)
        {
            Self = self;
            Function = function;
        }
    }

    /// <summary>A function defined by the script with <c>def</c>.</summary>
    internal sealed class PyFunction
    {
        public readonly Code Code;
        public readonly Value[] Defaults;

        public PyFunction(Code code, Value[] defaults)
        {
            Code = code;
            Defaults = defaults;
        }

        public string Name => Code.Name;
    }
}
