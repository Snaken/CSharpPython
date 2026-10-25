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
            AddModule(builder.Module);
        }

        public void RegisterModule(PyModule module) => AddModule(module);

        // Modules are kept in a flat table under their full names ("game.ai"). Registering one also creates the
        // packages above it that are missing, and links each module into its parent as an attribute, so that
        // "import game.ai" gives game.ai and "from game import ai" works.
        private void AddModule(PyModule module)
        {
            Modules[module.Name] = module;
            foreach (var other in Modules.Values)
            {
                string leaf = DirectChild(module.Name, other.Name);
                if (leaf != null && !module.Members.ContainsKey(leaf)) module.Members[leaf] = Value.FromModule(other);
            }
            for (var child = module; ; )
            {
                int dot = child.Name.LastIndexOf('.');
                if (dot < 0) break;
                string parentName = child.Name.Substring(0, dot);
                if (!Modules.TryGetValue(parentName, out PyModule parent)) Modules[parentName] = parent = new PyModule(parentName);
                parent.Members[child.Name.Substring(dot + 1)] = Value.FromModule(child);
                child = parent;
            }
        }

        // The last part of 'child' when it is a module directly inside 'parent', else null.
        private static string DirectChild(string parent, string child) =>
            child.Length > parent.Length + 1 && child.StartsWith(parent + ".", StringComparison.Ordinal) && child.IndexOf('.', parent.Length + 1) < 0
                ? child.Substring(parent.Length + 1) : null;

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

        /// <summary>
        /// Recreates a task from bytes written by <see cref="PyTask.Save"/>. <paramref name="script"/> must be the
        /// same compiled code the task was running, and the host API (modules, built-ins, exception types) must be
        /// registered as it was. Throws <see cref="SnapshotException"/> otherwise or if the data is damaged.
        /// </summary>
        public PyTask Restore(Script script, byte[] data) => PyTask.RestoreTask(this, script, data);

        /// <summary>Compiles and starts in one step.</summary>
        public PyTask Start(string source, string sourceName = "<script>") => Start(Compile(source, sourceName));
    }
}
