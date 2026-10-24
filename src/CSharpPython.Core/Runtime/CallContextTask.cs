namespace CSharpPython
{
    public sealed partial class CallContext
    {
        internal int WaitTicks = -1;
        internal Value WaitResult;

        /// <summary>The task that is calling the function, or null when called outside a task.</summary>
        public PyTask Task { get; internal set; }

        /// <summary>The VM that owns the calling task.</summary>
        public PyVm Vm => Task?.Vm;

        /// <summary>
        /// Pauses the calling script for <paramref name="ticks"/> ticks (see <see cref="PyTask.Tick"/>). The call
        /// returns <paramref name="result"/> to the script once it resumes. A host function calls this and then
        /// returns normally.
        /// </summary>
        public void Wait(int ticks, Value result = default)
        {
            WaitTicks = ticks < 0 ? 0 : ticks;
            WaitResult = result;
        }
    }
}
