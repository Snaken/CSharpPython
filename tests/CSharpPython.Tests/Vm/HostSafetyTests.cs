using System;

namespace CSharpPython.Tests.Vm
{
    /// <summary>Nothing a script does may reach the host as an exception, not even running out of memory.</summary>
    public class HostSafetyTests
    {
        private static PyTask Run(Func<Value> hostFunction)
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m => m.Function("boom", () => hostFunction()));
            var task = vm.Start("import game\ngame.boom()");
            Assert.DoesNotThrow(() => task.RunUntilDone());
            return task;
        }

        [Test]
        public void RunningOutOfMemoryEndsOnlyTheTask()
        {
            var task = Run(() => throw new OutOfMemoryException("host allocation"));
            Assert.That(task.State, Is.EqualTo(TaskState.Error));
            Assert.That(task.Error.TypeName, Is.EqualTo("MemoryLimitExceeded"));
            Assert.That(task.Error.InnerException, Is.InstanceOf<OutOfMemoryException>());
        }

        [Test]
        public void ScriptsCannotCatchTheMemoryError()
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m => m.Function("boom", () => throw new OutOfMemoryException()));
            var task = vm.Start("import game\ntry:\n    game.boom()\nexcept BaseException:\n    print('caught')");
            task.RunUntilDone();
            Assert.That(task.Error.TypeName, Is.EqualTo("MemoryLimitExceeded"));
        }

        [Test]
        public void AHostBugEndsOnlyTheTask()
        {
            var task = Run(() => throw new InvalidOperationException("secret"));
            Assert.That(task.Error.TypeName, Is.EqualTo("HostError"));
            Assert.That(task.Error.Problem, Does.Not.Contain("secret"));
        }
    }
}
