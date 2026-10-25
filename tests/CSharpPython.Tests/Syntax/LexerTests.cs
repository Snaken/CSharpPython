using System.Collections.Generic;
using System.Linq;
using CSharpPython.Syntax;

namespace CSharpPython.Tests.Syntax
{
    public class LexerTests
    {
        private static List<Token> Lex(string src) => Lexer.Tokenize(src);

        private static string Kinds(string src) =>
            string.Join(" ", Lex(src).Select(t => t.Kind.ToString()));

        private static PySyntaxError Fails(string src)
        {
            var ex = Assert.Throws<PySyntaxError>(() => Lex(src));
            return ex;
        }

        [Test]
        public void EmptySourceProducesOnlyEndOfFile()
        {
            Assert.That(Kinds(""), Is.EqualTo("EndOfFile"));
        }

        [Test]
        public void NullSourceIsRejected()
        {
            Assert.Throws<System.ArgumentNullException>(() => Lexer.Tokenize(null));
        }

        [Test]
        public void SimpleAssignmentEndsWithNewlineEvenWithoutTrailingNewline()
        {
            Assert.That(Kinds("x = 1"), Is.EqualTo("Name Assign Int Newline EndOfFile"));
        }

        [Test]
        public void KeywordsAreRecognised()
        {
            var tokens = Lex("def if elif else while for in return break continue pass and or not is True False None global try except raise import from class as del assert with");
            var expected = new[]
            {
                TokenKind.KwDef, TokenKind.KwIf, TokenKind.KwElif, TokenKind.KwElse, TokenKind.KwWhile, TokenKind.KwFor,
                TokenKind.KwIn, TokenKind.KwReturn, TokenKind.KwBreak, TokenKind.KwContinue, TokenKind.KwPass,
                TokenKind.KwAnd, TokenKind.KwOr, TokenKind.KwNot, TokenKind.KwIs, TokenKind.KwTrue, TokenKind.KwFalse,
                TokenKind.KwNone, TokenKind.KwGlobal, TokenKind.KwTry, TokenKind.KwExcept, TokenKind.KwRaise,
                TokenKind.KwImport, TokenKind.KwFrom, TokenKind.KwClass, TokenKind.KwAs, TokenKind.KwDel, TokenKind.KwAssert, TokenKind.KwWith,
            };
            Assert.That(tokens.Take(expected.Length).Select(t => t.Kind), Is.EqualTo(expected));
        }

        [TestCase("yield")]
        [TestCase("async")]
        [TestCase("await")]
        public void UnsupportedKeywordsAreFlagged(string word)
        {
            Assert.That(Lex(word)[0].Kind, Is.EqualTo(TokenKind.KwUnsupported));
            Assert.That(Lex(word)[0].Text, Is.EqualTo(word));
        }

        [Test]
        public void LambdaAndNonlocalAreKeywords()
        {
            var t = Lex("lambda nonlocal");
            Assert.That(t.Take(2).Select(x => x.Kind), Is.EqualTo(new[] { TokenKind.KwLambda, TokenKind.KwNonlocal }));
        }

        [Test]
        public void IdentifiersMayContainUnderscoresDigitsAndUnicodeLetters()
        {
            var t = Lex("_a1 ünï x_2");
            Assert.That(t.Take(3).Select(x => x.Text), Is.EqualTo(new[] { "_a1", "ünï", "x_2" }));
        }

        [Test]
        public void KeywordPrefixOfNameIsNotAKeyword()
        {
            Assert.That(Lex("iffy")[0].Kind, Is.EqualTo(TokenKind.Name));
        }

        // ---- Indentation -------------------------------------------------------------------------------------

        [Test]
        public void IndentAndDedentAreEmitted()
        {
            Assert.That(Kinds("if x:\n    y\nz\n"),
                Is.EqualTo("KwIf Name Colon Newline Indent Name Newline Dedent Name Newline EndOfFile"));
        }

        [Test]
        public void MultipleDedentsAreEmittedAtOnce()
        {
            Assert.That(Kinds("if a:\n  if b:\n    c\nd\n"),
                Is.EqualTo("KwIf Name Colon Newline Indent KwIf Name Colon Newline Indent Name Newline Dedent Dedent Name Newline EndOfFile"));
        }

        [Test]
        public void DedentsAreEmittedAtEndOfFile()
        {
            Assert.That(Kinds("if a:\n  b"), Is.EqualTo("KwIf Name Colon Newline Indent Name Newline Dedent EndOfFile"));
        }

        [Test]
        public void BlankLinesAndCommentLinesDoNotAffectIndentation()
        {
            Assert.That(Kinds("if a:\n\n      # comment\n    \n  b\n# trailing\nc\n"),
                Is.EqualTo("KwIf Name Colon Newline Indent Name Newline Dedent Name Newline EndOfFile"));
        }

        [Test]
        public void CommentAtEndOfLineIsIgnored()
        {
            Assert.That(Kinds("x = 1  # set x\n"), Is.EqualTo("Name Assign Int Newline EndOfFile"));
        }

        [Test]
        public void CommentOnlySourceHasNoTokensButEof()
        {
            Assert.That(Kinds("# nothing here"), Is.EqualTo("EndOfFile"));
        }

        [Test]
        public void WhitespaceOnlyLastLineIsHandled()
        {
            Assert.That(Kinds("x\n    "), Is.EqualTo("Name Newline EndOfFile"));
        }

        [Test]
        public void TabsAdvanceToNextMultipleOfEight()
        {
            Assert.That(Kinds("if a:\n\tb\n        c\n"),
                Is.EqualTo("KwIf Name Colon Newline Indent Name Newline Name Newline Dedent EndOfFile"));
        }

        [Test]
        public void FormFeedResetsIndentationCount()
        {
            Assert.That(Kinds("a\n\f  b\n"), Is.EqualTo("Name Newline Indent Name Newline Dedent EndOfFile"));
        }

        [Test]
        public void InconsistentDedentIsAnError()
        {
            var ex = Fails("if a:\n        b\n    c\n");
            Assert.That(ex.Problem, Does.Contain("unindent"));
            Assert.That(ex.Line, Is.EqualTo(3));
        }

        [Test]
        public void CrLfLineEndingsAreSupported()
        {
            Assert.That(Kinds("if a:\r\n  b\r\nc\r\n"),
                Is.EqualTo("KwIf Name Colon Newline Indent Name Newline Dedent Name Newline EndOfFile"));
        }

        [Test]
        public void LoneCarriageReturnIsANewline()
        {
            Assert.That(Kinds("a\rb"), Is.EqualTo("Name Newline Name Newline EndOfFile"));
        }

        [Test]
        public void ConsecutiveNewlinesProduceOneNewlineToken()
        {
            Assert.That(Kinds("a\n\n\nb\n"), Is.EqualTo("Name Newline Name Newline EndOfFile"));
        }

        [Test]
        public void LeadingBlankLinesProduceNoNewlineToken()
        {
            Assert.That(Kinds("\n\nx\n"), Is.EqualTo("Name Newline EndOfFile"));
        }

        // ---- Brackets and continuation ----------------------------------------------------------------------

        [Test]
        public void NewlinesInsideBracketsAreIgnored()
        {
            Assert.That(Kinds("x = [1,\n  2,\n    3]\n"),
                Is.EqualTo("Name Assign LBracket Int Comma Int Comma Int RBracket Newline EndOfFile"));
        }

        [Test]
        public void IndentationInsideBracketsIsIgnored()
        {
            Assert.That(Kinds("f(a,\n        b)\n"), Is.EqualTo("Name LParen Name Comma Name RParen Newline EndOfFile"));
        }

        [Test]
        public void BackslashJoinsLines()
        {
            Assert.That(Kinds("x = 1 + \\\n    2\n"), Is.EqualTo("Name Assign Int Plus Int Newline EndOfFile"));
        }

        [Test]
        public void BackslashFollowedByOtherCharacterIsAnError()
        {
            Assert.That(Fails("x = 1 \\ 2").Problem, Does.Contain("line continuation"));
        }

        [Test]
        public void UnclosedBracketIsAnErrorAtTheOpeningBracket()
        {
            var ex = Fails("x = (1,\n 2");
            Assert.That(ex.Problem, Does.Contain("was never closed"));
            Assert.That(ex.Line, Is.EqualTo(1));
            Assert.That(ex.Column, Is.EqualTo(5));
        }

        [Test]
        public void UnmatchedClosingBracketIsAnError()
        {
            Assert.That(Fails("x = 1)").Problem, Does.Contain("unmatched"));
        }

        [Test]
        public void MismatchedBracketsAreAnError()
        {
            Assert.That(Fails("x = (1]").Problem, Does.Contain("does not match"));
        }

        [Test]
        public void AllBracketKindsAreTokenised()
        {
            Assert.That(Kinds("()[]{}"), Is.EqualTo("LParen RParen LBracket RBracket LBrace RBrace Newline EndOfFile"));
        }

        // ---- Numbers ------------------------------------------------------------------------------------------

        [TestCase("0", "0")]
        [TestCase("42", "42")]
        [TestCase("1_000_000", "1000000")]
        [TestCase("0xFF", "255")]
        [TestCase("0Xff", "255")]
        [TestCase("0b101", "5")]
        [TestCase("0o17", "15")]
        [TestCase("0x_ff", "255")]
        [TestCase("9223372036854775807", "9223372036854775807")]
        public void IntegerLiterals(string src, string expected)
        {
            var t = Lex(src)[0];
            Assert.That(t.Kind, Is.EqualTo(TokenKind.Int));
            Assert.That(t.Text, Is.EqualTo(expected));
        }

        [TestCase("1.5", "1.5")]
        [TestCase(".5", "0.5")]
        [TestCase("1.", "1.")]
        [TestCase("1e3", "1e3")]
        [TestCase("1E-3", "1e-3")]
        [TestCase("2.5e+2", "2.5e+2")]
        [TestCase("1_0.2_5", "10.25")]
        public void FloatLiterals(string src, string expected)
        {
            var t = Lex(src)[0];
            Assert.That(t.Kind, Is.EqualTo(TokenKind.Float));
            Assert.That(t.Text, Is.EqualTo(expected));
        }

        [Test]
        public void ExponentWithoutDigitsIsNotConsumed()
        {
            // "1e" - the 'e' is an identifier character stuck to a number, which is invalid
            Assert.That(Fails("1e").Problem, Does.Contain("invalid number"));
        }

        [Test]
        public void MethodCallOnIntegerLiteralNeedsSpaceButFloatDotWorks()
        {
            Assert.That(Kinds("1 .real"), Is.EqualTo("Int Dot Name Newline EndOfFile"));
        }

        [Test]
        public void IntegerTooLargeIsAnError()
        {
            Assert.That(Fails("9223372036854775808").Problem, Does.Contain("too large"));
        }

        [Test]
        public void HexIntegerTooLargeIsAnError()
        {
            Assert.That(Fails("0xFFFFFFFFFFFFFFFFFF").Problem, Does.Contain("too large"));
        }

        [TestCase("0x")]
        [TestCase("0b2")]
        [TestCase("0o8")]
        [TestCase("12abc")]
        [TestCase("0xfg")]
        [TestCase("1.5x")]
        public void InvalidNumbersAreErrors(string src)
        {
            Assert.That(Fails(src).Problem, Does.Contain("invalid number"));
        }

        // ---- Strings ------------------------------------------------------------------------------------------

        [Test]
        public void SingleAndDoubleQuotedStrings()
        {
            var t = Lex("'a' \"b\"");
            Assert.That(t[0].Kind, Is.EqualTo(TokenKind.String));
            Assert.That(t[0].Text, Is.EqualTo("a"));
            Assert.That(t[1].Text, Is.EqualTo("b"));
        }

        [Test]
        public void EmptyString()
        {
            Assert.That(Lex("''")[0].Text, Is.EqualTo(""));
            Assert.That(Lex("\"\"")[0].Text, Is.EqualTo(""));
        }

        [TestCase("'a\\nb'", "a\nb")]
        [TestCase("'a\\tb'", "a\tb")]
        [TestCase("'a\\rb'", "a\rb")]
        [TestCase("'a\\\\b'", "a\\b")]
        [TestCase("'a\\'b'", "a'b")]
        [TestCase("\"a\\\"b\"", "a\"b")]
        [TestCase("'\\a\\b\\f\\v'", "\a\b\f\v")]
        [TestCase("'\\x41'", "A")]
        [TestCase("'\\u00e9'", "é")]
        [TestCase("'\\U0001F600'", "\U0001F600")]
        [TestCase("'\\0'", "\0")]
        [TestCase("'\\101'", "A")]
        [TestCase("'\\q'", "\\q")]
        [TestCase("'a\\\nb'", "ab")]
        [TestCase("'a\\\r\nb'", "ab")]
        [TestCase("'a\\\rb'", "ab")]
        public void EscapeSequences(string src, string expected)
        {
            Assert.That(Lex(src)[0].Text, Is.EqualTo(expected));
        }

        [TestCase("'\\x4'")]
        [TestCase("'\\xZZ'")]
        [TestCase("'\\u12'")]
        [TestCase("'\\uZZZZ'")]
        [TestCase("'\\U00110000'")]
        public void BadNumericEscapesAreErrors(string src)
        {
            Assert.That(Fails(src).Problem, Does.Contain("escape"));
        }

        [Test]
        public void RawStringsKeepBackslashes()
        {
            Assert.That(Lex("r'a\\nb'")[0].Text, Is.EqualTo("a\\nb"));
            Assert.That(Lex("R'a\\nb'")[0].Text, Is.EqualTo("a\\nb"));
        }

        [Test]
        public void RawStringsStillAllowEscapedQuotes()
        {
            Assert.That(Lex("r'a\\'b'")[0].Text, Is.EqualTo("a\\'b"));
        }

        [Test]
        public void UnicodePrefixIsAccepted()
        {
            Assert.That(Lex("u'hi'")[0].Text, Is.EqualTo("hi"));
        }

        [Test]
        public void TripleQuotedStringsMayContainNewlinesAndQuotes()
        {
            var t = Lex("'''a\n'b'\n\"c\"'''");
            Assert.That(t[0].Kind, Is.EqualTo(TokenKind.String));
            Assert.That(t[0].Text, Is.EqualTo("a\n'b'\n\"c\""));
        }

        [Test]
        public void TripleQuotedStringWithCrLfNormalisesToLf()
        {
            Assert.That(Lex("\"\"\"a\r\nb\"\"\"")[0].Text, Is.EqualTo("a\nb"));
        }

        [Test]
        public void TripleQuotedStringWithLoneCarriageReturnNormalisesToLf()
        {
            Assert.That(Lex("\"\"\"a\rb\"\"\"")[0].Text, Is.EqualTo("a\nb"));
        }

        [Test]
        public void TokensAfterMultilineStringHaveCorrectLines()
        {
            var t = Lex("'''a\nb\nc''' x");
            Assert.That(t[1].Kind, Is.EqualTo(TokenKind.Name));
            Assert.That(t[1].Line, Is.EqualTo(3));
        }

        [Test]
        public void EscapedNewlineInsideTripleQuotedStringCountsLines()
        {
            var t = Lex("'''a\\\nb''' x");
            Assert.That(t[0].Text, Is.EqualTo("ab"));
            Assert.That(t[1].Line, Is.EqualTo(2));
        }

        [Test]
        public void EscapedCrLfInsideTripleQuotedStringCountsLines()
        {
            var t = Lex("'''a\\\r\nb''' x");
            Assert.That(t[0].Text, Is.EqualTo("ab"));
            Assert.That(t[1].Line, Is.EqualTo(2));
        }

        [Test]
        public void UnterminatedStringIsAnError()
        {
            Assert.That(Fails("'abc").Problem, Does.Contain("unterminated string"));
            Assert.That(Fails("'abc\nx").Problem, Does.Contain("unterminated string"));
        }

        [Test]
        public void UnterminatedStringEndingInBackslashIsAnError()
        {
            Assert.That(Fails("'abc\\").Problem, Does.Contain("unterminated string"));
        }

        [Test]
        public void UnterminatedTripleQuotedStringIsAnError()
        {
            Assert.That(Fails("'''abc").Problem, Does.Contain("unterminated triple"));
        }

        [Test]
        public void FStringKeepsRawBody()
        {
            var t = Lex("f'a{x}\\n'")[0];
            Assert.That(t.Kind, Is.EqualTo(TokenKind.FString));
            Assert.That(t.Text, Is.EqualTo("a{x}\\n"));
        }

        [Test]
        public void FStringPrefixIsCaseInsensitive()
        {
            Assert.That(Lex("F'x'")[0].Kind, Is.EqualTo(TokenKind.FString));
        }

        [Test]
        public void RawFStringsAreNotSupportedYet()
        {
            Assert.That(Fails("rf'x'").Problem, Does.Contain("raw f-strings"));
            Assert.That(Fails("fr'x'").Problem, Does.Contain("raw f-strings"));
        }

        [Test]
        public void BytesLiteralsAreNotSupported()
        {
            Assert.That(Fails("b'x'").Problem, Does.Contain("bytes"));
            Assert.That(Fails("rb'x'").Problem, Does.Contain("bytes"));
        }

        [Test]
        public void LongPrefixedNameBeforeQuoteIsAName()
        {
            var t = Lex("abc'x'");
            Assert.That(t[0].Kind, Is.EqualTo(TokenKind.Name));
            Assert.That(t[1].Kind, Is.EqualTo(TokenKind.String));
        }

        [Test]
        public void UnknownTwoLetterPrefixBeforeQuoteIsAName()
        {
            var t = Lex("xy'x'");
            Assert.That(t[0].Kind, Is.EqualTo(TokenKind.Name));
            Assert.That(t[1].Kind, Is.EqualTo(TokenKind.String));
        }

        // ---- Operators ----------------------------------------------------------------------------------------

        [Test]
        public void ArithmeticOperators()
        {
            Assert.That(Kinds("+ - * / // % **"),
                Is.EqualTo("Plus Minus Star Slash DoubleSlash Percent DoubleStar Newline EndOfFile"));
        }

        [Test]
        public void ComparisonOperators()
        {
            Assert.That(Kinds("== != < <= > >="), Is.EqualTo("EqEq NotEq Lt Le Gt Ge Newline EndOfFile"));
        }

        [Test]
        public void AssignmentOperators()
        {
            Assert.That(Kinds("= += -= *= /= //= %= **="),
                Is.EqualTo("Assign PlusAssign MinusAssign StarAssign SlashAssign DoubleSlashAssign PercentAssign DoubleStarAssign Newline EndOfFile"));
        }

        [Test]
        public void PunctuationOperators()
        {
            Assert.That(Kinds(", : . ;"), Is.EqualTo("Comma Colon Dot Semicolon Newline EndOfFile"));
        }

        [Test]
        public void BitOperatorsAreTokens()
        {
            Assert.That(Kinds("| & ^ ~ << >>"), Is.EqualTo("Pipe Ampersand Caret Tilde LShift RShift Newline EndOfFile"));
            Assert.That(Kinds("|= &= ^= <<= >>="), Is.EqualTo("PipeAssign AmpersandAssign CaretAssign LShiftAssign RShiftAssign Newline EndOfFile"));
            Assert.That(Kinds("a<<b>>c<d"), Is.EqualTo("Name LShift Name RShift Name Lt Name Newline EndOfFile"));
            Assert.That(Lex("<<=")[0].Text, Is.EqualTo("<<="));
        }

        [Test]
        public void OperatorTextIsPreserved()
        {
            Assert.That(Lex("//=")[0].Text, Is.EqualTo("//="));
        }

        [TestCase("@")]
        [TestCase("->")]
        [TestCase(":=")]
        public void UnsupportedOperatorsGiveHelpfulErrors(string op)
        {
            Assert.That(Fails("a " + op + " b").Problem, Does.Contain("not supported yet"));
        }

        [TestCase("$")]
        [TestCase("?")]
        [TestCase("!")]
        [TestCase("`")]
        public void UnexpectedCharactersAreErrors(string ch)
        {
            Assert.That(Fails("a " + ch).Problem, Does.Contain("unexpected character"));
        }

        // ---- Positions ----------------------------------------------------------------------------------------

        [Test]
        public void TokensCarryLineColumnAndOffsets()
        {
            var t = Lex("ab = 12\n  cd");
            var ab = t[0];
            Assert.That((ab.Line, ab.Column, ab.Start, ab.Length, ab.End), Is.EqualTo((1, 1, 0, 2, 2)));
            var num = t[2];
            Assert.That((num.Line, num.Column, num.Start), Is.EqualTo((1, 6, 5)));
            var cd = t.First(x => x.Text == "cd");
            Assert.That((cd.Line, cd.Column, cd.Start), Is.EqualTo((2, 3, 10)));
        }

        [Test]
        public void TokenToStringIsReadable()
        {
            Assert.That(Lex("x")[0].ToString(), Is.EqualTo("Name 'x' @1:1"));
            Assert.That(Lex("x")[1].ToString(), Does.StartWith("Newline @"));
        }

        [Test]
        public void SyntaxErrorMessageContainsPosition()
        {
            var ex = Fails("x = $");
            Assert.That(ex.Message, Does.Contain("line 1, column 5"));
            Assert.That(ex.Offset, Is.EqualTo(4));
            Assert.That(ex.Length, Is.EqualTo(1));
        }

        [Test]
        public void SyntaxErrorLengthIsAtLeastOne()
        {
            Assert.That(new PySyntaxError("m", 1, 1, 0, 0).Length, Is.EqualTo(1));
        }

        // ---- Tolerant mode ------------------------------------------------------------------------------------

        [Test]
        public void TolerantModeReportsMultipleProblemsAndKeepsGoing()
        {
            var tokens = Lexer.TokenizeTolerant("a = $\nb = 'oops\nc = 3\n", out var diags);
            Assert.That(diags.Count, Is.EqualTo(2));
            Assert.That(diags[0].Line, Is.EqualTo(1));
            Assert.That(diags[1].Line, Is.EqualTo(2));
            Assert.That(tokens.Count(t => t.Kind == TokenKind.Error), Is.EqualTo(2));
            Assert.That(tokens.Any(t => t.Kind == TokenKind.Name && t.Text == "c"), Is.True);
        }

        [Test]
        public void TolerantModeWithValidSourceHasNoDiagnostics()
        {
            Lexer.TokenizeTolerant("x = 1\n", out var diags);
            Assert.That(diags, Is.Empty);
        }

        [Test]
        public void TolerantModeRecoversFromBadDedent()
        {
            Lexer.TokenizeTolerant("if a:\n        b\n    c\n", out var diags);
            Assert.That(diags.Count, Is.EqualTo(1));
        }

        [Test]
        public void TolerantModeReportsUnclosedBrackets()
        {
            Lexer.TokenizeTolerant("x = (1,\n", out var diags);
            Assert.That(diags.Any(d => d.Message.Contains("never closed")), Is.True);
        }

        [Test]
        public void TolerantModeProducesErrorTokensForBadNumbersAndEscapes()
        {
            var tokens = Lexer.TokenizeTolerant("12abc '\\xZ1' rf'x'", out var diags);
            Assert.That(diags.Count, Is.EqualTo(3));
            Assert.That(tokens.Count(t => t.Kind == TokenKind.Error), Is.EqualTo(3));
        }

        [Test]
        public void DiagnosticConvertsToException()
        {
            Lexer.TokenizeTolerant("$", out var diags);
            var ex = diags[0].ToException();
            Assert.That(ex.Line, Is.EqualTo(1));
            Assert.That(ex.Problem, Is.EqualTo(diags[0].Message));
            Assert.That(diags[0].ToString(), Does.Contain("line 1"));
        }
    }
}
