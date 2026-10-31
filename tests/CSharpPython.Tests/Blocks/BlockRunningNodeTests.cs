using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    // The map from generated lines to blocks says which block a running task is at (docs/node-view-design.md, decision 15).
    public class BlockRunningNodeTests
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

        // main: print 1, call helper (h), print 2.   helper: wait (hw), print 3.
        private static BlockGraph Graph()
        {
            var main = new BlockFunction("main") { Start = "a" };
            main.Add(N("a", "call", "print").Literal("arg0", "1").Then("h"));
            main.Add(N("h", "call", "helper").Then("b"));
            main.Add(N("b", "call", "print").Literal("arg0", "2"));
            var helper = new BlockFunction("helper") { Start = "hw" };
            helper.Add(N("hw", "call", "game.wait").Literal("arg0", "2").Then("hp"));
            helper.Add(N("hp", "call", "print").Literal("arg0", "3"));
            return new BlockGraph().AddFunction(main).AddFunction(helper);
        }

        private static (BlockCompileResult result, PyTask task) Start(BlockGraph g)
        {
            var vm = Vm();
            var lib = new BlockLibrary().AddAllModules(vm);
            var r = BlockCompiler.Compile(g, lib);
            Assert.That(r.Errors.Select(e => e.ToString()), Is.Empty);
            return (r, vm.Start(r.Source + "main()\n"));
        }

        [Test]
        public void ATaskWaitingInACalledFunctionIsAtTheWaitingBlock()
        {
            var (r, task) = Start(Graph());
            Assert.That(task.Update(), Is.EqualTo(TaskState.Waiting));
            Assert.That(r.Map.Running(task), Is.EqualTo("hw"));
        }

        [Test]
        public void TheBlocksThatCalledItAreActiveToo()
        {
            var (r, task) = Start(Graph());
            task.Update();
            Assert.That(r.Map.ActiveNodes(task), Is.EqualTo(new[] { "hw", "h" }));
        }

        [Test]
        public void TheRunningBlockMovesAlongTheFlow()
        {
            var (r, task) = Start(Graph());
            task.Update();
            task.Tick();
            task.Tick();
            task.Update();
            Assert.That(task.State, Is.EqualTo(TaskState.Finished));
            Assert.That(r.Map.Running(task), Is.Null);
            Assert.That(r.Map.ActiveNodes(task), Is.Empty);
        }

        [Test]
        public void ATaskThatHasNotRunIsAtNoBlock()
        {
            var (r, task) = Start(Graph());
            // Before it runs the task is at the top-level call of main(), which no block owns.
            Assert.That(r.Map.Running(task), Is.Null);
        }

        [Test]
        public void ABlockIsListedOnceEvenIfSeveralFramesAreOnIt()
        {
            var (r, task) = Start(Graph());
            task.Update();
            Assert.That(r.Map.ActiveNodes(task).Distinct().Count(), Is.EqualTo(r.Map.ActiveNodes(task).Count));
        }

        [Test]
        public void AWaitInsideAJoinedTailIsFoundByItsBlock()
        {
            var f = new BlockFunction("main") { Start = "i" };
            f.Add(N("i", "if").Literal("cond", "True").Then("then", "x").Then("else", "y"));
            f.Add(N("x", "call", "print").Literal("arg0", "1").Then("w"));
            f.Add(N("y", "call", "print").Literal("arg0", "2").Then("w"));
            f.Add(N("w", "call", "game.wait").Literal("arg0", "5"));
            var (r, task) = Start(new BlockGraph().AddFunction(f));
            Assert.That(task.Update(), Is.EqualTo(TaskState.Waiting));
            Assert.That(r.Map.Running(task), Is.EqualTo("w"));
            Assert.That(r.Map.ActiveNodes(task), Is.EqualTo(new[] { "w" }), "the call of the tail is on the same block");
        }

        [Test]
        public void AWaitInsideALoopBodyIsFoundOnEachPass()
        {
            var f = new BlockFunction("main") { Start = "l" };
            f.Add(N("l", "repeat").Literal("count", "2").Then("body", "w"));
            f.Add(N("w", "call", "game.wait").Literal("arg0", "1"));
            var (r, task) = Start(new BlockGraph().AddFunction(f));
            task.Update();
            Assert.That(r.Map.Running(task), Is.EqualTo("w"));
            task.Tick();
            task.Tick();
            task.Update();
            Assert.That(r.Map.Running(task), Is.EqualTo("w"), "the second pass is on the same block");
        }

        [Test]
        public void ABlockInAnEventHandlerIsFoundWhileTheHandlerWaits()
        {
            var main = new BlockFunction("main") { Start = "mw" };
            main.Add(N("mw", "call", "game.wait").Literal("arg0", "20"));
            var hit = new BlockFunction("on_hit", "damage") { Start = "hw" };
            hit.Add(N("hw", "call", "game.wait").Literal("arg0", "2").Then("hp"));
            hit.Add(N("hp", "call", "print").Literal("arg0", "1"));
            var (r, task) = Start(new BlockGraph().AddFunction(main).AddFunction(hit));
            Assert.That(task.Update(), Is.EqualTo(TaskState.Waiting));
            Assert.That(r.Map.Running(task), Is.EqualTo("mw"));

            task.QueueEvent("on_hit", 5);
            task.Tick();
            task.Update();
            var active = r.Map.ActiveNodes(task);
            Assert.That(active[0], Is.EqualTo("hw"), "the handler's own wait is the innermost block");
        }

        [Test]
        public void ANullTaskIsRejected()
        {
            var (r, _) = Start(Graph());
            Assert.Throws<System.ArgumentNullException>(() => r.Map.Running(null));
            Assert.Throws<System.ArgumentNullException>(() => r.Map.ActiveNodes(null));
        }

        [Test]
        public void VariablesCanBeWatchedWhileTheTaskWaits()
        {
            var g = Graph();
            g.AddVariable("score", "7", "int");
            var (_, task) = Start(g);
            task.Update();
            Assert.That(task.Globals["score"].AsInt(), Is.EqualTo(7));
        }
    }
}
