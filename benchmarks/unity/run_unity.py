"""Builds the benchmark player in benchmarks/unity/Bench with the Unity CLI, runs it headless and writes the result.

Called by the run.py of each unity-* config folder. The config.json beside it says which editor and scripting backend:
    {"name": "unity-6.3-il2cpp", "label": "Unity 6.3 IL2CPP", "editor": "6000.3", "backend": "IL2CPP"}
"editor" is a version prefix; the newest installed editor that starts with it is used. The build happens in
benchmarks/.work/<name> (ignored by git), so the repository gets no Library, .meta or player files, and a second
run reuses the imported project.
"""
import json
import os
import platform
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import common  # noqa: E402

BUILD_TIMEOUT = 2400
RUN_TIMEOUT = 900


def installed_editors():
    result = subprocess.run(["unity", "editors", "--installed", "--format", "json"], capture_output=True, text=True)
    if result.returncode != 0:
        return []
    return [item["version"] for item in json.loads(result.stdout)["data"]["items"]]


def find_editor(prefix):
    matches = sorted(v for v in installed_editors() if v.startswith(prefix))
    return matches[-1] if matches else None


def platform_settings():
    """The Unity build target and the player's path (relative to the project) for this machine."""
    system = platform.system()
    if system == "Darwin":
        return "StandaloneOSX", "Build/Bench.app", "Build/Bench.app/Contents/MacOS/Bench"
    if system == "Windows":
        return "StandaloneWindows64", "Build/Bench.exe", "Build/Bench.exe"
    return "StandaloneLinux64", "Build/Bench.x86_64", "Build/Bench.x86_64"


def prepare(work, editor):
    shutil.copytree(os.path.join(HERE, "Bench"), work, dirs_exist_ok=True)
    package = os.path.join(work, "Packages", "com.rachlh.csharppython")
    shutil.rmtree(package, ignore_errors=True)
    shutil.copytree(os.path.join(common.ROOT, "src", "CSharpPython.Core"), package,
                    ignore=shutil.ignore_patterns("bin", "obj"))
    streaming = os.path.join(work, "Assets", "StreamingAssets")
    shutil.rmtree(streaming, ignore_errors=True)
    shutil.copytree(common.SCRIPTS, streaming, ignore=shutil.ignore_patterns("*.meta"))
    os.makedirs(os.path.join(work, "ProjectSettings"), exist_ok=True)
    with open(os.path.join(work, "ProjectSettings", "ProjectVersion.txt"), "w") as f:
        f.write("m_EditorVersion: " + editor + "\n")


def main(config_file):
    config_dir = os.path.dirname(os.path.abspath(config_file))
    config = common.load_config(config_dir)
    if shutil.which("unity") is None:
        common.skip("the Unity CLI ('unity') is not installed")
    editor = find_editor(config["editor"])
    if editor is None:
        common.skip("no Unity " + config["editor"] + " editor is installed (found: " + ", ".join(installed_editors()) + ")")

    target, player_dir, player_exe = platform_settings()
    work = os.path.join(common.HERE, ".work", config["name"])
    prepare(work, editor)

    print("building " + config["label"] + " with Unity " + editor + " ...", flush=True)
    env = dict(os.environ, BENCH_BACKEND=config["backend"], BENCH_TARGET=target, BENCH_OUT=os.path.join(work, player_dir))
    build = subprocess.run(["unity", "build", work, "--editor-version", editor, "--target", target,
                            "--execute-method", "BuildBench.Build", "--timeout", str(BUILD_TIMEOUT),
                            "--non-interactive", "--no-banner"], env=env, capture_output=True, text=True)
    if build.returncode != 0:
        print(build.stdout[-3000:])
        print(build.stderr[-1500:])
        sys.exit("the Unity build failed (is the " + config["backend"] + " module for " + target + " installed for " + editor + "?)")

    out_file = os.path.join(work, "timings.txt")
    if os.path.exists(out_file):
        os.remove(out_file)
    print("running the player (about two minutes) ...", flush=True)
    subprocess.run([os.path.join(work, player_exe), "-batchmode", "-nographics", "-benchout", out_file],
                   capture_output=True, timeout=RUN_TIMEOUT)
    if not os.path.exists(out_file):
        sys.exit("the player wrote no timings")
    text = open(out_file).read()
    if "# error" in text:
        sys.exit(text)
    ms = common.parse_timings(text)
    header = text.splitlines()[0].lstrip("# ").strip() if text.startswith("#") else "Unity " + editor
    common.write_result(config, header, ms)
    print("wrote benchmarks/results/" + config["name"] + ".json")
