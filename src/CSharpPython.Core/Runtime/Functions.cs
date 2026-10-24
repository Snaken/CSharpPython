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

        /// <summary>
        /// The parameter list as an editor should show it, such as <c>"x, y=0, *rest"</c>. When null, the editor
        /// makes one up from the argument counts.
        /// </summary>
        public string Signature { get; set; }

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

    /// <summary>A variable of a function that inner functions also use; both see the same cell.</summary>
    internal sealed class Cell
    {
        public Value Value;

        public Cell(Value value)
        {
            Value = value;
        }
    }

    /// <summary>A function defined by the script with <c>def</c> or <c>lambda</c>.</summary>
    internal sealed class PyFunction
    {
        public readonly Code Code;
        public readonly Value[] Defaults;

        /// <summary>The cells of the enclosing functions' variables this function uses, in <see cref="Compilation.Code.FreeNames"/> order.</summary>
        public readonly Cell[] Closure;

        public PyFunction(Code code, Value[] defaults, Cell[] closure = null)
        {
            Code = code;
            Defaults = defaults;
            Closure = closure ?? NoCells;
        }

        private static readonly Cell[] NoCells = new Cell[0];

        public string Name => Code.Name;
    }
}
