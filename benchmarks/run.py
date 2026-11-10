#!/usr/bin/env python3
"""Runs the benchmark scripts on every config under benchmarks/configs and on CPython, then writes the chart and table.

    python3 benchmarks/run.py                      # every config that can run here
    python3 benchmarks/run.py dotnet unity-7-il2cpp  # only these
    python3 benchmarks/run.py --report-only        # rebuild docs/performance.svg and results/table.md from results/*.json

A config is a folder in benchmarks/configs with a config.json and a run.py; one whose editor or module is not installed
is skipped. Each config can also be run alone: python3 benchmarks/configs/<name>/run.py
"""
import os
import subprocess
import sys

import common
import report


def main(argv):
    report_only = "--report-only" in argv
    wanted = [a for a in argv if not a.startswith("--")]
    available = sorted(d for d in os.listdir(common.CONFIGS) if os.path.exists(os.path.join(common.CONFIGS, d, "run.py")))
    unknown = [w for w in wanted if w not in available]
    if unknown:
        print("unknown config(s): " + ", ".join(unknown) + ". Available: " + ", ".join(available))
        return 2

    failed = []
    if not report_only:
        print("CPython ...", flush=True)
        common.write_cpython()
        for name in wanted or available:
            print("\n== " + name, flush=True)
            code = subprocess.run([sys.executable, os.path.join(common.CONFIGS, name, "run.py")], cwd=common.ROOT).returncode
            if code == common.SKIPPED:
                print("(skipped)")
            elif code != 0:
                failed.append(name)
        print()
    report.write_report()
    if failed:
        print("failed: " + ", ".join(failed))
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
