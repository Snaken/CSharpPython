#!/usr/bin/env python3
"""Regenerates the .expected file beside each script under tests/CSharpPython.Tests/Scripts by running it on CPython.

    python3 scripts/update-expected.py            # every script
    python3 scripts/update-expected.py stdlib     # only the scripts whose path contains "stdlib"

Review the diff of the .expected files before committing: they are the behaviour the tests hold CSharpPython to.
"""
import os
import subprocess
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "tests", "CSharpPython.Tests", "Scripts")
only = sys.argv[1] if len(sys.argv) > 1 else ""

count = 0
for folder, _, files in sorted(os.walk(ROOT)):
    for name in sorted(files):
        path = os.path.join(folder, name)
        if not name.endswith(".py") or only not in path:
            continue
        run = subprocess.run([sys.executable, "-I", path], capture_output=True, text=True)
        if run.returncode != 0:
            sys.exit("CPython failed on %s:\n%s" % (path, run.stderr))
        with open(path[:-3] + ".expected", "w") as out:
            out.write(run.stdout)
        count += 1
print("updated %d expected files" % count)
