using System.Text;

namespace CSharpPython.Tests.Vm
{
    public class EventTests
    {
        private static PyVm NewVm(StringBuilder sb, VmOptions options = null)
        {
            var vm = new PyVm(options) { Output = s => sb.Append(s) };
            vm.RegisterModule("game", m => m
                .Native("wait", (ctx, a) => { ctx.Wait((int)a[0].AsInt()); return Value.None; }, 1, 1));
            return vm;
        }

        [Test]
        public void HandlerRunsWhileMainScriptWaits()
        {
            var sb = new StringBuilder();
            const string src = @"
import game
def on_hit(amount):
    print('hit', amount)
print('start')
game.wait(10)
print('end')";
            var task = NewVm(sb).Start(src);
            Assert.That(task.Update(), Is.EqualTo(TaskState.Waiting));
            task.QueueEvent("on_hit", 5);
            Assert.That(task.Update(), Is.EqualTo(TaskState.Waiting));
            Assert.That(sb.ToString(), Is.EqualTo("start\nhit 5\n"));
            Assert.That(task.WaitTicksRemaining, Is.EqualTo(9));
            task.RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("start\nhit 5\nend\n"));
        }

        [Test]
        public void HandlerRunsAfterTheMainScriptFinished()
        {
            var sb = new StringBuilder();
            var task = NewVm(sb).Start("def on_ping():\n    print('pong')");
            Assert.That(task.Run(), Is.EqualTo(TaskState.Finished));
            task.QueueEvent("on_ping");
            Assert.That(task.PendingEvents, Is.EqualTo(1));
            Assert.That(task.Run(), Is.EqualTo(TaskState.Finished));
            Assert.That(sb.ToString(), Is.EqualTo("pong\n"));
            Assert.That(task.PendingEvents, Is.EqualTo(0));
        }

        [Test]
        public void EventsAreHandledInOrderOneAtATime()
        {
            var sb = new StringBuilder();
            const string src = @"
import game
log = []
def on_a(n):
    game.wait(2)
    log.append('a' + str(n))
def on_b():
    log.append('b')";
            var task = NewVm(sb).Start(src);
            task.Run();
            task.QueueEvent("on_a", 1);
            task.QueueEvent("on_b");
            task.QueueEvent("on_a", 2);
            task.RunUntilDone();
            Assert.That(task.Globals["log"].Repr(), Is.EqualTo("['a1', 'b', 'a2']"));
        }

        [Test]
        public void HandlersCanBeLongRunningAndInterruptTheMainScript()
        {
            var sb = new StringBuilder();
            const string src = @"
import game
def on_go():
    for i in range(3):
        print('h', i)
        game.wait(1)
print('m0')
game.wait(2)
print('m1')
game.wait(5)
print('m2')";
            var task = NewVm(sb).Start(src);
            task.Update();
            task.QueueEvent("on_go");
            for (int i = 0; i < 20; i++) task.Update();
            Assert.That(task.State, Is.EqualTo(TaskState.Finished));
            string text = sb.ToString();
            Assert.That(text, Does.Contain("h 0\nh 1\nh 2"));
            Assert.That(text.IndexOf("m0"), Is.LessThan(text.IndexOf("h 0")));
            Assert.That(text.IndexOf("m2"), Is.GreaterThan(text.IndexOf("h 2")));
        }

        [Test]
        public void MainWaitKeepsElapsingWhileAHandlerWaits()
        {
            var sb = new StringBuilder();
            const string src = @"
import game
def on_slow():
    game.wait(10)
    print('handler done')
print('m0')
game.wait(3)
print('m1')";
            var task = NewVm(sb).Start(src);
            task.Update();
            task.QueueEvent("on_slow");
            for (int i = 0; i < 4; i++) task.Update();
            // The main script's 3 tick wait has run out, but the handler still holds the task.
            Assert.That(sb.ToString(), Is.EqualTo("m0\n"));
            for (int i = 0; i < 20; i++) task.Update();
            Assert.That(sb.ToString(), Is.EqualTo("m0\nhandler done\nm1\n"));
        }

        [Test]
        public void EventForUndefinedHandlerIsIgnoredAfterTheScriptRan()
        {
            var sb = new StringBuilder();
            var task = NewVm(sb).Start("x = 1");
            task.Run();
            task.QueueEvent("on_nothing");
            task.QueueEvent("x");
            Assert.That(task.Run(), Is.EqualTo(TaskState.Finished));
            Assert.That(task.PendingEvents, Is.EqualTo(0));
        }

        [Test]
        public void EventQueuedBeforeTheHandlerIsDefinedWaitsForIt()
        {
            var sb = new StringBuilder();
            var task = NewVm(sb, new VmOptions { StepsPerSlice = 3 }).Start("y = 1\ny = 2\ny = 3\ndef on_x():\n    print('x')\ny = 4");
            task.QueueEvent("on_x");
            task.RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("x\n"));
        }

        [Test]
        public void HandlersSeeAndChangeGlobals()
        {
            var task = NewVm(new StringBuilder()).Start("hp = 10\ndef on_damage(n):\n    global hp\n    hp -= n");
            task.Run();
            task.QueueEvent("on_damage", 3);
            task.QueueEvent("on_damage", 4);
            task.Run();
            Assert.That(task.Globals["hp"].AsInt(), Is.EqualTo(3));
        }

        [Test]
        public void HandlersMayUseDefaultAndKeywordParameters()
        {
            var sb = new StringBuilder();
            var task = NewVm(sb).Start("def on_x(a, b=2):\n    print(a, b)");
            task.Run();
            task.QueueEvent("on_x", 1);
            task.QueueEvent("on_x", 1, 5);
            task.Run();
            Assert.That(sb.ToString(), Is.EqualTo("1 2\n1 5\n"));
        }

        [Test]
        public void ErrorInHandlerFailsTheTask()
        {
            var sb = new StringBuilder();
            const string src = "import game\ndef on_bad():\n    return 1 / 0\nprint('start')\ngame.wait(5)\nprint('never')";
            var task = NewVm(sb).Start(src);
            task.Update();
            task.QueueEvent("on_bad");
            Assert.That(task.Update(), Is.EqualTo(TaskState.Error));
            Assert.That(task.Error.TypeName, Is.EqualTo("ZeroDivisionError"));
            Assert.That(task.Error.Trace[0].Function, Is.EqualTo("on_bad"));
            Assert.That(task.Error.Trace[0].Line, Is.EqualTo(3));
            Assert.That(sb.ToString(), Is.EqualTo("start\n"));
        }

        [Test]
        public void HandlerCanCatchItsOwnErrors()
        {
            var sb = new StringBuilder();
            var task = NewVm(sb).Start("def on_x():\n    try:\n        1 / 0\n    except ZeroDivisionError:\n        print('handled')");
            task.Run();
            task.QueueEvent("on_x");
            Assert.That(task.Run(), Is.EqualTo(TaskState.Finished));
            Assert.That(sb.ToString(), Is.EqualTo("handled\n"));
        }

        [Test]
        public void WrongArgumentCountFailsTheTask()
        {
            var task = NewVm(new StringBuilder()).Start("def on_x(a):\n    pass");
            task.Run();
            task.QueueEvent("on_x");
            Assert.That(task.Run(), Is.EqualTo(TaskState.Error));
            Assert.That(task.Error.Problem, Does.Contain("missing 1 required positional argument"));
        }

        [Test]
        public void QueueIsBounded()
        {
            var task = NewVm(new StringBuilder(), new VmOptions { MaxQueuedEvents = 2 }).Start("def on_x():\n    pass");
            Assert.That(task.QueueEvent("on_x"), Is.True);
            Assert.That(task.QueueEvent("on_x"), Is.True);
            Assert.That(task.QueueEvent("on_x"), Is.False);
            Assert.That(task.PendingEvents, Is.EqualTo(2));
        }

        [Test]
        public void EventArgumentsMayBeNull()
        {
            var task = NewVm(new StringBuilder()).Start("def on_x():\n    pass");
            task.Run();
            Assert.That(task.QueueEvent("on_x", null), Is.True);
            Assert.That(task.Run(), Is.EqualTo(TaskState.Finished));
        }

        [Test]
        public void HandlerInterruptsALongRunningMainLoopBetweenSlices()
        {
            var sb = new StringBuilder();
            var task = NewVm(sb, new VmOptions { StepsPerSlice = 100 })
                .Start("count = 0\ndef on_stop():\n    global running\n    running = False\nrunning = True\nwhile running:\n    count += 1\nprint('stopped')");
            for (int i = 0; i < 5; i++) task.Run();
            Assert.That(task.State, Is.EqualTo(TaskState.Running));
            task.QueueEvent("on_stop");
            for (int i = 0; i < 3; i++) task.Run();
            Assert.That(task.State, Is.EqualTo(TaskState.Finished));
            Assert.That(sb.ToString(), Is.EqualTo("stopped\n"));
        }

        [Test]
        public void HandlerInterruptingMidExpressionDoesNotCorruptTheStack()
        {
            // With a one step slice the handler is dispatched between every pair of instructions.
            var task = NewVm(new StringBuilder(), new VmOptions { StepsPerSlice = 1 })
                .Start("n = 0\ndef on_tick():\n    global n\n    n += 1\ntotal = 0\nfor i in range(20):\n    total += [i, 1][0] * 2 + 1");
            for (int i = 0; i < 20000 && task.State != TaskState.Finished; i++)
            {
                if (i % 40 == 0) task.QueueEvent("on_tick");
                task.Run();
            }
            Assert.That(task.State, Is.EqualTo(TaskState.Finished));
            Assert.That(task.Globals["total"].AsInt(), Is.EqualTo(400));
            Assert.That(task.Globals["n"].AsInt(), Is.GreaterThanOrEqualTo(5));
        }
    }
}
