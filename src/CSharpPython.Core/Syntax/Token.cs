namespace CSharpPython.Syntax
{
    /// <summary>A single lexical token with its position in the source text.</summary>
    public readonly struct Token
    {
        public readonly TokenKind Kind;

        /// <summary>
        /// The token's text. For names, numbers and operators this is the source text. For strings it is the
        /// decoded value. For f-strings it is the raw body between the quotes (escapes are decoded by the parser).
        /// For <see cref="TokenKind.Error"/> it is the message.
        /// </summary>
        public readonly string Text;

        /// <summary>Zero-based offset of the first character of the token.</summary>
        public readonly int Start;

        /// <summary>Number of source characters covered by the token.</summary>
        public readonly int Length;

        /// <summary>One-based line number.</summary>
        public readonly int Line;

        /// <summary>One-based column number.</summary>
        public readonly int Column;

        public Token(TokenKind kind, string text, int start, int length, int line, int column)
        {
            Kind = kind;
            Text = text;
            Start = start;
            Length = length;
            Line = line;
            Column = column;
        }

        public int End => Start + Length;

        public override string ToString() => Kind + (Text != null ? " '" + Text + "'" : "") + " @" + Line + ":" + Column;
    }
}
