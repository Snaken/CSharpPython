namespace CSharpPython
{
    /// <summary>
    /// A snapshot of what a task has cost so far, from <see cref="PyTask.GetDiagnostics"/>. Reading it never walks the
    /// task's memory, so a host can take one every frame, show it in a debug panel or attach it to a report.
    /// </summary>
    public readonly struct TaskDiagnostics
    {
        internal TaskDiagnostics(long steps, long limit, int walks, long lastBytes, long stepsSince, long stepsUntilNext, double walkMs)
        {
            StepsExecuted = steps;
            MemoryLimitBytes = limit;
            MemoryMeasurements = walks;
            LastMeasuredMemoryBytes = lastBytes;
            StepsSinceMemoryMeasured = stepsSince;
            StepsUntilNextMemoryMeasurement = stepsUntilNext;
            MemoryWalkMilliseconds = walkMs;
        }

        /// <summary>The instructions the task has run (<see cref="PyTask.StepsExecuted"/>).</summary>
        public long StepsExecuted { get; }

        /// <summary>The task's <see cref="VmOptions.MaxMemoryBytes"/>, 0 when there is no limit.</summary>
        public long MemoryLimitBytes { get; }

        /// <summary>How many times the task's memory has been walked (<see cref="PyTask.MemoryMeasurements"/>).</summary>
        public int MemoryMeasurements { get; }

        /// <summary>What the last walk found, in bytes; 0 before the first (<see cref="PyTask.LastMeasuredMemoryBytes"/>).</summary>
        public long LastMeasuredMemoryBytes { get; }

        /// <summary>How many steps ago the last walk was taken (<see cref="PyTask.StepsSinceMemoryMeasured"/>).</summary>
        public long StepsSinceMemoryMeasured { get; }

        /// <summary>
        /// The most steps the task can still run before the memory check walks it again, assuming the worst growth the
        /// check allows for: 0 when a walk is due, -1 when there is no limit. A task far below its limit shows a large number.
        /// </summary>
        public long StepsUntilNextMemoryMeasurement { get; }

        /// <summary>
        /// The time all the memory walks have taken, in milliseconds. It is 0 unless <see cref="VmOptions.CollectDiagnostics"/>
        /// was on while they ran.
        /// </summary>
        public double MemoryWalkMilliseconds { get; }
    }
}
