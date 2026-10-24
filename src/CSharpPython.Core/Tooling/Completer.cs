using System;
using System.Collections.Generic;
using System.Linq;
using CSharpPython.Syntax;

namespace CSharpPython.Editor
{
    public enum CompletionKind
    {
        Variable,
        Parameter,
        Function,
        Method,
        Constant,
        Class,
        Module,
        Keyword,
    }

    /// <summary>One suggestion offered at the cursor.</summary>
    public sealed class CompletionItem
    {
        public string Text { get; }
        public CompletionKind Kind { get; }

        /// <summary>A short description for a tooltip, such as a signature or the module it comes from.</summary>
        public string Detail { get; }

        public CompletionItem(string text, CompletionKind kind, string detail = null)
        {
            Text = text;
            Kind = kind;
            Detail = detail;
        }

        public override string ToString() => Text + " (" + Kind + ")";
    }

    /// <summary>The suggestions for a cursor position and the text they replace.</summary>
    public sealed class CompletionList
    {
        /// <summary>Offset of the first character of the word being completed.</summary>
        public int ReplaceStart { get; }

        /// <summary>The length of the part of the word before the cursor.</summary>
        public int ReplaceLength { get; }

        public IReadOnlyList<CompletionItem> Items { get; }

        public CompletionList(int replaceStart, int replaceLength, IReadOnlyList<CompletionItem> items)
        {
            ReplaceStart = replaceStart;
            ReplaceLength = replaceLength;
            Items = items;
        }
    }

    /// <summary>
    /// Auto-complete for a script editor. It works on half-typed code, offers names the script defines, the VM's
    /// built-ins and host modules, methods of values whose type is obvious, and module members after a dot.
    /// </summary>
    public static class Completer
    {
        private static readonly string[] StatementKeywords =
        {
            "def", "if", "elif", "else", "while", "for", "return", "break", "continue", "pass", "global",
            "try", "except", "raise", "import", "from",
        };

        private static readonly string[] ExpressionKeywords = { "and", "or", "not", "in", "is", "if", "else" };
        private static readonly string[] Constants = { "True", "False", "None" };

        private static readonly ValueKind[] MethodKinds = { ValueKind.Str, ValueKind.List, ValueKind.Dict, ValueKind.Tuple };

        /// <summary>
        /// Suggests completions at <paramref name="offset"/> in <paramref name="source"/>. Never throws. The list is
        /// empty inside strings and comments. <paramref name="vm"/> supplies built-ins and modules; without one a
        /// default VM is assumed.
        /// </summary>
        public static CompletionList Complete(string source, int offset, PyVm vm = null)
        {
            source = source ?? "";
            offset = Math.Max(0, Math.Min(offset, source.Length));
            vm = vm ?? EditorDefaults.Vm;

            int wordStart = offset;
            while (wordStart > 0 && IsIdentChar(source[wordStart - 1])) wordStart--;
            string prefix = source.Substring(wordStart, offset - wordStart);
            var none = new CompletionList(wordStart, prefix.Length, new CompletionItem[0]);

            if (prefix.Length > 0 && char.IsDigit(prefix[0])) return none;
            if (InsideStringOrComment(source, offset)) return none;
            if (prefix.Length == 0 && wordStart > 0 && ")]}'\"".IndexOf(source[wordStart - 1]) >= 0) return none;
            if (prefix.Length == 0 && wordStart > 0 && char.IsDigit(source[wordStart - 1])) return none;

            var model = new SourceModel(source);
            var before = Lexer.TokenizeTolerant(source.Substring(0, wordStart), out _);
            // The lexer ends the text with synthetic zero-length tokens; a real Newline (one with a length) means the
            // cursor is on a fresh line.
            while (before.Count > 0 && before[before.Count - 1].Length == 0) before.RemoveAt(before.Count - 1);
            var items = new List<CompletionItem>();
            var last = before.Count > 0 ? before[before.Count - 1] : (Token?)null;

            var line = CurrentLine(before);
            if (line.Count > 0 && line[0].Kind == TokenKind.KwImport && (last.Value.Kind == TokenKind.KwImport || last.Value.Kind == TokenKind.Comma))
                AddModules(vm, items);
            else if (line.Count > 0 && line[0].Kind == TokenKind.KwFrom)
                FromContext(line, vm, items);
            else if (last.HasValue && last.Value.Kind == TokenKind.Dot)
                AddMembers(model, before, before.Count - 1, offset, vm, items);
            else if (last.HasValue && (last.Value.Kind == TokenKind.KwDef || last.Value.Kind == TokenKind.KwAs || last.Value.Kind == TokenKind.KwFor))
            {
                // A new name is being introduced; there is nothing sensible to suggest.
            }
            else
                AddGeneral(model, vm, offset, wordStart, !last.HasValue || last.Value.Kind == TokenKind.Newline, items);

            var filtered = Filter(items, prefix);
            return new CompletionList(wordStart, prefix.Length, filtered);
        }

        // ---- Contexts -------------------------------------------------------------------------------------------

        private static void FromContext(List<Token> line, PyVm vm, List<CompletionItem> items)
        {
            int import = line.FindIndex(t => t.Kind == TokenKind.KwImport);
            if (import < 0)
            {
                // 'from' or 'from name': modules first, then the 'import' keyword once a module is named.
                if (line.Count == 1) AddModules(vm, items);
                else if (line.Count == 2 && line[1].Kind == TokenKind.Name) items.Add(new CompletionItem("import", CompletionKind.Keyword));
                return;
            }
            if (import != 2 || line[1].Kind != TokenKind.Name) return;
            var last = line[line.Count - 1].Kind;
            if (last != TokenKind.KwImport && last != TokenKind.Comma && last != TokenKind.LParen) return;
            if (vm.Modules.TryGetValue(line[1].Text, out var module)) AddModuleMembers(module, items);
        }

        private static void AddModules(PyVm vm, List<CompletionItem> items)
        {
            foreach (var name in vm.Modules.Keys)
                items.Add(new CompletionItem(name, CompletionKind.Module, "module"));
        }

        private static void AddModuleMembers(PyModule module, List<CompletionItem> items)
        {
            foreach (var kv in module.Members)
            {
                var v = kv.Value;
                switch (v.Kind)
                {
                    case ValueKind.Native:
                        items.Add(new CompletionItem(kv.Key, CompletionKind.Function, module.Name + "." + kv.Key + Arity((NativeFunction)v.RawObject, 0)));
                        break;
                    case ValueKind.Module:
                        items.Add(new CompletionItem(kv.Key, CompletionKind.Module, "module"));
                        break;
                    case ValueKind.ExceptionType:
                        items.Add(new CompletionItem(kv.Key, CompletionKind.Class, "exception"));
                        break;
                    default:
                        items.Add(new CompletionItem(kv.Key, CompletionKind.Constant, module.Name + "." + kv.Key + " = " + Short(v.Repr())));
                        break;
                }
            }
        }

        private static void AddMembers(SourceModel model, List<Token> before, int dot, int offset, PyVm vm, List<CompletionItem> items)
        {
            if (dot == 0) return;
            var p = before[dot - 1];
            ValueKind? type = null;
            switch (p.Kind)
            {
                case TokenKind.Name:
                    if (dot >= 2 && before[dot - 2].Kind == TokenKind.Dot) return; // a.b.c: unknown
                    type = model.TypeOf(p.Text, offset, out var module);
                    if (module != null)
                    {
                        if (vm.Modules.TryGetValue(module.Module, out var m)) AddModuleMembers(m, items);
                        return;
                    }
                    break;
                case TokenKind.String:
                case TokenKind.FString:
                    type = ValueKind.Str;
                    break;
                case TokenKind.RBracket:
                {
                    int open = SourceModel.MatchBackward(before, dot - 1);
                    if (open < 0) return;
                    // 'xs[0].' is a subscript, whose type is unknown; '[1, 2].' is a list literal.
                    if (!IsValueEnd(open > 0 ? before[open - 1].Kind : TokenKind.Newline)) type = ValueKind.List;
                    break;
                }
                case TokenKind.RBrace:
                    type = SourceModel.MatchBackward(before, dot - 1) >= 0 ? ValueKind.Dict : (ValueKind?)null;
                    break;
                case TokenKind.RParen:
                {
                    int open = SourceModel.MatchBackward(before, dot - 1);
                    if (open > 0 && before[open - 1].Kind == TokenKind.Name && !(open > 1 && before[open - 2].Kind == TokenKind.Dot))
                        type = SourceModel.ConstructorType(before[open - 1].Text);
                    else if (open == 0 || !IsValueEnd(before[open - 1].Kind))
                        type = SourceModel.InferTypeAt(before, open);
                    break;
                }
                default:
                    return;
            }

            if (type.HasValue)
            {
                AddMethods(type.Value, items);
                return;
            }
            // The type is not obvious: offer every method name once.
            var seen = new HashSet<string>();
            foreach (var kind in MethodKinds)
                foreach (var name in Methods.NamesOf(kind).OrderBy(n => n, StringComparer.Ordinal))
                    if (seen.Add(name)) items.Add(MethodItem(kind, name));
        }

        private static void AddMethods(ValueKind kind, List<CompletionItem> items)
        {
            foreach (var name in Methods.NamesOf(kind).OrderBy(n => n, StringComparer.Ordinal))
                items.Add(MethodItem(kind, name));
        }

        private static CompletionItem MethodItem(ValueKind kind, string name)
        {
            var fn = Methods.Find(kind, name);
            return new CompletionItem(name, CompletionKind.Method, kind.ToString().ToLowerInvariant() + "." + name + Arity(fn, 1));
        }

        private static void AddGeneral(SourceModel model, PyVm vm, int offset, int wordStart, bool statementStart, List<CompletionItem> items)
        {
            var seen = new HashSet<string>();
            foreach (var d in model.VisibleAt(offset))
            {
                if (d.TokenStart == wordStart) continue; // the word being typed defines itself
                if (!seen.Add(d.Name)) continue;
                switch (d.Kind)
                {
                    case DefinitionKind.Function:
                        items.Add(new CompletionItem(d.Name, CompletionKind.Function, "def " + d.Name + "(" + d.Signature + ")"));
                        break;
                    case DefinitionKind.Parameter:
                        items.Add(new CompletionItem(d.Name, CompletionKind.Parameter, "parameter"));
                        break;
                    case DefinitionKind.Module:
                        items.Add(new CompletionItem(d.Name, CompletionKind.Module, "module " + d.Module));
                        break;
                    case DefinitionKind.ImportedName:
                        items.Add(ImportedItem(d, vm));
                        break;
                    default:
                        items.Add(new CompletionItem(d.Name, CompletionKind.Variable, d.Type.HasValue ? d.Type.Value.ToString().ToLowerInvariant() : null));
                        break;
                }
            }

            foreach (var kv in vm.Builtins)
            {
                if (!seen.Add(kv.Key)) continue;
                switch (kv.Value.Kind)
                {
                    case ValueKind.Native:
                        items.Add(new CompletionItem(kv.Key, CompletionKind.Function, "built-in " + kv.Key + Arity((NativeFunction)kv.Value.RawObject, 0)));
                        break;
                    case ValueKind.ExceptionType:
                        items.Add(new CompletionItem(kv.Key, CompletionKind.Class, "exception"));
                        break;
                    case ValueKind.Module:
                        items.Add(new CompletionItem(kv.Key, CompletionKind.Module, "module"));
                        break;
                    default:
                        items.Add(new CompletionItem(kv.Key, CompletionKind.Constant, Short(kv.Value.Repr())));
                        break;
                }
            }

            foreach (var c in Constants)
                if (seen.Add(c)) items.Add(new CompletionItem(c, CompletionKind.Constant));
            foreach (var k in statementStart ? StatementKeywords : ExpressionKeywords)
                if (seen.Add(k)) items.Add(new CompletionItem(k, CompletionKind.Keyword));
        }

        private static CompletionItem ImportedItem(Definition d, PyVm vm)
        {
            if (vm.Modules.TryGetValue(d.Module, out var module) && module.Members.TryGetValue(d.Member, out var v))
            {
                if (v.Kind == ValueKind.Native)
                    return new CompletionItem(d.Name, CompletionKind.Function, d.Module + "." + d.Member + Arity((NativeFunction)v.RawObject, 0));
                if (v.Kind == ValueKind.ExceptionType) return new CompletionItem(d.Name, CompletionKind.Class, "exception");
                return new CompletionItem(d.Name, CompletionKind.Constant, d.Module + "." + d.Member);
            }
            return new CompletionItem(d.Name, CompletionKind.Variable, "from " + d.Module);
        }

        // ---- Helpers --------------------------------------------------------------------------------------------

        private static List<CompletionItem> Filter(List<CompletionItem> items, string prefix)
        {
            var result = new List<CompletionItem>();
            foreach (var item in items)
                if (item.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) result.Add(item);
            // Same-case matches first, then by kind (the script's own names before built-ins), then alphabetically.
            return result
                .OrderBy(i => i.Text.StartsWith(prefix, StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(i => Rank(i.Kind))
                .ThenBy(i => i.Text, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Text, StringComparer.Ordinal)
                .ToList();
        }

        private static int Rank(CompletionKind kind)
        {
            switch (kind)
            {
                case CompletionKind.Parameter: return 0;
                case CompletionKind.Variable: return 1;
                case CompletionKind.Method: return 2;
                case CompletionKind.Function: return 3;
                case CompletionKind.Module: return 4;
                case CompletionKind.Constant: return 5;
                case CompletionKind.Class: return 6;
                default: return 7;
            }
        }

        private static string Arity(NativeFunction fn, int selfCount)
        {
            int min = fn.MinArgs - selfCount;
            int max = fn.MaxArgs < 0 ? -1 : fn.MaxArgs - selfCount;
            if (max < 0) return "(" + (min <= 0 ? "..." : min + "+ args") + ")";
            if (min == max) return min == 0 ? "()" : "(" + min + (min == 1 ? " arg)" : " args)");
            return "(" + min + "-" + max + " args)";
        }

        private static string Short(string s) => s.Length <= 24 ? s : s.Substring(0, 21) + "...";

        private static bool IsIdentChar(char c) => c == '_' || char.IsLetterOrDigit(c);

        private static bool IsValueEnd(TokenKind k) =>
            k == TokenKind.Name || k == TokenKind.RParen || k == TokenKind.RBracket || k == TokenKind.RBrace ||
            k == TokenKind.String || k == TokenKind.FString || k == TokenKind.Int || k == TokenKind.Float ||
            k == TokenKind.KwTrue || k == TokenKind.KwFalse || k == TokenKind.KwNone;

        // The tokens of the logical line the cursor is on, up to the cursor.
        private static List<Token> CurrentLine(List<Token> before)
        {
            int start = before.Count;
            while (start > 0 && before[start - 1].Kind != TokenKind.Newline) start--;
            return before.GetRange(start, before.Count - start);
        }

        private static bool InsideStringOrComment(string source, int offset)
        {
            var spans = SyntaxHighlighter.Highlight(source.Substring(0, offset));
            if (spans.Count == 0) return false;
            var last = spans[spans.Count - 1];
            if (last.End != offset) return false;
            if (last.Kind == HighlightKind.Comment) return true;
            if (last.Kind == HighlightKind.Error && last.Start < offset && (source[last.Start] == '\'' || source[last.Start] == '"'))
                return true;
            return false;
        }
    }

    internal static class EditorDefaults
    {
        // Only read from, to learn the names of built-ins and standard modules.
        public static readonly PyVm Vm = new PyVm();
    }
}
