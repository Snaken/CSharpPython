using System;

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
        /// <paramref name="latent"/> for a function that can pause the script. Throws
        /// <see cref="ArgumentException"/> when a type is malformed.
        /// </remarks>
        public ModuleBuilder Describe(string name, string signature, bool latent = false)
        {
            if (Module.Members.TryGetValue(name, out var member) && member.Kind == ValueKind.Native)
            {
                var fn = (NativeFunction)member.RawObject;
                var typed = signature == null ? null : HostSignature.Parse(signature);
                fn.Typed = typed;
                fn.Signature = typed?.ParameterText;
                fn.Latent = latent;
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
