using System;
using System.Collections.Generic;

namespace CSharpPython
{
    /// <summary>
    /// What a group of tasks has cost, added up from <see cref="PyTask.GetDiagnostics"/> by <see cref="Of"/>. Like
    /// one task's diagnostics it never walks a heap, so a host with hundreds of tasks can read it every frame.
    /// </summary>
    public readonly struct TaskTotals
    {
        /// <summary>How many tasks were added up.</summary>
        public int Tasks { get; }

        /// <summary>The instructions all the tasks have run.</summary>
        public long StepsExecuted { get; }

        /// <summary>How many times the tasks' memory has been walked, in all.</summary>
        public long MemoryMeasurements { get; }

        /// <summary>What the last walks found, added up, in bytes. It is a sum of figures of different ages.</summary>
        public long LastMeasuredMemoryBytes { get; }

        /// <summary>The largest last-measured figure of any one task, in bytes.</summary>
        public long LargestMeasuredMemoryBytes { get; }

        /// <summary>The time all the walks of all the tasks have taken; 0 unless <see cref="VmOptions.CollectDiagnostics"/> was on.</summary>
        public double MemoryWalkMilliseconds { get; }

        /// <summary>
        /// How many tasks have a memory limit and are due a walk (<see cref="TaskDiagnostics.StepsUntilNextMemoryMeasurement"/>
        /// is 0), which is to say are close enough to the limit that the next slice may stop them. A host that wants to
        /// pause or warn before the library stops a script can watch this.
        /// </summary>
        public int TasksNearMemoryLimit { get; }

        private TaskTotals(int tasks, long steps, long walks, long bytes, long largest, double walkMs, int near)
        {
            Tasks = tasks;
            StepsExecuted = steps;
            MemoryMeasurements = walks;
            LastMeasuredMemoryBytes = bytes;
            LargestMeasuredMemoryBytes = largest;
            MemoryWalkMilliseconds = walkMs;
            TasksNearMemoryLimit = near;
        }

        /// <summary>
        /// Adds up the diagnostics of <paramref name="tasks"/>. Null entries are skipped; a null collection is empty.
        /// The library keeps no list of the tasks a VM has started, so the host passes the ones it holds.
        /// </summary>
        public static TaskTotals Of(IEnumerable<PyTask> tasks)
        {
            int count = 0, near = 0;
            long steps = 0, walks = 0, bytes = 0, largest = 0;
            double ms = 0;
            if (tasks != null)
            {
                foreach (var task in tasks)
                {
                    if (task == null) continue;
                    var d = task.GetDiagnostics();
                    count++;
                    steps += d.StepsExecuted;
                    walks += d.MemoryMeasurements;
                    bytes += d.LastMeasuredMemoryBytes;
                    largest = Math.Max(largest, d.LastMeasuredMemoryBytes);
                    ms += d.MemoryWalkMilliseconds;
                    if (d.StepsUntilNextMemoryMeasurement == 0) near++;
                }
            }
            return new TaskTotals(count, steps, walks, bytes, largest, ms, near);
        }
    }
}
