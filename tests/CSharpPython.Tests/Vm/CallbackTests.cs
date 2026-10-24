using System.Linq;
using System.Text;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    /// <summary>Native functions that call back into the script (sort keys, <see cref="CallContext.Call"/>).</summary>
    public class CallbackTests
    {
        [Test]
        public void ErrorInAKeyFunctionReachesTheHostWithItsTraceback()
        {
            var error = Fails("def k(x):\n    return 1 // x\nsorted([1, 0], key=k)\n");
            Assert.That(error.TypeName, Is.EqualTo("ZeroDivisionError"));
            Assert.That(error.Trace.Select(f => f.Function), Is.EqualTo(new[] { "k", "<module>" }));
            Assert.That(error.Trace.Select(f => f.Line), Is.EqualTo(new[] { 2, 3 }));
        }

        [Test]
        public void ErrorInAKeyFunctionCanBeCaughtByTheCaller()
        {
            Assert.That(Out("def k(x):\n    raise KeyError('k')\ntry:\n    sorted([1, 2], key=k)\nexcept KeyError:\n    print('caught')\nprint(sorted([2, 1]))"),
                Is.EqualTo("caught\n[1, 2]"));
        }

        [Test]
        public void AKeyFunctionCannotWait()
        {
            var sb = new StringBuilder();
            var vm = new PyVm { Output = s => sb.Append(s) };
            vm.RegisterModule("game", m => m.Native("wait", (c, a) => { c.Wait(3); return Value.None; }));
            var task = vm.Start("import game\ndef k(x):\n    game.wait()\n    return x\ntry:\n    sorted([2, 1], key=k)\nexcept RuntimeError as e:\n    print('refused')\ngame.wait()\nprint('later')");
            Assert.That(task.Update(), Is.EqualTo(TaskState.Waiting));
            Assert.That(sb.ToString(), Is.EqualTo("refused\n"));
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Finished));
            Assert.That(sb.ToString(), Is.EqualTo("refused\nlater\n"));
        }

        [Test]
        public void ARunawayKeyFunctionHitsTheStepLimit()
        {
            var error = Fails("def k(x):\n    while True:\n        pass\nsorted([1, 2], key=k)\n", null, new VmOptions { MaxSteps = 5000 });
            Assert.That(error.TypeName, Is.EqualTo("StepLimitExceeded"));
            Assert.That(error.Trace[0].Function, Is.EqualTo("k"));
        }

        [Test]
        public void AKeyFunctionLongerThanASliceStillFinishesAndIsCounted()
        {
            var options = new VmOptions { StepsPerSlice = 50, MaxSteps = 0 };
            var vm = new PyVm(options) { Output = s => { } };
            var task = vm.Start("def k(x):\n    n = 0\n    for i in range(200):\n        n += i\n    return -x\nprint(sorted([1, 3, 2], key=k))");
            var sb = new StringBuilder();
            vm.Output = s => sb.Append(s);
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Finished));
            Assert.That(sb.ToString(), Is.EqualTo("[3, 2, 1]\n"));
            Assert.That(task.StepsExecuted, Is.GreaterThan(3 * 200 * 3));
        }

        [Test]
        public void StepCountingStaysExactAcrossNestedCalls()
        {
            string plain = "def k(x):\n    return -x\nxs = [k(1), k(3), k(2)]\n";
            string keyed = "def k(x):\n    return -x\nxs = sorted([1, 3, 2], key=k)\n";
            var a = new PyVm().Start(plain);
            a.RunUntilDone();
            var b = new PyVm(new VmOptions { StepsPerSlice = 7 }).Start(keyed);
            b.RunUntilDone();
            var c = new PyVm(new VmOptions { StepsPerSlice = 100000 }).Start(keyed);
            c.RunUntilDone();
            Assert.That(b.StepsExecuted, Is.EqualTo(c.StepsExecuted));
        }

        [Test]
        public void RunawayNestingIsStopped()
        {
            var error = Fails("def k(x):\n    return sorted([x, x], key=k)\nsorted([1, 2], key=k)\n");
            Assert.That(error.TypeName, Is.EqualTo("RecursionError"));
        }

        [Test]
        public void ManyCallbacksDoNotLeakStackOrFrames()
        {
            var vm = new PyVm { Output = s => { } };
            var task = vm.Start("def k(x):\n    return -x\ntotal = 0\nfor i in range(3000):\n    total += sorted([3, 1, 2], key=k)[0]\n");
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Finished));
            Assert.That(task.Globals["total"].AsInt, Is.EqualTo(3 * 3000));
            Assert.That(task.CallDepth, Is.EqualTo(0));
        }

        [Test]
        public void HostFunctionsCanCallScriptFunctionsNativesAndBoundMethods()
        {
            var sb = new StringBuilder();
            var vm = new PyVm { Output = s => sb.Append(s) };
            vm.RegisterModule("host", m => m.Native("apply", (c, a) => c.Call(a[0], a[1], a[2]), 3, 3));
            var task = vm.Start("import host\ndef add(a, b):\n    return a + b\nprint(host.apply(add, 1, 2))\nprint(host.apply(max, 4, 9))\nprint(host.apply(pow, 2, 5))\nxs = [1]\nhost.apply(xs.insert, 0, 7)\nprint(xs)");
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Finished));
            Assert.That(sb.ToString(), Is.EqualTo("3\n9\n32\n[7, 1]\n"));
        }

        [Test]
        public void CallingSomethingNotCallableIsATypeError()
        {
            var vm = new PyVm { Output = s => { } };
            vm.RegisterModule("host", m => m.Native("apply", (c, a) => c.Call(a[0]), 1, 1));
            var task = vm.Start("import host\nhost.apply(5)\n");
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Error));
            Assert.That(task.Error.TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void AHostFunctionKeepsItsKeywordArgumentsAfterACallback()
        {
            Value seen = default;
            var vm = new PyVm { Output = s => { } };
            vm.RegisterModule("host", m => m.Native("apply", (c, a) =>
            {
                c.Call(a[0], Value.FromInt(1));
                c.TryGetKeyword(a, "tag", out seen);
                return Value.None;
            }, 1, 1, true));
            var task = vm.Start("import host\ndef f(x):\n    return sorted([2, 1], key=abs)\nhost.apply(f, tag=42)\n");
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Finished));
            Assert.That(seen.AsInt, Is.EqualTo(42));
        }

        [Test]
        public void CallOutsideATaskIsRejected()
        {
            var ctx = new CallContext();
            Assert.Throws<System.InvalidOperationException>(() => ctx.Call(Value.None));
        }

        [Test]
        public void CallbacksWorkInsideEventHandlers()
        {
            var sb = new StringBuilder();
            var vm = new PyVm { Output = s => sb.Append(s) };
            var task = vm.Start("def k(x):\n    return -x\ndef on_sort(items):\n    print(sorted(items, key=k))\n");
            task.RunUntilDone();
            task.QueueEvent("on_sort", Value.FromList(new PyList(new[] { Value.FromInt(1), Value.FromInt(3), Value.FromInt(2) })));
            task.RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("[3, 2, 1]\n"));
        }

        [Test]
        public void SortAndMinMaxRejectBadKeywords()
        {
            Assert.That(Fails("min([1], foo=1)").Problem, Does.Contain("unexpected keyword argument 'foo'"));
            Assert.That(Fails("max(1, 2, default=3)").Problem, Does.Contain("Cannot specify a default"));
            Assert.That(Fails("min([])").Problem, Does.Contain("empty sequence"));
            Assert.That(Fails("sorted([1], key=5)").TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void KeyThatReturnsIncomparableValuesIsATypeError()
        {
            Assert.That(Fails("def k(x):\n    return x if x == 1 else 'a'\nsorted([1, 2], key=k)").TypeName, Is.EqualTo("TypeError"));
        }
    }
}
