using System;
using CSharpPython.Syntax;

namespace CSharpPython.Editor
{
    /// <summary>What an editor shows when the mouse rests on a name.</summary>
    public sealed class HoverInfo
    {
        /// <summary>The first line: a signature such as <c>def move(target, speed=1.0)</c> or a type such as <c>hp: int</c>.</summary>
        public string Label { get; }

        /// <summary>Where the name comes from: <c>script</c>, <c>built-in</c>, a module name, <c>parameter of f</c> and so on.</summary>
        public string Origin { get; }

        /// <summary>The docstring of a script function or class, or null.</summary>
        public string Documentation { get; }

        /// <summary>The hovered name's offset and length, for highlighting it.</summary>
        public int Offset { get; }

        public int Length { get; }

        public HoverInfo(string label, string origin, string documentation, int offset, int length)
        {
            Label = label;
            Origin = origin;
            Documentation = documentation;
            Offset = offset;
            Length = length;
        }

        /// <summary>The label, origin and documentation as plain text, ready for a tooltip.</summary>
        public string Text => Label + (Origin == null ? "" : "\n(" + Origin + ")") + (Documentation == null ? "" : "\n\n" + Documentation);

        public override string ToString() => Label;
    }

    /// <summary>Hover documentation for a script editor. Works on half-typed code and never throws.</summary>
    public static class Hover
    {
        /// <summary>
        /// Describes the name under <paramref name="offset"/>: a function or class (with its signature and docstring),
        /// a variable or parameter, an import, a built-in, a module function or a method of a value whose type is
        /// obvious. Returns null when the cursor is not on a name, or the name is unknown. <paramref name="vm"/>
        /// supplies built-ins and host modules; without one a default VM is assumed.
        /// </summary>
        public static HoverInfo Get(string source, int offset, PyVm vm = null)
        {
            source = source ?? "";
            offset = Math.Max(0, Math.Min(offset, source.Length));
            vm = vm ?? EditorDefaults.Vm;

            var model = new SourceModel(source);
            var tokens = model.Tokens;
            int index = -1;
            for (int i = 0; i < tokens.Count; i++)
                if (tokens[i].Kind == TokenKind.Name && tokens[i].Start <= offset && offset <= tokens[i].End) { index = i; break; }
            if (index < 0) return null;

            var t = tokens[index];
            string name = t.Text;
            bool member = index > 0 && tokens[index - 1].Kind == TokenKind.Dot;
            bool header = index > 0 && (tokens[index - 1].Kind == TokenKind.KwDef || tokens[index - 1].Kind == TokenKind.KwClass);

            if (!member && !(Navigator.KeywordCalleeOf(tokens, index) >= 0))
            {
                var def = model.Resolve(name, t.Start);
                if (def != null)
                {
                    var own = Own(model, def, t);
                    if (own != null) return own;
                }
            }
            if (header) return null;

            var before = tokens.GetRange(0, index + 1);
            if (!SignatureHelper.Describe(model, before, index, t.Start, vm, out _, out string origin, out string parameters)) return null;
            return new HoverInfo(name + "(" + parameters + ")", origin, null, t.Start, t.Length);
        }

        // Script definitions, which carry more than a signature (docstrings, variable types).
        private static HoverInfo Own(SourceModel model, Definition def, Token t)
        {
            switch (def.Kind)
            {
                case DefinitionKind.Function:
                    return new HoverInfo("def " + def.Name + "(" + (def.Signature ?? "") + ")", model.IsClassScope(def.Scope) ? "method of " + def.Scope.Name : "script", def.Doc, t.Start, t.Length);
                case DefinitionKind.Class:
                    return new HoverInfo("class " + def.Name + "(" + (def.Signature ?? "") + ")", "script", def.Doc, t.Start, t.Length);
                case DefinitionKind.Parameter:
                    return new HoverInfo(def.Name, "parameter of " + def.Scope.Name, null, t.Start, t.Length);
                case DefinitionKind.Variable:
                    string type = model.TypeOf(def.Name, t.Start, out _)?.ToString().ToLowerInvariant();
                    return new HoverInfo(type == null ? def.Name : def.Name + ": " + type, def.Scope == null ? "variable" : "variable in " + def.Scope.Name, null, t.Start, t.Length);
                case DefinitionKind.Module:
                    return new HoverInfo("import " + def.Module, "module", null, t.Start, t.Length);
                default:
                    return null;
            }
        }
    }
}
