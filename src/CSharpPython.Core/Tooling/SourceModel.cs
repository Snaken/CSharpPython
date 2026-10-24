using System;
using System.Collections.Generic;
using CSharpPython.Syntax;

namespace CSharpPython.Editor
{
    public enum DefinitionKind
    {
        Variable,
        Parameter,
        Function,
        Module,
        ImportedName,
    }

    internal sealed class Definition
    {
        public string Name;
        public DefinitionKind Kind;
        public int TokenStart;          // where the name appears at its definition
        public FunctionScope Scope;     // the def whose body or header it belongs to, or null for module level
        public ValueKind? Type;         // guessed from the assigned value, when it is obvious
        public string Module;           // for imports: the module imported (or imported from)
        public string Member;           // for 'from m import a': the member name
        public string Signature;        // for functions: the text inside the parentheses
    }

    internal sealed class FunctionScope
    {
        public string Name;
        public int Start;
        public int End = int.MaxValue;
        public int BodyDepth;
    }

    /// <summary>
    /// A shallow, token-level picture of what a script defines: names, parameters, imports and the obvious type of
    /// assigned values. It is deliberately forgiving so it still works on code that does not parse yet.
    /// </summary>
    internal sealed class SourceModel
    {
        public readonly string Source;
        public readonly List<Token> Tokens;
        public readonly List<Definition> Definitions = new List<Definition>();
        public readonly List<FunctionScope> Scopes = new List<FunctionScope>();

        public SourceModel(string source)
        {
            Source = source ?? "";
            Tokens = Lexer.TokenizeTolerant(Source, out _);
            Build();
        }

        // ---- Queries --------------------------------------------------------------------------------------------

        public IEnumerable<Definition> VisibleAt(int offset)
        {
            foreach (var d in Definitions)
                if (d.Scope == null || (d.Scope.Start <= offset && offset <= d.Scope.End)) yield return d;
        }

        /// <summary>The single type every visible assignment of <paramref name="name"/> agrees on, or null.</summary>
        public ValueKind? TypeOf(string name, int offset, out Definition module)
        {
            module = null;
            ValueKind? type = null;
            bool first = true;
            foreach (var d in VisibleAt(offset))
            {
                if (d.Name != name) continue;
                if (d.Kind == DefinitionKind.Module) module = d;
                if (d.Kind != DefinitionKind.Variable) { type = null; first = false; continue; }
                if (first) { type = d.Type; first = false; }
                else if (type != d.Type) type = null;
            }
            return type;
        }

        /// <summary>
        /// The definition a use of <paramref name="name"/> at <paramref name="offset"/> most likely refers to: the
        /// innermost function's own names first, then the enclosing scopes', preferring the latest one before the
        /// offset. Null when the script does not define it.
        /// </summary>
        public Definition Resolve(string name, int offset)
        {
            Definition best = null;
            foreach (var d in VisibleAt(offset))
            {
                if (d.Name != name) continue;
                if (best == null) { best = d; continue; }
                int depthBest = best.Scope == null ? -1 : best.Scope.Start;
                int depth = d.Scope == null ? -1 : d.Scope.Start;
                if (depth > depthBest) best = d;
                else if (depth == depthBest && d.TokenStart <= offset && (best.TokenStart > offset || d.TokenStart > best.TokenStart)) best = d;
            }
            return best;
        }

        // ---- Construction ---------------------------------------------------------------------------------------

        private void Build()
        {
            int depth = 0;
            bool atStart = true;
            var open = new List<FunctionScope>();

            for (int i = 0; i < Tokens.Count; i++)
            {
                var t = Tokens[i];
                switch (t.Kind)
                {
                    case TokenKind.Indent: depth++; atStart = true; continue;
                    case TokenKind.Dedent:
                        depth--;
                        for (int s = open.Count - 1; s >= 0; s--)
                            if (depth < open[s].BodyDepth)
                            {
                                open[s].End = t.Start;
                                open.RemoveAt(s);
                            }
                        atStart = true;
                        continue;
                    case TokenKind.Newline: atStart = true; continue;
                    case TokenKind.EndOfFile: continue;
                }

                var scope = open.Count > 0 ? open[open.Count - 1] : null;
                bool statementStart = atStart;
                atStart = false;

                switch (t.Kind)
                {
                    case TokenKind.KwDef:
                        i = ReadDef(i, depth, scope, open);
                        break;
                    case TokenKind.KwFor:
                        for (int j = i + 1; j < Tokens.Count && Tokens[j].Kind != TokenKind.KwIn && Tokens[j].Kind != TokenKind.Newline && Tokens[j].Kind != TokenKind.Colon; j++)
                            if (Tokens[j].Kind == TokenKind.Name && (Tokens[j - 1].Kind == TokenKind.KwFor || Tokens[j - 1].Kind == TokenKind.Comma || Tokens[j - 1].Kind == TokenKind.LParen))
                                AddVariable(Tokens[j], scope, null);
                        break;
                    case TokenKind.KwImport:
                        ReadImport(i, scope);
                        break;
                    case TokenKind.KwFrom:
                        ReadFrom(i, scope);
                        break;
                    case TokenKind.KwExcept:
                        for (int j = i + 1; j + 1 < Tokens.Count && Tokens[j].Kind != TokenKind.Newline && Tokens[j].Kind != TokenKind.Colon; j++)
                            if (Tokens[j].Kind == TokenKind.KwAs && Tokens[j + 1].Kind == TokenKind.Name)
                                AddVariable(Tokens[j + 1], scope, null);
                        break;
                    case TokenKind.Name:
                        if (statementStart) ReadAssignment(i, scope);
                        break;
                }
            }
        }

        private int ReadDef(int i, int depth, FunctionScope outer, List<FunctionScope> open)
        {
            if (i + 1 >= Tokens.Count || Tokens[i + 1].Kind != TokenKind.Name) return i;
            var name = Tokens[i + 1];
            var scope = new FunctionScope { Name = name.Text, Start = Tokens[i].Start, BodyDepth = depth + 1 };
            Scopes.Add(scope);
            open.Add(scope);

            var def = new Definition { Name = name.Text, Kind = DefinitionKind.Function, TokenStart = name.Start, Scope = outer };
            Definitions.Add(def);

            int j = i + 2;
            if (j < Tokens.Count && Tokens[j].Kind == TokenKind.LParen)
            {
                int paren = 1;
                int open0 = Tokens[j].End;
                for (j++; j < Tokens.Count && paren > 0; j++)
                {
                    var p = Tokens[j];
                    if (p.Kind == TokenKind.LParen || p.Kind == TokenKind.LBracket || p.Kind == TokenKind.LBrace) paren++;
                    else if (p.Kind == TokenKind.RParen || p.Kind == TokenKind.RBracket || p.Kind == TokenKind.RBrace)
                    {
                        paren--;
                        if (paren == 0) def.Signature = Normalize(Source.Substring(open0, Math.Max(0, p.Start - open0)));
                    }
                    else if (p.Kind == TokenKind.Name && paren == 1 &&
                             (Tokens[j - 1].Kind == TokenKind.LParen || Tokens[j - 1].Kind == TokenKind.Comma ||
                              ((Tokens[j - 1].Kind == TokenKind.Star || Tokens[j - 1].Kind == TokenKind.DoubleStar) &&
                               (Tokens[j - 2].Kind == TokenKind.LParen || Tokens[j - 2].Kind == TokenKind.Comma))))
                    {
                        Definitions.Add(new Definition { Name = p.Text, Kind = DefinitionKind.Parameter, TokenStart = p.Start, Scope = scope });
                    }
                    else if (p.Kind == TokenKind.Newline) break;
                }
                j--;
            }
            return Math.Max(i + 1, j - 1);
        }

        private static string Normalize(string s)
        {
            var sb = new System.Text.StringBuilder();
            bool space = false;
            foreach (char c in s)
            {
                if (char.IsWhiteSpace(c)) { space = sb.Length > 0; continue; }
                if (space) sb.Append(' ');
                space = false;
                sb.Append(c);
            }
            return sb.ToString();
        }

        private void ReadImport(int i, FunctionScope scope)
        {
            for (int j = i + 1; j < Tokens.Count && Tokens[j].Kind != TokenKind.Newline; j++)
            {
                if (Tokens[j].Kind != TokenKind.Name) continue;
                var prev = Tokens[j - 1].Kind;
                if (prev != TokenKind.KwImport && prev != TokenKind.Comma) continue;
                var original = Tokens[j];
                var alias = original;
                if (j + 2 < Tokens.Count && Tokens[j + 1].Kind == TokenKind.KwAs && Tokens[j + 2].Kind == TokenKind.Name)
                {
                    alias = Tokens[j + 2];
                    j += 2;
                }
                Definitions.Add(new Definition { Name = alias.Text, Kind = DefinitionKind.Module, TokenStart = alias.Start, Scope = scope, Module = original.Text });
            }
        }

        private void ReadFrom(int i, FunctionScope scope)
        {
            if (i + 2 >= Tokens.Count || Tokens[i + 1].Kind != TokenKind.Name || Tokens[i + 2].Kind != TokenKind.KwImport) return;
            string module = Tokens[i + 1].Text;
            for (int j = i + 3; j < Tokens.Count && Tokens[j].Kind != TokenKind.Newline; j++)
            {
                if (Tokens[j].Kind != TokenKind.Name) continue;
                var prev = Tokens[j - 1].Kind;
                if (prev != TokenKind.KwImport && prev != TokenKind.Comma && prev != TokenKind.LParen) continue;
                var local = Tokens[j];
                string member = Tokens[j].Text;
                if (j + 2 < Tokens.Count && Tokens[j + 1].Kind == TokenKind.KwAs && Tokens[j + 2].Kind == TokenKind.Name)
                {
                    local = Tokens[j + 2];
                    j += 2;
                }
                Definitions.Add(new Definition { Name = local.Text, Kind = DefinitionKind.ImportedName, TokenStart = local.Start, Scope = scope, Module = module, Member = member });
            }
        }

        // 'a = ...', 'a, b = ...', 'a = b = ...' at the start of a statement.
        private void ReadAssignment(int i, FunctionScope scope)
        {
            int j = i;
            while (true)
            {
                var targets = new List<Token>();
                int k = j;
                while (k < Tokens.Count && Tokens[k].Kind == TokenKind.Name)
                {
                    targets.Add(Tokens[k]);
                    k++;
                    if (k < Tokens.Count && Tokens[k].Kind == TokenKind.Comma) k++;
                    else break;
                }
                if (targets.Count == 0 || k >= Tokens.Count || Tokens[k].Kind != TokenKind.Assign) return;
                ValueKind? type = targets.Count == 1 ? InferType(k + 1) : null;
                foreach (var target in targets) AddVariable(target, scope, type);
                j = k + 1; // chained assignment: the next target follows
            }
        }

        private void AddVariable(Token name, FunctionScope scope, ValueKind? type)
        {
            Definitions.Add(new Definition { Name = name.Text, Kind = DefinitionKind.Variable, TokenStart = name.Start, Scope = scope, Type = type });
        }

        // ---- Obvious types --------------------------------------------------------------------------------------

        // The type of the expression starting at token index 'start', when it is a plain literal or constructor call.
        private ValueKind? InferType(int start)
        {
            return InferTypeAt(Tokens, start);
        }

        public static ValueKind? InferTypeAt(List<Token> tokens, int start)
        {
            if (start >= tokens.Count) return null;
            var t = tokens[start];
            switch (t.Kind)
            {
                case TokenKind.String:
                case TokenKind.FString:
                    return EndsSimply(tokens, start + 1) ? ValueKind.Str : (ValueKind?)null;
                case TokenKind.Int: return EndsSimply(tokens, start + 1) ? ValueKind.Int : (ValueKind?)null;
                case TokenKind.Float: return EndsSimply(tokens, start + 1) ? ValueKind.Float : (ValueKind?)null;
                case TokenKind.KwTrue:
                case TokenKind.KwFalse: return EndsSimply(tokens, start + 1) ? ValueKind.Bool : (ValueKind?)null;
                case TokenKind.LBracket:
                {
                    int close = Match(tokens, start);
                    return close >= 0 && EndsSimply(tokens, close + 1) ? ValueKind.List : (ValueKind?)null;
                }
                case TokenKind.LBrace:
                {
                    int close = Match(tokens, start);
                    return close >= 0 && EndsSimply(tokens, close + 1) ? ValueKind.Dict : (ValueKind?)null;
                }
                case TokenKind.LParen:
                {
                    int close = Match(tokens, start);
                    if (close < 0 || !EndsSimply(tokens, close + 1)) return null;
                    return HasTopLevelComma(tokens, start, close) ? ValueKind.Tuple : (ValueKind?)null;
                }
                case TokenKind.Name:
                {
                    if (start + 1 >= tokens.Count || tokens[start + 1].Kind != TokenKind.LParen) return null;
                    int close = Match(tokens, start + 1);
                    if (close < 0 || !EndsSimply(tokens, close + 1)) return null;
                    return ConstructorType(t.Text);
                }
            }
            return null;
        }

        public static ValueKind? ConstructorType(string name)
        {
            switch (name)
            {
                case "str": case "repr": case "chr": case "format": return ValueKind.Str;
                case "list": case "sorted": return ValueKind.List;
                case "dict": return ValueKind.Dict;
                case "tuple": return ValueKind.Tuple;
                case "int": case "len": case "ord": return ValueKind.Int;
                case "float": return ValueKind.Float;
                case "range": return ValueKind.Range;
                default: return null;
            }
        }

        // True when the expression ends at this token, so the literal or call before it is the whole value.
        private static bool EndsSimply(List<Token> tokens, int index)
        {
            if (index >= tokens.Count) return true;
            switch (tokens[index].Kind)
            {
                case TokenKind.Newline:
                case TokenKind.EndOfFile:
                case TokenKind.Semicolon:
                case TokenKind.Comma:
                case TokenKind.Dedent:
                case TokenKind.Indent:
                    return true;
                default: return false;
            }
        }

        /// <summary>The index of the bracket that closes the one at <paramref name="open"/>, or -1.</summary>
        public static int Match(List<Token> tokens, int open)
        {
            int depth = 0;
            for (int i = open; i < tokens.Count; i++)
            {
                var k = tokens[i].Kind;
                if (k == TokenKind.LParen || k == TokenKind.LBracket || k == TokenKind.LBrace) depth++;
                else if (k == TokenKind.RParen || k == TokenKind.RBracket || k == TokenKind.RBrace)
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        /// <summary>The index of the bracket that opens the one at <paramref name="close"/>, or -1.</summary>
        public static int MatchBackward(List<Token> tokens, int close)
        {
            int depth = 0;
            for (int i = close; i >= 0; i--)
            {
                var k = tokens[i].Kind;
                if (k == TokenKind.RParen || k == TokenKind.RBracket || k == TokenKind.RBrace) depth++;
                else if (k == TokenKind.LParen || k == TokenKind.LBracket || k == TokenKind.LBrace)
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        private static bool HasTopLevelComma(List<Token> tokens, int open, int close)
        {
            if (close == open + 1) return true; // ()
            int depth = 0;
            for (int i = open + 1; i < close; i++)
            {
                var k = tokens[i].Kind;
                if (k == TokenKind.LParen || k == TokenKind.LBracket || k == TokenKind.LBrace) depth++;
                else if (k == TokenKind.RParen || k == TokenKind.RBracket || k == TokenKind.RBrace) depth--;
                else if (k == TokenKind.Comma && depth == 0) return true;
            }
            return false;
        }
    }
}
