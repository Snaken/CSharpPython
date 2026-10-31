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
        /// changes the graph but not the task. Nothing is changed or raised. Compile again after each
        /// <see cref="Changed"/>; a compile of a graph near the limits is not free, so a view may wait for a pause in
        /// typing.
        /// </summary>
        public BlockCompileResult Compile() => BlockCompiler.Compile(Graph, Library);

        /// <summary>The blocks that name a function or block type the library does not have, to draw as placeholders.</summary>
        public IReadOnlyList<BlockPlaceholder> Placeholders() => BlockPlaceholders.Find(Graph, Library);
    }
}
