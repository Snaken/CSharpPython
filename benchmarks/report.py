"""Turns benchmarks/results/*.json into docs/performance.svg (a bar chart) and benchmarks/results/table.md."""
import math
import os
from xml.sax.saxutils import escape

import common

ORDER = ["dotnet", "unity-7-il2cpp", "unity-6.3-il2cpp", "unity-6.3-mono"]
# Okabe-Ito colours, which stay apart for colour-blind readers and read on light and dark backgrounds.
COLORS = ["#0072B2", "#009E73", "#E69F00", "#D55E00", "#CC79A7", "#56B4E9"]
SVG_PATH = os.path.join(common.ROOT, "docs", "performance.svg")
TABLE_PATH = os.path.join(common.RESULTS, "table.md")


def ordered(results):
    names = [n for n in ORDER if n in results] + sorted(n for n in results if n not in ORDER and n != "cpython")
    return [n for n in names if n != "cpython"]


def table(results, scripts):
    names = ordered(results)
    cpython = results["cpython"]
    head = "| Script | " + " | ".join([cpython["label"]] + [results[n]["label"] for n in names]) + " |"
    rule = "| --- | " + " | ".join(["---:"] * (len(names) + 1)) + " |"
    rows = [head, rule]
    for s in scripts:
        base = cpython["ms"][s]
        cells = [f"{base:.1f} ms"]
        for n in names:
            ms = results[n]["ms"].get(s)
            cells.append("n/a" if ms is None else f"{ms:.1f} ms ({ms / base:.1f}x)")
        rows.append(f"| {s} | " + " | ".join(cells) + " |")
    return "\n".join(rows) + "\n"


def chart(results, scripts):
    names = ordered(results)
    cpython = results["cpython"]["ms"]
    ratios = {n: {s: results[n]["ms"][s] / cpython[s] for s in scripts if s in results[n]["ms"]} for n in names}
    top_ratio = max(max(r.values()) for r in ratios.values())
    low_ratio = min(min(r.values()) for r in ratios.values())
    hi = 2 ** math.ceil(math.log2(max(top_ratio, 2)))
    lo = 0.5 if low_ratio < 0.75 else 0.8

    width, left, right, top, bottom = 960, 58, 16, 64, 58
    height = 470
    plot_w, plot_h = width - left - right, height - top - bottom

    def y(v):
        return top + plot_h * (1 - (math.log2(v) - math.log2(lo)) / (math.log2(hi) - math.log2(lo)))

    group_w = plot_w / len(scripts)
    inner = group_w * 0.8
    bar_w = inner / len(names)
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" role="img" aria-labelledby="t d">',
           '<title id="t">CSharpPython speed relative to CPython</title>',
           '<desc id="d">Grouped bars for each benchmark script: how many times slower than CPython the interpreter runs on each runtime. '
           'A bar above the CPython line is slower, below it is faster. The scale is logarithmic.</desc>',
           '<style>',
           ':root{--bg:#ffffff;--ink:#1f2328;--muted:#656d76;--grid:#d8dee4;--base:#1f2328}',
           '@media (prefers-color-scheme: dark){:root{--bg:#0d1117;--ink:#e6edf3;--muted:#8b949e;--grid:#30363d;--base:#e6edf3}}',
           'text{font-family:-apple-system,"Segoe UI",Helvetica,Arial,sans-serif;fill:var(--ink)}',
           '.m{fill:var(--muted)}.g{stroke:var(--grid);stroke-width:1}.b{stroke:var(--base);stroke-width:1.5;stroke-dasharray:5 3}',
           '</style>',
           f'<rect width="{width}" height="{height}" fill="var(--bg)"/>']

    ticks = ([0.5] if lo == 0.5 else []) + [2 ** k for k in range(0, int(math.log2(hi)) + 1)]
    for v in ticks:
        out.append(f'<line class="g" x1="{left}" x2="{width - right}" y1="{y(v):.1f}" y2="{y(v):.1f}"/>')
        out.append(f'<text class="m" x="{left - 8}" y="{y(v) + 4:.1f}" font-size="11" text-anchor="end">{v:g}x</text>')
    out.append(f'<line class="b" x1="{left}" x2="{width - right}" y1="{y(1):.1f}" y2="{y(1):.1f}"/>')
    out.append(f'<line class="b" x1="{width - right - 112}" x2="{width - right - 88}" y1="28" y2="28"/>')
    out.append(f'<text x="{width - right}" y="33" font-size="12" text-anchor="end">CPython (1x)</text>')

    for gi, s in enumerate(scripts):
        gx = left + gi * group_w + (group_w - inner) / 2
        for ni, n in enumerate(names):
            ms = results[n]["ms"].get(s)
            if ms is None:
                continue
            r = ms / cpython[s]
            y1, y2 = y(1), y(r)
            out.append(f'<rect x="{gx + ni * bar_w + 0.5:.1f}" y="{min(y1, y2):.1f}" width="{bar_w - 1:.1f}" '
                       f'height="{max(abs(y1 - y2), 1):.1f}" fill="{COLORS[ni % len(COLORS)]}">'
                       f'<title>{escape(results[n]["label"])}, {s}: {ms:.1f} ms, {r:.1f}x CPython</title></rect>')
        out.append(f'<text x="{left + (gi + 0.5) * group_w:.1f}" y="{height - bottom + 18}" font-size="11" text-anchor="middle">{s}</text>')

    lx = left
    for ni, n in enumerate(names):
        label = results[n]["label"]
        out.append(f'<rect x="{lx}" y="22" width="12" height="12" fill="{COLORS[ni % len(COLORS)]}"/>')
        out.append(f'<text x="{lx + 18}" y="33" font-size="12">{escape(label)}</text>')
        lx += 30 + 7.2 * len(label)
    cp = results["cpython"]
    out.append(f'<text class="m" x="{left}" y="{height - 14}" font-size="11">Time relative to {escape(cp["runtime"])} on the same machine '
               f'({escape(cp["machine"]["cpu"])}), best of 7 runs after warm-up. Log scale: above the line is slower.</text>')
    out.append("</svg>")
    return "\n".join(out) + "\n"


def write_report():
    results = common.load_results()
    if "cpython" not in results:
        raise SystemExit("no results/cpython.json: run the runner without --report-only first")
    scripts = common.script_names()
    names = ordered(results)
    if not names:
        raise SystemExit("no results to report")
    machines = {results[n]["machine"]["cpu"] for n in names + ["cpython"]}
    if len(machines) > 1:
        print("warning: the results come from different machines: " + ", ".join(sorted(machines)))
    with open(SVG_PATH, "w") as f:
        f.write(chart(results, scripts))
    with open(TABLE_PATH, "w") as f:
        f.write(table(results, scripts))
    print("wrote docs/performance.svg and benchmarks/results/table.md")
    return results
