using System.Collections;
using System.Collections.Generic;

namespace CSharpPython
{
    /// <summary>Where a global variable's value lives, so a call site that found it once can read it again without a lookup.</summary>
    internal sealed class GlobalCell
    {
        public Value Value;
    }

    /// <summary>
    /// The global variables of a task. It reads like a dictionary of names to values, and its <see cref="Version"/> goes up
    /// whenever a name is added or removed (assigning to an existing name leaves it alone), which tells a cached lookup that
    /// a name it resolved to a built-in may now be a global, or that a cell it holds is gone.
    /// </summary>
    public sealed class Globals : IEnumerable<KeyValuePair<string, Value>>
    {
        private readonly Dictionary<string, GlobalCell> _cells = new Dictionary<string, GlobalCell>();

        internal int Version;

        public int Count => _cells.Count;

        public bool ContainsKey(string name) => _cells.ContainsKey(name);

        public bool TryGetValue(string name, out Value value)
        {
            if (_cells.TryGetValue(name, out GlobalCell cell))
            {
                value = cell.Value;
                return true;
            }
            value = default;
            return false;
        }

        internal bool TryGetCell(string name, out GlobalCell cell) => _cells.TryGetValue(name, out cell);

        public Value this[string name]
        {
            get
            {
                if (!_cells.TryGetValue(name, out GlobalCell cell)) throw new KeyNotFoundException("the global '" + name + "' is not defined");
                return cell.Value;
            }
            set
            {
                if (_cells.TryGetValue(name, out GlobalCell cell)) cell.Value = value;
                else
                {
                    _cells[name] = new GlobalCell { Value = value };
                    Version++;
                }
            }
        }

        public bool Remove(string name)
        {
            if (!_cells.Remove(name)) return false;
            Version++;
            return true;
        }

        public IEnumerable<string> Keys => _cells.Keys;

        public IEnumerator<KeyValuePair<string, Value>> GetEnumerator()
        {
            foreach (var pair in _cells) yield return new KeyValuePair<string, Value>(pair.Key, pair.Value.Value);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>What a global-variable read last found at one place in a script (see <see cref="Globals"/>).</summary>
    internal sealed class GlobalSite
    {
        public Globals Globals;
        public int Version;
        public int BuiltinsVersion;
        public GlobalCell Cell;   // the cell of a global, which is always current; null when the name was a built-in
        public Value Value;       // the built-in
    }
}
