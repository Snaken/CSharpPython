using System;
using System.Collections.Generic;
using System.Linq;
using CSharpPython.Syntax;

namespace CSharpPython.Editor
{
    /// <summary>The signature of the function being called at the cursor.</summary>
    public sealed class SignatureHelp
    {
        /// <summary>The function's name, without any module or object in front.</summary>
        public string Name { get; }

        /// <summary>The whole signature, such as <c>move(target, speed=1.0)</c>.</summary>
        public string Label { get; }

        /// <summary>The parameters as written, such as <c>speed=1.0</c> or <c>*args</c>.</summary>
        public IReadOnlyList<string> Parameters { get; }

        /// <summary>The index in <see cref="Parameters"/> of the parameter the cursor is on, or -1 for none.</summary>
        public int ActiveParameter { get; }

        /// <summary>Where it comes from: "script", "built-in", a module name, or a type such as "str method".</summary>
        public string Origin { get; }

        public SignatureHelp(string name, string label, IReadOnlyList<string> parameters, int activeParameter, string origin)
        {
            Name = name;
            Label = label;
            Parameters = parameters;
            ActiveParameter = activeParameter;
            Origin = origin;
        }

        public override string ToString() => Label;
    }

    /// <summary>
    /// Signature help for a script editor: the parameters of the function whose call the cursor is inside, and
    /// which one is being typed. Works on half-typed code and never throws.
    /// </summary>
    public static class SignatureHelper
    {
        private static readonly ValueKind[] MethodKinds = { ValueKind.Str, ValueKind.List, ValueKind.Dict, ValueKind.Tuple, ValueKind.Set };

        /// <summary>
        /// Returns the signature for the innermost call open at <paramref name="offset"/>, or null when the cursor
        /// is not inside a call or the function is unknown. <paramref name="vm"/> supplies built-ins and host
        /// modules; without one a default VM is assumed.
        /// </summary>
        public static SignatureHelp Get(string source, int offset, PyVm vm = null)
        {
            source = source ?? "";
            offset = Math.Max(0, Math.Min(offset, source.Length));
            vm = vm ?? EditorDefaults.Vm;

            var before = Lexer.TokenizeTolerant(source.Substring(0, offset), out _);
            while (before.Count > 0 && before[before.Count - 1].Length == 0) before.RemoveAt(before.Count - 1);
            if (!FindCall(before, out int open, out int commas, out string keyword)) return null;
            if (open == 0) return null;

            var model = new SourceModel(source);
            string name, origin, parameters;
            if (!Describe(model, before, open - 1, offset, vm, out name, out origin, out parameters)) return null;

            var list = SplitParameters(parameters);
            return new SignatureHelp(name, name + "(" + parameters + ")", list, Active(list, commas, keyword), origin);
        }

        // The innermost unclosed '(' of a call: its token index, how many top-level commas follow it, and the
        // keyword being typed (as in 'f(a, b=|'), if any.
        private static bool FindCall(List<Token> before, out int open, out int commas, out string keyword)
        {
            open = -1;
            commas = 0;
            keyword = null;
            int depth = 0;
            int argStart = -1;
            for (int i = before.Count - 1; i >= 0; i--)
            {
                switch (before[i].Kind)
                {
                    case TokenKind.RParen:
                    case TokenKind.RBracket:
                    case TokenKind.RBrace:
                        depth++;
                        break;
                    case TokenKind.LBracket:
                    case TokenKind.LBrace:
                        if (depth > 0) depth--;
                        else { commas = 0; argStart = -1; } // inside a list or dict literal passed as an argument
                        break;
                    case TokenKind.LParen:
                        if (depth > 0) { depth--; break; }
                        open = i;
                        if (argStart < 0) argStart = i + 1;
                        if (argStart + 1 < before.Count && before[argStart].Kind == TokenKind.Name && before[argStart + 1].Kind == TokenKind.Assign)
                            keyword = before[argStart].Text;
                        return true;
                    case TokenKind.Comma:
                        if (depth == 0)
                        {
                            commas++;
                            if (argStart < 0) argStart = i + 1;
                        }
                        break;
                    case TokenKind.Newline:
                        if (depth == 0) return false;
                        break;
                }
            }
            return false;
        }

        internal static bool Describe(SourceModel model, List<Token> before, int nameIndex, int offset, PyVm vm,
                                     out string name, out string origin, out string parameters)
        {
            name = origin = parameters = null;
            var t = before[nameIndex];
            if (t.Kind != TokenKind.Name) return false;
            name = t.Text;

            if (nameIndex >= 1 && before[nameIndex - 1].Kind == TokenKind.Dot)
            {
                if (!Completer.ResolveReceiver(model, before, nameIndex - 1, offset, out var module, out var type)) return false;
                if (module != null)
                    return ModuleMember(vm, module.Module, name, name, out origin, out parameters);
                if (type.HasValue) return Method(type.Value, name, out origin, out parameters);

                // The type is not obvious: use the method if only one type has one by this name.
                string method = name;
                var kinds = MethodKinds.Where(k => Methods.Find(k, method) != null).ToList();
                return kinds.Count == 1 && Method(kinds[0], name, out origin, out parameters);
            }

            if (nameIndex >= 1 && (before[nameIndex - 1].Kind == TokenKind.KwDef || before[nameIndex - 1].Kind == TokenKind.KwClass)) return false;

            var def = model.Resolve(name, offset);
            if (def != null)
            {
                switch (def.Kind)
                {
                    case DefinitionKind.Class:
                    case DefinitionKind.Function:
                        origin = "script";
                        parameters = def.Signature ?? "";
                        return true;
                    case DefinitionKind.ImportedName:
                        return ModuleMember(vm, def.Module, def.Member, name, out origin, out parameters);
                    default:
                        return false;
                }
            }

            if (vm.Builtins.TryGetValue(name, out var builtin))
            {
                if (builtin.Kind == ValueKind.Native)
                {
                    origin = "built-in";
                    parameters = Signatures.For((NativeFunction)builtin.RawObject, name, 0);
                    return true;
                }
                if (builtin.Kind == ValueKind.Type)
                {
                    origin = "built-in";
                    var constructor = ((PyType)builtin.RawObject).Constructor;
                    parameters = constructor == null ? "" : Signatures.For(constructor, name, 0);
                    return true;
                }
                if (builtin.Kind == ValueKind.ExceptionType)
                {
                    origin = "exception";
                    parameters = "*args";
                    return true;
                }
            }
            return false;
        }

        private static bool ModuleMember(PyVm vm, string module, string member, string name, out string origin, out string parameters)
        {
            origin = parameters = null;
            if (!vm.Modules.TryGetValue(module, out var m) || !m.Members.TryGetValue(member, out var v)) return false;
            origin = module;
            if (v.Kind == ValueKind.Native)
            {
                parameters = Signatures.For((NativeFunction)v.RawObject, module + "." + member, 0);
                return true;
            }
            if (v.Kind == ValueKind.ExceptionType)
            {
                parameters = "*args";
                return true;
            }
            return false;
        }

        private static bool Method(ValueKind kind, string name, out string origin, out string parameters)
        {
            origin = parameters = null;
            var fn = Methods.Find(kind, name);
            if (fn == null) return false;
            string typeName = kind.ToString().ToLowerInvariant();
            origin = typeName + " method";
            parameters = Signatures.For(fn, typeName + "." + name, 1);
            return true;
        }

        private static int Active(List<string> parameters, int commas, string keyword)
        {
            if (keyword != null)
            {
                int named = parameters.FindIndex(p => ParameterName(p) == keyword);
                if (named >= 0) return named;
                return parameters.FindIndex(p => p.StartsWith("**", StringComparison.Ordinal));
            }
            int varargs = parameters.FindIndex(p => p.Length > 1 && p[0] == '*' && p[1] != '*');
            if (varargs >= 0 && commas >= varargs) return varargs;
            return commas < parameters.Count && !parameters[commas].StartsWith("*", StringComparison.Ordinal) ? commas : -1;
        }

        private static string ParameterName(string parameter)
        {
            int end = parameter.IndexOf('=');
            return (end < 0 ? parameter : parameter.Substring(0, end)).Trim().TrimStart('*');
        }

        // Splits "a, b=(1, 2), *rest" at the commas that are not inside brackets or strings.
        internal static List<string> SplitParameters(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return result;
            var tokens = Lexer.TokenizeTolerant(text, out _);
            int depth = 0;
            int start = 0;
            foreach (var t in tokens)
            {
                if (t.Length == 0) continue;
                if (t.Kind == TokenKind.LParen || t.Kind == TokenKind.LBracket || t.Kind == TokenKind.LBrace) depth++;
                else if (t.Kind == TokenKind.RParen || t.Kind == TokenKind.RBracket || t.Kind == TokenKind.RBrace) depth--;
                else if (t.Kind == TokenKind.Comma && depth == 0)
                {
                    result.Add(text.Substring(start, t.Start - start).Trim());
                    start = t.End;
                }
            }
            result.Add(text.Substring(start).Trim());
            return result.Where(p => p.Length > 0).ToList();
        }
    }
}
