using System.Collections.Generic;
using CSharpPython.Syntax;

namespace CSharpPython.Compilation
{
    /// <summary>
    /// The result of name resolution for one function (or the module). Inside a function every name that is
    /// assigned and not declared <c>global</c> or <c>nonlocal</c> is a local with a fixed slot. A name that is not
    /// local but is a local of an enclosing function is a free variable, read through a cell shared with that
    /// function; any other name is a global.
    /// </summary>
    internal sealed class Scope
    {
        public readonly DefStmt Function; // null for the module scope
        public readonly Scope Parent;
        public readonly List<Scope> Children = new List<Scope>();
        public readonly List<string> Locals = new List<string>();
        public readonly HashSet<string> DeclaredGlobals = new HashSet<string>();
        public readonly HashSet<string> DeclaredNonlocals = new HashSet<string>();
        public readonly Dictionary<string, Node> NonlocalSites = new Dictionary<string, Node>();

        /// <summary>Names read in this scope, in order of first use.</summary>
        public readonly List<string> Used = new List<string>();

        /// <summary>Locals that an inner function uses, so they live in a cell shared with it.</summary>
        public readonly List<string> CellVars = new List<string>();

        /// <summary>Names this function takes from an enclosing function's cells, in closure order.</summary>
        public readonly List<string> FreeVars = new List<string>();

        private readonly HashSet<string> _used = new HashSet<string>();
        private readonly Dictionary<string, int> _slots = new Dictionary<string, int>();
        private readonly HashSet<string> _cells = new HashSet<string>();
        private readonly Dictionary<string, int> _free = new Dictionary<string, int>();

        public Scope(DefStmt function, Scope parent = null)
        {
            Function = function;
            Parent = parent;
            parent?.Children.Add(this);
        }

        public bool IsModule => Function == null;

        /// <summary>
        /// Whether this is the body of a class. Its names are not visible to the functions defined inside it, so
        /// those skip it when looking for variables of enclosing scopes.
        /// </summary>
        public bool IsClass;

        /// <summary>Whether the body contains <c>yield</c>, which makes calls return a generator.</summary>
        public bool IsGenerator;

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

        public void Use(string name)
        {
            if (_used.Add(name)) Used.Add(name);
        }

        public bool IsCell(string name) => _cells.Contains(name);

        public void MakeCell(string name)
        {
            if (_cells.Add(name)) CellVars.Add(name);
        }

        public bool TryGetFree(string name, out int index) => _free.TryGetValue(name, out index);

        public void AddFree(string name)
        {
            if (_free.ContainsKey(name)) return;
            _free[name] = FreeVars.Count;
            FreeVars.Add(name);
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
