using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // The host-independent editing rules (docs/node-view-design.md, step 5a): searching for a block by text or by pin type.
    public class BlockEditorSearchTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        private static BlockEditor Editor()
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m =>
            {
                m.Native("say", (c, a) => Value.None, 1, 1).Describe("say", "text: str");
                m.Native("hp", (c, a) => Value.None, 0, 0).Describe("hp", "-> int");
            });
            var main = new BlockFunction("main") { Start = "s" };
            main.Add(N("s", "call", "game.say"));
            main.Add(N("v", "add").Literal("a", "1").Literal("b", "2"));
            main.Add(N("c", "comment", "note"));
            var graph = new BlockGraph().AddFunction(main).AddFunction(new BlockFunction("patrol", "speed"));
            return new BlockEditor(graph, new BlockLibrary().AddAllModules(vm));
        }

        // ---- Text ----

        [Test]
        public void SearchFindsBlocksAndFunctionsIgnoringCase()
        {
            var editor = Editor();
            var found = editor.Search("SAY");
            Assert.That(found.Any(c => c.Block == "call" && c.Name == "game.say"), Is.True);
            Assert.That(found.All(c => c.Pin == null), Is.True);
            Assert.That(editor.Search("add").Any(c => c.Block == "add"), Is.True);
        }

        [Test]
        public void SearchRanksAnExactTitleThenAPrefixThenTheRest()
        {
            var editor = Editor();
            var found = editor.Search("print");
            Assert.That(found[0].Title, Is.EqualTo("print"));
            var prefixed = editor.Search("pat");
            Assert.That(prefixed[0].Name, Is.EqualTo("patrol"));
        }

        [Test]
        public void SearchIncludesFunctionsOfTheGraphUnderTheirOwnHeading()
        {
            var patrol = Editor().Search("patrol").Single();
            Assert.That(patrol.Block, Is.EqualTo("call"));
            Assert.That(patrol.Category, Is.EqualTo("Graph"));
        }

        [Test]
        public void SearchMatchesTheModuleOrCategory()
        {
            var editor = Editor();
            var game = editor.Search("game");
            Assert.That(game.Select(c => c.Name), Is.EquivalentTo(new[] { "game.say", "game.hp" }));
        }

        [Test]
        public void SearchWithNoTextOffersEverythingAndMaxLimitsIt()
        {
            var editor = Editor();
            int all = editor.Search("", 100000).Count;
            Assert.That(all, Is.GreaterThan(20));
            Assert.That(editor.Search(null, 100000).Count, Is.EqualTo(all));
            Assert.That(editor.Search("", 5).Count, Is.EqualTo(5));
            Assert.That(editor.Search("", 0), Is.Empty);
            Assert.That(editor.Search("zzzz-not-a-block"), Is.Empty);
        }

        [Test]
        public void AChoiceCanBeAddedWithAddNode()
        {
            var editor = Editor();
            foreach (var choice in editor.Search("", 100000).Where(c => c.Block != "call" || c.Name != null))
            {
                var result = editor.AddNode("main", choice.Block, choice.Name);
                // Blocks that need a name are added without one and named later; none may be refused for its kind.
                Assert.That(result.Succeeded, Is.True, choice + ": " + result.Reason);
            }
        }

        // ---- By pin type ----

        [Test]
        public void DraggingFromAnOutputOffersBlocksThatTakeItsType()
        {
            var editor = Editor();
            var found = editor.SuggestForOutput("main", "v");
            Assert.That(found, Is.Not.Empty);
            Assert.That(found.Any(c => c.Name == "print" && c.Pin == "arg0"), Is.True, "print takes anything");
            Assert.That(found.Any(c => c.Name == "game.say"), Is.False, "an int does not go into a str pin");
            Assert.That(found.Any(c => c.Block == "add" && c.Pin != null), Is.True, "a built-in block is offered too");
            Assert.That(found.All(c => c.Pin != null), Is.True);
        }

        [Test]
        public void DraggingFromAnOutputWithNoValueOffersNothing()
        {
            var editor = Editor();
            Assert.That(editor.SuggestForOutput("main", "c"), Is.Empty, "a comment");
            Assert.That(editor.SuggestForOutput("main", "nope"), Is.Empty);
            Assert.That(editor.SuggestForOutput("nope", "v"), Is.Empty);
        }

        [Test]
        public void DraggingFromAnInputOffersBlocksThatGiveItsType()
        {
            var editor = Editor();
            var found = editor.SuggestForInput("main", "s", "arg0");
            Assert.That(found, Is.Not.Empty);
            Assert.That(found.Any(c => c.Name == "game.hp"), Is.False, "an int does not fit a str pin");
            Assert.That(found.Any(c => c.Block == "literal" || c.Block == "get" || c.Block == "choose"), Is.True);

            var number = editor.SuggestForInput("main", "v", "a");
            Assert.That(number.Any(c => c.Name == "game.hp"), Is.True);
        }

        [Test]
        public void DraggingFromAnInputThatDoesNotExistOffersNothing()
        {
            var editor = Editor();
            Assert.That(editor.SuggestForInput("main", "s", "nope"), Is.Empty);
            Assert.That(editor.SuggestForInput("main", "x", "arg0"), Is.Empty);
            Assert.That(editor.SuggestForInput("nope", "s", "arg0"), Is.Empty);
        }

        [Test]
        public void ADraggedInputOfAGraphFunctionTakesAnyValue()
        {
            var editor = Editor();
            editor.AddNode("main", "call", "patrol");
            string id = editor.Graph.Functions[0].Nodes.Last().Id;
            Assert.That(editor.SuggestForInput("main", id, "arg0").Any(c => c.Name == "game.hp"), Is.True);
        }

        [Test]
        public void TheIndexIsRebuiltWhenTheLibraryGrows()
        {
            var editor = Editor();
            int before = editor.SuggestForOutput("main", "v").Count;
            editor.Library.AddFunction("double_it", new[] { "n" });
            Assert.That(editor.SuggestForOutput("main", "v").Count, Is.GreaterThan(before));
            Assert.That(editor.Search("double").Any(c => c.Name == "double_it"), Is.True);
        }

        [Test]
        public void SearchChangesNothingAndRaisesNothing()
        {
            var editor = Editor();
            int raised = 0;
            editor.Changed += () => raised++;
            string before = editor.Graph.ToJson();
            editor.Search("a");
            editor.SuggestForOutput("main", "v");
            editor.SuggestForInput("main", "s", "arg0");
            Assert.That(editor.Graph.ToJson(), Is.EqualTo(before));
            Assert.That(raised, Is.EqualTo(0));
        }
    }
}
