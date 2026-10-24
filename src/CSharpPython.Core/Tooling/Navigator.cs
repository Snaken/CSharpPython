using System;
using System.Collections.Generic;
using CSharpPython.Syntax;

namespace CSharpPython.Editor
{
    /// <summary>Where a name is defined in the script.</summary>
    public sealed class DefinitionLocation
    {
        public string Name { get; }
        public DefinitionKind Kind { get; }

        /// <summary>Zero-based offset of the name at its definition.</summary>
        public int Offset { get; }

        public int Length { get; }

        /// <summary>One-based line and column of the name at its definition.</summary>
        public int Line { get; }

        public int Column { get; }

        public DefinitionLocation(string name, DefinitionKind kind, int offset, int line, int column)
        {
            Name = name;
            Kind = kind;
            Offset = offset;
            Length = name.Length;
            Line = line;
            Column = column;
        }

        public override string ToString() => Name + " (" + Kind + ") at " + Line + ":" + Column;
    }

    /// <summary>Go-to-definition for a script editor. Works on half-typed code and never throws.</summary>
    public static class Navigator
    {
        /// <summary>
        /// Finds where the name under <paramref name="offset"/> is defined in the script: a function, parameter,
        /// variable or import. Returns null when the cursor is not on a name, or the name is a built-in, a member
        /// of a module or object, a keyword argument, or not defined.
        /// </summary>
        public static DefinitionLocation FindDefinition(string source, int offset)
        {
            source = source ?? "";
            offset = Math.Max(0, Math.Min(offset, source.Length));

            var tokens = Lexer.TokenizeTolerant(source, out _);
            int index = -1;
            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t.Kind == TokenKind.Name && t.Start <= offset && offset <= t.End) { index = i; break; }
            }
            if (index < 0) return null;
            if (index > 0 && tokens[index - 1].Kind == TokenKind.Dot) return null;
            if (IsKeywordArgument(tokens, index)) return null;

            var model = new SourceModel(source);
            var def = model.Resolve(tokens[index].Text, offset);
            if (def == null) return null;
            return new DefinitionLocation(def.Name, def.Kind, def.TokenStart, LineOf(tokens, def.TokenStart), ColumnOf(tokens, def.TokenStart));
        }

        // 'f(name=1)': the name before '=' directly inside a call's parentheses labels an argument.
        private static bool IsKeywordArgument(List<Token> tokens, int index)
        {
            if (index + 1 >= tokens.Count || tokens[index + 1].Kind != TokenKind.Assign || index == 0) return false;
            var prev = tokens[index - 1].Kind;
            if (prev != TokenKind.LParen && prev != TokenKind.Comma) return false;
            int depth = 0;
            for (int i = index - 1; i >= 0; i--)
            {
                var k = tokens[i].Kind;
                if (k == TokenKind.RParen || k == TokenKind.RBracket || k == TokenKind.RBrace) depth++;
                else if (k == TokenKind.LBracket || k == TokenKind.LBrace) { if (depth > 0) depth--; else return false; }
                else if (k == TokenKind.LParen)
                {
                    if (depth > 0) depth--;
                    else return i > 0 && (tokens[i - 1].Kind == TokenKind.Name || tokens[i - 1].Kind == TokenKind.RParen) &&
                                !(i > 1 && tokens[i - 2].Kind == TokenKind.KwDef);
                }
                else if (k == TokenKind.Newline && depth == 0) return false;
            }
            return false;
        }

        private static int LineOf(List<Token> tokens, int offset)
        {
            foreach (var t in tokens) if (t.Start == offset && t.Kind == TokenKind.Name) return t.Line;
            return 1;
        }

        private static int ColumnOf(List<Token> tokens, int offset)
        {
            foreach (var t in tokens) if (t.Start == offset && t.Kind == TokenKind.Name) return t.Column;
            return 1;
        }
    }
}
