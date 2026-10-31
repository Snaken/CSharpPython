using System;
using System.Collections.Generic;

namespace CSharpPython.Blocks
{
    /// <summary>
    /// The declared types of the graph's global variables (docs/node-view-design.md, decision 16). A variable with
    /// no <see cref="BlockVariable.Type"/> is <c>any</c>. A number, bool or string variable always has a value of
    /// its type; a list or handle variable may start as <c>None</c>.
    /// </summary>
    internal static class BlockVariableTypes
    {
        /// <summary>The declared type, <c>any</c> when there is none, or null (with the reason) when the declaration is not usable.</summary>
        public static PinType Declared(BlockVariable variable, out string problem)
        {
            problem = null;
            if (string.IsNullOrWhiteSpace(variable.Type)) return PinType.Any;
            try
            {
                var type = PinType.Parse(variable.Type);
                if (Mentions(type, t => t.IsVariable)) problem = "a variable cannot have a wildcard type like '" + variable.Type + "'";
                else if (Mentions(type, t => t.Equals(PinType.None))) problem = "a variable cannot have the type 'none'";
                return problem == null ? type : null;
            }
            catch (ArgumentException e)
            {
                problem = e.Message;
                return null;
            }
        }

        /// <summary>The types of the variables that have a usable declaration other than <c>any</c>, by name.</summary>
        public static Dictionary<string, PinType> Table(BlockGraph graph)
        {
            var table = new Dictionary<string, PinType>();
            foreach (var v in graph.Variables)
            {
                if (v == null || string.IsNullOrEmpty(v.Name) || table.ContainsKey(v.Name)) continue;
                var type = Declared(v, out _);
                if (type != null && !type.Equals(PinType.Any)) table[v.Name] = type;
            }
            return table;
        }

        /// <summary>True for the types whose values are always present: int, float, bool and str.</summary>
        public static bool IsScalar(PinType type) =>
            type.Equals(PinType.Int) || type.Equals(PinType.Float) || type.Equals(PinType.Bool) || type.Equals(PinType.Str);

        /// <summary>The Python literal a scalar variable starts with when none is given.</summary>
        public static string DefaultLiteral(PinType type) =>
            type.Equals(PinType.Int) ? "0" : type.Equals(PinType.Float) ? "0.0" : type.Equals(PinType.Bool) ? "False" : "\"\"";

        private static bool Mentions(PinType type, Func<PinType, bool> test) =>
            test(type) || (type.IsList && Mentions(type.Element, test));
    }
}
