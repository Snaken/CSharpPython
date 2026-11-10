using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CSharpPython;
using UnityEngine;

// Runs the benchmark scripts in a player, as benchmarks/CSharpPython.Benchmarks does, and writes name<TAB>ms to a file.
public static class BenchRunner
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Start()
    {
        string[] args = Environment.GetCommandLineArgs();
        int at = Array.IndexOf(args, "-benchout");
        if (at < 0) return;
        string outPath = args[at + 1];
        var sb = new StringBuilder();
#if ENABLE_IL2CPP
        string backend = "IL2CPP";
#else
        string backend = "Mono";
#endif
        sb.AppendLine("# Unity " + Application.unityVersion + " " + backend + " " + (UnityEngine.Debug.isDebugBuild ? "development" : "release"));
        try
        {
            string dir = Application.streamingAssetsPath;
            foreach (string file in Directory.GetFiles(dir, "*.py").OrderBy(f => f, StringComparer.Ordinal))
            {
                string source = File.ReadAllText(file);
                var vm = new PyVm();
                vm.Options.StepsPerSlice = 100000;
                vm.Options.MaxSequenceLength = 1000000;
                var warm = Stopwatch.StartNew();
                while (warm.ElapsedMilliseconds < 1000) RunOnce(vm, source);
                double best = double.MaxValue;
                for (int i = 0; i < 7; i++) best = Math.Min(best, RunOnce(vm, source));
                sb.AppendLine(Path.GetFileNameWithoutExtension(file) + "\t" + best.ToString("F3"));
            }
        }
        catch (Exception e) { sb.AppendLine("# error " + e); }
        File.WriteAllText(outPath, sb.ToString());
        Application.Quit();
    }

    private static double RunOnce(PyVm vm, string source)
    {
        var w = Stopwatch.StartNew();
        PyTask t = vm.Start(source);
        while (t.Update() == TaskState.Running) { }
        w.Stop();
        if (t.State == TaskState.Error) throw new Exception(t.Error.ToString());
        return w.Elapsed.TotalMilliseconds;
    }
}
