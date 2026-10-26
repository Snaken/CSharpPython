#!/usr/bin/env python3
"""Times every script in CSharpPython.Benchmarks/scripts on CSharpPython and on CPython, and prints a Markdown table.

    python3 benchmarks/run.py            # builds and runs everything
"""
import glob
import os
import platform
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
RUNS = 7


def cpython(path):
    code = compile(open(path).read(), path, "exec")
    best = float("inf")
    for _ in range(RUNS + 1):
        start = time.perf_counter()
        exec(code, {})
        best = min(best, time.perf_counter() - start)
    return best * 1000


def csharp():
    project = os.path.join(HERE, "CSharpPython.Benchmarks")
    out = subprocess.run(["dotnet", "run", "-c", "Release", "--project", project],
                         check=True, capture_output=True, text=True, cwd=ROOT).stdout
    return {name: float(ms) for name, ms in (line.split("\t") for line in out.strip().splitlines())}


def main():
    ours = csharp()
    print(f"CPython {platform.python_version()}, .NET {subprocess.run(['dotnet', '--version'], capture_output=True, text=True).stdout.strip()}, "
          f"{platform.system()} {platform.machine()}, best of {RUNS} runs\n")
    print("| Script | CSharpPython | CPython | Slower by |")
    print("| --- | ---: | ---: | ---: |")
    for path in sorted(glob.glob(os.path.join(HERE, "CSharpPython.Benchmarks", "scripts", "*.py"))):
        name = os.path.splitext(os.path.basename(path))[0]
        py = cpython(path)
        print(f"| {name} | {ours[name]:.1f} ms | {py:.1f} ms | {ours[name] / py:.1f}x |")


if __name__ == "__main__":
    sys.exit(main())
