#!/usr/bin/env python3
"""Fail if line coverage in a Cobertura report is below a threshold.
Usage: check-coverage.py <coverage.cobertura.xml> [min-line-percent=95]
"""
import sys
import xml.etree.ElementTree as ET

path = sys.argv[1]
minimum = float(sys.argv[2]) if len(sys.argv) > 2 else 95.0
root = ET.parse(path).getroot()
line = float(root.get("line-rate")) * 100
branch = float(root.get("branch-rate")) * 100
print("line %.2f%%  branch %.2f%%  (minimum line %.1f%%)" % (line, branch, minimum))
sys.exit(0 if line >= minimum else 1)
