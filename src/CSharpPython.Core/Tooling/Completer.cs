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
            "def", "class", "if", "elif", "else", "while", "for", "return", "break", "continue", "pass", "global", "nonlocal",
            "try", "except", "finally", "raise", "import", "from", "del", "assert", "with", "yield",
        };

        private static readonly string[] ExpressionKeywords = { "and", "or", "not", "in", "is", "if", "else", "lambda" };
        private static readonly string[] Constants = { "True", "False", "None" };

        private static readonly ValueKind[] MethodKinds = { ValueKind.Str, ValueKind.List, ValueKind.Dict, ValueKind.Tuple, ValueKind.Set };

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
            if (line.Count > 0 && line[0].Kind == TokenKind.KwImport && (last.Value.Kind == TokenKind.KwImport || last.Value.Kind == TokenKind.Comma || last.Value.Kind == TokenKind.Dot))
                AddModules(vm, items, ModulePrefix(line));
            else if (line.Count > 0 && line[0].Kind == TokenKind.KwFrom)
                FromContext(line, vm, items);
            else if (last.HasValue && last.Value.Kind == TokenKind.Dot)
                AddMembers(model, before, before.Count - 1, offset, vm, items);
            else if (last.HasValue && (last.Value.Kind == TokenKind.KwDef || last.Value.Kind == TokenKind.KwClass || last.Value.Kind == TokenKind.KwAs || last.Value.Kind == TokenKind.KwFor))
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
                var lastKind = line[line.Count - 1].Kind;
                if (line.Count == 1 || lastKind == TokenKind.Dot) AddModules(vm, items, ModulePrefix(line));
                else if (lastKind == TokenKind.Name) items.Add(new CompletionItem("import", CompletionKind.Keyword));
                return;
            }
            string name = DottedName(line, 1, import);
            if (name == null) return;
            var last = line[line.Count - 1].Kind;
            if (last != TokenKind.KwImport && last != TokenKind.Comma && last != TokenKind.LParen) return;
            if (vm.Modules.TryGetValue(name, out var module)) AddModuleMembers(module, items);
        }

        // The text of tokens [from, to) when they are a.b.c, else null.
        private static string DottedName(List<Token> line, int from, int to)
        {
            if (to <= from || (to - from) % 2 == 0) return null;
            var sb = new System.Text.StringBuilder();
            for (int i = from; i < to; i++)
            {
                bool expectName = (i - from) % 2 == 0;
                if (line[i].Kind != (expectName ? TokenKind.Name : TokenKind.Dot)) return null;
                sb.Append(line[i].Text);
            }
            return sb.ToString();
        }

        // The package typed so far in "import a.b." or "from a.b.": "a.b", or "" when the line ends before a name.
        private static string ModulePrefix(List<Token> line)
        {
            int end = line.Count;
            if (end == 0 || line[end - 1].Kind != TokenKind.Dot) return "";
            int start = end - 1;
            while (start - 1 >= 0 && (line[start - 1].Kind == TokenKind.Name || line[start - 1].Kind == TokenKind.Dot)) start--;
            return DottedName(line, start, end - 1) ?? "";
        }

        // The modules whose names continue 'prefix' by one part: the top-level ones when the prefix is empty.
        private static void AddModules(PyVm vm, List<CompletionItem> items, string prefix = "")
        {
            string dotted = prefix.Length == 0 ? "" : prefix + ".";
            foreach (var name in vm.Modules.Keys)
                if (name.StartsWith(dotted, System.StringComparison.Ordinal) && name.IndexOf('.', dotted.Length) < 0 && name.Length > dotted.Length)
                    items.Add(new CompletionItem(name.Substring(dotted.Length), CompletionKind.Module, "module"));
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
            if (AddScriptMembers(model, before, dot, offset, vm, items)) return;
            if (!ResolveReceiver(model, before, dot, offset, out var module, out var type)) return;
            if (module != null)
            {
                if (vm.Modules.TryGetValue(module.Module, out var m)) AddModuleMembers(m, items);
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

        // Members of script classes: 'self.', 'super().', a variable assigned 'Name(...)' and the 'e' of 'except X as e'.
        // Returns false when the receiver is none of those, so the general rules apply.
        private static bool AddScriptMembers(SourceModel model, List<Token> before, int dot, int offset, PyVm vm, List<CompletionItem> items)
        {
            if (dot == 0) return false;
            var p = before[dot - 1];
            var members = new Dictionary<string, Definition>();
            var attributes = new Dictionary<string, string>();
            var outside = new List<string>();
            bool exception = false;

            if (p.Kind == TokenKind.RParen)
            {
                int open = SourceModel.MatchBackward(before, dot - 1);
                if (open < 1 || before[open - 1].Kind != TokenKind.Name || before[open - 1].Text != "super" || (open > 1 && before[open - 2].Kind == TokenKind.Dot)) return false;
                var cls = model.EnclosingClass(offset);
                if (cls == null) return false;
                model.ClassMembers(cls, members, attributes, outside, new HashSet<Definition>(), includeOwn: false);
            }
            else if (p.Kind == TokenKind.Name && !(dot >= 2 && before[dot - 2].Kind == TokenKind.Dot))
            {
                var def = model.Resolve(p.Text, offset);
                if (def == null) return false;
                if (def.Kind == DefinitionKind.Parameter && def.Name == "self")
                {
                    var cls = model.EnclosingClass(offset);
                    if (cls == null) return false;
                    model.ClassMembers(cls, members, attributes, outside, new HashSet<Definition>());
                }
                else if (def.Kind == DefinitionKind.Variable && (def.InstanceOf != null || def.IsExceptionVariable))
                {
                    var cls = def.InstanceOf == null ? null : model.Resolve(def.InstanceOf, def.TokenStart);
                    if (cls != null && cls.Kind == DefinitionKind.Class) model.ClassMembers(cls, members, attributes, outside, new HashSet<Definition>());
                    else if (def.IsExceptionVariable) exception = true;
                    else if (def.InstanceOf != null && vm.Builtins.TryGetValue(def.InstanceOf, out var builtin) && builtin.Kind == ValueKind.ExceptionType) exception = true;
                    else return false;
                }
                else return false;
            }
            else return false;

            foreach (var name in outside)
                if (vm.Builtins.TryGetValue(name, out var b) && b.Kind == ValueKind.ExceptionType) exception = true;
            if (exception) attributes["args"] = "attribute";

            foreach (var kv in members.OrderBy(m => m.Key, StringComparer.Ordinal))
            {
                var d = kv.Value;
                if (d.Kind == DefinitionKind.Function) items.Add(new CompletionItem(kv.Key, CompletionKind.Method, "def " + kv.Key + "(" + d.Signature + ")"));
                else if (d.Kind == DefinitionKind.Class) items.Add(new CompletionItem(kv.Key, CompletionKind.Class, "class " + kv.Key));
                else items.Add(new CompletionItem(kv.Key, CompletionKind.Variable, d.Type.HasValue ? d.Type.Value.ToString().ToLowerInvariant() : "attribute"));
            }
            foreach (var kv in attributes.OrderBy(m => m.Key, StringComparer.Ordinal))
                if (!members.ContainsKey(kv.Key)) items.Add(new CompletionItem(kv.Key, CompletionKind.Variable, kv.Value));
            return true;
        }

        // What is to the left of the dot at token index 'dot': an imported module, a value of an obvious type, or
        // something unknown. Returns false when nothing sensible can be said, such as for 'a.b.c'.
        internal static bool ResolveReceiver(SourceModel model, List<Token> before, int dot, int offset, out Definition module, out ValueKind? type)
        {
            module = null;
            type = null;
            if (dot == 0) return false;
            var p = before[dot - 1];
            switch (p.Kind)
            {
                case TokenKind.Name:
                    if (dot >= 2 && before[dot - 2].Kind == TokenKind.Dot) return false; // a.b.c: unknown
                    type = model.TypeOf(p.Text, offset, out module);
                    return true;
                case TokenKind.String:
                case TokenKind.FString:
                    type = ValueKind.Str;
                    return true;
                case TokenKind.RBracket:
                {
                    int open = SourceModel.MatchBackward(before, dot - 1);
                    if (open < 0) return false;
                    // 'xs[0].' is a subscript, whose type is unknown; '[1, 2].' is a list literal.
                    if (!IsValueEnd(open > 0 ? before[open - 1].Kind : TokenKind.Newline)) type = ValueKind.List;
                    return true;
                }
                case TokenKind.RBrace:
                {
                    int open = SourceModel.MatchBackward(before, dot - 1);
                    type = open >= 0 ? SourceModel.BraceKind(before, open, dot - 1) : (ValueKind?)null;
                    return true;
                }
                case TokenKind.RParen:
                {
                    int open = SourceModel.MatchBackward(before, dot - 1);
                    if (open > 0 && before[open - 1].Kind == TokenKind.Name && !(open > 1 && before[open - 2].Kind == TokenKind.Dot))
                        type = SourceModel.ConstructorType(before[open - 1].Text);
                    else if (open == 0 || !IsValueEnd(before[open - 1].Kind))
                        type = SourceModel.InferTypeAt(before, open);
                    return true;
                }
                default:
                    return false;
            }
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
                    case DefinitionKind.Class:
                        items.Add(new CompletionItem(d.Name, CompletionKind.Class, "class " + d.Name + (d.Signature != null ? "(" + d.Signature + ")" : "")));
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
                if (kv.Key[0] == '.' || !seen.Add(kv.Key)) continue; // the names a 'with' statement is rewritten with
                switch (kv.Value.Kind)
                {
                    case ValueKind.Native:
                        items.Add(new CompletionItem(kv.Key, CompletionKind.Function, "built-in " + kv.Key + Arity((NativeFunction)kv.Value.RawObject, 0)));
                        break;
                    case ValueKind.ExceptionType:
                        items.Add(new CompletionItem(kv.Key, CompletionKind.Class, "exception"));
                        break;
                    case ValueKind.Type:
                        items.Add(new CompletionItem(kv.Key, CompletionKind.Class, "type"));
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
        internal static List<Token> CurrentLine(List<Token> before)
        {
            int start = before.Count;
            while (start > 0 && before[start - 1].Kind != TokenKind.Newline) start--;
            return before.GetRange(start, before.Count - start);
        }

        internal static bool InsideStringOrComment(string source, int offset)
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
