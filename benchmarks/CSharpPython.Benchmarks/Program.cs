using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CSharpPython;

// Times each script in scripts/ on the CSharpPython VM and prints "name<TAB>milliseconds" (best of several runs after a warm-up).
// Compile time is measured separately from run time. See run.py for the comparison with CPython.
namespace CSharpPython.Benchmarks
{
    internal static class Program
    {
        private const int Runs = 7;
        private const int WarmUpMilliseconds = 1000;

        private static int Main(string[] args)
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "scripts");
            foreach (string file in Directory.GetFiles(dir, "*.py").OrderBy(f => f, StringComparer.Ordinal))
            {
                string source = File.ReadAllText(file);
                var vm = new PyVm();
                vm.Options.StepsPerSlice = 100000;

                // Warm up for a second first, so the JIT has promoted the interpreter loop to fully optimised code, as it
                // would be in a game that runs scripts for a while. Then keep the best of several runs.
                var warmUp = Stopwatch.StartNew();
                while (warmUp.ElapsedMilliseconds < WarmUpMilliseconds)
                    if (Run(vm, source, file) < 0) return 1;
                double best = double.MaxValue;
                for (int i = 0; i < Runs; i++)
                {
                    double ms = Run(vm, source, file);
                    if (ms < 0) return 1;
                    best = Math.Min(best, ms);
                }
                Console.WriteLine($"{Path.GetFileNameWithoutExtension(file)}\t{best:F3}");
            }
            return 0;
        }

        // One run of the script in milliseconds, or -1 after printing the error it ended with.
        private static double Run(PyVm vm, string source, string file)
        {
            var watch = Stopwatch.StartNew();
            PyTask task = vm.Start(source);
            while (task.Update() == TaskState.Running) { }
            watch.Stop();
            if (task.State != TaskState.Error) return watch.Elapsed.TotalMilliseconds;
            Console.Error.WriteLine($"{Path.GetFileName(file)}: {task.Error}");
            return -1;
        }
    }
}
