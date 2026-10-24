using System;
using System.Collections.Generic;
using CSharpPython.Compilation;
using CSharpPython.Syntax;

namespace CSharpPython
{
    /// <summary>
    /// Compiles scripts and starts tasks. A VM holds the host API (modules and built-ins) and the limits; the
    /// running state lives in each <see cref="PyTask"/>, so one VM can serve many game objects.
    /// </summary>
    public sealed class PyVm
    {
        internal readonly Dictionary<string, Value> Builtins = new Dictionary<string, Value>();
        internal readonly Dictionary<string, PyModule> Modules = new Dictionary<string, PyModule>();

        public VmOptions Options { get; }

        /// <summary>Receives everything scripts <c>print</c>.</summary>
        public Action<string> Output;

        public PyVm(VmOptions options = null)
        {
            Options = options ?? new VmOptions();
            CSharpPython.Builtins.Install(Builtins, Modules);
        }

        /// <summary>Makes a module available to <c>import</c>. Registering a name again replaces the module.</summary>
        public void RegisterModule(string name, Action<ModuleBuilder> configure)
        {
            var builder = new ModuleBuilder(name);
            configure(builder);
            Modules[name] = builder.Module;
        }

        public void RegisterModule(PyModule module) => Modules[module.Name] = module;

        /// <summary>Adds a name that every script can use without importing, such as a global helper function.</summary>
        public void SetBuiltin(string name, Value value) => Builtins[name] = value;

        /// <summary>
        /// Defines an exception class that host functions can raise with <c>new PyError(name, message)</c> and that
        /// scripts can catch by name. <paramref name="parent"/> defaults to <c>Exception</c>.
        /// </summary>
        public PyExceptionType RegisterExceptionType(string name, string parent = null)
        {
            PyExceptionType parentType = null;
            if (parent != null && !ExceptionTypes.TryGet(parent, out parentType))
                throw new ArgumentException("unknown parent exception type '" + parent + "'", nameof(parent));
            var type = ExceptionTypes.Register(name, parentType);
            Builtins[name] = Value.FromExceptionType(type);
            return type;
        }

        /// <summary>Compiles source text. Throws <see cref="PySyntaxError"/> with the line and column of a mistake.</summary>
        public Script Compile(string source, string sourceName = "<script>")
        {
            var module = Parser.ParseModule(source);
            return new Script(Compiler.Compile(module, sourceName));
        }

        /// <summary>Starts a new task for a script. Nothing runs until you call <see cref="PyTask.Run"/>.</summary>
        public PyTask Start(Script script) => new PyTask(this, script);

        /// <summary>Compiles and starts in one step.</summary>
        public PyTask Start(string source, string sourceName = "<script>") => Start(Compile(source, sourceName));
    }
}
