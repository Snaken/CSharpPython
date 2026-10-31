using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CSharpPython.Blocks
{
    public sealed partial class BlockEditor
    {
        // ---- Functions ----

        /// <summary>
        /// Adds a function with the given parameter names. An event handler is a function with the name of the event.
        /// Refused when the name or a parameter is not a valid name, when a function has the name already, or when a
        /// limit would be passed.
        /// </summary>
        public EditResult AddFunction(string name, params string[] parameters)
        {
            string problem = CheckNewFunctionName(name);
            if (problem != null) return EditResult.Refused(problem);
            var names = parameters ?? new string[0];
            if (names.Length > Limits.MaxParameters) return EditResult.Refused("A function may have at most " + Limits.MaxParameters + " parameters.");
            for (int i = 0; i < names.Length; i++)
            {
                problem = CheckName(names[i], "parameter");
                if (problem == null && Array.IndexOf(names, names[i]) != i) problem = "The parameter '" + names[i] + "' is listed twice.";
                if (problem != null) return EditResult.Refused(problem);
            }
            if (Graph.Functions.Count(f => f != null) >= Limits.MaxFunctions)
                return EditResult.Refused("The graph already has the most functions it may have (" + Limits.MaxFunctions + ").");

            Graph.AddFunction(new BlockFunction(name, names));
            return Made(EditResult.Done());
        }

        /// <summary>Renames a function and every <c>call</c> block that calls it.</summary>
        public EditResult RenameFunction(string name, string newName)
        {
            if (FindFunction(name) == null) return EditResult.Refused(NoFunction(name));
            if (name == newName) return EditResult.Refused("The function already has that name.");
            string problem = CheckNewFunctionName(newName);
            if (problem != null) return EditResult.Refused(problem);

            FindFunction(name).Name = newName;
            foreach (var call in Calls(name)) call.Name = newName;
            return Made(EditResult.Done());
        }

        /// <summary>
        /// Removes a function with its blocks and their editor entries. Blocks that call it stay, and compiling reports
        /// the call as unknown until the function is added again.
        /// </summary>
        public EditResult RemoveFunction(string name)
        {
            var fn = FindFunction(name);
            if (fn == null) return EditResult.Refused(NoFunction(name));
            Graph.Functions.Remove(fn);
            Graph.Editor.Prune(Graph);
            return Made(EditResult.Done());
        }

        /// <summary>Adds a parameter at the end. Blocks that call the function get a new, empty input pin.</summary>
        public EditResult AddParameter(string function, string parameter)
        {
            var fn = FindFunction(function);
            if (fn == null) return EditResult.Refused(NoFunction(function));
            string problem = CheckName(parameter, "parameter");
            if (problem == null && fn.Parameters.Contains(parameter)) problem = "The function '" + function + "' already has the parameter '" + parameter + "'.";
            if (problem == null && fn.Parameters.Count >= Limits.MaxParameters) problem = "A function may have at most " + Limits.MaxParameters + " parameters.";
            if (problem != null) return EditResult.Refused(problem);
            fn.Parameters.Add(parameter);
            return Made(EditResult.Done());
        }

        /// <summary>
        /// Removes a parameter. The wires and literals of the blocks that call the function move up to fill its place
        /// (the one for the removed parameter is dropped). Blocks that read the parameter stay, and compiling reports
        /// the variable as unknown.
        /// </summary>
        public EditResult RemoveParameter(string function, string parameter)
        {
            var fn = FindFunction(function);
            if (fn == null) return EditResult.Refused(NoFunction(function));
            int at = fn.Parameters.IndexOf(parameter);
            if (at < 0) return EditResult.Refused(NoParameter(function, parameter));

            fn.Parameters.RemoveAt(at);
            foreach (var call in Calls(function))
            {
                ClearInput(call, FunctionSpec.ArgPin(at));
                for (int i = at + 1; i <= fn.Parameters.Count; i++)
                    RenamePin(call, FunctionSpec.ArgPin(i), FunctionSpec.ArgPin(i - 1));
            }
            return Made(EditResult.Done());
        }

        /// <summary>Renames a parameter and the blocks inside the function that read or set it.</summary>
        public EditResult RenameParameter(string function, string parameter, string newName)
        {
            var fn = FindFunction(function);
            if (fn == null) return EditResult.Refused(NoFunction(function));
            int at = fn.Parameters.IndexOf(parameter);
            if (at < 0) return EditResult.Refused(NoParameter(function, parameter));
            if (parameter == newName) return EditResult.Refused("The parameter already has that name.");
            string problem = CheckName(newName, "parameter");
            if (problem == null && fn.Parameters.Contains(newName)) problem = "The function '" + function + "' already has the parameter '" + newName + "'.";
            if (problem != null) return EditResult.Refused(problem);

            fn.Parameters[at] = newName;
            RenameUses(fn, parameter, newName);
            return Made(EditResult.Done());
        }

        // ---- Variables ----

        /// <summary>
        /// Adds a global variable. <paramref name="initial"/> is one Python literal, or null for the default of its
        /// type; <paramref name="type"/> is a type such as <c>int</c> or <c>list[str]</c>, or null for <c>any</c>.
        /// </summary>
        public EditResult AddVariable(string name, string initial = null, string type = null)
        {
            string problem = CheckName(name, "variable");
            if (problem == null && Graph.Variables.Any(v => v != null && v.Name == name)) problem = "There is a variable '" + name + "' already.";
            if (problem == null) problem = CheckInitial(initial) ?? CheckType(type);
            if (problem == null && Graph.Variables.Count(v => v != null) >= Limits.MaxVariables)
                problem = "The graph already has the most variables it may have (" + Limits.MaxVariables + ").";
            if (problem != null) return EditResult.Refused(problem);

            Graph.AddVariable(name, initial, type);
            return Made(EditResult.Done());
        }

        /// <summary>Removes a global variable. Blocks that use it stay, and compiling reports the variable as unknown.</summary>
        public EditResult RemoveVariable(string name)
        {
            var variable = FindVariable(name);
            if (variable == null) return EditResult.Refused(NoVariable(name));
            Graph.Variables.Remove(variable);
            return Made(EditResult.Done());
        }

        /// <summary>
        /// Renames a global variable and the blocks that read or set it, in every function except those with a
        /// parameter of the old name (the parameter hides the variable there).
        /// </summary>
        public EditResult RenameVariable(string name, string newName)
        {
            var variable = FindVariable(name);
            if (variable == null) return EditResult.Refused(NoVariable(name));
            if (name == newName) return EditResult.Refused("The variable already has that name.");
            string problem = CheckName(newName, "variable");
            if (problem == null && FindVariable(newName) != null) problem = "There is a variable '" + newName + "' already.";
            if (problem != null) return EditResult.Refused(problem);

            variable.Name = newName;
            foreach (var fn in Graph.Functions.Where(f => f != null && !f.Parameters.Contains(name)))
                RenameUses(fn, name, newName);
            return Made(EditResult.Done());
        }

        /// <summary>Sets the declared type of a global variable, or clears it (null or empty) for <c>any</c>.</summary>
        public EditResult SetVariableType(string name, string type)
        {
            var variable = FindVariable(name);
            if (variable == null) return EditResult.Refused(NoVariable(name));
            string problem = CheckType(type);
            if (problem != null) return EditResult.Refused(problem);
            variable.Type = type;
            return Made(EditResult.Done());
        }

        /// <summary>Sets the starting value of a global variable (one Python literal), or clears it for the default of its type.</summary>
        public EditResult SetVariableInitial(string name, string initial)
        {
            var variable = FindVariable(name);
            if (variable == null) return EditResult.Refused(NoVariable(name));
            string problem = CheckInitial(initial);
            if (problem != null) return EditResult.Refused(problem);
            variable.Initial = initial;
            return Made(EditResult.Done());
        }

        // ---- Helpers ----

        private BlockVariable FindVariable(string name) =>
            name == null ? null : Graph.Variables.FirstOrDefault(v => v != null && v.Name == name);

        private static string NoVariable(string name) => "There is no variable '" + name + "'.";

        private static string NoParameter(string function, string parameter) => "The function '" + function + "' has no parameter '" + parameter + "'.";

        private IEnumerable<BlockNode> Calls(string function) =>
            Graph.Functions.Where(f => f != null).SelectMany(f => f.Nodes).Where(n => n != null && n.Block == "call" && n.Name == function);

        private string CheckName(string name, string what)
        {
            if (!Generator.IsIdentifier(name)) return "'" + name + "' is not a valid " + what + " name.";
            if (name.Length > Limits.MaxNameLength) return "A name may be at most " + Limits.MaxNameLength + " characters.";
            return null;
        }

        private string CheckNewFunctionName(string name)
        {
            string problem = CheckName(name, "function");
            if (problem == null && FindFunction(name) != null) problem = "There is a function '" + name + "' already.";
            return problem;
        }

        private string CheckInitial(string initial)
        {
            if (initial != null && initial.Length > Limits.MaxLiteralLength) return "A starting value may be at most " + Limits.MaxLiteralLength + " characters.";
            return null;
        }

        private string CheckType(string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return null;
            if (type.Length > Limits.MaxTypeLength) return "A type may be at most " + Limits.MaxTypeLength + " characters.";
            BlockVariableTypes.Declared(new BlockVariable("x", null, type), out string problem);
            return problem;
        }

        // The blocks that name a variable: reading, setting or changing it, and the loop blocks, which name theirs.
        private static bool NamesVariable(BlockNode node) =>
            node.Block == "get" || node.Block == "set" || node.Block == "change"
            || node.Block == "for_each" || node.Block == "for_range" || node.Block == "repeat";

        private static void RenameUses(BlockFunction fn, string name, string newName)
        {
            foreach (var node in fn.Nodes.Where(n => n != null && NamesVariable(n) && n.Name == name)) node.Name = newName;
        }

        // Moves what an input pin holds to another pin name, with the reroute points of its wire.
        private void RenamePin(BlockNode node, string from, string to)
        {
            foreach (var input in node.Inputs.Where(i => i != null && i.Pin == from)) input.Pin = to;
            foreach (var bends in Graph.Editor.Wires.Where(w => w != null && w.Node == node.Id && w.Pin == from)) bends.Pin = to;
        }
    }
}
