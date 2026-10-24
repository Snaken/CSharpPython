namespace CSharpPython
{
    /// <summary>Limits and settings for a <see cref="PyVm"/>.</summary>
    public sealed class VmOptions
    {
        /// <summary>How many instructions a task runs per call to <see cref="PyTask.Run"/> before it yields back.</summary>
        public int StepsPerSlice = 10000;

        /// <summary>
        /// A hard cap on the total instructions one task may execute, or 0 for no cap. Reaching it ends the task
        /// with an error that scripts cannot catch, so a runaway <c>while True:</c> cannot hang a game.
        /// </summary>
        public long MaxSteps = 0;

        /// <summary>The deepest nesting of function calls. Going deeper raises a catchable <c>RecursionError</c>.</summary>
        public int MaxCallDepth = 200;

        /// <summary>The most events that may wait in a task's queue; further events are dropped.</summary>
        public int MaxQueuedEvents = 1000;
    }

    /// <summary>Where a task is in its life.</summary>
    public enum TaskState
    {
        /// <summary>Ready to run (or paused because its slice of steps ran out). Call <see cref="PyTask.Run"/> again.</summary>
        Running,

        /// <summary>Paused by a host function (see <see cref="CallContext.Wait"/>) until enough ticks have passed.</summary>
        Waiting,

        /// <summary>The script ran to the end. It can still handle events.</summary>
        Finished,

        /// <summary>The script stopped with an error; see <see cref="PyTask.Error"/>.</summary>
        Error,
    }
}
