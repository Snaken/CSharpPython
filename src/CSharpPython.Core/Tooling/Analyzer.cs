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
            var tokens = Lexer.TokenizeTolerant(source, out var diagnostics);
            return Analyze(tokens, diagnostics);
        }

        // For callers that have lexed the text already: tokens and the lexical problems found with them.
        internal static List<Diagnostic> Analyze(List<Token> tokens, List<Diagnostic> diagnostics)
        {
            if (diagnostics.Count > 0) return diagnostics;
            try
            {
                Compiler.Compile(Parser.ParseModule(tokens), "<script>");
            }
            catch (PySyntaxError e)
            {
                diagnostics.Add(new Diagnostic(e.Problem, e.Line, e.Column, e.Offset, e.Length));
            }
            return diagnostics;
        }
    }
}
