using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CSharpPython.Syntax
{
    /// <summary>
    /// Converts source text into tokens. Python's significant whitespace is handled here: an indentation stack
    /// produces <see cref="TokenKind.Indent"/>/<see cref="TokenKind.Dedent"/>/<see cref="TokenKind.Newline"/> tokens
    /// and newlines inside brackets are ignored.
    /// </summary>
    /// <remarks>
    /// <see cref="Tokenize"/> throws a <see cref="PySyntaxError"/> on the first problem. Editor tooling should use
    /// <see cref="TokenizeTolerant"/>, which keeps going and reports every problem as a <see cref="Diagnostic"/>.
    /// </remarks>
    public sealed class Lexer
    {
        private const int TabSize = 8;

        private static readonly Dictionary<string, TokenKind> Keywords = new Dictionary<string, TokenKind>
        {
            { "def", TokenKind.KwDef }, { "if", TokenKind.KwIf }, { "elif", TokenKind.KwElif },
            { "else", TokenKind.KwElse }, { "while", TokenKind.KwWhile }, { "for", TokenKind.KwFor },
            { "in", TokenKind.KwIn }, { "return", TokenKind.KwReturn }, { "break", TokenKind.KwBreak },
            { "continue", TokenKind.KwContinue }, { "pass", TokenKind.KwPass }, { "and", TokenKind.KwAnd },
            { "or", TokenKind.KwOr }, { "not", TokenKind.KwNot }, { "is", TokenKind.KwIs },
            { "True", TokenKind.KwTrue }, { "False", TokenKind.KwFalse }, { "None", TokenKind.KwNone },
            { "global", TokenKind.KwGlobal }, { "nonlocal", TokenKind.KwNonlocal }, { "lambda", TokenKind.KwLambda },
            { "try", TokenKind.KwTry }, { "except", TokenKind.KwExcept }, { "finally", TokenKind.KwFinally },
            { "raise", TokenKind.KwRaise }, { "import", TokenKind.KwImport }, { "from", TokenKind.KwFrom },
            { "as", TokenKind.KwAs }, { "class", TokenKind.KwClass },
            { "yield", TokenKind.KwYield }, { "with", TokenKind.KwWith },
            { "del", TokenKind.KwDel }, { "assert", TokenKind.KwAssert }, { "async", TokenKind.KwUnsupported },
            { "await", TokenKind.KwUnsupported },
        };

        private readonly string _src;
        private readonly bool _tolerant;
        private readonly List<Token> _tokens = new List<Token>();
        private readonly List<Diagnostic> _diagnostics = new List<Diagnostic>();
        private readonly List<int> _indents = new List<int> { 0 };
        private readonly List<OpenBracket> _brackets = new List<OpenBracket>();

        private int _pos;
        private int _line = 1;
        private int _lineStart;
        private bool _atLineStart = true;

        private struct OpenBracket
        {
            public char Char;
            public int Offset, Line, Column;
        }

        private Lexer(string source, bool tolerant)
        {
            _src = source ?? throw new ArgumentNullException(nameof(source));
            _tolerant = tolerant;
        }

        /// <summary>Tokenizes <paramref name="source"/>, throwing <see cref="PySyntaxError"/> on the first problem.</summary>
        public static List<Token> Tokenize(string source)
        {
            var lexer = new Lexer(source, false);
            lexer.Run();
            return lexer._tokens;
        }

        /// <summary>
        /// Tokenizes <paramref name="source"/> without throwing. Malformed text becomes
        /// <see cref="TokenKind.Error"/> tokens and every problem is added to <paramref name="diagnostics"/>.
        /// </summary>
        public static List<Token> TokenizeTolerant(string source, out List<Diagnostic> diagnostics)
        {
            var lexer = new Lexer(source, true);
            lexer.Run();
            diagnostics = lexer._diagnostics;
            return lexer._tokens;
        }

        private int Column => _pos - _lineStart + 1;

        private char Peek(int ahead = 0)
        {
            int i = _pos + ahead;
            return i < _src.Length ? _src[i] : '\0';
        }

        private bool AtEnd => _pos >= _src.Length;

        private void Run()
        {
            while (true)
            {
                if (_atLineStart && _brackets.Count == 0)
                {
                    if (!HandleIndentation()) break;
                }
                if (AtEnd) break;
                ScanToken();
            }
            Finish();
        }

        // Returns false when the end of the source was reached.
        private bool HandleIndentation()
        {
            while (true)
            {
                int indent = 0;
                int p = _pos;
                while (p < _src.Length)
                {
                    char c = _src[p];
                    if (c == ' ') indent++;
                    else if (c == '\t') indent = (indent / TabSize + 1) * TabSize;
                    else if (c == '\f') indent = 0;
                    else break;
                    p++;
                }
                _pos = p;
                if (AtEnd) return false;

                char ch = _src[_pos];
                if (ch == '#')
                {
                    while (!AtEnd && !IsNewline(_src[_pos])) _pos++;
                    ch = AtEnd ? '\0' : _src[_pos];
                    if (AtEnd) return false;
                }
                if (IsNewline(ch))
                {
                    ConsumeNewline();
                    continue; // blank or comment-only line: indentation is irrelevant
                }

                int top = _indents[_indents.Count - 1];
                if (indent > top)
                {
                    _indents.Add(indent);
                    Add(TokenKind.Indent, null, _pos, 0);
                }
                else if (indent < top)
                {
                    while (indent < _indents[_indents.Count - 1])
                    {
                        _indents.RemoveAt(_indents.Count - 1);
                        Add(TokenKind.Dedent, null, _pos, 0);
                    }
                    if (indent != _indents[_indents.Count - 1])
                    {
                        Error("unindent does not match any outer indentation level", _pos, 1);
                        _indents.Add(indent);
                    }
                }
                _atLineStart = false;
                return true;
            }
        }

        private static bool IsNewline(char c) => c == '\n' || c == '\r';

        private void ConsumeNewline()
        {
            if (_src[_pos] == '\r' && Peek(1) == '\n') _pos++;
            _pos++;
            _line++;
            _lineStart = _pos;
        }

        private void Add(TokenKind kind, string text, int start, int length)
        {
            _tokens.Add(new Token(kind, text, start, length, _line, start - _lineStart + 1));
        }

        private void AddAt(TokenKind kind, string text, int start, int length, int line, int column)
        {
            _tokens.Add(new Token(kind, text, start, length, line, column));
        }

        private void Error(string message, int offset, int length)
        {
            int column = offset - _lineStart + 1;
            if (!_tolerant) throw new PySyntaxError(message, _line, column, offset, length);
            _diagnostics.Add(new Diagnostic(message, _line, column, offset, length));
        }

        private void ErrorAt(string message, int offset, int length, int line, int column)
        {
            if (!_tolerant) throw new PySyntaxError(message, line, column, offset, length);
            _diagnostics.Add(new Diagnostic(message, line, column, offset, length));
        }

        private void ErrorToken(string message, int start, int length)
        {
            Error(message, start, length);
            Add(TokenKind.Error, message, start, length);
        }

        private void Finish()
        {
            for (int i = _brackets.Count - 1; i >= 0; i--)
            {
                var b = _brackets[i];
                ErrorAt("'" + b.Char + "' was never closed", b.Offset, 1, b.Line, b.Column);
            }

            if (_tokens.Count > 0)
            {
                TokenKind last = _tokens[_tokens.Count - 1].Kind;
                if (last != TokenKind.Newline && last != TokenKind.Dedent && last != TokenKind.Indent)
                    Add(TokenKind.Newline, null, _pos, 0);
            }
            while (_indents.Count > 1)
            {
                _indents.RemoveAt(_indents.Count - 1);
                Add(TokenKind.Dedent, null, _pos, 0);
            }
            Add(TokenKind.EndOfFile, null, _pos, 0);
        }

        private void ScanToken()
        {
            char c = _src[_pos];

            if (c == ' ' || c == '\t' || c == '\f') { _pos++; return; }

            if (IsNewline(c))
            {
                int start = _pos;
                if (_brackets.Count > 0)
                {
                    ConsumeNewline();
                    return;
                }
                int line = _line, col = Column;
                ConsumeNewline();
                if (_tokens.Count > 0 && _tokens[_tokens.Count - 1].Kind != TokenKind.Newline)
                    AddAt(TokenKind.Newline, null, start, _pos - start, line, col);
                _atLineStart = true;
                return;
            }

            if (c == '#')
            {
                while (!AtEnd && !IsNewline(_src[_pos])) _pos++;
                return;
            }

            if (c == '\\')
            {
                if (IsNewline(Peek(1)))
                {
                    _pos++;
                    ConsumeNewline();
                    return;
                }
                ErrorToken("unexpected character after line continuation character", _pos, 1);
                _pos++;
                return;
            }

            if (IsIdentStart(c)) { ScanNameOrString(); return; }
            if (c >= '0' && c <= '9' || c == '.' && Peek(1) >= '0' && Peek(1) <= '9') { ScanNumber(); return; }
            if (c == '"' || c == '\'') { ScanString(_pos, false, false); return; }

            ScanOperator(c);
        }

        private static bool IsIdentStart(char c) => c == '_' || char.IsLetter(c);

        private static bool IsIdentPart(char c) => c == '_' || char.IsLetterOrDigit(c);

        private void ScanNameOrString()
        {
            int start = _pos;
            while (!AtEnd && IsIdentPart(_src[_pos])) _pos++;
            string word = _src.Substring(start, _pos - start);

            if (!AtEnd && (_src[_pos] == '"' || _src[_pos] == '\'') && word.Length <= 2)
            {
                string prefix = word.ToLowerInvariant();
                switch (prefix)
                {
                    case "r": ScanString(start, true, false); return;
                    case "f": ScanString(start, false, true); return;
                    case "u": ScanString(start, false, false); return;
                    case "rf":
                    case "fr":
                        ScanString(start, false, false, "raw f-strings are not supported yet");
                        return;
                    case "b":
                    case "br":
                    case "rb":
                        ScanString(start, false, false, "bytes literals are not supported");
                        return;
                }
            }

            if (Keywords.TryGetValue(word, out TokenKind kw)) Add(kw, word, start, word.Length);
            else Add(TokenKind.Name, word, start, word.Length);
        }

        private void ScanNumber()
        {
            int start = _pos;
            int col = Column;
            char c = _src[_pos];

            if (c == '0' && (Peek(1) == 'x' || Peek(1) == 'X' || Peek(1) == 'b' || Peek(1) == 'B' || Peek(1) == 'o' || Peek(1) == 'O'))
            {
                int radix = char.ToLowerInvariant(Peek(1)) == 'x' ? 16 : char.ToLowerInvariant(Peek(1)) == 'b' ? 2 : 8;
                _pos += 2;
                long value = 0;
                int digits = 0;
                bool overflow = false;
                while (!AtEnd)
                {
                    char d = _src[_pos];
                    if (d == '_') { _pos++; continue; }
                    int dv = DigitValue(d);
                    if (dv < 0 || dv >= radix) break;
                    try { value = checked(value * radix + dv); }
                    catch (OverflowException) { overflow = true; }
                    digits++;
                    _pos++;
                }
                if (digits == 0) { ErrorToken("invalid number literal", start, _pos - start); return; }
                if (!AtEnd && IsIdentPart(_src[_pos])) { SkipIdent(); ErrorToken("invalid number literal", start, _pos - start); return; }
                if (overflow) { ErrorToken("integer literal is too large (big integers are not supported yet)", start, _pos - start); return; }
                AddAt(TokenKind.Int, value.ToString(CultureInfo.InvariantCulture), start, _pos - start, _line, col);
                return;
            }

            var sb = new StringBuilder();
            bool isFloat = false;
            ScanDigits(sb);
            if (!AtEnd && _src[_pos] == '.')
            {
                isFloat = true;
                sb.Append('.');
                _pos++;
                ScanDigits(sb);
            }
            if (!AtEnd && (_src[_pos] == 'e' || _src[_pos] == 'E'))
            {
                int save = _pos;
                var exp = new StringBuilder("e");
                _pos++;
                if (!AtEnd && (_src[_pos] == '+' || _src[_pos] == '-')) { exp.Append(_src[_pos]); _pos++; }
                if (!AtEnd && _src[_pos] >= '0' && _src[_pos] <= '9')
                {
                    isFloat = true;
                    ScanDigits(exp);
                    sb.Append(exp);
                }
                else
                {
                    _pos = save;
                }
            }
            if (!AtEnd && IsIdentPart(_src[_pos]))
            {
                SkipIdent();
                ErrorToken("invalid number literal", start, _pos - start);
                return;
            }

            string text = sb.ToString();
            if (isFloat)
            {
                if (text.StartsWith(".", StringComparison.Ordinal)) text = "0" + text;
                AddAt(TokenKind.Float, text, start, _pos - start, _line, col);
            }
            else
            {
                if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long _))
                {
                    ErrorToken("integer literal is too large (big integers are not supported yet)", start, _pos - start);
                    return;
                }
                AddAt(TokenKind.Int, text, start, _pos - start, _line, col);
            }
        }

        private void SkipIdent()
        {
            while (!AtEnd && IsIdentPart(_src[_pos])) _pos++;
        }

        private void ScanDigits(StringBuilder sb)
        {
            while (!AtEnd)
            {
                char d = _src[_pos];
                if (d >= '0' && d <= '9') sb.Append(d);
                else if (d != '_') break;
                _pos++;
            }
        }

        private static int DigitValue(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        // 'start' is the offset of the prefix (if any); _pos is at the opening quote.
        private void ScanString(int start, bool raw, bool fstring, string unsupported = null)
        {
            int line = _line;
            int col = start - _lineStart + 1;
            char quote = _src[_pos];
            bool triple = Peek(1) == quote && Peek(2) == quote;
            _pos += triple ? 3 : 1;

            var body = new StringBuilder();
            bool terminated = false;
            while (!AtEnd)
            {
                char c = _src[_pos];
                if (c == '\\' && _pos + 1 < _src.Length)
                {
                    char next = _src[_pos + 1];
                    body.Append(c).Append(next);
                    _pos += 2;
                    if (IsNewline(next))
                    {
                        if (next == '\r' && Peek() == '\n') { body.Append('\n'); _pos++; }
                        _line++;
                        _lineStart = _pos;
                    }
                    continue;
                }
                if (c == quote)
                {
                    if (!triple) { _pos++; terminated = true; break; }
                    if (Peek(1) == quote && Peek(2) == quote) { _pos += 3; terminated = true; break; }
                }
                else if (IsNewline(c) && !triple)
                {
                    break;
                }

                if (IsNewline(c))
                {
                    // Universal newlines: \r\n and \r inside a triple-quoted string become \n.
                    if (c == '\r' && Peek(1) == '\n') _pos++;
                    body.Append('\n');
                    _pos++;
                    _line++;
                    _lineStart = _pos;
                    continue;
                }
                body.Append(c);
                _pos++;
            }

            int length = _pos - start;
            if (!terminated)
            {
                ErrorAt(triple ? "unterminated triple-quoted string literal" : "unterminated string literal", start, length, line, col);
                AddAt(TokenKind.Error, "unterminated string literal", start, length, line, col);
                return;
            }
            if (unsupported != null)
            {
                ErrorAt(unsupported, start, length, line, col);
                AddAt(TokenKind.Error, unsupported, start, length, line, col);
                return;
            }

            string raw_body = body.ToString();
            if (fstring)
            {
                AddAt(TokenKind.FString, raw_body, start, length, line, col);
                return;
            }
            if (raw)
            {
                AddAt(TokenKind.String, raw_body, start, length, line, col);
                return;
            }
            if (!TryDecodeEscapes(raw_body, out string decoded, out string error))
            {
                ErrorAt(error, start, length, line, col);
                AddAt(TokenKind.Error, error, start, length, line, col);
                return;
            }
            AddAt(TokenKind.String, decoded, start, length, line, col);
        }

        /// <summary>Decodes the backslash escapes of a non-raw string body.</summary>
        internal static bool TryDecodeEscapes(string body, out string result, out string error)
        {
            error = null;
            if (body.IndexOf('\\') < 0) { result = body; return true; }

            var sb = new StringBuilder(body.Length);
            int i = 0;
            while (i < body.Length)
            {
                char c = body[i];
                if (c != '\\' || i + 1 >= body.Length) { sb.Append(c); i++; continue; }
                char n = body[i + 1];
                i += 2;
                switch (n)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case '\\': sb.Append('\\'); break;
                    case '\'': sb.Append('\''); break;
                    case '"': sb.Append('"'); break;
                    case 'a': sb.Append('\a'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'v': sb.Append('\v'); break;
                    case '\n': break;
                    case '\r': if (i < body.Length && body[i] == '\n') i++; break;
                    case 'x':
                    case 'u':
                    case 'U':
                    {
                        int count = n == 'x' ? 2 : n == 'u' ? 4 : 8;
                        if (i + count > body.Length) { result = null; error = "truncated \\" + n + " escape"; return false; }
                        long code = 0;
                        for (int k = 0; k < count; k++)
                        {
                            int dv = DigitValue(body[i + k]);
                            if (dv < 0) { result = null; error = "invalid \\" + n + " escape"; return false; }
                            code = code * 16 + dv;
                        }
                        i += count;
                        if (code > 0x10FFFF) { result = null; error = "invalid \\" + n + " escape"; return false; }
                        sb.Append(char.ConvertFromUtf32((int)code));
                        break;
                    }
                    default:
                        if (n >= '0' && n <= '7')
                        {
                            int code = n - '0';
                            int digits = 1;
                            while (digits < 3 && i < body.Length && body[i] >= '0' && body[i] <= '7')
                            {
                                code = code * 8 + (body[i] - '0');
                                i++;
                                digits++;
                            }
                            sb.Append((char)code);
                        }
                        else
                        {
                            sb.Append('\\').Append(n);
                        }
                        break;
                }
            }
            result = sb.ToString();
            return true;
        }

        private void ScanOperator(char c)
        {
            int start = _pos;
            char n = Peek(1);
            char n2 = Peek(2);
            TokenKind kind;
            int len = 1;

            switch (c)
            {
                case '+': if (n == '=') { kind = TokenKind.PlusAssign; len = 2; } else kind = TokenKind.Plus; break;
                case '-':
                    if (n == '=') { kind = TokenKind.MinusAssign; len = 2; }
                    else if (n == '>') { kind = TokenKind.Arrow; len = 2; }
                    else kind = TokenKind.Minus;
                    break;
                case '*':
                    if (n == '*') { if (n2 == '=') { kind = TokenKind.DoubleStarAssign; len = 3; } else { kind = TokenKind.DoubleStar; len = 2; } }
                    else if (n == '=') { kind = TokenKind.StarAssign; len = 2; }
                    else kind = TokenKind.Star;
                    break;
                case '/':
                    if (n == '/') { if (n2 == '=') { kind = TokenKind.DoubleSlashAssign; len = 3; } else { kind = TokenKind.DoubleSlash; len = 2; } }
                    else if (n == '=') { kind = TokenKind.SlashAssign; len = 2; }
                    else kind = TokenKind.Slash;
                    break;
                case '%': if (n == '=') { kind = TokenKind.PercentAssign; len = 2; } else kind = TokenKind.Percent; break;
                case '=': if (n == '=') { kind = TokenKind.EqEq; len = 2; } else kind = TokenKind.Assign; break;
                case '!':
                    if (n == '=') { kind = TokenKind.NotEq; len = 2; break; }
                    ErrorToken("unexpected character '!'", start, 1);
                    _pos++;
                    return;
                case '<':
                    if (n == '<') { if (n2 == '=') { kind = TokenKind.LShiftAssign; len = 3; } else { kind = TokenKind.LShift; len = 2; } break; }
                    if (n == '=') { kind = TokenKind.Le; len = 2; } else kind = TokenKind.Lt;
                    break;
                case '>':
                    if (n == '>') { if (n2 == '=') { kind = TokenKind.RShiftAssign; len = 3; } else { kind = TokenKind.RShift; len = 2; } break; }
                    if (n == '=') { kind = TokenKind.Ge; len = 2; } else kind = TokenKind.Gt;
                    break;
                case '(': kind = TokenKind.LParen; OpenBracketAt(c); break;
                case '[': kind = TokenKind.LBracket; OpenBracketAt(c); break;
                case '{': kind = TokenKind.LBrace; OpenBracketAt(c); break;
                case ')': kind = TokenKind.RParen; if (!CloseBracket(c)) { _pos++; return; } break;
                case ']': kind = TokenKind.RBracket; if (!CloseBracket(c)) { _pos++; return; } break;
                case '}': kind = TokenKind.RBrace; if (!CloseBracket(c)) { _pos++; return; } break;
                case ',': kind = TokenKind.Comma; break;
                case ':':
                    if (n == '=') { kind = TokenKind.Walrus; len = 2; break; }
                    kind = TokenKind.Colon;
                    break;
                case '.':
                    if (n == '.' && n2 == '.') { kind = TokenKind.Ellipsis; len = 3; break; }
                    kind = TokenKind.Dot;
                    break;
                case ';': kind = TokenKind.Semicolon; break;
                case '|': if (n == '=') { kind = TokenKind.PipeAssign; len = 2; } else kind = TokenKind.Pipe; break;
                case '&': if (n == '=') { kind = TokenKind.AmpersandAssign; len = 2; } else kind = TokenKind.Ampersand; break;
                case '^': if (n == '=') { kind = TokenKind.CaretAssign; len = 2; } else kind = TokenKind.Caret; break;
                case '~': kind = TokenKind.Tilde; break;
                case '@':
                    if (!StartsLine())
                    {
                        Unsupported("'" + c + "'", 1);
                        return;
                    }
                    kind = TokenKind.At;
                    break;
                default:
                    ErrorToken("unexpected character '" + c + "'", start, 1);
                    _pos++;
                    return;
            }

            _pos += len;
            Add(kind, _src.Substring(start, len), start, len);
        }

        // True when nothing but indentation came before on this logical line, which is where a decorator's '@' goes.
        private bool StartsLine()
        {
            if (_brackets.Count > 0) return false;
            if (_tokens.Count == 0) return true;
            TokenKind last = _tokens[_tokens.Count - 1].Kind;
            return last == TokenKind.Newline || last == TokenKind.Indent || last == TokenKind.Dedent;
        }

        private void Unsupported(string what, int length)
        {
            ErrorToken(what + " is not supported yet", _pos, length);
            _pos += length;
        }

        private void OpenBracketAt(char c)
        {
            _brackets.Add(new OpenBracket { Char = c, Offset = _pos, Line = _line, Column = Column });
        }

        private bool CloseBracket(char c)
        {
            if (_brackets.Count == 0)
            {
                ErrorToken("unmatched '" + c + "'", _pos, 1);
                return false;
            }
            var open = _brackets[_brackets.Count - 1];
            char expected = open.Char == '(' ? ')' : open.Char == '[' ? ']' : '}';
            if (c != expected)
            {
                ErrorToken("closing '" + c + "' does not match opening '" + open.Char + "' on line " + open.Line, _pos, 1);
                _brackets.RemoveAt(_brackets.Count - 1);
                return false;
            }
            _brackets.RemoveAt(_brackets.Count - 1);
            return true;
        }
    }
}
