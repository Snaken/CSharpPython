using System.Linq;
using System.Text;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    public class BlockPaletteTests
    {
        private static PyVm Host()
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m =>
            {
                m.Function("spawn", (Value kind, Value count) => Value.None);
                m.Native("log", (ctx, args) => Value.None, 1, 3);
                m.Describe("log", "text, level=1, tag=\"x\"");
                m.Native("tally", (ctx, args) => Value.None, 0, -1, true);
                m.Native("_hidden", (ctx, args) => Value.None, 0, 0);
                m.Constant("MAX", Value.FromInt(3));
                m.Action("ping", () => { });
            });
            return vm;
        }

        private static BlockLibrary Lib(PyVm vm)
        {
            var lib = new BlockLibrary();
            lib.AddModule(vm, "game");
            return lib;
        }

        private static BlockNode N(string id, string block, string name = null) => new BlockNode(id, block, name);

        private static BlockFunction Single(BlockNode call)
        {
            var f = new BlockFunction("main") { Start = call.Id };
            f.Add(call);
            return f;
        }

        [Test]
        public void AddModuleReadsParametersFromSignatures()
        {
            var vm = Host();
            var lib = new BlockLibrary();
            Assert.That(lib.AddModule(vm, "game"), Is.True);
            var names = lib.Functions.Where(f => f.Module == "game").Select(f => f.Name).ToArray();
            Assert.That(names, Is.EqualTo(new[] { "log", "ping", "spawn", "tally" }));

            var log = lib.FindFunction("game.log");
            Assert.That(log.Parameters, Is.EqualTo(new[] { "text", "level", "tag" }));
            Assert.That(log.RequiredParameters, Is.EqualTo(1));
            Assert.That(log.Signature, Is.EqualTo("text, level=1, tag=\"x\""));

            var spawn = lib.FindFunction("game.spawn");
            Assert.That(spawn.Parameters.Count, Is.EqualTo(2));
            Assert.That(spawn.RequiredParameters, Is.EqualTo(2));
            Assert.That(lib.FindFunction("game.tally").Parameters, Is.Empty);
            Assert.That(lib.FindFunction("game.ping").Pure, Is.False);
        }

        [Test]
        public void AddModuleCanMarkFunctionsPure()
        {
            var lib = new BlockLibrary();
            lib.AddModule(Host(), "game", pure: true);
            Assert.That(lib.FindFunction("game.spawn").Pure, Is.True);
        }

        [Test]
        public void UnknownModulesAndNullsAreHandled()
        {
            var lib = new BlockLibrary();
            Assert.That(lib.AddModule(Host(), "nope"), Is.False);
            Assert.That(lib.AddModule(Host(), null), Is.False);
            Assert.Throws<System.ArgumentNullException>(() => lib.AddModule(null, "game"));
            Assert.Throws<System.ArgumentNullException>(() => lib.AddAllModules(null));
        }

        [Test]
        public void AddAllModulesTreatsMathAsPure()
        {
            var lib = new BlockLibrary().AddAllModules(Host());
            Assert.That(lib.FindFunction("math.sqrt").Pure, Is.True);
            Assert.That(lib.FindFunction("game.spawn").Pure, Is.False);
            Assert.That(lib.FindFunction("math.sqrt").Parameters.Count, Is.EqualTo(1));
        }

        [Test]
        public void ExplicitSignatureAndRequiredAreKept()
        {
            var lib = new BlockLibrary().AddFunction("f", new[] { "a", "b" }, required: 1, signature: "a, b=2")
                .AddFunction("g", new[] { "a" }, required: 9).AddFunction("h", new[] { "a" }, required: -5);
            Assert.That(lib.FindFunction("f").RequiredParameters, Is.EqualTo(1));
            Assert.That(lib.FindFunction("f").Signature, Is.EqualTo("a, b=2"));
            Assert.That(lib.FindFunction("g").RequiredParameters, Is.EqualTo(1));
            Assert.That(lib.FindFunction("h").RequiredParameters, Is.EqualTo(1));
            Assert.That(lib.FindFunction("h").Signature, Is.EqualTo("a"));
        }

        [Test]
        public void OptionalPinsMayBeLeftOut()
        {
            var vm = Host();
            var sb = new StringBuilder();
            vm.RegisterModule("game", m => m.Native("log", (ctx, args) => { sb.Append(args.Length).Append(';'); return Value.None; }, 1, 3));
            var lib = Lib(vm);

            var one = Single(N("c", "call", "game.log").Literal("arg0", "\"a\""));
            var two = Single(N("c", "call", "game.log").Literal("arg0", "\"a\"").Literal("arg1", "2"));
            var three = Single(N("c", "call", "game.log").Literal("arg0", "\"a\"").Literal("arg1", "2").Literal("arg2", "3"));
            foreach (var f in new[] { one, two, three })
            {
                var r = BlockCompiler.Compile(new BlockGraph().AddFunction(f), lib);
                Assert.That(r.Errors, Is.Empty);
                var task = vm.Start(r.Source + "main()\n");
                task.RunUntilDone();
            }
            Assert.That(sb.ToString(), Is.EqualTo("1;2;3;"));
            Assert.That(BlockCompiler.Compile(new BlockGraph().AddFunction(two), lib).Source, Does.Contain("game.log(\"a\", 2)"));
        }

        [Test]
        public void RequiredPinsStillNeedValues()
        {
            var lib = Lib(Host());
            var r = BlockCompiler.Compile(new BlockGraph().AddFunction(Single(N("c", "call", "game.log"))), lib);
            Assert.That(r.Errors.Single().Pin, Is.EqualTo("arg0"));
        }

        [Test]
        public void AnOptionalPinAfterAGapIsAnError()
        {
            var lib = Lib(Host());
            var call = N("c", "call", "game.log").Literal("arg0", "\"a\"").Literal("arg2", "3");
            var r = BlockCompiler.Compile(new BlockGraph().AddFunction(Single(call)), lib);
            var error = r.Errors.Single();
            Assert.That(error.Pin, Is.EqualTo("arg2"));
            Assert.That(error.Message, Does.Contain("before it is empty"));
        }

        [Test]
        public void PaletteGroupsBlocksThenFunctionsThenModules()
        {
            var lib = Lib(Host());
            var palette = lib.Palette();
            var names = palette.Select(g => g.Name).ToList();
            Assert.That(names.Count, Is.EqualTo(names.Distinct().Count()));
            Assert.That(names.IndexOf("Functions"), Is.GreaterThan(names.IndexOf("Notes")));
            Assert.That(names.Last(), Is.EqualTo("game"));
            Assert.That(palette.SelectMany(g => g.Blocks).Count(), Is.EqualTo(lib.Blocks.Count));
            Assert.That(palette.SelectMany(g => g.Functions).Count(), Is.EqualTo(lib.Functions.Count));
            Assert.That(palette.Single(g => g.Name == "game").Functions.Select(f => f.Name), Is.EqualTo(new[] { "log", "ping", "spawn", "tally" }));
            Assert.That(palette.Single(g => g.Name == "Functions").Blocks, Is.Empty);
        }
    }
}
