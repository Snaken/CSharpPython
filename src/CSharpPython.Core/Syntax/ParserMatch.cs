using System.Collections.Generic;

namespace CSharpPython.Syntax
{
    // The 'match' statement. Like 'with', it is rewritten into ordinary statements while parsing, so the rest of the
    // compiler and the VM do not know about it:
    //
    //   match subject:               .m1 = subject
    //       case [x, 0] if x > 1:    if .matchseq(.m1) and len(.m1) == 2 and ((x := .m1[0]) is x) and .m1[1] == 0 and x > 1:
    //           body                     body
    //       case _:                  else:
    //           other                    other
    //
    // A capture is an assignment expression that is always true, '.m1' and '.t1_2' are names no script can write, and
    // the helpers '.matchseq', '.matchmap', '.maprest', '.matchclass', '.len' and '.list' are built in.
    public sealed partial class Parser
    {
        private int _matchCount;

        private abstract class Pattern
        {
            public Token Tok;
        }

        private sealed class LiteralPattern : Pattern
        {
            public Expr Value;
            public bool Identity; // None, True and False are compared with 'is'
        }

        private sealed class CapturePattern : Pattern
        {
            public string Name; // null for the wildcard '_'
        }

        private sealed class ValuePattern : Pattern
        {
            public Expr Value;
        }

        private sealed class StarPattern : Pattern
        {
            public string Name; // null for '*_'
        }

        private sealed class SequencePattern : Pattern
        {
            public List<Pattern> Items = new List<Pattern>();
            public int Star = -1;
        }

        private sealed class MappingPattern : Pattern
        {
            public List<Expr> Keys = new List<Expr>();
            public List<Pattern> Values = new List<Pattern>();
            public string Rest;
        }

        private sealed class ClassPattern : Pattern
        {
            public Expr Class;
            public List<Pattern> Positional = new List<Pattern>();
            public List<string> KeywordNames = new List<string>();
            public List<Pattern> KeywordPatterns = new List<Pattern>();
        }

        private sealed class OrPattern : Pattern
        {
            public List<Pattern> Alternatives = new List<Pattern>();
        }

        private sealed class AsPattern : Pattern
        {
            public Pattern Inner;
            public string Name;
        }

        // 'match' is only a keyword at the start of a statement that ends in ':' and is followed by an indented 'case'.
        private bool LooksLikeMatch()
        {
            if (Cur.Kind != TokenKind.Name || Cur.Text != "match") return false;
            int end = 1;
            while (PeekAt(end).Kind != TokenKind.Newline && PeekAt(end).Kind != TokenKind.EndOfFile) end++;
            if (end < 2 || PeekAt(end - 1).Kind != TokenKind.Colon) return false;
            return PeekAt(end + 1).Kind == TokenKind.Indent && PeekAt(end + 2).Kind == TokenKind.Name && PeekAt(end + 2).Text == "case";
        }

        private void ParseMatch(List<Stmt> into)
        {
            var matchTok = Advance();
            int id = ++_matchCount;
            var subject = ParseExprList();
            Expect(TokenKind.Colon, "':'");
            Expect(TokenKind.Newline, "a newline");
            SkipNewlines();
            Expect(TokenKind.Indent, "an indented block");

            Enter();
            var cases = new List<(Token tok, Pattern pattern, Expr guard, List<Stmt> body)>();
            while (Cur.Kind == TokenKind.Name && Cur.Text == "case")
            {
                var caseTok = Advance();
                var pattern = ParseTopPattern();
                Expr guard = null;
                if (Accept(TokenKind.KwIf)) guard = ParseNamed();
                Expect(TokenKind.Colon, "':'");
                var body = ParseBlock("'case' block");
                cases.Add((caseTok, pattern, guard, body));
                SkipNewlines();
            }
            Leave();
            if (cases.Count == 0) throw Error("expected a 'case' block", Cur);
            if (Cur.Kind != TokenKind.Dedent && Cur.Kind != TokenKind.EndOfFile) throw Error("expected a 'case' block, found " + Describe(Cur), Cur);
            Accept(TokenKind.Dedent);

            string name = ".m" + id;
            into.Add(At(new AssignStmt { Value = subject, Targets = { At(new NameExpr { Name = name }, matchTok) } }, matchTok));

            // Built from the last case back, so that each 'if' has the rest as its 'else'.
            var rest = new List<Stmt>();
            var lowering = new MatchLowering(this, id);
            for (int i = cases.Count - 1; i >= 0; i--)
            {
                var c = cases[i];
                bool irrefutable = c.pattern is CapturePattern wild && wild.Name == null && c.guard == null;
                if (irrefutable)
                {
                    if (i != cases.Count - 1) throw Error("wildcard makes remaining patterns unreachable", c.tok);
                    rest = c.body;
                    continue;
                }
                var test = lowering.Complete(lowering.Lower(c.pattern, () => lowering.Name(name, c.tok), c.tok), c.tok);
                if (c.guard != null) test = lowering.And(test, c.guard, c.tok);
                var node = At(new IfStmt { Test = test, Body = c.body, Else = rest }, c.tok);
                rest = new List<Stmt> { node };
            }
            into.AddRange(rest);
        }

        // ---- Parsing patterns -----------------------------------------------------------------------------------

        // A pattern after 'case': several separated by commas make a sequence pattern without brackets.
        private Pattern ParseTopPattern()
        {
            var tok = Cur;
            var first = ParseSequenceItem();
            if (Cur.Kind != TokenKind.Comma && !(first is StarPattern)) return first;
            var items = new List<Pattern> { first };
            while (Accept(TokenKind.Comma))
            {
                if (Cur.Kind == TokenKind.Colon || Cur.Kind == TokenKind.KwIf) break;
                items.Add(ParseSequenceItem());
            }
            return MakeSequence(items, tok);
        }

        private Pattern ParseSequenceItem()
        {
            if (Cur.Kind != TokenKind.Star) return ParseAsPattern();
            var star = Advance();
            string name = Expect(TokenKind.Name, "a name after '*'").Text;
            return new StarPattern { Tok = star, Name = name == "_" ? null : name };
        }

        private Pattern MakeSequence(List<Pattern> items, Token tok)
        {
            var sequence = new SequencePattern { Tok = tok, Items = items };
            for (int i = 0; i < items.Count; i++)
            {
                if (!(items[i] is StarPattern)) continue;
                if (sequence.Star >= 0) throw Error("multiple starred names in sequence pattern", items[i].Tok);
                sequence.Star = i;
            }
            return sequence;
        }

        private Pattern ParseAsPattern()
        {
            var pattern = ParseOrPattern();
            while (Cur.Kind == TokenKind.KwAs)
            {
                var asTok = Advance();
                var nameTok = Expect(TokenKind.Name, "a name after 'as'");
                if (nameTok.Text == "_") throw Error("cannot use '_' as a target", nameTok);
                pattern = new AsPattern { Tok = asTok, Inner = pattern, Name = nameTok.Text };
            }
            return pattern;
        }

        private Pattern ParseOrPattern()
        {
            var tok = Cur;
            var first = ParseClosedPattern();
            if (Cur.Kind != TokenKind.Pipe) return first;
            var or = new OrPattern { Tok = tok };
            or.Alternatives.Add(first);
            while (Accept(TokenKind.Pipe)) or.Alternatives.Add(ParseClosedPattern());
            return or;
        }

        private Pattern ParseClosedPattern()
        {
            var tok = Cur;
            switch (tok.Kind)
            {
                case TokenKind.Int:
                case TokenKind.Float:
                case TokenKind.Minus:
                case TokenKind.String:
                case TokenKind.FString:
                    return new LiteralPattern { Tok = tok, Value = ParseLiteralValue() };
                case TokenKind.KwNone:
                case TokenKind.KwTrue:
                case TokenKind.KwFalse:
                    return new LiteralPattern { Tok = tok, Value = ParseLiteralValue(), Identity = true };
                case TokenKind.Name:
                    return ParseNamePattern();
                case TokenKind.LParen:
                {
                    Advance();
                    if (Accept(TokenKind.RParen)) return new SequencePattern { Tok = tok };
                    var first = ParseSequenceItem();
                    if (Cur.Kind != TokenKind.Comma && !(first is StarPattern))
                    {
                        Expect(TokenKind.RParen, "')'");
                        return first;
                    }
                    return ParseSequenceRest(first, tok, TokenKind.RParen, "')'");
                }
                case TokenKind.LBracket:
                {
                    Advance();
                    if (Accept(TokenKind.RBracket)) return new SequencePattern { Tok = tok };
                    return ParseSequenceRest(ParseSequenceItem(), tok, TokenKind.RBracket, "']'");
                }
                case TokenKind.LBrace:
                    return ParseMappingPattern();
                default:
                    throw Error("invalid pattern: unexpected " + Describe(tok), tok);
            }
        }

        // The rest of a bracketed sequence pattern after its first item.
        private Pattern ParseSequenceRest(Pattern first, Token tok, TokenKind close, string closeText)
        {
            var items = new List<Pattern> { first };
            while (Accept(TokenKind.Comma))
            {
                if (Cur.Kind == close) break;
                items.Add(ParseSequenceItem());
            }
            Expect(close, closeText);
            return MakeSequence(items, tok);
        }

        // A capture, '_', a value such as Color.RED, or a class pattern such as Point(x=1).
        private Pattern ParseNamePattern()
        {
            var tok = Advance();
            Expr expr = At(new NameExpr { Name = tok.Text }, tok);
            bool dotted = false;
            while (Cur.Kind == TokenKind.Dot)
            {
                Advance();
                var attr = Expect(TokenKind.Name, "an attribute name");
                expr = At(new AttributeExpr { Target = expr, Name = attr.Text }, attr);
                dotted = true;
            }
            if (Cur.Kind == TokenKind.LParen) return ParseClassPattern(expr, tok);
            if (dotted) return new ValuePattern { Tok = tok, Value = expr };
            return new CapturePattern { Tok = tok, Name = tok.Text == "_" ? null : tok.Text };
        }

        private Pattern ParseClassPattern(Expr cls, Token tok)
        {
            Expect(TokenKind.LParen, "'('");
            var pattern = new ClassPattern { Tok = tok, Class = cls };
            while (Cur.Kind != TokenKind.RParen)
            {
                if (Cur.Kind == TokenKind.Name && PeekAt(1).Kind == TokenKind.Assign)
                {
                    var nameTok = Advance();
                    Advance();
                    if (pattern.KeywordNames.Contains(nameTok.Text)) throw Error("attribute name repeated in class pattern: " + nameTok.Text, nameTok);
                    pattern.KeywordNames.Add(nameTok.Text);
                    pattern.KeywordPatterns.Add(ParseAsPattern());
                }
                else
                {
                    if (pattern.KeywordNames.Count > 0) throw Error("positional patterns follow keyword patterns", Cur);
                    pattern.Positional.Add(ParseAsPattern());
                }
                if (!Accept(TokenKind.Comma)) break;
            }
            Expect(TokenKind.RParen, "')'");
            return pattern;
        }

        private Pattern ParseMappingPattern()
        {
            var pattern = new MappingPattern { Tok = Advance() };
            while (Cur.Kind != TokenKind.RBrace)
            {
                if (Cur.Kind == TokenKind.DoubleStar)
                {
                    Advance();
                    pattern.Rest = Expect(TokenKind.Name, "a name after '**'").Text;
                    Accept(TokenKind.Comma);
                    break;
                }
                pattern.Keys.Add(ParseMappingKey());
                Expect(TokenKind.Colon, "':'");
                pattern.Values.Add(ParseAsPattern());
                if (!Accept(TokenKind.Comma)) break;
            }
            Expect(TokenKind.RBrace, "'}'");
            return pattern;
        }

        // A key of a mapping pattern: a literal, or a dotted name.
        private Expr ParseMappingKey()
        {
            if (Cur.Kind != TokenKind.Name) return ParseLiteralValue();
            var tok = Advance();
            Expr expr = At(new NameExpr { Name = tok.Text }, tok);
            if (Cur.Kind != TokenKind.Dot) throw Error("mapping pattern keys must be literals or dotted names", tok);
            while (Accept(TokenKind.Dot))
            {
                var attr = Expect(TokenKind.Name, "an attribute name");
                expr = At(new AttributeExpr { Target = expr, Name = attr.Text }, attr);
            }
            return expr;
        }

        // A number (with an optional '-'), a string, None, True or False.
        private Expr ParseLiteralValue()
        {
            var tok = Cur;
            switch (tok.Kind)
            {
                case TokenKind.Minus:
                {
                    Advance();
                    if (Cur.Kind != TokenKind.Int && Cur.Kind != TokenKind.Float) throw Error("invalid pattern: expected a number after '-'", Cur);
                    var number = At((ConstantExpr)ParsePrimary(), tok);
                    switch (number.Value)
                    {
                        case long l: number.Value = -(System.Numerics.BigInteger)l; break;
                        case System.Numerics.BigInteger b: number.Value = -b; break;
                        default: number.Value = -(double)number.Value; break;
                    }
                    if (number.Value is System.Numerics.BigInteger big && big >= long.MinValue && big <= long.MaxValue) number.Value = (long)big;
                    return number;
                }
                case TokenKind.Int:
                case TokenKind.Float:
                case TokenKind.KwNone:
                case TokenKind.KwTrue:
                case TokenKind.KwFalse:
                    return ParsePrimary();
                case TokenKind.String:
                case TokenKind.FString:
                {
                    var text = ParseStringLiterals();
                    if (text is FStringExpr) throw Error("patterns may only match literals and attribute lookups", tok);
                    return text;
                }
                default:
                    throw Error("invalid pattern: unexpected " + Describe(tok), tok);
            }
        }

        /// <summary>Turns patterns into the tests that stand for them (see the comment at the top of the file).</summary>
        private sealed class MatchLowering
        {
            private readonly Parser _parser;
            private readonly int _id;
            private int _temps;

            public MatchLowering(Parser parser, int id)
            {
                _parser = parser;
                _id = id;
            }

            public Expr Name(string name, Token tok) => _parser.At(new NameExpr { Name = name }, tok);

            private Expr Const(object value, Token tok) => _parser.At(new ConstantExpr { Value = value }, tok);

            private Expr Call(string function, Token tok, params Expr[] args)
            {
                var call = _parser.At(new CallExpr { Func = Name(function, tok) }, tok);
                call.Args.AddRange(args);
                return call;
            }

            private Expr Compare(CompareOp op, Expr left, Expr right, Token tok)
            {
                var compare = _parser.At(new CompareExpr { First = left }, tok);
                compare.Ops.Add(op);
                compare.Rest.Add(right);
                return compare;
            }

            public Expr And(Expr left, Expr right, Token tok) => _parser.At(new BoolOpExpr { IsAnd = true, Left = left, Right = right }, tok);

            private Expr Or(Expr left, Expr right, Token tok) => _parser.At(new BoolOpExpr { IsAnd = false, Left = left, Right = right }, tok);

            private Expr Index(Expr target, long index, Token tok) => _parser.At(new SubscriptExpr { Target = target, Index = Const(index, tok) }, tok);

            // (name := value) is name: assigns, and is always true.
            private Expr Assign(string name, Expr value, Token tok) =>
                Compare(CompareOp.Is, _parser.At(new NamedExpr { Name = name, Value = value }, tok), Name(name, tok), tok);

            // The names a pattern captures. They are kept in temporaries while the pattern is being matched and only given
            // to the script's variables when all of it matched, as CPython does, so a case that fails leaves them alone.
            private readonly List<string> _captured = new List<string>();

            private Expr Bind(string name, Expr value, Token tok)
            {
                if (!_captured.Contains(name)) _captured.Add(name);
                return Assign(CaptureTemp(name), value, tok);
            }

            private string CaptureTemp(string name) => ".c" + _id + "_" + name;

            /// <summary>The test for a whole case: the pattern, then the assignment of what it captured.</summary>
            public Expr Complete(Expr test, Token tok)
            {
                foreach (var name in _captured) test = And(test, Assign(name, Name(CaptureTemp(name), tok), tok), tok);
                _captured.Clear();
                return test;
            }

            private string Temp() => ".t" + _id + "_" + (++_temps);

            private static Expr Clone(Expr key)
            {
                switch (key)
                {
                    case ConstantExpr c: return new ConstantExpr { Value = c.Value, Line = c.Line, Column = c.Column };
                    case NameExpr n: return new NameExpr { Name = n.Name, Line = n.Line, Column = n.Column };
                    default:
                        var a = (AttributeExpr)key;
                        return new AttributeExpr { Target = Clone(a.Target), Name = a.Name, Line = a.Line, Column = a.Column };
                }
            }

            public Expr Lower(Pattern pattern, System.Func<Expr> subject, Token tok)
            {
                switch (pattern)
                {
                    case LiteralPattern l:
                        return Compare(l.Identity ? CompareOp.Is : CompareOp.Eq, subject(), l.Value, l.Tok);
                    case CapturePattern c:
                        return c.Name == null ? Const(true, c.Tok) : Bind(c.Name, subject(), c.Tok);
                    case ValuePattern v:
                        return Compare(CompareOp.Eq, subject(), v.Value, v.Tok);
                    case AsPattern a:
                        return And(Lower(a.Inner, subject, a.Tok), Bind(a.Name, subject(), a.Tok), a.Tok);
                    case OrPattern o:
                    {
                        var result = Lower(o.Alternatives[0], subject, o.Tok);
                        for (int i = 1; i < o.Alternatives.Count; i++) result = Or(result, Lower(o.Alternatives[i], subject, o.Tok), o.Tok);
                        return result;
                    }
                    case SequencePattern q: return LowerSequence(q, subject);
                    case MappingPattern m: return LowerMapping(m, subject);
                    case ClassPattern k: return LowerClass(k, subject);
                    default:
                        throw _parser.Error("invalid pattern", tok);
                }
            }

            // A part of the subject: used as it is when the pattern only reads it once, else kept in a temporary first.
            private Expr LowerPart(Pattern pattern, System.Func<Expr> part, Token tok)
            {
                if (pattern is LiteralPattern || pattern is ValuePattern || pattern is CapturePattern) return Lower(pattern, part, tok);
                string temp = Temp();
                return And(Compare(CompareOp.Is, _parser.At(new NamedExpr { Name = temp, Value = part() }, tok), Name(temp, tok), tok),
                    Lower(pattern, () => Name(temp, tok), tok), tok);
            }

            private Expr LowerSequence(SequencePattern q, System.Func<Expr> subject)
            {
                var tok = q.Tok;
                int count = q.Items.Count;
                var test = Call(".matchseq", tok, subject());
                var length = Call(".len", tok, subject());
                test = And(test, q.Star < 0
                    ? Compare(CompareOp.Eq, length, Const((long)count, tok), tok)
                    : Compare(CompareOp.Ge, length, Const((long)(count - 1), tok), tok), tok);
                for (int i = 0; i < count; i++)
                {
                    if (i == q.Star) continue;
                    long index = q.Star >= 0 && i > q.Star ? i - count : i;
                    test = And(test, LowerPart(q.Items[i], () => Index(subject(), index, tok), tok), tok);
                }
                if (q.Star >= 0 && ((StarPattern)q.Items[q.Star]).Name != null)
                {
                    int after = count - q.Star - 1;
                    var slice = _parser.At(new SliceExpr { Lower = Const((long)q.Star, tok), Upper = after > 0 ? Const((long)-after, tok) : null }, tok);
                    var middle = _parser.At(new SubscriptExpr { Target = subject(), Index = slice }, tok);
                    test = And(test, Bind(((StarPattern)q.Items[q.Star]).Name, Call(".list", tok, middle), tok), tok);
                }
                return test;
            }

            private Expr LowerMapping(MappingPattern m, System.Func<Expr> subject)
            {
                var tok = m.Tok;
                var test = Call(".matchmap", tok, subject());
                for (int i = 0; i < m.Keys.Count; i++)
                {
                    var key = m.Keys[i];
                    test = And(test, Compare(CompareOp.In, Clone(key), subject(), tok), tok);
                    test = And(test, LowerPart(m.Values[i], () => _parser.At(new SubscriptExpr { Target = subject(), Index = Clone(key) }, tok), tok), tok);
                }
                if (m.Rest != null)
                {
                    var args = new List<Expr> { subject() };
                    foreach (var key in m.Keys) args.Add(Clone(key));
                    test = And(test, Bind(m.Rest, Call(".maprest", tok, args.ToArray()), tok), tok);
                }
                return test;
            }

            private Expr LowerClass(ClassPattern k, System.Func<Expr> subject)
            {
                var tok = k.Tok;
                string temp = Temp();
                var args = new List<Expr> { subject(), k.Class, Const((long)k.Positional.Count, tok) };
                foreach (var name in k.KeywordNames) args.Add(Const(name, tok));
                var call = Call(".matchclass", tok, args.ToArray());
                var test = Compare(CompareOp.IsNot, _parser.At(new NamedExpr { Name = temp, Value = call }, tok), Const(null, tok), tok);
                int total = k.Positional.Count + k.KeywordPatterns.Count;
                for (int i = 0; i < total; i++)
                {
                    var sub = i < k.Positional.Count ? k.Positional[i] : k.KeywordPatterns[i - k.Positional.Count];
                    long at = i;
                    test = And(test, LowerPart(sub, () => Index(Name(temp, tok), at, tok), tok), tok);
                }
                return test;
            }
        }
    }
}
