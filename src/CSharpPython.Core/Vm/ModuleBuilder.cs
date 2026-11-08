using System;
using System.Linq;

namespace CSharpPython
{
    /// <summary>Builds a module of host functions that scripts can <c>import</c>.</summary>
    public sealed class ModuleBuilder
    {
        public PyModule Module { get; }

        internal ModuleBuilder(string name)
        {
            Module = new PyModule(name);
        }

        public string Name => Module.Name;

        public ModuleBuilder Constant(string name, Value value)
        {
            Module.Members[name] = value;
            return this;
        }

        /// <summary>Adds a function that works directly with the call context, for waits and keyword arguments.</summary>
        public ModuleBuilder Native(string name, NativeFn fn, int minArgs = 0, int maxArgs = -1, bool acceptsKeywords = false)
        {
            Module.Members[name] = Value.FromNative(new NativeFunction(name, fn, minArgs, maxArgs, acceptsKeywords));
            return this;
        }

        /// <summary>
        /// Gives a function already added to this module a parameter list for the editor's signature help, such as
        /// <c>"target, speed=1.0"</c>. Does nothing when there is no such function.
        /// </summary>
        /// <remarks>
        /// Parameters and the result may carry types: <c>"kind: str, count: int = 1 -&gt; Entity"</c>, or several
        /// named results as <c>"-&gt; (hit: bool, point: Vec)"</c>. Signature help still shows the plain list. Set
        /// <paramref name="latent"/> for a function that can pause the script, and <paramref name="pure"/> for one that only
        /// computes a value and changes nothing, so the block editor can use it as a value block. <paramref name="doc"/>
        /// is a sentence or two on what the function does, shown by hover, signature help, completion and the
        /// block palette. Throws
        /// <see cref="ArgumentException"/> when a type is malformed or a function is both latent and pure.
        /// </remarks>
        public ModuleBuilder Describe(string name, string signature, bool latent = false, bool pure = false, string doc = null)
        {
            if (latent && pure) throw new ArgumentException("A function that can pause the script cannot be pure.", nameof(pure));
            if (Module.Members.TryGetValue(name, out var member) && member.Kind == ValueKind.Native)
            {
                var fn = (NativeFunction)member.RawObject;
                var typed = signature == null ? null : HostSignature.Parse(signature);
                fn.Typed = typed;
                fn.Signature = typed?.ParameterText;
                fn.Latent = latent;
                fn.Pure = pure;
                fn.Doc = string.IsNullOrWhiteSpace(doc) ? null : doc;
            }
            return this;
        }

        /// <summary>
        /// Says what one parameter of a function already given a <see cref="Describe"/> is, for signature help and the
        /// block palette's pin tooltips. Does nothing when there is no such function; throws
        /// <see cref="ArgumentException"/> when the function has no parameter of that name.
        /// </summary>
        public ModuleBuilder DescribeParameter(string function, string parameter, string text)
        {
            if (Module.Members.TryGetValue(function, out var member) && member.Kind == ValueKind.Native)
            {
                var fn = (NativeFunction)member.RawObject;
                if (fn.Typed == null || !fn.Typed.Parameters.Any(p => p.Kind != ParameterKind.Marker && p.Name == parameter))
                    throw new ArgumentException("'" + function + "' has no parameter '" + parameter + "' (call Describe first).", nameof(parameter));
                fn.SetParameterDoc(parameter, text);
            }
            return this;
        }

        public ModuleBuilder Function(string name, Func<Value> fn) =>
            Native(name, (ctx, a) => fn(), 0, 0);

        public ModuleBuilder Function(string name, Func<Value, Value> fn) =>
            Native(name, (ctx, a) => fn(a[0]), 1, 1);

        public ModuleBuilder Function(string name, Func<Value, Value, Value> fn) =>
            Native(name, (ctx, a) => fn(a[0], a[1]), 2, 2);

        public ModuleBuilder Function(string name, Func<Value, Value, Value, Value> fn) =>
            Native(name, (ctx, a) => fn(a[0], a[1], a[2]), 3, 3);

        /// <summary>Adds a function that returns nothing (None to the script).</summary>
        public ModuleBuilder Action(string name, Action fn) =>
            Native(name, (ctx, a) => { fn(); return Value.None; }, 0, 0);

        public ModuleBuilder Action(string name, Action<Value> fn) =>
            Native(name, (ctx, a) => { fn(a[0]); return Value.None; }, 1, 1);

        public ModuleBuilder Action(string name, Action<Value, Value> fn) =>
            Native(name, (ctx, a) => { fn(a[0], a[1]); return Value.None; }, 2, 2);

        public ModuleBuilder Action(string name, Action<Value, Value, Value> fn) =>
            Native(name, (ctx, a) => { fn(a[0], a[1], a[2]); return Value.None; }, 3, 3);
    }
}
