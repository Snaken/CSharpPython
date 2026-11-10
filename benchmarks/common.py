"""Shared helpers for the benchmark runner (run.py) and the per-config run.py files.

A config is a folder under benchmarks/configs with a config.json and a run.py. Running it writes results/<name>.json:
    {"name", "label", "runtime", "machine", "date", "ms": {script: best milliseconds}}
"""
import datetime
import glob
import json
import os
import platform
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SCRIPTS = os.path.join(HERE, "scripts")
RESULTS = os.path.join(HERE, "results")
CONFIGS = os.path.join(HERE, "configs")
CPYTHON_RUNS = 7
SKIPPED = 77  # the exit code of a config's run.py when it cannot run here (missing editor or module); not a failure


def script_names():
    return [os.path.splitext(os.path.basename(p))[0] for p in sorted(glob.glob(os.path.join(SCRIPTS, "*.py")))]


def load_config(config_dir):
    with open(os.path.join(config_dir, "config.json")) as f:
        return json.load(f)


def machine():
    cpu = platform.processor() or platform.machine()
    if platform.system() == "Darwin":
        out = subprocess.run(["sysctl", "-n", "machdep.cpu.brand_string"], capture_output=True, text=True).stdout.strip()
        if out:
            cpu = out
    return {"os": f"{platform.system()} {platform.machine()}", "cpu": cpu}


def write_result(config, runtime, ms):
    os.makedirs(RESULTS, exist_ok=True)
    data = {
        "name": config["name"],
        "label": config["label"],
        "runtime": runtime,
        "machine": machine(),
        "date": datetime.date.today().isoformat(),
        "ms": ms,
    }
    with open(os.path.join(RESULTS, config["name"] + ".json"), "w") as f:
        json.dump(data, f, indent=2)
        f.write("\n")
    return data


def parse_timings(text):
    """Reads lines of 'name<TAB>milliseconds'; lines starting with # are comments."""
    ms = {}
    for line in text.strip().splitlines():
        if line.startswith("#") or "\t" not in line:
            continue
        name, value = line.split("\t")
        ms[name] = float(value)
    return ms


def cpython_timings():
    ms = {}
    for path in sorted(glob.glob(os.path.join(SCRIPTS, "*.py"))):
        code = compile(open(path).read(), path, "exec")
        best = float("inf")
        for _ in range(CPYTHON_RUNS + 1):
            start = time.perf_counter()
            exec(code, {})
            best = min(best, time.perf_counter() - start)
        ms[os.path.splitext(os.path.basename(path))[0]] = best * 1000
    return ms


def write_cpython():
    ms = cpython_timings()
    data = {"name": "cpython", "label": "CPython " + platform.python_version(), "runtime": "CPython " + platform.python_version(),
            "machine": machine(), "date": datetime.date.today().isoformat(), "ms": ms}
    os.makedirs(RESULTS, exist_ok=True)
    with open(os.path.join(RESULTS, "cpython.json"), "w") as f:
        json.dump(data, f, indent=2)
        f.write("\n")
    return data


def load_results():
    results = {}
    for path in glob.glob(os.path.join(RESULTS, "*.json")):
        with open(path) as f:
            data = json.load(f)
        results[data["name"]] = data
    return results


def skip(reason):
    print("SKIPPED: " + reason)
    sys.exit(SKIPPED)
