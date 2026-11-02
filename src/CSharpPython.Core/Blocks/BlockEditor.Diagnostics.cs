using System.Collections.Generic;

namespace CSharpPython.Blocks
{
    public sealed partial class BlockEditor
    {
        /// <summary>
        /// Compiles the graph as it is now, with the editor's library. The result has the generated Python to show
        /// beside the blocks, the diagnostics (compile and type errors, warnings) for the editor to badge blocks with
        /// (see <see cref="BlockCompileResult.DiagnosticsOf"/>), and the source map that says which block a running
        /// task is at: keep the result of the compile whose source the task runs, and ask its
        /// <see cref="SourceMap.ActiveNodes"/> or <see cref="SourceMap.Running"/>, because editing after that
        /// changes the graph but not the task. Nothing is changed or raised. A compile of a graph near the limits is not
        /// free, so the result is kept: asking again returns the same result object until an edit that could change the
        /// program (not one that only moves, collapses, comments or groups blocks, see <see cref="ProgramVersion"/>),
        /// an undo or redo, or a change to the library. A view may therefore call this after every <see cref="Changed"/>
        /// without paying for edits that only change the drawing. The graph is the editor's to change: if code changes
        /// <see cref="Graph"/> directly, call <see cref="InvalidateCompile"/> before compiling again.
        /// </summary>
        public BlockCompileResult Compile()
        {
            int revision = Library.Revision;
            if (_compiled != null && _compiledVersion == _programVersion && _compiledRevision == revision) return _compiled;
            var result = BlockCompiler.Compile(Graph, Library);
            _compiled = result;
            _compiledVersion = _programVersion;
            _compiledRevision = revision;
            return result;
        }

        /// <summary>
        /// A number that changes after every edit that could change what <see cref="Compile"/> returns: any edit that is
        /// not only about drawing, and every undo and redo. Moving a block, collapsing it, and comment boxes, reroute
        /// points and groups leave it as it was. A view can compare it with the number it last saw to skip work.
        /// </summary>
        public int ProgramVersion => _programVersion;

        /// <summary>
        /// Makes the next <see cref="Compile"/> start again. Needed only when something other than an editor edit has
        /// changed the graph (code that writes to <see cref="Graph"/> directly); it also changes <see cref="ProgramVersion"/>.
        /// </summary>
        public void InvalidateCompile() => _programVersion++;

        private int _programVersion;
        private BlockCompileResult _compiled;
        private int _compiledVersion;
        private int _compiledRevision;

        /// <summary>The blocks that name a function or block type the library does not have, to draw as placeholders.</summary>
        public IReadOnlyList<BlockPlaceholder> Placeholders() => BlockPlaceholders.Find(Graph, Library);
    }
}
