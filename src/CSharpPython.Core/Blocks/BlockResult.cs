using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharpPython.Blocks
{
    public enum BlockSeverity
    {
        /// <summary>The graph cannot be turned into a script.</summary>
        Error,

        /// <summary>The graph compiles, but something is probably a mistake (for example a block connected to nothing).</summary>
        Warning,
    }

    /// <summary>A problem found in a graph, tied to the block (and pin) the editor should highlight.</summary>
    public sealed class BlockDiagnostic
    {
        public BlockSeverity Severity { get; }

        /// <summary>The block at fault, or null when the problem is with the function or graph as a whole.</summary>
        public string NodeId { get; }

        /// <summary>The pin at fault, or null.</summary>
        public string Pin { get; }

        /// <summary>The function the problem is in, or null for a problem with a global variable or the graph.</summary>
        public string Function { get; }

        public string Message { get; }

        public BlockDiagnostic(BlockSeverity severity, string message, string function = null, string nodeId = null, string pin = null)
        {
            Severity = severity;
            Message = message;
            Function = function;
            NodeId = nodeId;
            Pin = pin;
        }

        public override string ToString()
        {
            string where = NodeId != null ? " [" + NodeId + (Pin != null ? "." + Pin : "") + "]" : "";
            return Severity + where + ": " + Message;
        }
    }

    /// <summary>
    /// Maps lines of generated Python back to the blocks that produced them, so a runtime error or breakpoint can be
    /// shown on the right block. Lines are one-based, like <see cref="TraceFrame.Line"/>.
    /// </summary>
    public sealed class SourceMap
    {
        private readonly string[] _nodes;

        internal SourceMap(string[] nodeIdPerLine)
        {
            _nodes = nodeIdPerLine;
        }

        /// <summary>The number of lines in the generated source.</summary>
        public int LineCount => _nodes.Length;

        /// <summary>The block that produced <paramref name="line"/>, or null for lines no single block owns (imports, globals, <c>def</c>).</summary>
        public string NodeAt(int line) => line >= 1 && line <= _nodes.Length ? _nodes[line - 1] : null;

        /// <summary>Every line the block produced, in order. Empty if the block produced no code.</summary>
        public IReadOnlyList<int> LinesOf(string nodeId)
        {
            var lines = new List<int>();
            for (int i = 0; i < _nodes.Length; i++)
                if (_nodes[i] == nodeId) lines.Add(i + 1);
            return lines;
        }

        /// <summary>
        /// Finds the block to blame for a runtime error: the innermost frame of the traceback that lands on a line
        /// owned by a block. Returns null if no frame does.
        /// </summary>
        public string Locate(PyRuntimeError error)
        {
            if (error == null) throw new ArgumentNullException(nameof(error));
            foreach (var frame in error.Trace)
            {
                string id = NodeAt(frame.Line);
                if (id != null) return id;
            }
            return null;
        }
    }

    /// <summary>The outcome of <see cref="BlockCompiler.Compile"/>.</summary>
    public sealed class BlockCompileResult
    {
        /// <summary>The generated Python, or null when there are errors.</summary>
        public string Source { get; }

        /// <summary>Maps generated lines to blocks, or null when there are errors.</summary>
        public SourceMap Map { get; }

        public IReadOnlyList<BlockDiagnostic> Diagnostics { get; }

        public bool Succeeded => Source != null;

        public IEnumerable<BlockDiagnostic> Errors => Diagnostics.Where(d => d.Severity == BlockSeverity.Error);

        internal BlockCompileResult(string source, SourceMap map, List<BlockDiagnostic> diagnostics)
        {
            Source = source;
            Map = map;
            Diagnostics = diagnostics;
        }
    }
}
