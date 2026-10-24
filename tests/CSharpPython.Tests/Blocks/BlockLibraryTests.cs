using System;
using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    public class BlockLibraryTests
    {
        [Test]
        public void BuiltInFunctionsExistInTheVm()
        {
            var vm = new PyVm();
            foreach (var f in new BlockLibrary().Functions.Where(f => f.Module == null))
                Assert.That(vm.Builtins.ContainsKey(f.Name), Is.True, f.Name);
        }

        [Test]
        public void CatalogueDescribesEveryBlock()
        {
            var lib = new BlockLibrary();
            Assert.That(lib.Blocks.Select(b => b.Name), Is.Unique);
            Assert.That(lib.Blocks.All(b => !string.IsNullOrEmpty(b.Title) && !string.IsNullOrEmpty(b.Category)), Is.True);
            Assert.That(lib.FindBlock("if").Kind, Is.EqualTo(BlockKind.Flow));
            Assert.That(lib.FindBlock("if").FlowPins, Is.EqualTo(new[] { "then", "else", "next" }));
            Assert.That(lib.FindBlock("add").Kind, Is.EqualTo(BlockKind.Value));
            Assert.That(lib.FindBlock("set").NeedsName, Is.True);
            Assert.That(lib.FindBlock("nope"), Is.Null);
            Assert.That(lib.FindBlock(null), Is.Null);
            Assert.That(lib.FindFunction("len").Pure, Is.True);
            Assert.That(lib.FindFunction("print").Pure, Is.False);
            Assert.That(lib.FindFunction(null), Is.Null);
            Assert.That(FunctionSpec.ArgPin(2), Is.EqualTo("arg2"));
        }

        [Test]
        public void ModelClassesCanBeBuiltFieldByFieldForSerializers()
        {
            var node = new BlockNode { Id = "a", Block = "set", Name = "x" };
            node.Inputs.Add(new BlockInput { Pin = "value", Literal = "1" });
            node.Flow.Add(new BlockLink { Pin = "next", To = "" });
            var fn = new BlockFunction { Name = "main", Start = "a" };
            fn.Nodes.Add(node);
            var graph = new BlockGraph();
            graph.Variables.Add(new BlockVariable { Name = "x", Initial = "0" });
            graph.Functions.Add(fn);
            Assert.That(BlockCompiler.Compile(graph).Source, Is.EqualTo("x = 0\n\ndef main():\n    global x\n    x = 1\n"));
        }

        [Test]
        public void HostFunctionsCanBeAddedAndReplaced()
        {
            var lib = new BlockLibrary().AddFunction("spawn", new[] { "kind", "x" }, module: "game");
            var spec = lib.FindFunction("game.spawn");
            Assert.That(spec.Module, Is.EqualTo("game"));
            Assert.That(spec.Parameters, Is.EqualTo(new[] { "kind", "x" }));
            lib.AddFunction("spawn", new[] { "kind" }, module: "game");
            Assert.That(lib.FindFunction("game.spawn").Parameters.Count, Is.EqualTo(1));
            Assert.That(lib.Functions.Count(f => f.Name == "spawn"), Is.EqualTo(1));
            lib.AddFunction("noargs", null);
            Assert.That(lib.FindFunction("noargs").Parameters, Is.Empty);
            Assert.Throws<ArgumentException>(() => lib.AddFunction("", null));
        }
    }
}
