using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpPython
{
    /// <summary>
    /// The type of a pin or parameter in a host function description: <c>any</c>, <c>int</c>, <c>float</c>,
    /// <c>bool</c>, <c>str</c>, <c>none</c>, a list with an element type (<c>list[int]</c>), a type variable
    /// (<c>$T</c>), or the name of a host handle type (<c>Entity</c>). Whether two types fit together is decided
    /// by <see cref="PinCompatibility"/>.
    /// </summary>
    public sealed class PinType : IEquatable<PinType>
    {
        public static readonly PinType Any = new PinType("any", null);
        public static readonly PinType Int = new PinType("int", null);
        public static readonly PinType Float = new PinType("float", null);
        public static readonly PinType Bool = new PinType("bool", null);
        public static readonly PinType Str = new PinType("str", null);
        public static readonly PinType None = new PinType("none", null);

        /// <summary>The type name: <c>list</c> for a list, otherwise a built-in name or a host handle type.</summary>
        public string Name { get; }

        /// <summary>The element type of a list, otherwise null.</summary>
        public PinType Element { get; }

        public bool IsList => Element != null;

        /// <summary>
        /// True for a type variable such as <c>$T</c>, a wildcard that stands for whichever type the wires of the node
        /// give it (see <c>BlockTypeChecker</c>). Written <c>$T</c> in a description.
        /// </summary>
        public bool IsVariable => Name.Length > 0 && Name[0] == '$';

        private PinType(string name, PinType element)
        {
            Name = name;
            Element = element;
        }

        public static PinType ListOf(PinType element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            return new PinType("list", element);
        }

        /// <summary>A type variable: <c>Variable("T")</c> is written <c>$T</c>.</summary>
        public static PinType Variable(string name)
        {
            if (name == null || name.Length == 0 || !IsName(name)) throw new ArgumentException("Bad type variable name '" + name + "'.");
            return new PinType("$" + name, null);
        }

        /// <summary>A host handle type such as <c>Character</c>.</summary>
        public static PinType Handle(string name) => Named(name);

        /// <summary>Reads a type such as <c>int</c>, <c>list[str]</c> or <c>Entity</c>. Throws <see cref="ArgumentException"/> when malformed.</summary>
        public static PinType Parse(string text)
        {
            string t = text == null ? "" : text.Trim();
            if (t.Length == 0) throw new ArgumentException("Empty type name.");
            int open = t.IndexOf('[');
            if (open >= 0)
            {
                if (t[t.Length - 1] != ']' || t.Substring(0, open).Trim() != "list")
                    throw new ArgumentException("Bad type '" + t + "': only list can take an element type, as list[int].");
                return ListOf(Parse(t.Substring(open + 1, t.Length - open - 2)));
            }
            if (t == "list") return ListOf(Any);
            return Named(t);
        }

        private static PinType Named(string name)
        {
            if (name.Length > 1 && name[0] == '$') return Variable(name.Substring(1));
            if (name.Length == 0 || !IsName(name)) throw new ArgumentException("Bad type name '" + name + "'.");
            switch (name)
            {
                case "any": return Any;
                case "int": return Int;
                case "float": return Float;
                case "bool": return Bool;
                case "str": return Str;
                case "none": return None;
                default: return new PinType(name, null);
            }
        }

        private static bool IsName(string s)
        {
            if (!(char.IsLetter(s[0]) || s[0] == '_')) return false;
            foreach (char c in s)
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '.')) return false;
            return true;
        }

        public bool Equals(PinType other) =>
            other != null && Name == other.Name && Equals(Element, other.Element);

        public override bool Equals(object obj) => Equals(obj as PinType);

        public override int GetHashCode() => Name.GetHashCode() * 31 + (Element == null ? 0 : Element.GetHashCode());

        public override string ToString() => IsList ? "list[" + Element + "]" : Name;
    }

    /// <summary>How a parameter takes its arguments.</summary>
    public enum ParameterKind
    {
        Normal,
        /// <summary><c>*args</c>.</summary>
        VarArgs,
        /// <summary><c>**kwargs</c>.</summary>
        VarKeywords,
        /// <summary>A bare <c>/</c> or <c>*</c> in the list. It takes no argument.</summary>
        Marker,
    }

    /// <summary>One parameter in a host function description.</summary>
    public sealed class HostParameter
    {
        public string Name { get; }
        public PinType Type { get; }
        public ParameterKind Kind { get; }

        /// <summary>The default value as written (<c>1</c>, <c>"x"</c>), or null when the parameter is required.</summary>
        public string Default { get; }

        public bool HasDefault => Default != null;

        internal HostParameter(string name, PinType type, ParameterKind kind, string defaultText)
        {
            Name = name;
            Type = type;
            Kind = kind;
            Default = defaultText;
        }
    }

    /// <summary>One result of a host function description. A single result has no name.</summary>
    public sealed class HostOutput
    {
        public string Name { get; }
        public PinType Type { get; }

        internal HostOutput(string name, PinType type)
        {
            Name = name;
            Type = type;
        }
    }

    /// <summary>
    /// A host function description with types, read from a string such as
    /// <c>"kind: str, count: int = 1 -&gt; Entity"</c> or <c>"origin: Vec -&gt; (hit: bool, point: Vec)"</c>. A
    /// parameter without a type is <see cref="PinType.Any"/>, and a string with no <c>-&gt;</c> declares no result,
    /// so every description written before types existed still reads.
    /// </summary>
    public sealed class HostSignature
    {
        private static readonly HostOutput[] NoOutputs = new HostOutput[0];

        public IReadOnlyList<HostParameter> Parameters { get; }

        /// <summary>The results, in order. Empty when the description has no <c>-&gt;</c>; see <see cref="HasResult"/>.</summary>
        public IReadOnlyList<HostOutput> Outputs { get; }

        /// <summary>True when the description declares a result (even <c>-&gt; none</c>).</summary>
        public bool HasResult { get; }

        /// <summary>The parameter list without types or the result, as signature help shows it: <c>"kind, count=1"</c>.</summary>
        public string ParameterText { get; }

        private HostSignature(IReadOnlyList<HostParameter> parameters, IReadOnlyList<HostOutput> outputs, bool hasResult, string parameterText)
        {
            Parameters = parameters;
            Outputs = outputs;
            HasResult = hasResult;
            ParameterText = parameterText;
        }

        /// <summary>Reads a description. Throws <see cref="ArgumentException"/> when a type or result is malformed.</summary>
        public static HostSignature Parse(string signature)
        {
            string text = signature ?? "";
            int arrow = FindArrow(text);
            string paramText = arrow < 0 ? text : text.Substring(0, arrow);

            var parameters = new List<HostParameter>();
            var plain = new List<string>();
            bool typed = false;
            foreach (string raw in Split(paramText))
            {
                string part = raw.Trim();
                if (part.Length == 0) continue;
                if (part == "/" || part == "*")
                {
                    parameters.Add(new HostParameter(part, PinType.Any, ParameterKind.Marker, null));
                    plain.Add(part);
                    continue;
                }

                string prefix = "";
                var kind = ParameterKind.Normal;
                if (part.StartsWith("**", StringComparison.Ordinal)) { prefix = "**"; kind = ParameterKind.VarKeywords; }
                else if (part[0] == '*') { prefix = "*"; kind = ParameterKind.VarArgs; }
                part = part.Substring(prefix.Length);

                string defaultText = null;
                int eq = IndexOfTopLevel(part, '=');
                if (eq >= 0)
                {
                    defaultText = part.Substring(eq + 1).Trim();
                    part = part.Substring(0, eq);
                }

                string name = part.Trim();
                var type = PinType.Any;
                int colon = IndexOfTopLevel(part, ':');
                if (colon >= 0)
                {
                    name = part.Substring(0, colon).Trim();
                    type = PinType.Parse(part.Substring(colon + 1));
                    typed = true;
                }
                parameters.Add(new HostParameter(name, type, kind, defaultText));
                plain.Add(prefix + name + (defaultText == null ? "" : "=" + defaultText));
            }

            IReadOnlyList<HostOutput> outputs = NoOutputs;
            if (arrow >= 0) outputs = ParseResult(text.Substring(arrow + 2));

            // A description with no types is shown exactly as the host wrote it.
            string parameterText = typed || arrow >= 0 ? string.Join(", ", plain) : paramText;
            return new HostSignature(parameters, outputs, arrow >= 0, parameterText);
        }

        private static IReadOnlyList<HostOutput> ParseResult(string text)
        {
            string t = text.Trim();
            if (t.Length == 0) throw new ArgumentException("Nothing after '->'.");
            if (t[0] != '(') return new[] { new HostOutput(null, PinType.Parse(t)) };
            if (t[t.Length - 1] != ')') throw new ArgumentException("Unclosed '(' in the result '" + t + "'.");

            var outputs = new List<HostOutput>();
            var names = new HashSet<string>();
            foreach (string raw in Split(t.Substring(1, t.Length - 2)))
            {
                string part = raw.Trim();
                int colon = IndexOfTopLevel(part, ':');
                if (colon < 0) throw new ArgumentException("Result '" + part + "' needs a name and a type, as name: type.");
                string name = part.Substring(0, colon).Trim();
                if (name.Length == 0 || !names.Add(name)) throw new ArgumentException("Result name '" + name + "' is empty or repeated.");
                outputs.Add(new HostOutput(name, PinType.Parse(part.Substring(colon + 1))));
            }
            if (outputs.Count == 0) throw new ArgumentException("An empty result list.");
            return outputs;
        }

        // The index of the first "->" outside quotes and brackets, or -1.
        private static int FindArrow(string s)
        {
            int depth = 0;
            char quote = '\0';
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (quote != '\0')
                {
                    if (c == '\\') i++;
                    else if (c == quote) quote = '\0';
                }
                else if (c == '"' || c == '\'') quote = c;
                else if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') depth--;
                else if (c == '-' && depth == 0 && i + 1 < s.Length && s[i + 1] == '>') return i;
            }
            return -1;
        }

        private static int IndexOfTopLevel(string s, char target)
        {
            int depth = 0;
            char quote = '\0';
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (quote != '\0')
                {
                    if (c == '\\') i++;
                    else if (c == quote) quote = '\0';
                }
                else if (c == '"' || c == '\'') quote = c;
                else if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') depth--;
                else if (c == target && depth == 0) return i;
            }
            return -1;
        }

        // Splits on commas outside quotes and brackets.
        private static List<string> Split(string s)
        {
            var parts = new List<string>();
            var current = new StringBuilder();
            int depth = 0;
            char quote = '\0';
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (quote != '\0')
                {
                    current.Append(c);
                    if (c == '\\' && i + 1 < s.Length) current.Append(s[++i]);
                    else if (c == quote) quote = '\0';
                    continue;
                }
                if (c == '"' || c == '\'') quote = c;
                else if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') depth--;
                else if (c == ',' && depth == 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                    continue;
                }
                current.Append(c);
            }
            parts.Add(current.ToString());
            return parts;
        }
    }
}
