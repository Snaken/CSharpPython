#!/usr/bin/env python3
"""Runs the benchmark scripts on the .NET console app in this folder and writes benchmarks/results/dotnet.json."""
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(os.path.dirname(HERE)))
import common  # noqa: E402


def main():
    if subprocess.run(["dotnet", "--version"], capture_output=True).returncode != 0:
        common.skip("the dotnet SDK is not installed")
    out = subprocess.run(["dotnet", "run", "-c", "Release", "--project", HERE],
                         check=True, capture_output=True, text=True, cwd=common.ROOT).stdout
    version = subprocess.run(["dotnet", "--version"], capture_output=True, text=True).stdout.strip()
    common.write_result(common.load_config(HERE), ".NET " + version, common.parse_timings(out))
    print("wrote benchmarks/results/dotnet.json")


if __name__ == "__main__":
    main()
