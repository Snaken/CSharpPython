using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // BlockEditor.Compile keeps its result until an edit could change the program, so a big graph is not compiled again
    // for a block that was only moved.
    public class BlockCompileCacheTests
    {
        private static BlockEditor Editor(out string printId, BlockLibrary library = null)
        {
            var editor = new BlockEditor(new BlockGraph().AddFunction(new BlockFunction("main")), library);
            printId = editor.AddNode("main", "call", "print").Node.Id;
            Assert.That(editor.SetLiteral("main", printId, "arg0", "1").Succeeded, Is.True);
            Assert.That(editor.SetStart("main", printId).Succeeded, Is.True);
            return editor;
        }

        [Test]
        public void AskingAgainReturnsTheSameResultWhileNothingChanges()
        {
            var editor = Editor(out _);
            var first = editor.Compile();
            int version = editor.ProgramVersion;
            Assert.That(first.Source, Does.Contain("print(1)"));
            Assert.That(editor.Compile(), Is.SameAs(first));
            Assert.That(editor.Compile(), Is.SameAs(first));
            Assert.That(editor.ProgramVersion, Is.EqualTo(version));
        }

        [Test]
        public void AnEditThatCouldChangeTheProgramGivesANewResult()
        {
            var editor = Editor(out var print);
            var before = editor.Compile();
            int version = editor.ProgramVersion;
            Assert.That(editor.SetLiteral("main", print, "arg0", "2").Succeeded, Is.True);
            Assert.That(editor.ProgramVersion, Is.Not.EqualTo(version));
            var after = editor.Compile();
            Assert.That(after, Is.Not.SameAs(before));
            Assert.That(after.Source, Does.Contain("print(2)"));
            Assert.That(before.Source, Does.Contain("print(1)"), "the earlier result still describes the earlier graph");
        }

        [Test]
        public void EditsThatOnlyChangeTheDrawingKeepTheResultButStillRaiseChanged()
        {
            var editor = Editor(out var print);
            var kept = editor.Compile();
            int version = editor.ProgramVersion;
            int raised = 0;
            editor.Changed += () => raised++;

            Assert.That(editor.MoveNode("main", print, 10, 20).Succeeded, Is.True);
            Assert.That(editor.SetCollapsed("main", print, true).Succeeded, Is.True);
            Assert.That(editor.AddComment("note", 0, 0, 50, 20).Succeeded, Is.True);
            Assert.That(editor.MoveComment(0, 5, 5).Succeeded, Is.True);
            Assert.That(editor.SetCommentText(0, "changed").Succeeded, Is.True);
            Assert.That(editor.AddGroup("g", print).Succeeded, Is.True);
            Assert.That(editor.RenameGroup(0, "renamed").Succeeded, Is.True);
            Assert.That(editor.SetGroupCollapsed(0, true).Succeeded, Is.True);

            Assert.That(raised, Is.EqualTo(8), "the view still hears about each edit");
            Assert.That(editor.ProgramVersion, Is.EqualTo(version));
            Assert.That(editor.Compile(), Is.SameAs(kept), "the generated Python does not depend on where a block is");
        }

        [Test]
        public void ARefusedEditChangesNothing()
        {
            var editor = Editor(out var print);
            var kept = editor.Compile();
            int version = editor.ProgramVersion;
            Assert.That(editor.MoveNode("main", print, float.NaN, 0).Succeeded, Is.False);
            Assert.That(editor.SetLiteral("main", print, "arg0", "not a literal").Succeeded, Is.False);
            Assert.That(editor.ProgramVersion, Is.EqualTo(version));
            Assert.That(editor.Compile(), Is.SameAs(kept));
        }

        [Test]
        public void UndoAndRedoCompileAgain()
        {
            var editor = Editor(out var print);
            Assert.That(editor.SetLiteral("main", print, "arg0", "2").Succeeded, Is.True);
            Assert.That(editor.Compile().Source, Does.Contain("print(2)"));
            Assert.That(editor.Undo().Succeeded, Is.True);
            Assert.That(editor.Compile().Source, Does.Contain("print(1)"));
            Assert.That(editor.Redo().Succeeded, Is.True);
            Assert.That(editor.Compile().Source, Does.Contain("print(2)"));
        }

        [Test]
        public void AChangeToTheLibraryCompilesAgain()
        {
            var library = new BlockLibrary();
            // A graph loaded from a file may name a function the host does not have yet (a placeholder).
            var main = new BlockFunction("main") { Start = "c" };
            main.Add(new BlockNode("c", "call", "game.explode"));
            var editor = new BlockEditor(new BlockGraph().AddFunction(main), library);
            var missing = editor.Compile();
            Assert.That(missing.Succeeded, Is.False, "the host does not have game.explode yet");
            Assert.That(editor.Compile(), Is.SameAs(missing));

            library.AddFunction("explode", new string[0], module: "game");
            var found = editor.Compile();
            Assert.That(found, Is.Not.SameAs(missing));
            Assert.That(found.Succeeded, Is.True);
        }

        [Test]
        public void ChangesMadeBehindTheEditorNeedInvalidateCompile()
        {
            var editor = Editor(out _);
            var before = editor.Compile();
            editor.Graph.AddVariable("score", "0");
            Assert.That(editor.Compile(), Is.SameAs(before), "the editor cannot see a change it did not make");
            editor.InvalidateCompile();
            var after = editor.Compile();
            Assert.That(after, Is.Not.SameAs(before));
            Assert.That(after.Source, Does.Contain("score = 0"));
        }

        [Test]
        public void AGroupOfEditsIsStillFollowedByAFreshCompile()
        {
            var editor = Editor(out var print);
            var before = editor.Compile();
            editor.BeginGroup();
            editor.SetLiteral("main", print, "arg0", "5");
            editor.MoveNode("main", print, 1, 1);
            editor.EndGroup();
            var after = editor.Compile();
            Assert.That(after, Is.Not.SameAs(before));
            Assert.That(after.Source, Does.Contain("print(5)"));
        }
    }
}
