using System;
using System.Runtime.CompilerServices;

namespace CSharpPython.Syntax
{
    /// <summary>
    /// Keeps hostile source from overflowing the host's stack. The parser, resolver and compiler are recursive, and a
    /// stack overflow cannot be caught: it ends the whole process.
    /// </summary>
    internal static class Nesting
    {
        /// <summary>The deepest nesting of brackets, operators and blocks the parser accepts.</summary>
        public const int MaxDepth = 300;

        /// <summary>The longest run of chained operators (<c>a + b + c</c>, <c>a.b.c</c>, <c>a(b)(c)</c>) in one expression.</summary>
        public const int MaxChain = 500;

        /// <summary>Raises a syntax error when the thread is close to running out of stack.</summary>
        public static void CheckStack(int line, int column)
        {
            try
            {
                RuntimeHelpers.EnsureSufficientExecutionStack();
            }
            catch (InsufficientExecutionStackException)
            {
                throw TooDeep(line, column);
            }
        }

        public static void CheckStack(Node at) => CheckStack(at.Line, at.Column);

        public static PySyntaxError TooDeep(int line, int column) =>
            new PySyntaxError("the code is nested too deeply", line, column);

        public static PySyntaxError TooLong(int line, int column) =>
            new PySyntaxError("too many chained operators in one expression (limit " + MaxChain + ")", line, column);
    }
}
