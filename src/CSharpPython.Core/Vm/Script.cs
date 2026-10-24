using System.Collections.Generic;
using CSharpPython.Compilation;

namespace CSharpPython
{
    /// <summary>A compiled script. It is immutable, so one script can be started as many tasks.</summary>
    public sealed class Script
    {
        internal readonly Code Code;
        private Code[] _codes;
        private Dictionary<Code, int> _codeIndex;
        private ulong _fingerprint;

        internal Script(Code code)
        {
            Code = code;
        }

        public string Name => Code.SourceName;

        /// <summary>A readable bytecode listing, for debugging.</summary>
        public string Disassemble() => Code.Disassemble();

        /// <summary>
        /// A hash of the compiled code. A snapshot records it so that it is only restored against the same script.
        /// </summary>
        public ulong Fingerprint
        {
            get
            {
                Index();
                return _fingerprint;
            }
        }

        // Every code object in the script, module first, in a fixed depth-first order. Snapshots refer to
        // functions by their position in this list.
        internal Code[] Codes
        {
            get
            {
                Index();
                return _codes;
            }
        }

        internal int IndexOf(Code code)
        {
            Index();
            return _codeIndex.TryGetValue(code, out int i) ? i : -1;
        }

        private void Index()
        {
            if (_codes != null) return;
            var list = new List<Code>();
            var map = new Dictionary<Code, int>();
            var hash = new Fnv();
            Visit(Code, list, map, ref hash);
            _codeIndex = map;
            _fingerprint = hash.Value;
            _codes = list.ToArray();
        }

        private static void Visit(Code code, List<Code> list, Dictionary<Code, int> map, ref Fnv h)
        {
            map[code] = list.Count;
            list.Add(code);
            h.Add(code.Name);
            h.Add(code.IsModule ? 1 : 0);
            h.Add(code.NumLocals);
            h.Add(code.NumParams);
            h.Add(code.NumDefaults);
            foreach (var n in code.ParamNames ?? System.Array.Empty<string>()) h.Add(n);
            foreach (var n in code.LocalNames ?? System.Array.Empty<string>()) h.Add(n);
            foreach (var n in code.Names ?? System.Array.Empty<string>()) h.Add(n);
            foreach (var c in code.Constants) h.Add(c.Kind + ":" + c.Repr());
            foreach (var l in code.Lines) h.Add(l);
            foreach (var ins in code.Instructions)
            {
                h.Add((int)ins.Op);
                h.Add(ins.Arg);
            }
            h.Add(code.Functions.Length);
            foreach (var f in code.Functions) Visit(f, list, map, ref h);
        }

        // FNV-1a, 64 bit: stable across runs and platforms (string.GetHashCode is not).
        private struct Fnv
        {
            private ulong _h;
            private bool _init;

            public ulong Value => _init ? _h : 14695981039346656037UL;

            private void Byte(byte b)
            {
                if (!_init) { _h = 14695981039346656037UL; _init = true; }
                _h = (_h ^ b) * 1099511628211UL;
            }

            public void Add(int v)
            {
                for (int i = 0; i < 4; i++) Byte((byte)(v >> (8 * i)));
            }

            public void Add(string s)
            {
                s = s ?? "";
                Add(s.Length);
                foreach (char c in s)
                {
                    Byte((byte)c);
                    Byte((byte)(c >> 8));
                }
            }
        }
    }
}
