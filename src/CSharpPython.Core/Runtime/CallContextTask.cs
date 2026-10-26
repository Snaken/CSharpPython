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
        /// Calls a script function, native function or bound method from inside a native function and returns its
        /// result. The call runs to completion before this returns, so it cannot wait. If it raises, the exception
        /// continues up through the calling script as usual. Use this for callbacks such as sort keys.
        /// </summary>
        public Value Call(Value callee, params Value[] args)
        {
            if (Task == null) throw new System.InvalidOperationException("There is no running task to make the call.");
            return Task.CallSync(callee, args ?? System.Array.Empty<Value>());
        }

        /// <summary>
        /// Counts <paramref name="steps"/> of work done inside a native function as script steps. Call it from a native
        /// loop that can run for a long time (once per batch of items is enough), so that it ends the current slice and
        /// is stopped by <see cref="VmOptions.MaxSteps"/> like script code. It does nothing outside a running task.
        /// </summary>
        public void Charge(long steps) => Task?.Charge(steps);

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
