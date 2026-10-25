using System;
using System.Collections.Generic;
using CSharpPython.Syntax;

namespace CSharpPython.Editor
{
    /// <summary>What a piece of source text is, for colouring it in an editor.</summary>
    public enum HighlightKind
    {
        Keyword,
        /// <summary>True, False and None.</summary>
        Constant,
        /// <summary>A Python keyword this library does not support yet (yield, with, ...).</summary>
        UnsupportedKeyword,
        Number,
        String,
        Comment,
        Operator,
        Punctuation,
        Identifier,
        /// <summary>The name being defined by a <c>def</c>.</summary>
        FunctionName,
        /// <summary>A name that is a built-in of the VM, such as <c>print</c> or <c>len</c>.</summary>
        Builtin,
        /// <summary>A name after <c>import</c> or <c>from</c>.</summary>
        Module,
        /// <summary>A name after a dot, such as <c>append</c> in <c>xs.append</c>.</summary>
        Member,
        /// <summary>Malformed text, such as an unterminated string or a stray character.</summary>
        Error,
        /// <summary>The name being defined by a <c>class</c>.</summary>
        ClassName,
    }

    /// <summary>A coloured stretch of source text.</summary>
    public readonly struct HighlightSpan
    {
        public readonly int Start;
        public readonly int Length;
        public readonly HighlightKind Kind;

        public HighlightSpan(int start, int length, HighlightKind kind)
        {
            Start = start;
            Length = length;
            Kind = kind;
        }

        public int End => Start + Length;

        public override string ToString() => Kind + " [" + Start + ", " + End + ")";
    }

    /// <summary>
    /// Splits source text into coloured spans. It uses the tolerant lexer, so it works on half-typed code and
    /// never throws. Spans are sorted, do not overlap and always lie inside the text; whitespace is not covered.
    /// </summary>
    public static class SyntaxHighlighter
    {
        /// <summary>
        /// Highlights <paramref name="source"/>. The built-in names of <paramref name="vm"/> (a default VM when
        /// null) are marked as <see cref="HighlightKind.Builtin"/> unless the script defines a function of that name.
        /// </summary>
        public static List<HighlightSpan> Highlight(string source, PyVm vm = null)
        {
            source = source ?? "";
            vm = vm ?? EditorDefaults.Vm;
            var tokens = Lexer.TokenizeTolerant(source, out _);
            var spans = new List<HighlightSpan>();
            HashSet<string> defined = null;
            int cursor = 0; // end of the last thing emitted; gaps after it may hold comments
            TokenKind prev = TokenKind.Newline;
            bool inImport = false;

            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t.Length <= 0 || t.Kind == TokenKind.Newline || t.Kind == TokenKind.EndOfFile)
                {
                    if (t.Kind == TokenKind.Newline) inImport = false;
                    if (t.Kind == TokenKind.Newline && t.Length > 0)
                    {
                        cursor = Math.Max(AddComments(source, cursor, t.Start, spans), t.End);
                    }
                    if (t.Kind != TokenKind.Newline && t.Kind != TokenKind.EndOfFile) continue;
                    if (t.Kind == TokenKind.EndOfFile) cursor = AddComments(source, cursor, source.Length, spans);
                    prev = t.Kind;
                    continue;
                }

                int start = Math.Max(t.Start, cursor);
                int end = Math.Min(t.End, source.Length);
                if (end <= start) continue;
                AddComments(source, cursor, start, spans);

                HighlightKind kind;
                switch (t.Kind)
                {
                    case TokenKind.Name:
                        if (prev == TokenKind.Dot) kind = HighlightKind.Member;
                        else if (prev == TokenKind.KwDef) kind = HighlightKind.FunctionName;
                        else if (prev == TokenKind.KwClass) kind = HighlightKind.ClassName;
                        else if (inImport && prev != TokenKind.KwAs) kind = HighlightKind.Module;
                        else if (vm.Builtins.ContainsKey(t.Text) && !IsDefined(tokens, ref defined, t.Text))
                            kind = HighlightKind.Builtin;
                        else kind = HighlightKind.Identifier;
                        break;
                    case TokenKind.Int:
                    case TokenKind.Float: kind = HighlightKind.Number; break;
                    case TokenKind.String:
                    case TokenKind.FString: kind = HighlightKind.String; break;
                    case TokenKind.Error: kind = HighlightKind.Error; break;
                    case TokenKind.KwTrue:
                    case TokenKind.KwFalse:
                    case TokenKind.KwNone: kind = HighlightKind.Constant; break;
                    case TokenKind.KwUnsupported: kind = HighlightKind.UnsupportedKeyword; break;
                    case TokenKind.Indent:
                    case TokenKind.Dedent: continue;
                    default:
                        kind = t.Kind >= TokenKind.KwDef && t.Kind <= TokenKind.KwAs ? HighlightKind.Keyword
                            : IsPunctuation(t.Kind) ? HighlightKind.Punctuation : HighlightKind.Operator;
                        break;
                }
                if (t.Kind == TokenKind.KwImport || t.Kind == TokenKind.KwFrom) inImport = true;
                if (t.Kind == TokenKind.KwImport && IsFromStatement(tokens, i)) inImport = false;
                spans.Add(new HighlightSpan(start, end - start, kind));
                cursor = end;
                prev = t.Kind;
            }
            AddComments(source, cursor, source.Length, spans);
            return spans;
        }

        private static bool IsFromStatement(List<Token> tokens, int importIndex)
        {
            for (int i = importIndex - 1; i >= 0; i--)
            {
                var k = tokens[i].Kind;
                if (k == TokenKind.KwFrom) return true;
                if (k == TokenKind.Newline || k == TokenKind.Indent || k == TokenKind.Dedent) return false;
            }
            return false;
        }

        private static bool IsDefined(List<Token> tokens, ref HashSet<string> defined, string name)
        {
            if (defined == null)
            {
                defined = new HashSet<string>();
                for (int i = 0; i + 1 < tokens.Count; i++)
                    if ((tokens[i].Kind == TokenKind.KwDef || tokens[i].Kind == TokenKind.KwClass) && tokens[i + 1].Kind == TokenKind.Name)
                        defined.Add(tokens[i + 1].Text);
            }
            return defined.Contains(name);
        }

        private static bool IsPunctuation(TokenKind k) =>
            k == TokenKind.LParen || k == TokenKind.RParen || k == TokenKind.LBracket || k == TokenKind.RBracket ||
            k == TokenKind.LBrace || k == TokenKind.RBrace || k == TokenKind.Comma || k == TokenKind.Colon ||
            k == TokenKind.Dot || k == TokenKind.Semicolon;

        // The lexer drops comments, so find them in the text between tokens.
        // Returns where scanning stopped, so a gap is never searched twice.
        private static int AddComments(string source, int from, int to, List<HighlightSpan> spans)
        {
            to = Math.Min(to, source.Length);
            int cursor = Math.Max(from, 0);
            for (int i = cursor; i < to; i++)
            {
                if (source[i] != '#') continue;
                int end = i;
                while (end < to && source[end] != '\n' && source[end] != '\r') end++;
                spans.Add(new HighlightSpan(i, end - i, HighlightKind.Comment));
                i = end;
                cursor = end;
            }
            return Math.Max(cursor, from);
        }
    }
}
