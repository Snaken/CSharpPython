using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CSharpPython;

// Times each script in scripts/ on the CSharpPython VM and prints "name<TAB>milliseconds" (best of several runs).
// Compile time is measured separately from run time. See run.py for the comparison with CPython.
internal static class Program
{
    private const int Runs = 7;

    private static int Main(string[] args)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "scripts");
        foreach (string file in Directory.GetFiles(dir, "*.py").OrderBy(f => f, StringComparer.Ordinal))
        {
            string source = File.ReadAllText(file);
            var vm = new PyVm();
            vm.Options.StepsPerSlice = 100000;

            double best = double.MaxValue;
            for (int i = 0; i < Runs + 1; i++)   // the first run warms up the JIT and is dropped
            {
                var watch = Stopwatch.StartNew();
                PyTask task = vm.Start(source);
                while (task.Update() == TaskState.Running) { }
                watch.Stop();
                if (task.State == TaskState.Error)
                {
                    Console.Error.WriteLine($"{Path.GetFileName(file)}: {task.Error}");
                    return 1;
                }
                if (i > 0) best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
            }
            Console.WriteLine($"{Path.GetFileNameWithoutExtension(file)}\t{best:F3}");
        }
        return 0;
    }
}
