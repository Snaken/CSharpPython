using System;

namespace CSharpPython.Blocks
{
    public sealed partial class BlockEditor
    {
        /// <summary>
        /// Builds, edits, compiles and saves a small graph in an editor of its own so that the .NET runtime compiles the
        /// block code now, not the first time the player opens the editor. Call it once while the game loads, after
        /// <see cref="PyVm.WarmUp"/> if you call both (this does not run the program). Where code is compiled as it is
        /// first used (the Unity editor, Mono and desktop .NET) the first block compile and the first editor cost about 20
        /// to 30 ms between them. It touches no graph, library or editor of yours, never throws, and may be called
        /// any number of times, from any thread. Under IL2CPP there is little to gain.
        /// </summary>
        public static void WarmUp()
        {
            try
            {
                var library = new BlockLibrary().AddAllModules(new PyVm());
                var function = new BlockFunction("main") { Start = "s" };
                function.Add(new BlockNode("s", "set", "n").Literal("value", "0").Then("next", "f"));
                function.Add(new BlockNode("f", "for_range", "i").Literal("stop", "5").Then("body", "t").Then("next", "p"));
                function.Add(new BlockNode("t", "if").Connect("cond", "g").Then("then", "c").Then("else", "d"));
                function.Add(new BlockNode("g", "gt").Connect("a", "iv").Literal("b", "2"));
                function.Add(new BlockNode("iv", "get", "i"));
                function.Add(new BlockNode("c", "change", "n").Literal("value", "1"));
                function.Add(new BlockNode("d", "change", "n").Literal("value", "2"));
                function.Add(new BlockNode("p", "call", "print").Connect("arg0", "nv"));
                function.Add(new BlockNode("nv", "get", "n"));
                var graph = BlockGraph.FromJson(new BlockGraph().AddFunction(function).ToJson());

                var editor = new BlockEditor(graph, library);
                editor.Compile();
                foreach (var node in function.Nodes) editor.PinsOf("main", node.Id);
                var added = editor.AddNode("main", "list", null, 10, 10);
                if (added.Node != null)
                {
                    editor.SetLiteral("main", added.Node.Id, "item0", "1");
                    editor.SuggestForInput("main", "p", "arg0");
                    editor.SuggestForOutput("main", added.Node.Id);
                }
                editor.Search("add", 10);
                editor.Undo();
                editor.Compile().DiagnosticsOf("p");
                graph.ToJson();
            }
            catch (Exception)
            {
                // A warm-up is only a speed-up; a failure leaves the first real use to do the work.
            }
        }
    }
}
