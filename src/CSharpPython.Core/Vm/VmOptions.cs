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

        /// <summary>
        /// The most bits an integer result may have. Bigger results raise a catchable <c>OverflowError</c>, so a script
        /// cannot stall the game with <c>2 ** 10 ** 9</c>. Python itself has no such limit.
        /// </summary>
        public int MaxIntBits = 16384;

        /// <summary>The default of <see cref="MaxMemoryBytes"/>: 64 MB.</summary>
        public const long DefaultMaxMemoryBytes = 64L * 1024 * 1024;

        /// <summary>
        /// The most memory one task may hold, in bytes, or 0 for no limit. It is an estimate of what the script can reach
        /// (see <see cref="PyTask.EstimatedMemoryBytes"/>), not of what the process uses, and it is checked as the task runs,
        /// so a script can pass it by a little before it is stopped. Going over ends the task with an uncatchable
        /// <c>MemoryLimitExceeded</c> error, as <see cref="MaxSteps"/> does. The default is 64 MB; any positive size may be set.
        /// A negative value is refused when the <see cref="PyVm"/> is made. See docs/resource-limits.md.
        /// </summary>
        public long MaxMemoryBytes = DefaultMaxMemoryBytes;

        /// <summary>The default of <see cref="MaxSequenceLength"/>.</summary>
        public const int DefaultMaxSequenceLength = 100000;

        /// <summary>The most <see cref="MaxSequenceLength"/> may be set to: the size the VM was built and tested for.</summary>
        public const int SequenceLengthCeiling = 1 << 22;

        /// <summary>
        /// The most items one string, list, tuple, dict, set or deque may hold, and the widest a format field may be.
        /// Building something bigger raises a catchable <c>MemoryError</c>. The default, 100,000, suits a game; a host that
        /// needs more can raise it up to <see cref="SequenceLengthCeiling"/> (4,194,304). Any other value is refused
        /// when the <see cref="PyVm"/> is made. See docs/resource-limits.md.
        /// </summary>
        public int MaxSequenceLength = DefaultMaxSequenceLength;

        /// <summary>
        /// Turns on <see cref="PyTask.Save"/> and <see cref="PyVm.Restore"/>. It is off by default so that a game
        /// that never saves script state does not depend on it; calling them while it is off throws.
        /// </summary>
        public bool EnableSnapshots = false;
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
