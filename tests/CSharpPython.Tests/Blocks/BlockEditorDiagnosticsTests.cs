using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // The host-independent editing rules (docs/node-view-design.md, step 5a): compiling, diagnostics and the running block.
    public class BlockEditorDiagnosticsTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        private static PyVm Vm()
        {
            var vm = new PyVm { Output = s => { } };
            vm.RegisterModule("game", m => m
                .Native("wait", (c, a) => { c.Wait((int)a[0].AsInt()); return Value.None; }, 1, 1)
                .Describe("wait", "ticks: int", latent: true));
            return vm;
        }

        private static BlockEditor Editor(PyVm vm)
        {
            var main = new BlockFunction("main") { Start = "a" };
            main.Add(N("a", "call", "print").Literal("arg0", "1").Then("next", "w"));
            main.Add(N("w", "call", "game.wait").Literal("arg0", "2").Then("next", "b"));
            main.Add(N("b", "call", "print").Literal("arg0", "2"));
            return new BlockEditor(new BlockGraph().AddFunction(main), new BlockLibrary().AddAllModules(vm));
        }

        [Test]
        public void CompileGivesTheSourceAndTheMapOfTheGraphAsItIsNow()
        {
            var editor = Editor(Vm());
            var first = editor.Compile();
            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.Source, Does.Contain("print(2)"));
            editor.SetLiteral("main", "b", "arg0", "7");
            Assert.That(editor.Compile().Source, Does.Contain("print(7)"));
            Assert.That(first.Source, Does.Contain("print(2)"), "an earlier result does not change");
        }

        [Test]
        public void CompileChangesNothingAndRaisesNothing()
        {
            var editor = Editor(Vm());
            int raised = 0;
            editor.Changed += () => raised++;
            string before = editor.Graph.ToJson();
            editor.Compile();
            editor.Placeholders();
            Assert.That(editor.Graph.ToJson(), Is.EqualTo(before));
            Assert.That(raised, Is.EqualTo(0));
            Assert.That(editor.CanUndo, Is.False);
        }

        [Test]
        public void ABrokenGraphReportsDiagnosticsOnTheBlocksAtFault()
        {
            var editor = Editor(Vm());
            editor.Disconnect("main", "w", "arg0");
            var result = editor.Compile();
            Assert.That(result.Succeeded, Is.False);
            var onWait = result.DiagnosticsOf("w");
            Assert.That(onWait, Is.Not.Empty);
            Assert.That(onWait[0].Severity, Is.EqualTo(BlockSeverity.Error));
            Assert.That(result.DiagnosticsOf("a"), Is.Empty);
            Assert.That(result.DiagnosticsOf("nope"), Is.Empty);
            Assert.That(result.DiagnosticsOf(null), Is.Empty);
        }

        [Test]
        public void DiagnosticsOfListsErrorsBeforeWarnings()
        {
            var editor = Editor(Vm());
            editor.AddNode("main", "add");
            var warned = editor.Compile().Diagnostics.FirstOrDefault(d => d.Severity == BlockSeverity.Warning);
            Assert.That(warned, Is.Not.Null, "a block connected to nothing is a warning");
            var all = editor.Compile().DiagnosticsOf(warned.NodeId);
            Assert.That(all.Select(d => d.Severity), Is.Ordered);
        }

        [Test]
        public void AWrongTypeTypedIntoAPinIsReported()
        {
            var editor = Editor(Vm());
            editor.SetLiteral("main", "w", "arg0", "\"two\"");
            var result = editor.Compile();
            Assert.That(result.Succeeded, Is.False, "a string typed into an int pin");
            Assert.That(result.DiagnosticsOf("w"), Is.Not.Empty);
        }

        [Test]
        public void PlaceholdersListBlocksThatNameSomethingTheLibraryLacks()
        {
            var editor = Editor(Vm());
            Assert.That(editor.Placeholders(), Is.Empty);
            var without = new BlockEditor(editor.Graph, new BlockLibrary());
            var missing = without.Placeholders().Single();
            Assert.That(missing.NodeId, Is.EqualTo("w"));
            Assert.That(missing.Name, Is.EqualTo("game.wait"));
            Assert.That(without.Compile().DiagnosticsOf("w").Any(d => d.Message.Contains("placeholder")), Is.True);
        }

        [Test]
        public void TheCompileOfTheRunSourceSaysWhichBlockTheTaskIsAt()
        {
            var vm = Vm();
            var editor = Editor(vm);
            var compiled = editor.Compile();
            var task = vm.Start(compiled.Source + "main()\n");
            Assert.That(task.Update(), Is.EqualTo(TaskState.Waiting));
            Assert.That(compiled.Map.Running(task), Is.EqualTo("w"));

            editor.RemoveNode("main", "a");
            Assert.That(compiled.Map.Running(task), Is.EqualTo("w"), "the kept result still describes the task that is running");
        }

        [Test]
        public void ADiagnosticFollowsAnEditAndGoesWhenItIsFixedOrUndone()
        {
            var editor = Editor(Vm());
            editor.Disconnect("main", "w", "arg0");
            Assert.That(editor.Compile().DiagnosticsOf("w"), Is.Not.Empty);
            editor.SetLiteral("main", "w", "arg0", "3");
            Assert.That(editor.Compile().DiagnosticsOf("w"), Is.Empty);
            editor.Undo();
            Assert.That(editor.Compile().DiagnosticsOf("w"), Is.Not.Empty);
        }
    }
}
