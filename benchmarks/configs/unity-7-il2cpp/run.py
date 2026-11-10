#!/usr/bin/env python3
"""Builds and runs the benchmark player for this config (see benchmarks/unity/run_unity.py) and writes benchmarks/results/<name>.json."""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(HERE)), "unity"))
import run_unity  # noqa: E402

if __name__ == "__main__":
    run_unity.main(__file__)
