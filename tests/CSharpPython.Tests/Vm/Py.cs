using System;
using System.Text;

namespace CSharpPython.Tests.Vm
{
    /// <summary>Runs script source and returns what it printed.</summary>
    public static class Py
    {
        public static string Run(string source, Action<PyVm> setup = null, VmOptions options = null)
        {
            var sb = new StringBuilder();
            var vm = new PyVm(options) { Output = s => sb.Append(s) };
            setup?.Invoke(vm);
            var task = vm.Start(source);
            var state = task.RunUntilDone();
            if (state == TaskState.Error) throw task.Error;
            Assert.That(state, Is.EqualTo(TaskState.Finished));
            return sb.ToString();
        }

        /// <summary>Runs the script and returns its output with trailing newline removed, for one-line checks.</summary>
        public static string Out(string source) => Run(source).TrimEnd('\n');

        public static PyRuntimeError Fails(string source, Action<PyVm> setup = null, VmOptions options = null)
        {
            var vm = new PyVm(options) { Output = s => { } };
            setup?.Invoke(vm);
            var task = vm.Start(source);
            var state = task.RunUntilDone();
            Assert.That(state, Is.EqualTo(TaskState.Error), "expected the script to fail");
            return task.Error;
        }

        public static string Eval(string expr) => Out("print(" + expr + ")");
    }
}
