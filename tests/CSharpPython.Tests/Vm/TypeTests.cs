using System.Linq;
using CSharpPython.Editor;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class TypeTests
    {
        private static string Message(string source) => Fails(source).Problem;

        [Test]
        public void ErrorMessagesNameTheProblem()
        {
            Assert.That(Message("isinstance(1, 'int')"), Is.EqualTo("isinstance() arg 2 must be a type or a tuple of types"));
            Assert.That(Message("isinstance(1, (str, 'x'))"), Is.EqualTo("isinstance() arg 2 must be a type or a tuple of types"));
            Assert.That(Message("issubclass(1, int)"), Is.EqualTo("issubclass() arg 1 must be a class"));
            Assert.That(Message("issubclass(int, 1)"), Is.EqualTo("issubclass() arg 2 must be a class or a tuple of classes"));
            Assert.That(Message("type(1, 2)"), Is.EqualTo("type() takes exactly 1 argument (2 given)"));
            Assert.That(Message("type(len)()"), Is.EqualTo("cannot create 'builtin_function_or_method' instances"));
            Assert.That(Message("type(int).nothing"), Does.Contain("has no attribute 'nothing'"));
        }

        [Test]
        public void TypesPrintLikeCpython()
        {
            Assert.That(Out("print(int, ValueError, type(None))"), Is.EqualTo("<class 'int'> <class 'ValueError'> <class 'NoneType'>"));
            Assert.That(Out("print(repr(float))"), Is.EqualTo("<class 'float'>"));
        }

        [Test]
        public void TypeValuesAreReadableFromTheHost()
        {
            var vm = new PyVm();
            var task = vm.Start("t = type(1)\nu = type('a')\nsame = t is type(2)");
            task.Update();
            Assert.That(task.Globals["same"].IsTruthy, Is.True);
            Assert.That(task.Globals["t"].TypeName, Is.EqualTo("type"));
            Assert.That(task.Globals["t"].Repr(), Is.EqualTo("<class 'int'>"));
        }

        [Test]
        public void TypesSurviveSnapshots()
        {
            var vm = new PyVm(new VmOptions { EnableSnapshots = true, StepsPerSlice = 1 });
            vm.RegisterModule("game", m => m.Native("wait", (ctx, a) => { ctx.Wait(1); return Value.None; }, 1, 1));
            var script = vm.Compile("import game\nt = type(1)\nu = ValueError\nv = int\nd = {t: 'x'}\ngame.wait(1)\nprint(t(5), v('7'), d[int], u('m'), type(2) is t, isinstance(1, v))");
            var task = vm.Start(script);
            task.Update();
            byte[] bytes = task.Save();
            var sb = new System.Text.StringBuilder();
            var vm2 = new PyVm(new VmOptions { EnableSnapshots = true });
            vm2.Output = s => sb.Append(s);
            vm2.RegisterModule("game", m => m.Native("wait", (ctx, a) => { ctx.Wait(1); return Value.None; }, 1, 1));
            var restored = vm2.Restore(script, bytes);
            Assert.That(restored.Save(), Is.EqualTo(bytes));
            while (restored.Update() != TaskState.Finished) { }
            Assert.That(sb.ToString().TrimEnd(), Is.EqualTo("5 7 x m True True"));
        }

        [Test]
        public void ADamagedTypeNameInASnapshotIsRefused()
        {
            var vm = new PyVm(new VmOptions { EnableSnapshots = true });
            vm.RegisterModule("game", m => m.Native("wait", (ctx, a) => { ctx.Wait(1); return Value.None; }, 1, 1));
            var script = vm.Compile("import game\nt = type(1)\ngame.wait(1)");
            var task = vm.Start(script);
            task.Update();
            byte[] bytes = task.Save();
            // 'int' is stored as an int length (3) followed by UTF-16 characters; change it to 'xnt'.
            var text = System.Text.Encoding.Unicode.GetBytes("int");
            int at = -1;
            for (int i = 4; i + text.Length <= bytes.Length && at < 0; i++)
            {
                bool match = bytes[i - 4] == 3 && bytes[i - 3] == 0 && bytes[i - 2] == 0 && bytes[i - 1] == 0;
                for (int k = 0; k < text.Length && match; k++) match = bytes[i + k] == text[k];
                if (match) at = i;
            }
            Assert.That(at, Is.GreaterThan(0));
            bytes[at] = (byte)'x';
            Assert.Throws<SnapshotException>(() => vm.Restore(script, bytes));
        }

        [Test]
        public void ScriptsCanShadowTypeNames()
        {
            Assert.That(Out("def isinstance(a, b):\n    return 'mine'\nprint(isinstance(1, int))"), Is.EqualTo("mine"));
            Assert.That(Out("type = 5\nprint(type)"), Is.EqualTo("5"));
        }

        [Test]
        public void TypeNamesInCompletionAreClasses()
        {
            var list = Completer.Complete("fl", 2);
            var item = list.Items.First(i => i.Text == "float");
            Assert.That(item.Kind, Is.EqualTo(CompletionKind.Class));
            Assert.That(Completer.Complete("isi", 3).Items.Select(i => i.Text), Does.Contain("isinstance"));
        }

        [Test]
        public void HostFunctionsCanReceiveTypesAndCallThem()
        {
            var vm = new PyVm();
            vm.RegisterModule("host", m => m.Native("make", (ctx, a) => ctx.Call(a[0], a[1]), 2, 2));
            var sb = new System.Text.StringBuilder();
            vm.Output = s => sb.Append(s);
            vm.Start("import host\nprint(host.make(int, '12') + 1, host.make(str, 5) + '!')").Update();
            Assert.That(sb.ToString().TrimEnd(), Is.EqualTo("13 5!"));
        }
    }
}
