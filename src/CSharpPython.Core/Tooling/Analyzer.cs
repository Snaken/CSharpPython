using System.Collections.Generic;
using CSharpPython.Compilation;
using CSharpPython.Syntax;

namespace CSharpPython.Editor
{
    /// <summary>Finds the problems in a script for an editor to underline, without running it.</summary>
    public static class Analyzer
    {
        /// <summary>
        /// Returns every lexical problem and, when there are none, the first syntax or scoping error. An empty list
        /// means the script compiles. Never throws.
        /// </summary>
        public static List<Diagnostic> Analyze(string source)
        {
            source = source ?? "";
            Lexer.TokenizeTolerant(source, out var diagnostics);
            if (diagnostics.Count > 0) return diagnostics;
            try
            {
                Compiler.Compile(Parser.ParseModule(source), "<script>");
            }
            catch (PySyntaxError e)
            {
                diagnostics.Add(new Diagnostic(e.Problem, e.Line, e.Column, e.Offset, e.Length));
            }
            return diagnostics;
        }
    }
}
