using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // A graph that names a function or block the host does not have still loads, keeping the block and its wires
    // (docs/node-view-design.md, decision 18).
    public class BlockPlaceholderTests
    {
        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        private static BlockLibrary Library(bool withSpawn)
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m =>
            {
                if (withSpawn) m.Native("spawn", (c, a) => Value.None, 1, 1).Describe("spawn", "kind: str");
                m.Native("other", (c, a) => Value.None, 0, 0).Describe("other", "");
            });
            return new BlockLibrary().AddAllModules(vm);
        }

        private static BlockGraph Graph()
        {
            var f = new BlockFunction("main") { Start = "s" };
            f.Add(N("s", "call", "game.spawn").Literal("arg0", "\"orc\"").Then("p"));
            f.Add(N("p", "call", "print").Connect("arg0", "k"));
            f.Add(N("k", "call", "len").Connect("arg0", "t"));
            f.Add(N("t", "literal").Literal("value", "\"abc\""));
            return new BlockGraph().AddFunction(f);
        }

        [Test]
        public void AFunctionTheHostLacksIsAPlaceholder()
        {
            var found = BlockPlaceholders.Find(Graph(), Library(false));
            Assert.That(found, Has.Count.EqualTo(1));
            Assert.That(found[0].NodeId, Is.EqualTo("s"));
            Assert.That(found[0].Function, Is.EqualTo("main"));
            Assert.That(found[0].Block, Is.EqualTo("call"));
            Assert.That(found[0].Name, Is.EqualTo("game.spawn"));
            Assert.That(found[0].Reason, Does.Contain("'game.spawn'"));
        }

        [Test]
        public void NothingIsAPlaceholderWhenTheHostHasEverything()
        {
            Assert.That(BlockPlaceholders.Find(Graph(), Library(true)), Is.Empty);
            Assert.That(BlockPlaceholders.Find(Graph()).Select(p => p.NodeId), Is.EqualTo(new[] { "s" }), "built-ins are in the default library");
        }

        [Test]
        public void ABlockTypeTheHostLacksIsAPlaceholder()
        {
            var f = new BlockFunction("main") { Start = "a" };
            f.Add(N("a", "frobnicate"));
            var found = BlockPlaceholders.Find(new BlockGraph().AddFunction(f));
            Assert.That(found.Single().Block, Is.EqualTo("frobnicate"));
            Assert.That(found.Single().Name, Is.Null);
            Assert.That(found.Single().Reason, Does.Contain("block type 'frobnicate'"));
        }

        [Test]
        public void FunctionsOfTheGraphAreNotPlaceholders()
        {
            var main = new BlockFunction("main") { Start = "c" };
            main.Add(N("c", "call", "helper"));
            var g = new BlockGraph().AddFunction(main).AddFunction(new BlockFunction("helper"));
            Assert.That(BlockPlaceholders.Find(g), Is.Empty);
        }

        [Test]
        public void AnUnfinishedCallBlockIsNotAPlaceholder()
        {
            var f = new BlockFunction("main") { Start = "c" };
            f.Add(N("c", "call"));
            Assert.That(BlockPlaceholders.Find(new BlockGraph().AddFunction(f)), Is.Empty);
        }

        [Test]
        public void ThePlaceholderKeepsItsWiresThroughASaveAndLoad()
        {
            var back = BlockGraph.FromJson(Graph().ToJson());
            var node = back.Functions[0].Nodes.First(n => n.Id == "s");
            Assert.That(node.Name, Is.EqualTo("game.spawn"));
            Assert.That(node.Inputs.Single().Literal, Is.EqualTo("\"orc\""));
            Assert.That(node.Flow.Single().To, Is.EqualTo("p"));
            Assert.That(BlockPlaceholders.Find(back, Library(false)).Single().NodeId, Is.EqualTo("s"));
        }

        [Test]
        public void CompilingReportsThePlaceholderAndOnlyThat()
        {
            var r = BlockCompiler.Compile(Graph(), Library(false));
            Assert.That(r.Succeeded, Is.False);
            var error = r.Errors.Single();
            Assert.That(error.NodeId, Is.EqualTo("s"));
            Assert.That(error.Message, Does.Contain("unknown function 'game.spawn'").And.Contain("placeholder"));
            Assert.That(BlockCompiler.Compile(Graph(), Library(true)).Succeeded, Is.True, "the same graph compiles once the host has the function");
        }

        [Test]
        public void ThePlaceholdersNullArgumentIsRejected()
        {
            Assert.Throws<System.ArgumentNullException>(() => BlockPlaceholders.Find(null));
        }
    }
}
