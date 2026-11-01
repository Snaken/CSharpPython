using System;
using System.Collections.Generic;
using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Vm
{
    // What a host or a person sees when a script stops with an error: the path of calls that led to it.
    public class TracebackTests
    {
        private static PyRuntimeError Fails(string source, VmOptions options = null)
        {
            var vm = new PyVm(options ?? new VmOptions());
            var task = vm.Start(source, "robot.py");
            task.RunUntilDone();
            Assert.That(task.State, Is.EqualTo(TaskState.Error));
            return task.Error;
        }

        [Test]
        public void EachFrameShowsItsLineOfTheScript()
        {
            var error = Fails("def inner(a, b):\n    return a / b\n\ndef outer():\n    return inner(1, 0)\n\nouter()\n");
            Assert.That(error.FormatTraceback(), Is.EqualTo(
                "Traceback (most recent call last):\n" +
                "  File \"robot.py\", line 7, in <module>\n    outer()\n" +
                "  File \"robot.py\", line 5, in outer\n    return inner(1, 0)\n" +
                "  File \"robot.py\", line 2, in inner\n    return a / b\n" +
                "ZeroDivisionError: division by zero"));
        }

        [Test]
        public void ToStringIsTheWholeTraceback()
        {
            var error = Fails("def f():\n    return 1 // 0\nf()\n");
            Assert.That(error.ToString(), Is.EqualTo(error.FormatTraceback()));
            Assert.That(error.Message, Is.EqualTo("ZeroDivisionError: division by zero (line 2)"));
        }

        [Test]
        public void ARepeatedCallIsShownThreeTimesThenCounted()
        {
            var error = Fails("def f(n):\n    return f(n + 1)\nf(0)\n");
            Assert.That(error.TypeName, Is.EqualTo("RecursionError"));
            string text = error.FormatTraceback();
            Assert.That(text, Does.Contain("[Previous line repeated "));
            Assert.That(text.Split('\n').Length, Is.LessThan(20));
            Assert.That(text, Does.EndWith("RecursionError: " + error.Problem));
        }

        [TestCase(3, 0, "")]
        [TestCase(4, 1, "  [Previous line repeated 1 more time]\n")]
        [TestCase(10, 7, "  [Previous line repeated 7 more times]\n")]
        public void TheRepeatCountMatchesCPython(int frames, int counted, string note)
        {
            var trace = new List<TraceFrame>();
            for (int i = 0; i < frames; i++) trace.Add(new TraceFrame("f", 2));
            trace.Add(new TraceFrame("<module>", 3));
            trace.Reverse();
            var error = new PyRuntimeError("RecursionError", "deep", "x.py", trace);
            string text = error.FormatTraceback();
            Assert.That(text.Split(new[] { "in f" }, StringSplitOptions.None).Length - 1, Is.EqualTo(Math.Min(frames, 3)));
            Assert.That(text.Contains("Previous line repeated"), Is.EqualTo(counted > 0));
            if (counted > 0) Assert.That(text, Does.Contain(note));
        }

        [Test]
        public void DifferentCallsAreNotCollapsed()
        {
            var trace = new List<TraceFrame> { new TraceFrame("g", 5), new TraceFrame("f", 2), new TraceFrame("g", 5), new TraceFrame("f", 2), new TraceFrame("<module>", 1) };
            string text = new PyRuntimeError("E", "", "x.py", trace).FormatTraceback();
            Assert.That(text, Does.Not.Contain("Previous line repeated"));
        }

        [Test]
        public void ARestoredErrorShowsTheSourceToo()
        {
            var vm = new PyVm(new VmOptions { EnableSnapshots = true });
            var script = vm.Compile("def f():\n    return 1 // 0\nf()\n", "a.py");
            var task = vm.Start(script);
            task.RunUntilDone();
            var restored = vm.Restore(script, task.Save());
            Assert.That(restored.Error.FormatTraceback(), Does.Contain("    return 1 // 0"));
        }

        [Test]
        public void ChainedErrorsShowTheSourceOfBoth()
        {
            var error = Fails("try:\n    1 / 0\nexcept ZeroDivisionError:\n    raise ValueError('bad')\n");
            string text = error.FormatTraceback();
            Assert.That(text, Does.Contain("    1 / 0"));
            Assert.That(text, Does.Contain("    raise ValueError('bad')"));
        }

        [Test]
        public void ALimitErrorStillHasItsTrace()
        {
            var error = Fails("def spin():\n    while True:\n        pass\nspin()\n", new VmOptions { MaxSteps = 1000 });
            Assert.That(error.TypeName, Is.EqualTo("StepLimitExceeded"));
            Assert.That(error.Trace.Select(t => t.Function), Is.EqualTo(new[] { "spin", "<module>" }));
            Assert.That(error.FormatTraceback(), Does.Contain("    while True:"));
        }

        [Test]
        public void AHostErrorHasTheTraceAndNamesTheCause()
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m => m.Native("boom", (c, a) => throw new InvalidOperationException("secret detail"), 0, 0));
            var task = vm.Start("import game\ndef go():\n    game.boom()\ngo()\n");
            task.RunUntilDone();
            var error = task.Error;
            Assert.That(error.TypeName, Is.EqualTo("HostError"));
            Assert.That(error.Trace.Select(t => t.Function), Is.EqualTo(new[] { "go", "<module>" }));
            Assert.That(error.Problem, Does.Not.Contain("secret detail"));
            Assert.That(error.ToString(), Does.Contain("secret detail").And.Contain("Caused by"));
        }

        [Test]
        public void TheScriptKeepsItsSource()
        {
            var script = new PyVm().Compile("a = 1\r\nb = 2\n", "s.py");
            Assert.That(script.Source, Is.EqualTo("a = 1\r\nb = 2\n"));
            Assert.That(script.SourceLine(1), Is.EqualTo("a = 1"));
            Assert.That(script.SourceLine(2), Is.EqualTo("b = 2"));
            Assert.That(script.SourceLine(0), Is.Null);
            Assert.That(script.SourceLine(99), Is.Null);
        }

        [Test]
        public void TheSourceMapListsTheBlocksOnThePathToAnError()
        {
            var work = new BlockFunction("work") { Start = "d" };
            work.Add(new BlockNode("d", "set", "x").Connect("value", "div"));
            work.Add(new BlockNode("div", "floordiv").Literal("a", "1").Literal("b", "0"));
            var main = new BlockFunction("main") { Start = "c" };
            main.Add(new BlockNode("c", "call", "work"));
            var graph = new BlockGraph().AddVariable("x", "0", "int").AddFunction(work).AddFunction(main);
            var result = BlockCompiler.Compile(graph);
            Assert.That(result.Succeeded, Is.True, string.Join("; ", result.Errors));
            var task = new PyVm().Start(result.Source + "\nmain()\n");
            task.RunUntilDone();
            Assert.That(task.Error.TypeName, Is.EqualTo("ZeroDivisionError"));
            var path = result.Map.Path(task.Error);
            Assert.That(path, Is.EqualTo(new[] { "d", "c" }));
            Assert.That(path[0], Is.EqualTo(result.Map.Locate(task.Error)));
            Assert.Throws<ArgumentNullException>(() => result.Map.Path(null));
        }
    }
}
