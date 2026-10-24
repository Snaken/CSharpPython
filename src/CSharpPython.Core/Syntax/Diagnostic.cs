namespace CSharpPython.Syntax
{
    /// <summary>A problem found while lexing, reported by the tolerant lexing mode used by editor tooling.</summary>
    public readonly struct Diagnostic
    {
        public readonly string Message;
        public readonly int Line;
        public readonly int Column;
        public readonly int Offset;
        public readonly int Length;

        public Diagnostic(string message, int line, int column, int offset, int length)
        {
            Message = message;
            Line = line;
            Column = column;
            Offset = offset;
            Length = length;
        }

        public PySyntaxError ToException() => new PySyntaxError(Message, Line, Column, Offset, Length);

        public override string ToString() => Message + " (line " + Line + ", column " + Column + ")";
    }
}
