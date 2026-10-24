using System.Collections.Generic;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class HostApiTests
    {
        [Test]
        public void ModuleFunctionsOfEveryArityAreCallable()
        {
            var seen = new List<string>();
            const string src = @"
import game
print(game.zero(), game.one(1), game.two(1, 2), game.three(1, 2, 3))
game.log0()
game.log1('a')
game.log2('a', 'b')
game.log3('a', 'b', 'c')";
            string output = Run(src, vm => vm.RegisterModule("game", m => m
                .Function("zero", () => 0)
                .Function("one", a => a)
                .Function("two", (a, b) => a.AsInt() + b.AsInt())
                .Function("three", (a, b, c) => a.AsInt() + b.AsInt() + c.AsInt())
                .Action("log0", () => seen.Add("0"))
                .Action("log1", a => seen.Add(a.AsString()))
                .Action("log2", (a, b) => seen.Add(a.AsString() + b.AsString()))
                .Action("log3", (a, b, c) => seen.Add(a.AsString() + b.AsString() + c.AsString()))));
            Assert.That(output.TrimEnd(), Is.EqualTo("0 1 3 6"));
            Assert.That(seen, Is.EqualTo(new[] { "0", "a", "ab", "abc" }));
        }

        [Test]
        public void ModuleConstantsAreReadable()
        {
            var output = Run("import game\nprint(game.MAX, game.NAME)", vm =>
                vm.RegisterModule("game", m => m.Constant("MAX", 10).Constant("NAME", "tank")));
            Assert.That(output.TrimEnd(), Is.EqualTo("10 tank"));
        }

        [Test]
        public void FromImportOfHostFunction()
        {
            var output = Run("from game import double\nprint(double(21))", vm =>
                vm.RegisterModule("game", m => m.Function("double", a => a.AsInt() * 2)));
            Assert.That(output.TrimEnd(), Is.EqualTo("42"));
        }

        [Test]
        public void HostArityIsChecked()
        {
            var e = Fails("import game\ngame.one()", vm => vm.RegisterModule("game", m => m.Function("one", a => a)));
            Assert.That(e.TypeName, Is.EqualTo("TypeError"));
            Assert.That(e.Problem, Is.EqualTo("one() takes exactly 1 argument (0 given)"));
        }

        [Test]
        public void HostArgumentTypeErrorsBecomeTypeErrors()
        {
            var e = Fails("import game\ngame.one('x')", vm => vm.RegisterModule("game", m => m.Function("one", a => a.AsInt())));
            Assert.That(e.TypeName, Is.EqualTo("TypeError"));
            Assert.That(e.Problem, Does.Contain("expected an int"));
        }

        [Test]
        public void NativeFunctionsReceiveKeywordArguments()
        {
            const string src = "import game\nprint(game.move(3, speed=2), game.move(1))";
            var output = Run(src, vm => vm.RegisterModule("game", m => m.Native("move", (ctx, a) =>
            {
                ctx.TryGetKeyword(a, "speed", out Value speed);
                return a[0].AsInt() * (speed.IsNone ? 1 : speed.AsInt());
            }, 1, 1, acceptsKeywords: true)));
            Assert.That(output.TrimEnd(), Is.EqualTo("6 1"));
        }

        [Test]
        public void HandlesPassThroughScripts()
        {
            var output = Run("import game\nh = game.spawn()\nprint(h, game.id_of(h), h == game.spawn())", vm =>
                vm.RegisterModule("game", m => m
                    .Function("spawn", () => Value.FromHandle(3, 42))
                    .Function("id_of", h => h.HandleId)));
            Assert.That(output.TrimEnd(), Is.EqualTo("<handle 3#42> 42 True"));
        }

        [Test]
        public void HandlesWorkAsDictKeys()
        {
            var output = Run("import game\nd = {game.spawn(): 'a'}\nprint(d[game.spawn()])", vm =>
                vm.RegisterModule("game", m => m.Function("spawn", () => Value.FromHandle(1, 7))));
            Assert.That(output.TrimEnd(), Is.EqualTo("a"));
        }

        [Test]
        public void BuiltinsCanBeAddedToEveryScript()
        {
            var output = Run("print(answer, twice(4))", vm =>
            {
                vm.SetBuiltin("answer", 42);
                vm.SetBuiltin("twice", Value.FromNative(new NativeFunction("twice", (c, a) => a[0].AsInt() * 2, 1, 1)));
            });
            Assert.That(output.TrimEnd(), Is.EqualTo("42 8"));
        }

        [Test]
        public void ReRegisteringAModuleReplacesIt()
        {
            var vm = new PyVm { Output = s => { } };
            vm.RegisterModule("m", b => b.Constant("v", 1));
            vm.RegisterModule("m", b => b.Constant("v", 2));
            var task = vm.Start("import m\nresult = m.v");
            task.RunUntilDone();
            Assert.That(task.Globals["result"].AsInt(), Is.EqualTo(2));
        }

        [Test]
        public void RegisterPreBuiltModule()
        {
            var vm = new PyVm { Output = s => { } };
            var module = new PyModule("extra");
            module.Members["x"] = 5;
            vm.RegisterModule(module);
            var task = vm.Start("import extra\nresult = extra.x");
            task.RunUntilDone();
            Assert.That(task.Globals["result"].AsInt(), Is.EqualTo(5));
        }

        [Test]
        public void ModuleBuilderExposesItsName()
        {
            string name = null;
            new PyVm().RegisterModule("abc", b => name = b.Name);
            Assert.That(name, Is.EqualTo("abc"));
        }

        [Test]
        public void ScriptsCanBeStartedManyTimesWithSeparateGlobals()
        {
            var vm = new PyVm { Output = s => { } };
            var script = vm.Compile("n = 0\nfor i in range(3):\n    n += 1");
            var a = vm.Start(script);
            var b = vm.Start(script);
            a.RunUntilDone();
            Assert.That(a.Globals["n"].AsInt(), Is.EqualTo(3));
            Assert.That(b.Globals.ContainsKey("n"), Is.False);
            b.RunUntilDone();
            Assert.That(b.Globals["n"].AsInt(), Is.EqualTo(3));
            Assert.That(script.Name, Is.EqualTo("<script>"));
            Assert.That(script.Disassemble(), Does.Contain("Return"));
        }

        [Test]
        public void OutputGoesToTheVmSinkAndNullIsFine()
        {
            var vm = new PyVm();
            var task = vm.Start("print('lost')");
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Finished));
        }

        [Test]
        public void OwnerAndStartSourceNameAreKept()
        {
            var vm = new PyVm();
            var task = vm.Start("1/0", "robot.py");
            task.Owner = "bot";
            task.RunUntilDone();
            Assert.That(task.Owner, Is.EqualTo("bot"));
            Assert.That(task.Error.SourceName, Is.EqualTo("robot.py"));
            Assert.That(task.Error.FormatTraceback(), Does.Contain("robot.py"));
        }

        [Test]
        public void CallContextExposesTaskAndVm()
        {
            PyTask seenTask = null;
            PyVm seenVm = null;
            var vm = new PyVm();
            vm.RegisterModule("game", m => m.Native("probe", (ctx, a) =>
            {
                seenTask = ctx.Task;
                seenVm = ctx.Vm;
                return Value.None;
            }, 0, 0));
            var task = vm.Start("import game\ngame.probe()");
            task.RunUntilDone();
            Assert.That(seenTask, Is.SameAs(task));
            Assert.That(seenVm, Is.SameAs(vm));
            Assert.That(new CallContext().Vm, Is.Null);
        }
    }
}
