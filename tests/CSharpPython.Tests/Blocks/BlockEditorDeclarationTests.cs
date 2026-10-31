using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // The host-independent editing rules (docs/node-view-design.md, step 5a): functions, parameters and global variables.
    public class BlockEditorDeclarationTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        // main calls add_two(x, y) and reads and sets the global 'score'; add_two reads its parameters.
        private static BlockGraph Graph()
        {
            var main = new BlockFunction("main") { Start = "c" };
            main.Add(N("c", "call", "add_two").Literal("arg0", "1").Connect("arg1", "g").Then("next", "s"));
            main.Add(N("g", "get", "score"));
            main.Add(N("s", "set", "score").Literal("value", "5"));
            var add = new BlockFunction("add_two", "x", "y") { Start = "r" };
            add.Add(N("r", "set", "total").Connect("value", "px"));
            add.Add(N("px", "get", "x"));
            add.Add(N("sc", "get", "score"));
            var shadow = new BlockFunction("shadow", "score") { Start = "q" };
            shadow.Add(N("q", "set", "score").Literal("value", "1"));
            return new BlockGraph().AddVariable("score", "0", "int").AddFunction(main).AddFunction(add).AddFunction(shadow);
        }

        private static (BlockEditor editor, int[] changes) Editor()
        {
            var editor = new BlockEditor(Graph(), new BlockLibrary());
            var changes = new int[1];
            editor.Changed += () => changes[0]++;
            return (editor, changes);
        }

        private static BlockNode Find(BlockEditor e, string id) => e.Graph.Functions.SelectMany(f => f.Nodes).First(n => n.Id == id);

        private static BlockFunction Fn(BlockEditor e, string name) => e.Graph.Functions.First(f => f.Name == name);

        private static string Json(BlockEditor e) => e.Graph.ToJson();

        // ---- Functions ----

        [Test]
        public void AddFunctionMakesAnEmptyFunctionWithParameters()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.AddFunction("on_hit", "damage").Succeeded, Is.True);
            var fn = Fn(editor, "on_hit");
            Assert.That(fn.Parameters, Is.EqualTo(new[] { "damage" }));
            Assert.That(fn.Nodes, Is.Empty);
            Assert.That(editor.AddFunction("plain").Succeeded, Is.True);
            Assert.That(changes[0], Is.EqualTo(2));
        }

        [Test]
        public void AddFunctionRefusesBadNamesDuplicatesAndLimits()
        {
            var (editor, changes) = Editor();
            string before = Json(editor);
            Assert.That(editor.AddFunction("main").Succeeded, Is.False, "taken");
            Assert.That(editor.AddFunction("").Succeeded, Is.False);
            Assert.That(editor.AddFunction(null).Succeeded, Is.False);
            Assert.That(editor.AddFunction("two words").Succeeded, Is.False);
            Assert.That(editor.AddFunction("1up").Succeeded, Is.False);
            Assert.That(editor.AddFunction("class").Succeeded, Is.False, "a Python keyword");
            Assert.That(editor.AddFunction("_b1").Succeeded, Is.False, "reserved by the compiler");
            Assert.That(editor.AddFunction("_join2").Succeeded, Is.False, "reserved by the compiler");
            Assert.That(editor.AddFunction("f", "a", "a").Succeeded, Is.False, "parameter twice");
            Assert.That(editor.AddFunction("f", "a b").Succeeded, Is.False);
            editor.Limits = new BlockLimits { MaxParameters = 1 };
            Assert.That(editor.AddFunction("f", "a", "b").Succeeded, Is.False);
            editor.Limits = new BlockLimits { MaxNameLength = 3 };
            Assert.That(editor.AddFunction("long_name").Succeeded, Is.False);
            editor.Limits = new BlockLimits { MaxFunctions = 3 };
            Assert.That(editor.AddFunction("fourth").Succeeded, Is.False);
            Assert.That(Json(editor), Is.EqualTo(before));
            Assert.That(changes[0], Is.EqualTo(0));
        }

        [Test]
        public void RenameFunctionRenamesItsCalls()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.RenameFunction("add_two", "sum").Succeeded, Is.True);
            Assert.That(Fn(editor, "sum").Parameters, Is.EqualTo(new[] { "x", "y" }));
            Assert.That(Find(editor, "c").Name, Is.EqualTo("sum"));
            Assert.That(changes[0], Is.EqualTo(1));
            Assert.That(BlockCompiler.Compile(editor.Graph, editor.Library).Source, Does.Contain("def sum("));
        }

        [Test]
        public void RenameFunctionRefusesBadRequests()
        {
            var (editor, changes) = Editor();
            string before = Json(editor);
            Assert.That(editor.RenameFunction("nope", "x").Succeeded, Is.False);
            Assert.That(editor.RenameFunction("main", "main").Succeeded, Is.False);
            Assert.That(editor.RenameFunction("main", "shadow").Succeeded, Is.False);
            Assert.That(editor.RenameFunction("main", "not valid").Succeeded, Is.False);
            Assert.That(Json(editor), Is.EqualTo(before));
            Assert.That(changes[0], Is.EqualTo(0));
        }

        [Test]
        public void RemoveFunctionTakesItsBlocksAndEditorEntriesAndLeavesCallsToCompileAsUnknown()
        {
            var (editor, changes) = Editor();
            editor.MoveNode("add_two", "r", 5, 5);
            Assert.That(editor.RemoveFunction("add_two").Succeeded, Is.True);
            Assert.That(editor.Graph.Functions.Any(f => f.Name == "add_two"), Is.False);
            Assert.That(editor.Graph.Editor.Nodes, Is.Empty);
            Assert.That(Find(editor, "c").Name, Is.EqualTo("add_two"));
            var result = BlockCompiler.Compile(editor.Graph, editor.Library);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(editor.RemoveFunction("add_two").Succeeded, Is.False);
            Assert.That(changes[0], Is.EqualTo(2), "the move and the removal");
        }

        // ---- Parameters ----

        [Test]
        public void AddParameterAppendsAndCallsGetAnEmptyPin()
        {
            var (editor, _) = Editor();
            Assert.That(editor.AddParameter("add_two", "z").Succeeded, Is.True);
            Assert.That(Fn(editor, "add_two").Parameters, Is.EqualTo(new[] { "x", "y", "z" }));
            Assert.That(editor.Connect("main", "c", "arg2", "g").Succeeded, Is.True, "the call has the new pin");
        }

        [Test]
        public void AddParameterRefusesBadRequests()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.AddParameter("nope", "z").Succeeded, Is.False);
            Assert.That(editor.AddParameter("add_two", "x").Succeeded, Is.False, "already there");
            Assert.That(editor.AddParameter("add_two", "not valid").Succeeded, Is.False);
            editor.Limits = new BlockLimits { MaxParameters = 2 };
            Assert.That(editor.AddParameter("add_two", "z").Succeeded, Is.False);
            Assert.That(changes[0], Is.EqualTo(0));
        }

        [Test]
        public void RemoveParameterMovesTheCallersPinsUp()
        {
            var (editor, changes) = Editor();
            editor.Graph.Editor.Wires.Add(new BlockWireBends { Node = "c", Pin = "arg1" });
            Assert.That(editor.RemoveParameter("add_two", "x").Succeeded, Is.True);
            Assert.That(Fn(editor, "add_two").Parameters, Is.EqualTo(new[] { "y" }));
            var inputs = Find(editor, "c").Inputs;
            Assert.That(inputs.Count, Is.EqualTo(1), "the literal for x is gone");
            Assert.That(inputs[0].Pin, Is.EqualTo("arg0"));
            Assert.That(inputs[0].From, Is.EqualTo("g"), "the wire for y moved up");
            Assert.That(editor.Graph.Editor.Wires.Single().Pin, Is.EqualTo("arg0"), "and so did its reroute points");
            Assert.That(changes[0], Is.EqualTo(1));
        }

        [Test]
        public void RemoveParameterRefusesAMissingOne()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.RemoveParameter("add_two", "q").Succeeded, Is.False);
            Assert.That(editor.RemoveParameter("nope", "x").Succeeded, Is.False);
            Assert.That(changes[0], Is.EqualTo(0));
        }

        [Test]
        public void RenameParameterRenamesTheBlocksThatUseIt()
        {
            var (editor, _) = Editor();
            Assert.That(editor.RenameParameter("add_two", "x", "first").Succeeded, Is.True);
            Assert.That(Fn(editor, "add_two").Parameters, Is.EqualTo(new[] { "first", "y" }));
            Assert.That(Find(editor, "px").Name, Is.EqualTo("first"));
            Assert.That(Find(editor, "sc").Name, Is.EqualTo("score"), "other names are left alone");
            Assert.That(BlockCompiler.Compile(editor.Graph, editor.Library).Source, Does.Contain("def add_two(first, y)"));
        }

        [Test]
        public void RenameParameterRefusesBadRequests()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.RenameParameter("add_two", "x", "y").Succeeded, Is.False, "taken");
            Assert.That(editor.RenameParameter("add_two", "x", "x").Succeeded, Is.False);
            Assert.That(editor.RenameParameter("add_two", "x", "not valid").Succeeded, Is.False);
            Assert.That(editor.RenameParameter("add_two", "q", "z").Succeeded, Is.False);
            Assert.That(editor.RenameParameter("nope", "x", "z").Succeeded, Is.False);
            Assert.That(changes[0], Is.EqualTo(0));
        }

        // ---- Variables ----

        [Test]
        public void AddVariableWithATypeAndAStartingValue()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.AddVariable("health", "100", "int").Succeeded, Is.True);
            Assert.That(editor.AddVariable("names", null, "list[str]").Succeeded, Is.True);
            Assert.That(editor.AddVariable("anything").Succeeded, Is.True);
            var health = editor.Graph.Variables.First(v => v.Name == "health");
            Assert.That(health.Initial, Is.EqualTo("100"));
            Assert.That(health.Type, Is.EqualTo("int"));
            Assert.That(changes[0], Is.EqualTo(3));
        }

        [Test]
        public void AddVariableRefusesBadRequests()
        {
            var (editor, changes) = Editor();
            string before = Json(editor);
            Assert.That(editor.AddVariable("score").Succeeded, Is.False, "taken");
            Assert.That(editor.AddVariable("not valid").Succeeded, Is.False);
            Assert.That(editor.AddVariable("v", null, "not a type[").Succeeded, Is.False);
            Assert.That(editor.AddVariable("v", null, "$T").Succeeded, Is.False, "a wildcard");
            Assert.That(editor.AddVariable("v", null, "none").Succeeded, Is.False);
            editor.Limits = new BlockLimits { MaxLiteralLength = 2 };
            Assert.That(editor.AddVariable("v", "12345").Succeeded, Is.False);
            editor.Limits = new BlockLimits { MaxTypeLength = 2 };
            Assert.That(editor.AddVariable("v", null, "list[str]").Succeeded, Is.False);
            editor.Limits = new BlockLimits { MaxVariables = 1 };
            Assert.That(editor.AddVariable("v").Succeeded, Is.False);
            Assert.That(Json(editor), Is.EqualTo(before));
            Assert.That(changes[0], Is.EqualTo(0));
        }

        [Test]
        public void RemoveVariableLeavesItsBlocksToCompileAsUnknown()
        {
            var (editor, changes) = Editor();
            Assert.That(BlockCompiler.Compile(editor.Graph, editor.Library).Succeeded, Is.True);
            Assert.That(editor.RemoveVariable("score").Succeeded, Is.True);
            Assert.That(editor.Graph.Variables, Is.Empty);
            Assert.That(Find(editor, "g").Name, Is.EqualTo("score"));
            Assert.That(editor.RemoveVariable("score").Succeeded, Is.False);
            Assert.That(changes[0], Is.EqualTo(1));
        }

        [Test]
        public void RenameVariableRenamesUsesExceptWhereAParameterHidesIt()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.RenameVariable("score", "points").Succeeded, Is.True);
            Assert.That(editor.Graph.Variables.Single().Name, Is.EqualTo("points"));
            Assert.That(Find(editor, "g").Name, Is.EqualTo("points"));
            Assert.That(Find(editor, "s").Name, Is.EqualTo("points"));
            Assert.That(Find(editor, "sc").Name, Is.EqualTo("points"));
            Assert.That(Find(editor, "q").Name, Is.EqualTo("score"), "'shadow' has a parameter called score");
            Assert.That(changes[0], Is.EqualTo(1));
        }

        [Test]
        public void RenameVariableRefusesBadRequests()
        {
            var (editor, changes) = Editor();
            editor.AddVariable("other");
            Assert.That(editor.RenameVariable("score", "other").Succeeded, Is.False, "taken");
            Assert.That(editor.RenameVariable("score", "score").Succeeded, Is.False);
            Assert.That(editor.RenameVariable("score", "not valid").Succeeded, Is.False);
            Assert.That(editor.RenameVariable("nope", "x").Succeeded, Is.False);
            Assert.That(changes[0], Is.EqualTo(1), "only the add");
        }

        [Test]
        public void SetVariableTypeAndInitial()
        {
            var (editor, changes) = Editor();
            Assert.That(editor.SetVariableType("score", "float").Succeeded, Is.True);
            Assert.That(editor.Graph.Variables.Single().Type, Is.EqualTo("float"));
            Assert.That(editor.SetVariableType("score", null).Succeeded, Is.True, "back to any");
            Assert.That(editor.SetVariableInitial("score", "7").Succeeded, Is.True);
            Assert.That(editor.Graph.Variables.Single().Initial, Is.EqualTo("7"));
            Assert.That(editor.SetVariableInitial("score", null).Succeeded, Is.True);
            Assert.That(changes[0], Is.EqualTo(4));
        }

        [Test]
        public void SetVariableTypeAndInitialRefuseBadRequests()
        {
            var (editor, changes) = Editor();
            string before = Json(editor);
            Assert.That(editor.SetVariableType("nope", "int").Succeeded, Is.False);
            Assert.That(editor.SetVariableType("score", "not a type[").Succeeded, Is.False);
            Assert.That(editor.SetVariableInitial("nope", "1").Succeeded, Is.False);
            editor.Limits = new BlockLimits { MaxLiteralLength = 2 };
            Assert.That(editor.SetVariableInitial("score", "12345").Succeeded, Is.False);
            Assert.That(Json(editor), Is.EqualTo(before));
            Assert.That(changes[0], Is.EqualTo(0));
        }
    }
}
