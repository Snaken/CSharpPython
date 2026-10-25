using System;
using System.Collections.Generic;
using CSharpPython.Syntax;

namespace CSharpPython.Editor
{
    /// <summary>One occurrence of a name in the script.</summary>
    public sealed class ReferenceLocation
    {
        /// <summary>Zero-based offset of the name.</summary>
        public int Offset { get; }

        public int Length { get; }

        /// <summary>One-based line and column.</summary>
        public int Line { get; }

        public int Column { get; }

        /// <summary>True where the name is defined or assigned, false where it is only used.</summary>
        public bool IsDefinition { get; }

        public ReferenceLocation(int offset, int length, int line, int column, bool isDefinition)
        {
            Offset = offset;
            Length = length;
            Line = line;
            Column = column;
            IsDefinition = isDefinition;
        }

        public override string ToString() => Line + ":" + Column + (IsDefinition ? " (definition)" : "");
    }

    /// <summary>The outcome of <see cref="Refactoring.Rename"/>: the new text, or the reason nothing was changed.</summary>
    public sealed class RenameResult
    {
        public bool Success { get; }

        /// <summary>Why the rename was refused, or null.</summary>
        public string Error { get; }

        /// <summary>The script with the name replaced (the original text when nothing changed).</summary>
        public string NewSource { get; }

        /// <summary>The places that were changed, in source order.</summary>
        public IReadOnlyList<ReferenceLocation> Edits { get; }

        internal RenameResult(string error, string source, IReadOnlyList<ReferenceLocation> edits)
        {
            Success = error == null;
            Error = error;
            NewSource = source;
            Edits = edits;
        }
    }

    /// <summary>Find-all-references and rename for a script editor. Works on half-typed code and never throws.</summary>
    public static class Refactoring
    {
        /// <summary>
        /// Every place the name under <paramref name="offset"/> is defined or used, in source order. Attribute
        /// accesses (<c>obj.name</c>) are not included. Empty when the cursor is not on a name the script defines.
        /// </summary>
        public static IReadOnlyList<ReferenceLocation> FindReferences(string source, int offset)
        {
            return Analyze(source, offset, out _, out _, out var refs) ? refs : new List<ReferenceLocation>();
        }

        /// <summary>
        /// Renames the function, class, variable or parameter under <paramref name="offset"/> everywhere it is used.
        /// Refuses (with an explanation, changing nothing) for imported names, methods and class attributes, names
        /// declared <c>global</c>/<c>nonlocal</c>, an invalid new name, or a new name that is already in use.
        /// </summary>
        public static RenameResult Rename(string source, int offset, string newName)
        {
            source = source ?? "";
            if (!IsValidName(newName)) return Refuse(source, "'" + newName + "' is not a valid name.");
            if (!Analyze(source, offset, out var model, out var target, out var refs)) return Refuse(source, "There is no renamable name here.");
            if (target.Kind == DefinitionKind.Module || target.Kind == DefinitionKind.ImportedName)
                return Refuse(source, "An imported name cannot be renamed.");
            if (model.IsClassScope(target.Scope))
                return Refuse(source, "Methods and class attributes cannot be renamed safely.");
            if (target.Name == newName) return new RenameResult(null, source, new List<ReferenceLocation>());

            var tokens = model.Tokens;
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Kind != TokenKind.KwGlobal && tokens[i].Kind != TokenKind.KwNonlocal) continue;
                for (int j = i + 1; j < tokens.Count && tokens[j].Kind != TokenKind.Newline; j++)
                    if (tokens[j].Kind == TokenKind.Name && tokens[j].Text == target.Name)
                        return Refuse(source, "'" + target.Name + "' is used in a global or nonlocal statement; rename it by hand.");
            }

            if (target.Kind == DefinitionKind.Parameter && model.IsClassScope(OuterOf(model, target)))
                for (int i = 0; i < tokens.Count; i++)
                    if (tokens[i].Kind == TokenKind.Name && tokens[i].Text == target.Name && KeywordCallee(tokens, i) >= 0)
                        return Refuse(source, "'" + target.Name + "' is passed as a keyword argument somewhere; rename it by hand.");

            // The new name must not already mean something at any of the places that change.
            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t.Kind != TokenKind.Name || t.Text != newName || (i > 0 && tokens[i - 1].Kind == TokenKind.Dot)) continue;
                if (KeywordCallee(tokens, i) >= 0) continue;
                if (model.Resolve(newName, t.Start) == null || InScope(target, t.Start))
                    return Refuse(source, "'" + newName + "' is already in use.");
            }
            foreach (var d in model.Definitions)
                if (d.Name == newName && d.Scope == target.Scope) return Refuse(source, "'" + newName + "' is already in use.");

            var sb = new System.Text.StringBuilder(source);
            for (int i = refs.Count - 1; i >= 0; i--) sb.Remove(refs[i].Offset, refs[i].Length).Insert(refs[i].Offset, newName);
            return new RenameResult(null, sb.ToString(), refs);
        }

        // True when a use at 'offset' would see the renamed name, so a same-named thing there would be shadowed.
        private static bool InScope(Definition target, int offset)
        {
            return target.Scope == null || (target.Scope.Start <= offset && offset <= target.Scope.End);
        }

        private static FunctionScope OuterOf(SourceModel model, Definition parameter)
        {
            foreach (var d in model.Definitions)
                if (d.Kind == DefinitionKind.Function && NameStart(model, parameter.Scope) == d.TokenStart) return d.Scope;
            return null;
        }

        private static RenameResult Refuse(string source, string error) => new RenameResult(error, source, new List<ReferenceLocation>());

        private static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var tokens = Lexer.TokenizeTolerant(name, out var diagnostics);
            return diagnostics.Count == 0 && tokens.Count > 0 && tokens[0].Kind == TokenKind.Name && tokens[0].Text == name && tokens[0].End == name.Length;
        }

        // The offset of the name token right after a scope's 'def' or 'class'.
        private static int NameStart(SourceModel model, FunctionScope scope)
        {
            for (int i = 0; i + 1 < model.Tokens.Count; i++)
                if (model.Tokens[i].Start == scope.Start && (model.Tokens[i].Kind == TokenKind.KwDef || model.Tokens[i].Kind == TokenKind.KwClass)) return model.Tokens[i + 1].Start;
            return -1;
        }

        private static bool Analyze(string source, int offset, out SourceModel model, out Definition target, out List<ReferenceLocation> refs)
        {
            source = source ?? "";
            offset = Math.Max(0, Math.Min(offset, source.Length));
            model = new SourceModel(source);
            target = null;
            refs = new List<ReferenceLocation>();
            var tokens = model.Tokens;

            int index = -1;
            for (int i = 0; i < tokens.Count; i++)
                if (tokens[i].Kind == TokenKind.Name && tokens[i].Start <= offset && offset <= tokens[i].End) { index = i; break; }
            if (index < 0 || (index > 0 && tokens[index - 1].Kind == TokenKind.Dot)) return false;
            if (KeywordCallee(tokens, index) >= 0) return false;
            target = model.Resolve(tokens[index].Text, offset);
            if (target == null) return false;

            var importLine = new bool[tokens.Count];
            bool inImport = false, lineStart = true;
            for (int i = 0; i < tokens.Count; i++)
            {
                var k = tokens[i].Kind;
                if (k == TokenKind.Newline || k == TokenKind.Indent || k == TokenKind.Dedent) { lineStart = true; inImport = false; continue; }
                if (lineStart) inImport = k == TokenKind.KwImport || k == TokenKind.KwFrom;
                lineStart = false;
                importLine[i] = inImport;
            }

            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t.Kind != TokenKind.Name || t.Text != target.Name) continue;
                if (i > 0 && tokens[i - 1].Kind == TokenKind.Dot) continue;

                bool isDefinition = false;
                foreach (var d in model.Definitions)
                    if (d.TokenStart == t.Start && d.Name == target.Name && d.Scope == target.Scope) isDefinition = true;

                bool matches = isDefinition;
                int callee = KeywordCallee(tokens, i);
                if (!matches && callee >= 0)
                {
                    // 'f(name=1)' belongs to the parameter called name of f.
                    if (target.Kind == DefinitionKind.Parameter && tokens[callee].Kind == TokenKind.Name && (callee == 0 || tokens[callee - 1].Kind != TokenKind.Dot))
                    {
                        var fn = model.Resolve(tokens[callee].Text, t.Start);
                        matches = fn != null && fn.Kind == DefinitionKind.Function && fn.TokenStart == NameStart(model, target.Scope);
                    }
                }
                else if (!matches && !importLine[i])
                {
                    var d = model.Resolve(t.Text, t.Start);
                    matches = d != null && d.Name == target.Name && d.Scope == target.Scope;
                }
                if (matches) refs.Add(new ReferenceLocation(t.Start, t.Text.Length, t.Line, t.Column, isDefinition));
            }
            return true;
        }

        private static int KeywordCallee(List<Token> tokens, int index) => Navigator.KeywordCalleeOf(tokens, index);
    }
}
