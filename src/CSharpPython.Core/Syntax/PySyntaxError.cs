using System;

namespace CSharpPython.Syntax
{
    /// <summary>Thrown when source text cannot be tokenized, parsed or resolved.</summary>
    public sealed class PySyntaxError : Exception
    {
        /// <summary>One-based line number.</summary>
        public int Line { get; }

        /// <summary>One-based column number.</summary>
        public int Column { get; }

        /// <summary>Zero-based offset into the source, or -1 when unknown.</summary>
        public int Offset { get; }

        /// <summary>Number of characters the error covers (at least 1).</summary>
        public int Length { get; }

        /// <summary>The message without the position suffix.</summary>
        public string Problem { get; }

        public PySyntaxError(string message, int line, int column, int offset = -1, int length = 1)
            : base(message + " (line " + line + ", column " + column + ")")
        {
            Problem = message;
            Line = line;
            Column = column;
            Offset = offset;
            Length = length < 1 ? 1 : length;
        }
    }
}
