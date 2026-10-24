using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CSharpPython.Syntax
{
    /// <summary>
    /// Recursive-descent parser for statements with a precedence-climbing (Pratt) parser for arithmetic.
    /// Anything outside the supported subset is rejected with a message that says it is not supported yet.
    /// </summary>
    public sealed class Parser
    {
        // Binding powers for the binary arithmetic operators.
        private const int PowerAdditive = 10;
        private const int PowerMultiplicative = 20;
        private const int PowerUnary = 30;
        private const int PowerExponent = 40;

        private readonly List<Token> _tokens;
        private readonly int _lineBase;
        private readonly int _columnBase;
        private int _index;
        private bool _noIn;

        private Parser(List<Token> tokens, int lineBase, int columnBase)
        {
            _tokens = tokens;
            _lineBase = lineBase;
            _columnBase = columnBase;
        }

        /// <summary>Parses a whole script. Throws <see cref="PySyntaxError"/> on invalid input.</summary>
        public static ModuleNode ParseModule(string source)
        {
            var parser = new Parser(Lexer.Tokenize(source), 0, 0);
            return parser.ParseModuleBody();
        }

        /// <summary>Parses a single expression, such as the contents of an f-string replacement field.</summary>
        public static Expr ParseExpression(string source) => ParseEmbedded(source, 1, 1);

        private static Expr ParseEmbedded(string source, int line, int column)
        {
            // Wrapping in parentheses allows surrounding whitespace, newlines and tuples.
            List<Token> tokens;
            try
            {
                tokens = Lexer.Tokenize("(" + source + ")");
            }
            catch (PySyntaxError e)
            {
                throw Shift(e, line, column);
            }
            var parser = new Parser(tokens, line - 1, column - 1 - 1);
            var expr = parser.ParseTernary();
            parser.SkipNewlines();
            if (parser.Cur.Kind != TokenKind.EndOfFile)
                throw parser.Error("unexpected " + Describe(parser.Cur), parser.Cur);
            return expr;
        }

        private static PySyntaxError Shift(PySyntaxError e, int line, int column)
        {
            int newLine = e.Line + line - 1;
            int newColumn = e.Line == 1 ? e.Column + column - 2 : e.Column;
            return new PySyntaxError(e.Problem, newLine, newColumn, -1, e.Length);
        }

        // ---- Token helpers --------------------------------------------------------------------------------------

        private Token Cur => _tokens[_index];

        private Token PeekAt(int ahead)
        {
            int i = _index + ahead;
            return i < _tokens.Count ? _tokens[i] : _tokens[_tokens.Count - 1];
        }

        private Token Advance()
        {
            var t = _tokens[_index];
            if (_index < _tokens.Count - 1) _index++;
            return t;
        }

        private bool Accept(TokenKind kind)
        {
            if (Cur.Kind != kind) return false;
            Advance();
            return true;
        }

        private Token Expect(TokenKind kind, string what)
        {
            if (Cur.Kind != kind) throw Error("expected " + what + ", found " + Describe(Cur), Cur);
            return Advance();
        }

        private void SkipNewlines()
        {
            while (Cur.Kind == TokenKind.Newline) Advance();
        }

        private int LineOf(Token t) => t.Line + _lineBase;

        private int ColumnOf(Token t) => t.Line == 1 ? t.Column + _columnBase : t.Column;

        private PySyntaxError Error(string message, Token at)
        {
            return new PySyntaxError(message, LineOf(at), ColumnOf(at), at.Start, Math.Max(1, at.Length));
        }

        private T At<T>(T node, Token t) where T : Node
        {
            node.Line = LineOf(t);
            node.Column = ColumnOf(t);
            return node;
        }

        private static T Copy<T>(T node, Node from) where T : Node
        {
            node.Line = from.Line;
            node.Column = from.Column;
            return node;
        }

        private static string Describe(Token t)
        {
            switch (t.Kind)
            {
                case TokenKind.EndOfFile: return "end of file";
                case TokenKind.Newline: return "end of line";
                case TokenKind.Indent: return "indent";
                case TokenKind.Dedent: return "dedent";
                case TokenKind.String:
                case TokenKind.FString: return "string";
                case TokenKind.Error: return "invalid token";
                default: return "'" + t.Text + "'";
            }
        }

        // ---- Statements -----------------------------------------------------------------------------------------

        private ModuleNode ParseModuleBody()
        {
            var module = new ModuleNode { Line = 1, Column = 1 };
            SkipNewlines();
            while (Cur.Kind != TokenKind.EndOfFile)
            {
                if (Cur.Kind == TokenKind.Indent) throw Error("unexpected indent", Cur);
                ParseStatement(module.Body);
                SkipNewlines();
            }
            return module;
        }

        private void ParseStatement(List<Stmt> into)
        {
            switch (Cur.Kind)
            {
                case TokenKind.KwDef: into.Add(ParseDef()); return;
                case TokenKind.KwIf: into.Add(ParseIf()); return;
                case TokenKind.KwWhile: into.Add(ParseWhile()); return;
                case TokenKind.KwFor: into.Add(ParseFor()); return;
                case TokenKind.KwTry: into.Add(ParseTry()); return;
                default: ParseSimpleLine(into); return;
            }
        }

        // Parses the body after a ':' - either an indented block or statements on the same line.
        private List<Stmt> ParseBlock(string owner)
        {
            var body = new List<Stmt>();
            if (Cur.Kind == TokenKind.Newline)
            {
                Advance();
                SkipNewlines();
                if (Cur.Kind != TokenKind.Indent)
                    throw Error("expected an indented block after " + owner, Cur);
                Advance();
                while (Cur.Kind != TokenKind.Dedent && Cur.Kind != TokenKind.EndOfFile)
                {
                    if (Cur.Kind == TokenKind.Indent) throw Error("unexpected indent", Cur);
                    ParseStatement(body);
                    SkipNewlines();
                }
                Accept(TokenKind.Dedent);
            }
            else
            {
                ParseSimpleLine(body);
            }
            return body;
        }

        private DefStmt ParseDef()
        {
            var def = At(new DefStmt(), Advance());
            var name = Expect(TokenKind.Name, "a function name");
            def.Name = name.Text;
            Expect(TokenKind.LParen, "'('");
            bool seenDefault = false;
            while (Cur.Kind != TokenKind.RParen)
            {
                if (Cur.Kind == TokenKind.Star || Cur.Kind == TokenKind.DoubleStar)
                    throw Error("variable arguments (*args / **kwargs) are not supported yet", Cur);
                var pname = Expect(TokenKind.Name, "a parameter name");
                var param = new Param { Name = pname.Text, Line = LineOf(pname), Column = ColumnOf(pname) };
                if (Cur.Kind == TokenKind.Colon)
                    throw Error("type annotations are not supported yet", Cur);
                if (Accept(TokenKind.Assign))
                {
                    param.Default = ParseTernary();
                    seenDefault = true;
                }
                else if (seenDefault)
                {
                    throw Error("non-default argument '" + param.Name + "' follows default argument", pname);
                }
                def.Params.Add(param);
                if (!Accept(TokenKind.Comma)) break;
            }
            Expect(TokenKind.RParen, "')'");
            Expect(TokenKind.Colon, "':'");
            def.Body = ParseBlock("function definition");
            return def;
        }

        private IfStmt ParseIf()
        {
            var tok = Advance(); // 'if' or 'elif'
            var node = At(new IfStmt(), tok);
            node.Test = ParseTernary();
            Expect(TokenKind.Colon, "':'");
            node.Body = ParseBlock("'" + tok.Text + "' statement");
            if (Cur.Kind == TokenKind.KwElif)
            {
                node.Else.Add(ParseIf());
            }
            else if (Cur.Kind == TokenKind.KwElse)
            {
                Advance();
                Expect(TokenKind.Colon, "':'");
                node.Else = ParseBlock("'else' statement");
            }
            return node;
        }

        private WhileStmt ParseWhile()
        {
            var node = At(new WhileStmt(), Advance());
            node.Test = ParseTernary();
            Expect(TokenKind.Colon, "':'");
            node.Body = ParseBlock("'while' statement");
            if (Cur.Kind == TokenKind.KwElse) throw Error("'else' on a loop is not supported yet", Cur);
            return node;
        }

        private ForStmt ParseFor()
        {
            var node = At(new ForStmt(), Advance());
            bool saved = _noIn;
            _noIn = true;
            node.Target = ParseExprList();
            _noIn = saved;
            CheckTarget(node.Target);
            Expect(TokenKind.KwIn, "'in'");
            node.Iterable = ParseExprList();
            Expect(TokenKind.Colon, "':'");
            node.Body = ParseBlock("'for' statement");
            if (Cur.Kind == TokenKind.KwElse) throw Error("'else' on a loop is not supported yet", Cur);
            return node;
        }

        private TryStmt ParseTry()
        {
            var node = At(new TryStmt(), Advance());
            Expect(TokenKind.Colon, "':'");
            node.Body = ParseBlock("'try' statement");
            while (Cur.Kind == TokenKind.KwExcept)
            {
                var tok = Advance();
                var handler = new ExceptHandler { Line = LineOf(tok), Column = ColumnOf(tok) };
                if (Cur.Kind != TokenKind.Colon)
                {
                    handler.Type = ParseTernary();
                    if (Accept(TokenKind.KwAs)) handler.Name = Expect(TokenKind.Name, "a name").Text;
                }
                Expect(TokenKind.Colon, "':'");
                handler.Body = ParseBlock("'except' clause");
                if (node.Handlers.Count > 0 && node.Handlers[node.Handlers.Count - 1].Type == null)
                    throw new PySyntaxError("default 'except:' must be last", handler.Line, handler.Column);
                node.Handlers.Add(handler);
            }
            if (node.Handlers.Count == 0)
            {
                if (Cur.Kind == TokenKind.KwUnsupported && Cur.Text == "finally")
                    throw Error("'finally' is not supported yet", Cur);
                throw Error("expected an 'except' block after 'try'", Cur);
            }
            if (Cur.Kind == TokenKind.KwElse) throw Error("'else' on a try statement is not supported yet", Cur);
            if (Cur.Kind == TokenKind.KwUnsupported && Cur.Text == "finally")
                throw Error("'finally' is not supported yet", Cur);
            return node;
        }

        private void ParseSimpleLine(List<Stmt> into)
        {
            into.Add(ParseSimpleStatement());
            while (Cur.Kind == TokenKind.Semicolon)
            {
                Advance();
                if (Cur.Kind == TokenKind.Newline || Cur.Kind == TokenKind.EndOfFile) break;
                into.Add(ParseSimpleStatement());
            }
            if (Cur.Kind == TokenKind.Newline) Advance();
            else if (Cur.Kind != TokenKind.EndOfFile && Cur.Kind != TokenKind.Dedent)
                throw Error("unexpected " + Describe(Cur), Cur);
        }

        private static bool EndsStatement(TokenKind kind)
        {
            return kind == TokenKind.Newline || kind == TokenKind.Semicolon || kind == TokenKind.EndOfFile
                || kind == TokenKind.Dedent;
        }

        private Stmt ParseSimpleStatement()
        {
            var tok = Cur;
            switch (tok.Kind)
            {
                case TokenKind.KwPass: Advance(); return At(new PassStmt(), tok);
                case TokenKind.KwBreak: Advance(); return At(new BreakStmt(), tok);
                case TokenKind.KwContinue: Advance(); return At(new ContinueStmt(), tok);
                case TokenKind.KwReturn:
                {
                    Advance();
                    var node = At(new ReturnStmt(), tok);
                    if (!EndsStatement(Cur.Kind)) node.Value = ParseExprList();
                    return node;
                }
                case TokenKind.KwGlobal:
                {
                    Advance();
                    var node = At(new GlobalStmt(), tok);
                    do node.Names.Add(Expect(TokenKind.Name, "a name").Text);
                    while (Accept(TokenKind.Comma));
                    return node;
                }
                case TokenKind.KwRaise:
                {
                    Advance();
                    var node = At(new RaiseStmt(), tok);
                    if (!EndsStatement(Cur.Kind)) node.Value = ParseTernary();
                    if (Cur.Kind == TokenKind.KwFrom) throw Error("'raise ... from' is not supported yet", Cur);
                    return node;
                }
                case TokenKind.KwImport: return ParseImport();
                case TokenKind.KwFrom: return ParseImportFrom();
                case TokenKind.KwUnsupported:
                    throw Error("'" + tok.Text + "' is not supported yet", tok);
                default: return ParseExpressionOrAssignment();
            }
        }

        private Stmt ParseImport()
        {
            var node = At(new ImportStmt(), Advance());
            node.Module = ParseModuleName();
            if (Accept(TokenKind.KwAs)) node.Alias = Expect(TokenKind.Name, "a name").Text;
            if (Cur.Kind == TokenKind.Comma)
                throw Error("importing several modules in one statement is not supported yet", Cur);
            return node;
        }

        private string ParseModuleName()
        {
            string name = Expect(TokenKind.Name, "a module name").Text;
            if (Cur.Kind == TokenKind.Dot) throw Error("dotted module names are not supported yet", Cur);
            return name;
        }

        private Stmt ParseImportFrom()
        {
            var node = At(new ImportFromStmt(), Advance());
            node.Module = ParseModuleName();
            Expect(TokenKind.KwImport, "'import'");
            if (Cur.Kind == TokenKind.Star) throw Error("'from ... import *' is not supported", Cur);
            do
            {
                node.Names.Add(Expect(TokenKind.Name, "a name").Text);
                node.Aliases.Add(Accept(TokenKind.KwAs) ? Expect(TokenKind.Name, "a name").Text : null);
            }
            while (Accept(TokenKind.Comma));
            return node;
        }

        private static bool TryAugOp(TokenKind kind, out BinaryOp op)
        {
            switch (kind)
            {
                case TokenKind.PlusAssign: op = BinaryOp.Add; return true;
                case TokenKind.MinusAssign: op = BinaryOp.Subtract; return true;
                case TokenKind.StarAssign: op = BinaryOp.Multiply; return true;
                case TokenKind.SlashAssign: op = BinaryOp.Divide; return true;
                case TokenKind.DoubleSlashAssign: op = BinaryOp.FloorDivide; return true;
                case TokenKind.PercentAssign: op = BinaryOp.Modulo; return true;
                case TokenKind.DoubleStarAssign: op = BinaryOp.Power; return true;
                default: op = default; return false;
            }
        }

        private Stmt ParseExpressionOrAssignment()
        {
            var startTok = Cur;
            var first = ParseExprList();

            if (Cur.Kind == TokenKind.Assign)
            {
                var node = At(new AssignStmt(), startTok);
                Expr current = first;
                while (Accept(TokenKind.Assign))
                {
                    CheckTarget(current);
                    node.Targets.Add(current);
                    current = ParseExprList();
                }
                node.Value = current;
                return node;
            }

            if (TryAugOp(Cur.Kind, out BinaryOp op))
            {
                var opTok = Advance();
                if (!(first is NameExpr || first is AttributeExpr || first is SubscriptExpr))
                    throw Error("illegal target for augmented assignment", startTok);
                var node = At(new AugAssignStmt { Op = op, Target = first }, opTok);
                node.Value = ParseExprList();
                return node;
            }

            if (Cur.Kind == TokenKind.Colon && (first is NameExpr || first is AttributeExpr || first is SubscriptExpr))
                throw Error("type annotations are not supported yet", Cur);

            return Copy(new ExprStmt { Value = first }, first);
        }

        private void CheckTarget(Expr e)
        {
            switch (e)
            {
                case NameExpr _:
                case AttributeExpr _:
                case SubscriptExpr _:
                    return;
                case TupleExpr t:
                    foreach (var item in t.Items) CheckTarget(item);
                    return;
                case ListExpr l:
                    foreach (var item in l.Items) CheckTarget(item);
                    return;
            }
            string what;
            switch (e)
            {
                case ConstantExpr _: what = "literal"; break;
                case CallExpr _: what = "function call"; break;
                case FStringExpr _: what = "f-string expression"; break;
                case CompareExpr _: what = "comparison"; break;
                case BoolOpExpr _:
                case BinaryExpr _:
                case UnaryExpr _: what = "expression"; break;
                case DictExpr _: what = "dict literal"; break;
                case IfExpr _: what = "conditional expression"; break;
                default: what = "expression"; break;
            }
            throw new PySyntaxError("cannot assign to " + what, e.Line, e.Column);
        }

        // ---- Expressions ----------------------------------------------------------------------------------------

        private static bool IsExprStart(TokenKind kind)
        {
            switch (kind)
            {
                case TokenKind.Name:
                case TokenKind.Int:
                case TokenKind.Float:
                case TokenKind.String:
                case TokenKind.FString:
                case TokenKind.KwTrue:
                case TokenKind.KwFalse:
                case TokenKind.KwNone:
                case TokenKind.KwNot:
                case TokenKind.Minus:
                case TokenKind.Plus:
                case TokenKind.LParen:
                case TokenKind.LBracket:
                case TokenKind.LBrace:
                case TokenKind.KwUnsupported:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Parses one expression or a comma separated list, which becomes a tuple.</summary>
        private Expr ParseExprList()
        {
            var startTok = Cur;
            var first = ParseTernary();
            if (Cur.Kind != TokenKind.Comma) return first;

            var tuple = At(new TupleExpr(), startTok);
            tuple.Items.Add(first);
            while (Accept(TokenKind.Comma))
            {
                if (!IsExprStart(Cur.Kind)) break;
                tuple.Items.Add(ParseTernary());
            }
            return tuple;
        }

        private Expr ParseTernary()
        {
            var value = ParseOr();
            if (Cur.Kind != TokenKind.KwIf) return value;
            var ifTok = Advance();
            var node = At(new IfExpr { Then = value }, ifTok);
            node.Test = ParseOr();
            Expect(TokenKind.KwElse, "'else' in conditional expression");
            node.Else = ParseTernary();
            return node;
        }

        private Expr ParseOr()
        {
            var left = ParseAnd();
            while (Cur.Kind == TokenKind.KwOr)
            {
                var tok = Advance();
                left = At(new BoolOpExpr { IsAnd = false, Left = left, Right = ParseAnd() }, tok);
            }
            return left;
        }

        private Expr ParseAnd()
        {
            var left = ParseNot();
            while (Cur.Kind == TokenKind.KwAnd)
            {
                var tok = Advance();
                left = At(new BoolOpExpr { IsAnd = true, Left = left, Right = ParseNot() }, tok);
            }
            return left;
        }

        private Expr ParseNot()
        {
            if (Cur.Kind != TokenKind.KwNot) return ParseComparison();
            var tok = Advance();
            return At(new UnaryExpr { Op = UnaryOp.Not, Operand = ParseNot() }, tok);
        }

        private bool TryCompareOp(out CompareOp op, out int tokenCount)
        {
            tokenCount = 1;
            switch (Cur.Kind)
            {
                case TokenKind.EqEq: op = CompareOp.Eq; return true;
                case TokenKind.NotEq: op = CompareOp.NotEq; return true;
                case TokenKind.Lt: op = CompareOp.Lt; return true;
                case TokenKind.Le: op = CompareOp.Le; return true;
                case TokenKind.Gt: op = CompareOp.Gt; return true;
                case TokenKind.Ge: op = CompareOp.Ge; return true;
                case TokenKind.KwIn:
                    op = CompareOp.In;
                    return !_noIn;
                case TokenKind.KwNot:
                    op = CompareOp.NotIn;
                    tokenCount = 2;
                    return !_noIn && PeekAt(1).Kind == TokenKind.KwIn;
                case TokenKind.KwIs:
                    if (PeekAt(1).Kind == TokenKind.KwNot)
                    {
                        op = CompareOp.IsNot;
                        tokenCount = 2;
                    }
                    else
                    {
                        op = CompareOp.Is;
                    }
                    return true;
                default:
                    op = default;
                    return false;
            }
        }

        private Expr ParseComparison()
        {
            var left = ParseBinary(0);
            CompareExpr chain = null;
            while (TryCompareOp(out CompareOp op, out int count))
            {
                var tok = Cur;
                for (int i = 0; i < count; i++) Advance();
                if (chain == null) chain = At(new CompareExpr { First = left }, tok);
                chain.Ops.Add(op);
                chain.Rest.Add(ParseBinary(0));
            }
            return chain ?? left;
        }

        private static bool TryBinaryOp(TokenKind kind, out BinaryOp op, out int power)
        {
            switch (kind)
            {
                case TokenKind.Plus: op = BinaryOp.Add; power = PowerAdditive; return true;
                case TokenKind.Minus: op = BinaryOp.Subtract; power = PowerAdditive; return true;
                case TokenKind.Star: op = BinaryOp.Multiply; power = PowerMultiplicative; return true;
                case TokenKind.Slash: op = BinaryOp.Divide; power = PowerMultiplicative; return true;
                case TokenKind.DoubleSlash: op = BinaryOp.FloorDivide; power = PowerMultiplicative; return true;
                case TokenKind.Percent: op = BinaryOp.Modulo; power = PowerMultiplicative; return true;
                case TokenKind.DoubleStar: op = BinaryOp.Power; power = PowerExponent; return true;
                default: op = default; power = 0; return false;
            }
        }

        private Expr ParseBinary(int minPower)
        {
            var left = ParseUnary();
            while (TryBinaryOp(Cur.Kind, out BinaryOp op, out int power) && power > minPower)
            {
                var tok = Advance();
                // '**' is right associative, everything else is left associative.
                var right = ParseBinary(op == BinaryOp.Power ? power - 1 : power);
                left = At(new BinaryExpr { Op = op, Left = left, Right = right }, tok);
            }
            return left;
        }

        private Expr ParseUnary()
        {
            if (Cur.Kind == TokenKind.Minus || Cur.Kind == TokenKind.Plus)
            {
                var tok = Advance();
                var operand = ParseBinary(PowerUnary);
                if (tok.Kind == TokenKind.Minus && operand is ConstantExpr c)
                {
                    if (c.Value is long l && l != long.MinValue) return Copy(new ConstantExpr { Value = -l }, At(new ConstantExpr(), tok));
                    if (c.Value is double d) return Copy(new ConstantExpr { Value = -d }, At(new ConstantExpr(), tok));
                }
                return At(new UnaryExpr { Op = tok.Kind == TokenKind.Minus ? UnaryOp.Negate : UnaryOp.Plus, Operand = operand }, tok);
            }
            return ParsePostfix(ParsePrimary());
        }

        private Expr ParsePostfix(Expr expr)
        {
            while (true)
            {
                var tok = Cur;
                switch (tok.Kind)
                {
                    case TokenKind.LParen:
                        Advance();
                        expr = ParseCall(expr, tok);
                        break;
                    case TokenKind.LBracket:
                        Advance();
                        expr = ParseSubscript(expr, tok);
                        break;
                    case TokenKind.Dot:
                        Advance();
                        var name = Expect(TokenKind.Name, "an attribute name");
                        expr = At(new AttributeExpr { Target = expr, Name = name.Text }, name);
                        break;
                    default:
                        return expr;
                }
            }
        }

        private Expr ParseCall(Expr func, Token open)
        {
            bool saved = _noIn;
            _noIn = false;
            var call = At(new CallExpr { Func = func }, open);
            while (Cur.Kind != TokenKind.RParen)
            {
                if (Cur.Kind == TokenKind.Star || Cur.Kind == TokenKind.DoubleStar)
                    throw Error("argument unpacking (*args / **kwargs) is not supported yet", Cur);
                if (Cur.Kind == TokenKind.Name && PeekAt(1).Kind == TokenKind.Assign)
                {
                    var nameTok = Advance();
                    Advance();
                    if (call.KeywordNames.Contains(nameTok.Text))
                        throw Error("keyword argument repeated: " + nameTok.Text, nameTok);
                    call.KeywordNames.Add(nameTok.Text);
                    call.KeywordValues.Add(ParseTernary());
                }
                else
                {
                    var argTok = Cur;
                    if (call.KeywordNames.Count > 0)
                        throw Error("positional argument follows keyword argument", argTok);
                    call.Args.Add(ParseTernary());
                    if (Cur.Kind == TokenKind.KwFor)
                        throw Error("comprehensions and generator expressions are not supported yet", Cur);
                }
                if (!Accept(TokenKind.Comma)) break;
            }
            Expect(TokenKind.RParen, "')'");
            _noIn = saved;
            return call;
        }

        private Expr ParseSubscript(Expr target, Token open)
        {
            bool saved = _noIn;
            _noIn = false;
            var startTok = Cur;
            var index = ParseSubscriptItem();
            if (Cur.Kind == TokenKind.Comma)
            {
                var tuple = At(new TupleExpr(), startTok);
                tuple.Items.Add(index);
                while (Accept(TokenKind.Comma))
                {
                    if (Cur.Kind == TokenKind.RBracket) break;
                    tuple.Items.Add(ParseSubscriptItem());
                }
                index = tuple;
            }
            Expect(TokenKind.RBracket, "']'");
            _noIn = saved;
            return At(new SubscriptExpr { Target = target, Index = index }, open);
        }

        private Expr ParseSubscriptItem()
        {
            var startTok = Cur;
            Expr lower = null;
            if (Cur.Kind != TokenKind.Colon)
            {
                lower = ParseTernary();
                if (Cur.Kind != TokenKind.Colon) return lower;
            }
            var slice = At(new SliceExpr { Lower = lower }, startTok);
            Expect(TokenKind.Colon, "':'");
            if (Cur.Kind != TokenKind.Colon && Cur.Kind != TokenKind.RBracket && Cur.Kind != TokenKind.Comma)
                slice.Upper = ParseTernary();
            if (Accept(TokenKind.Colon))
            {
                if (Cur.Kind != TokenKind.RBracket && Cur.Kind != TokenKind.Comma)
                    slice.Step = ParseTernary();
            }
            return slice;
        }

        private Expr ParsePrimary()
        {
            var tok = Cur;
            switch (tok.Kind)
            {
                case TokenKind.Name:
                    Advance();
                    return At(new NameExpr { Name = tok.Text }, tok);
                case TokenKind.Int:
                    Advance();
                    return At(new ConstantExpr { Value = long.Parse(tok.Text, CultureInfo.InvariantCulture) }, tok);
                case TokenKind.Float:
                    Advance();
                    return At(new ConstantExpr { Value = double.Parse(tok.Text, NumberStyles.Float, CultureInfo.InvariantCulture) }, tok);
                case TokenKind.String:
                case TokenKind.FString:
                    return ParseStringLiterals();
                case TokenKind.KwTrue: Advance(); return At(new ConstantExpr { Value = true }, tok);
                case TokenKind.KwFalse: Advance(); return At(new ConstantExpr { Value = false }, tok);
                case TokenKind.KwNone: Advance(); return At(new ConstantExpr { Value = null }, tok);
                case TokenKind.LParen: return ParseParenthesised();
                case TokenKind.LBracket: return ParseListLiteral();
                case TokenKind.LBrace: return ParseDictLiteral();
                case TokenKind.KwUnsupported:
                    throw Error("'" + tok.Text + "' is not supported yet", tok);
                default:
                    throw Error("unexpected " + Describe(tok), tok);
            }
        }

        private Expr ParseParenthesised()
        {
            var open = Advance();
            bool saved = _noIn;
            _noIn = false;
            try
            {
                if (Accept(TokenKind.RParen)) return At(new TupleExpr(), open);
                var first = ParseTernary();
                if (Cur.Kind == TokenKind.KwFor)
                    throw Error("comprehensions and generator expressions are not supported yet", Cur);
                if (Cur.Kind != TokenKind.Comma)
                {
                    Expect(TokenKind.RParen, "')'");
                    return first;
                }
                var tuple = At(new TupleExpr(), open);
                tuple.Items.Add(first);
                while (Accept(TokenKind.Comma))
                {
                    if (Cur.Kind == TokenKind.RParen) break;
                    tuple.Items.Add(ParseTernary());
                }
                Expect(TokenKind.RParen, "')'");
                return tuple;
            }
            finally
            {
                _noIn = saved;
            }
        }

        private Expr ParseListLiteral()
        {
            var open = Advance();
            bool saved = _noIn;
            _noIn = false;
            var list = At(new ListExpr(), open);
            while (Cur.Kind != TokenKind.RBracket)
            {
                list.Items.Add(ParseTernary());
                if (Cur.Kind == TokenKind.KwFor)
                    throw Error("comprehensions and generator expressions are not supported yet", Cur);
                if (!Accept(TokenKind.Comma)) break;
            }
            Expect(TokenKind.RBracket, "']'");
            _noIn = saved;
            return list;
        }

        private Expr ParseDictLiteral()
        {
            var open = Advance();
            bool saved = _noIn;
            _noIn = false;
            var dict = At(new DictExpr(), open);
            while (Cur.Kind != TokenKind.RBrace)
            {
                if (Cur.Kind == TokenKind.DoubleStar)
                    throw Error("dictionary unpacking is not supported yet", Cur);
                var key = ParseTernary();
                if (Cur.Kind != TokenKind.Colon)
                {
                    if (dict.Keys.Count == 0 && (Cur.Kind == TokenKind.Comma || Cur.Kind == TokenKind.RBrace))
                        throw Error("set literals are not supported yet", Cur);
                    throw Error("expected ':' in dictionary literal, found " + Describe(Cur), Cur);
                }
                Advance();
                dict.Keys.Add(key);
                dict.Values.Add(ParseTernary());
                if (Cur.Kind == TokenKind.KwFor)
                    throw Error("comprehensions and generator expressions are not supported yet", Cur);
                if (!Accept(TokenKind.Comma)) break;
            }
            Expect(TokenKind.RBrace, "'}'");
            _noIn = saved;
            return dict;
        }

        // ---- String literals ------------------------------------------------------------------------------------

        // Adjacent literals are concatenated at parse time ("a" "b" and "a" f"{x}").
        private Expr ParseStringLiterals()
        {
            var first = Cur;
            var parts = new List<FStringPart>();
            bool anyFString = false;
            while (Cur.Kind == TokenKind.String || Cur.Kind == TokenKind.FString)
            {
                var tok = Advance();
                if (tok.Kind == TokenKind.String) AddLiteral(parts, tok.Text);
                else
                {
                    anyFString = true;
                    ParseFStringBody(tok, parts);
                }
            }

            if (!anyFString)
            {
                var sb = new StringBuilder();
                foreach (var p in parts) sb.Append(p.Literal);
                return At(new ConstantExpr { Value = sb.ToString() }, first);
            }
            var node = At(new FStringExpr(), first);
            node.Parts = parts;
            return node;
        }

        private static void AddLiteral(List<FStringPart> parts, string text)
        {
            if (text.Length == 0) return;
            if (parts.Count > 0 && parts[parts.Count - 1].Value == null)
                parts[parts.Count - 1].Literal += text;
            else
                parts.Add(new FStringPart { Literal = text });
        }

        private void ParseFStringBody(Token tok, List<FStringPart> parts)
        {
            string s = tok.Text;
            int quoteLen = Math.Max(1, (tok.Length - 1 - s.Length) / 2);
            var lit = new StringBuilder();
            int i = 0;

            void Flush()
            {
                if (lit.Length == 0) return;
                if (!Lexer.TryDecodeEscapes(lit.ToString(), out string decoded, out string error))
                    throw Error(error, tok);
                AddLiteral(parts, decoded);
                lit.Clear();
            }

            while (i < s.Length)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    lit.Append(c).Append(s[i + 1]);
                    i += 2;
                    continue;
                }
                if (c == '}')
                {
                    if (i + 1 < s.Length && s[i + 1] == '}') { lit.Append('}'); i += 2; continue; }
                    throw Error("f-string: single '}' is not allowed", tok);
                }
                if (c != '{') { lit.Append(c); i++; continue; }
                if (i + 1 < s.Length && s[i + 1] == '{') { lit.Append('{'); i += 2; continue; }

                Flush();
                int exprStart = i + 1;
                int j = exprStart;
                int depth = 0;
                char quote = '\0';
                for (; j < s.Length; j++)
                {
                    char d = s[j];
                    if (quote != '\0')
                    {
                        if (d == '\\') j++;
                        else if (d == quote) quote = '\0';
                        continue;
                    }
                    if (d == '\'' || d == '"') { quote = d; continue; }
                    if (d == '(' || d == '[' || d == '{') { depth++; continue; }
                    if (d == ')' || d == ']') { depth--; continue; }
                    if (d == '}')
                    {
                        if (depth == 0) break;
                        depth--;
                        continue;
                    }
                    if (depth == 0 && d == ':') break;
                    if (depth == 0 && d == '!' && j + 1 < s.Length && s[j + 1] != '=') break;
                }
                if (j >= s.Length) throw Error("f-string: expecting '}'", tok);

                string exprText = s.Substring(exprStart, j - exprStart);
                if (exprText.Trim().Length == 0) throw Error("f-string: empty expression not allowed", tok);
                string trimmed = exprText.TrimEnd();
                if (trimmed.EndsWith("=", StringComparison.Ordinal) && !trimmed.EndsWith("==", StringComparison.Ordinal)
                    && !trimmed.EndsWith("!=", StringComparison.Ordinal) && !trimmed.EndsWith("<=", StringComparison.Ordinal)
                    && !trimmed.EndsWith(">=", StringComparison.Ordinal))
                    throw Error("f-string: the '=' specifier is not supported yet", tok);

                var part = new FStringPart();
                int line = tok.Line;
                int col;
                int before = s.LastIndexOf('\n', exprStart - 1, exprStart);
                if (before < 0) col = tok.Column + 1 + quoteLen + exprStart;
                else
                {
                    for (int k = 0; k < exprStart; k++) if (s[k] == '\n') line++;
                    col = exprStart - before;
                }
                part.Value = ParseEmbedded(exprText, line + _lineBase, (tok.Line == 1 && line == tok.Line ? col + _columnBase : col));

                if (s[j] == '!')
                {
                    if (j + 1 >= s.Length || "sra".IndexOf(s[j + 1]) < 0)
                        throw Error("f-string: invalid conversion character (expected 's', 'r' or 'a')", tok);
                    part.Conversion = s[j + 1];
                    j += 2;
                    if (j >= s.Length || (s[j] != '}' && s[j] != ':')) throw Error("f-string: expecting '}'", tok);
                }
                if (s[j] == ':')
                {
                    int specStart = j + 1;
                    int k = specStart;
                    while (k < s.Length && s[k] != '}')
                    {
                        if (s[k] == '{') throw Error("f-string: nested replacement fields in format specs are not supported yet", tok);
                        k++;
                    }
                    if (k >= s.Length) throw Error("f-string: expecting '}'", tok);
                    part.FormatSpec = s.Substring(specStart, k - specStart);
                    j = k;
                }
                parts.Add(part);
                i = j + 1;
            }
            Flush();
        }
    }
}
