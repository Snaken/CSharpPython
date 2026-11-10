using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

// Builds the benchmark player. Called by unity/run_unity.py through "unity build --execute-method BuildBench.Build" with
// BENCH_BACKEND (Mono or IL2CPP), BENCH_TARGET (a BuildTarget name) and BENCH_OUT (the player path) in the environment.
public static class BuildBench
{
    public static void Build()
    {
        string backend = Environment.GetEnvironmentVariable("BENCH_BACKEND") ?? "Mono";
        string output = Environment.GetEnvironmentVariable("BENCH_OUT") ?? "Build/Bench";
        var buildTarget = (BuildTarget)Enum.Parse(typeof(BuildTarget), Environment.GetEnvironmentVariable("BENCH_TARGET") ?? "StandaloneOSX");
        var target = NamedBuildTarget.Standalone;
        PlayerSettings.productName = "Bench";
        PlayerSettings.SetScriptingBackend(target, backend == "IL2CPP" ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);
        PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.Disabled);
        EditorUserBuildSettings.development = false;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/Empty.unity");
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Empty.unity" },
            locationPathName = output,
            target = buildTarget,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
