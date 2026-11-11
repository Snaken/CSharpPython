using System.Text;

namespace CSharpPython.Tests.Vm
{
    // A global read remembers what it found, so it must notice a built-in that the host replaces, and tasks that
    // share one compiled script must not see each other's globals.
    public class GlobalReadTests
    {
        private static PyVm NewVm(StringBuilder sb)
        {
            var vm = new PyVm { Output = s => sb.Append(s) };
            vm.RegisterModule("game", m => m
                .Native("wait", (ctx, a) => { ctx.Wait((int)a[0].AsInt()); return Value.None; }, 1, 1));
            return vm;
        }

        [Test]
        public void ABuiltinTheHostReplacesWhileAScriptWaitsIsReadAgain()
        {
            var sb = new StringBuilder();
            var vm = NewVm(sb);
            vm.SetBuiltin("level", Value.FromInt(1));
            var task = vm.Start("import game\ndef show():\n    print(level)\nshow()\ngame.wait(1)\nshow()\ngame.wait(1)\nshow()\n");
            task.Update();
            vm.SetBuiltin("level", Value.FromInt(2));
            task.Update();
            task.Update();
            Assert.That(task.State, Is.EqualTo(TaskState.Finished), task.Error?.ToString());
            Assert.That(sb.ToString().Replace("\r", ""), Is.EqualTo("1\n2\n2\n"));
        }

        [Test]
        public void TasksThatShareAScriptEachReadTheirOwnGlobals()
        {
            var sb = new StringBuilder();
            var vm = NewVm(sb);
            var script = vm.Compile("import game\nname = 'x'\ndef show():\n    print(name, len(name))\nshow()\ngame.wait(1)\nshow()\nname = 'longer'\ngame.wait(1)\nshow()\n");
            var a = vm.Start(script);
            var b = vm.Start(script);
            a.Update();
            b.Update();
            a.Globals["name"] = Value.FromString("aa");
            for (int i = 0; i < 4; i++)
            {
                a.Update();
                b.Update();
            }
            Assert.That(a.State, Is.EqualTo(TaskState.Finished), a.Error?.ToString());
            Assert.That(b.State, Is.EqualTo(TaskState.Finished), b.Error?.ToString());
            Assert.That(sb.ToString().Replace("\r", ""), Is.EqualTo("x 1\nx 1\naa 2\nx 1\nlonger 6\nlonger 6\n"));
        }
    }
}
