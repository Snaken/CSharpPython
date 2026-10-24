using System.Collections.Generic;
using CSharpPython.Syntax;

namespace CSharpPython.Compilation
{
    /// <summary>
    /// The result of name resolution for one function (or the module). Inside a function every name that is
    /// assigned and not declared <c>global</c> is a local with a fixed slot; all other names are globals.
    /// </summary>
    internal sealed class Scope
    {
        public readonly DefStmt Function; // null for the module scope
        public readonly List<string> Locals = new List<string>();
        public readonly HashSet<string> DeclaredGlobals = new HashSet<string>();
        private readonly Dictionary<string, int> _slots = new Dictionary<string, int>();

        public Scope(DefStmt function)
        {
            Function = function;
        }

        public bool IsModule => Function == null;

        public int LocalCount => Locals.Count;

        public bool TryGetLocal(string name, out int slot)
        {
            return _slots.TryGetValue(name, out slot);
        }

        public bool IsLocal(string name) => _slots.ContainsKey(name);

        public int AddLocal(string name)
        {
            if (_slots.TryGetValue(name, out int existing)) return existing;
            int slot = Locals.Count;
            Locals.Add(name);
            _slots[name] = slot;
            return slot;
        }
    }

    /// <summary>The scopes of a whole script, keyed by the function that owns them.</summary>
    internal sealed class ResolvedModule
    {
        public readonly ModuleNode Module;
        public readonly Scope ModuleScope = new Scope(null);
        public readonly Dictionary<DefStmt, Scope> Functions = new Dictionary<DefStmt, Scope>();

        public ResolvedModule(ModuleNode module)
        {
            Module = module;
        }
    }
}
