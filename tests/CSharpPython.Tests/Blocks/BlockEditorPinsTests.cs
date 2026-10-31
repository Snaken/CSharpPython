using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // BlockEditor.PinsOf describes the pins the editing rules use, so a canvas can draw them.
    public class BlockEditorPinsTests
    {
        private static BlockEditor Editor()
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m =>
            {
                m.Native("say", (c, a) => Value.None, 1, 1).Describe("say", "text: str -> none");
                m.Native("hp", (c, a) => Value.None, 0, 0).Describe("hp", "-> int");
                m.Native("ray", (c, a) => Value.None, 1, 1).Describe("ray", "origin: str -> (hit: bool, point: str)");
            });
            var main = new BlockFunction("main") { Start = "s" };
            main.Add(new BlockNode("s", "call", "game.say"));
            main.Add(new BlockNode("h", "call", "game.hp"));
            main.Add(new BlockNode("r", "call", "game.ray"));
            main.Add(new BlockNode("a", "add").Literal("a", "1"));
            main.Add(new BlockNode("i", "if"));
            main.Add(new BlockNode("l", "list").Literal("item0", "1").Literal("item1", "2"));
            main.Add(new BlockNode("e", "for_each"));
            main.Add(new BlockNode("g", "call", "patrol"));
            main.Add(new BlockNode("u", "call", "nope"));
            main.Add(new BlockNode("c", "comment", "note"));
            var graph = new BlockGraph().AddFunction(main).AddFunction(new BlockFunction("patrol", "speed"));
            return new BlockEditor(graph, new BlockLibrary().AddAllModules(vm));
        }

        private static string[] Pins(BlockEditor editor, string id) =>
            editor.PinsOf("main", id).Select(p => p.ToString()).ToArray();

        [Test]
        public void AStatementCallHasAnEntryItsArgumentsAndAnExit() =>
            Assert.That(Pins(Editor(), "s"), Is.EqualTo(new[] { "FlowIn -", "Input arg0: str", "FlowOut next" }));

        [Test]
        public void ACallThatReturnsAValueHasAnOutput() =>
            Assert.That(Pins(Editor(), "h"), Is.EqualTo(new[] { "FlowIn -", "FlowOut next", "Output -: int" }),
                "a call is a step unless it is declared pure");

        [Test]
        public void ACallWithSeveralResultsHasANamedOutputForEach() =>
            Assert.That(Pins(Editor(), "r").Where(p => p.StartsWith("Output")), Is.EqualTo(new[] { "Output hit: bool", "Output point: str" }));

        [Test]
        public void AValueBlockHasNoFlowPins() =>
            Assert.That(Pins(Editor(), "a"), Is.EqualTo(new[] { "Input a: any", "Input b: any", "Output -: any" }));

        [Test]
        public void AControlBlockHasItsExitsAndNoValue() =>
            Assert.That(Pins(Editor(), "i"), Is.EqualTo(new[] { "FlowIn -", "Input cond: any", "FlowOut then", "FlowOut else", "FlowOut next" }));

        [Test]
        public void ALoopHasItsValueOutput() =>
            Assert.That(Pins(Editor(), "e").Last(), Is.EqualTo("Output -: any"));

        [Test]
        public void AListHasItsItemsAndOneFreePin()
        {
            var editor = Editor();
            Assert.That(Pins(editor, "l").Where(p => p.StartsWith("Input")), Is.EqualTo(new[] { "Input item0: any", "Input item1: any", "Input item2: any" }));
            Assert.That(Pins(editor, "l").Last(), Is.EqualTo("Output -: list[int]"));
        }

        [Test]
        public void ACallOfAGraphFunctionHasAPinForEachParameter() =>
            Assert.That(Pins(Editor(), "g"), Is.EqualTo(new[] { "FlowIn -", "Input arg0: any", "FlowOut next", "Output -: any" }));

        [Test]
        public void AMissingFunctionOrBlockHasNoPins()
        {
            var editor = Editor();
            Assert.That(editor.PinsOf("main", "zz"), Is.Empty);
            Assert.That(editor.PinsOf("zz", "s"), Is.Empty);
            Assert.That(Pins(editor, "u"), Is.Empty, "an unknown call is a placeholder");
        }

        [Test]
        public void EveryInputAndExitListedIsAcceptedByTheEditingRules()
        {
            var editor = Editor();
            foreach (var node in editor.Graph.Functions[0].Nodes.Where(n => n.Id != "u"))
                foreach (var pin in editor.PinsOf("main", node.Id))
                {
                    if (pin.Kind == BlockPinKind.Input)
                        Assert.That(editor.SetLiteral("main", node.Id, pin.Name, "None").Reason ?? "", Does.Not.Contain("has no"), node.Id + " " + pin);
                    if (pin.Kind == BlockPinKind.FlowOut)
                        Assert.That(editor.Link("main", node.Id, pin.Name, "s").Reason ?? "", Does.Not.Contain("no execution pin"), node.Id + " " + pin);
                }
        }

        [Test]
        public void PinsOfChangesNothingAndRaisesNothing()
        {
            var editor = Editor();
            int raised = 0;
            editor.Changed += () => raised++;
            string before = editor.Graph.ToJson();
            foreach (var node in editor.Graph.Functions[0].Nodes) editor.PinsOf("main", node.Id);
            Assert.That(editor.Graph.ToJson(), Is.EqualTo(before));
            Assert.That(raised, Is.EqualTo(0));
        }
    }
}
