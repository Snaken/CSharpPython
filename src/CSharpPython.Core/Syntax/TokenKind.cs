namespace CSharpPython.Syntax
{
    /// <summary>The lexical categories produced by <see cref="Lexer"/>.</summary>
    public enum TokenKind
    {
        EndOfFile,
        Newline,
        Indent,
        Dedent,
        /// <summary>A malformed piece of source. Only produced by the tolerant lexing mode.</summary>
        Error,

        Name,
        Int,
        Float,
        String,
        FString,

        // Keywords.
        KwDef, KwIf, KwElif, KwElse, KwWhile, KwFor, KwIn, KwReturn, KwBreak, KwContinue, KwPass,
        KwAnd, KwOr, KwNot, KwIs, KwTrue, KwFalse, KwNone, KwGlobal, KwNonlocal, KwLambda,
        KwTry, KwExcept, KwFinally, KwRaise, KwImport, KwFrom, KwClass, KwAs, KwDel, KwAssert, KwWith,
        KwYield,

        /// <summary>A reserved Python keyword (async, await, ...) that is not supported yet.</summary>
        KwUnsupported,

        // Operators and punctuation.
        Plus, Minus, Star, Slash, DoubleSlash, Percent, DoubleStar,
        Pipe, Ampersand, Caret, Tilde, LShift, RShift,
        Assign, EqEq, NotEq, Lt, Le, Gt, Ge,
        PlusAssign, MinusAssign, StarAssign, SlashAssign, DoubleSlashAssign, PercentAssign, DoubleStarAssign,
        PipeAssign, AmpersandAssign, CaretAssign, LShiftAssign, RShiftAssign,
        LParen, RParen, LBracket, RBracket, LBrace, RBrace,
        Comma, Colon, Dot, Semicolon,
    }
}
