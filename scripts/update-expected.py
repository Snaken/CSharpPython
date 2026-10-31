#!/usr/bin/env python3
"""Regenerates the .expected file beside each script under tests/CSharpPython.Tests/Scripts by running it on CPython.

A script under errors/ holds several short snippets separated by a line "# ---". Its .expected file lists, one line
for each snippet, the type of the exception CPython raises. A snippet that starts with "# expected: Name" keeps that
name instead, for the few places where CSharpPython deliberately differs.

    python3 scripts/update-expected.py            # every script
    python3 scripts/update-expected.py stdlib     # only the scripts whose path contains "stdlib"

Review the diff of the .expected files before committing: they are the behaviour the tests hold CSharpPython to.
"""
import os
import subprocess
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "tests", "CSharpPython.Tests", "Scripts")
only = sys.argv[1] if len(sys.argv) > 1 else ""

# Runs a snippet and reports the type of the exception it raises on stderr (the snippet's own output is ignored).
RUNNER = """import sys
try:
    exec(compile(sys.stdin.read(), '<snippet>', 'exec'), {'__name__': '__main__'})
except BaseException as e:
    print(type(e).__name__, file=sys.stderr)
else:
    print('(no error)', file=sys.stderr)
"""


def error_types(path):
    with open(path) as f:
        snippets = f.read().split("\n# ---\n")
    names = []
    for snippet in snippets:
        first = snippet.split("\n", 1)[0]
        if first.startswith("# expected: "):
            names.append(first[len("# expected: "):].split(" ")[0])
            continue
        run = subprocess.run([sys.executable, "-I", "-c", RUNNER], input=snippet, capture_output=True, text=True)
        names.append(run.stderr.strip().split("\n")[-1])
    return "\n".join(names) + "\n"


count = 0
for folder, _, files in sorted(os.walk(ROOT)):
    for name in sorted(files):
        path = os.path.join(folder, name)
        if not name.endswith(".py") or only not in path:
            continue
        if os.sep + "errors" + os.sep in path:
            expected = error_types(path)
        else:
            run = subprocess.run([sys.executable, "-I", path], capture_output=True, text=True)
            if run.returncode != 0:
                sys.exit("CPython failed on %s:\n%s" % (path, run.stderr))
            expected = run.stdout
        with open(path[:-3] + ".expected", "w") as out:
            out.write(expected)
        count += 1
print("updated %d expected files" % count)
