# Performance

CSharpPython is a bytecode interpreter written in plain C#, built to be safe and predictable inside a game rather than to be fast. As a rough guide it runs **between as fast as CPython and about 3 times slower on .NET, and about 1.5 to 6 times slower in a Unity IL2CPP player**, which is itself a slow-ish language (about 6 to 19 times slower under Unity's Mono). Scripts that do game logic once per tick are nowhere near this limit; heavy number crunching belongs in C#, exposed as a [native function](https://github.com/Snaken/CSharpPython/blob/main/docs/host-api.md).

## Results

![Time relative to CPython for each benchmark script on .NET and in Unity players](performance.svg)

The chart shows how many times slower than CPython each runtime is (the dashed line is CPython, and the scale is logarithmic). Measured with CPython 3.14.7, .NET 10.0.402, Unity 6000.3.27f2 and Unity 7000.0.0a16 (an alpha), on macOS arm64 (Apple M5 Pro), release players, best of 7 runs after a one-second warm-up (so a JIT has optimised the interpreter, as it would in a game that has been running for a while):

| Script | CPython 3.14.7 | .NET 10 (JIT) | Unity 7 IL2CPP | Unity 6.3 IL2CPP | Unity 6.3 Mono |
| --- | ---: | ---: | ---: | ---: | ---: |
| builtins_stream | 64.3 ms | 63.2 ms (1.0x) | 113.8 ms (1.8x) | 115.9 ms (1.8x) | 365.9 ms (5.7x) |
| classes | 6.0 ms | 15.8 ms (2.6x) | 34.0 ms (5.7x) | 50.8 ms (8.5x) | 111.2 ms (18.6x) |
| collections_ops | 11.0 ms | 25.1 ms (2.3x) | 30.1 ms (2.7x) | 35.5 ms (3.2x) | 86.0 ms (7.8x) |
| dict_ops | 3.1 ms | 5.1 ms (1.6x) | 8.8 ms (2.8x) | 9.1 ms (2.9x) | 27.1 ms (8.7x) |
| fib | 1.9 ms | 4.8 ms (2.5x) | 6.5 ms (3.3x) | 8.1 ms (4.2x) | 20.4 ms (10.5x) |
| generators | 3.5 ms | 9.4 ms (2.7x) | 14.6 ms (4.1x) | 15.4 ms (4.4x) | 44.9 ms (12.7x) |
| list_ops | 5.3 ms | 9.4 ms (1.8x) | 14.8 ms (2.8x) | 15.3 ms (2.9x) | 43.8 ms (8.3x) |
| loop_sum | 17.2 ms | 26.9 ms (1.6x) | 45.3 ms (2.6x) | 47.7 ms (2.8x) | 169.4 ms (9.8x) |
| random_math | 11.7 ms | 11.5 ms (1.0x) | 16.7 ms (1.4x) | 20.7 ms (1.8x) | 70.2 ms (6.0x) |
| strings | 2.0 ms | 2.1 ms (1.0x) | 4.8 ms (2.4x) | 7.1 ms (3.5x) | 13.3 ms (6.5x) |

On .NET the scripts run from as fast as CPython to about 2.6 times slower. A game does not run on .NET, though: under Unity's IL2CPP they run 1.4 to 8.5 times slower than CPython (1.4 to 5.7 on the Unity 7 alpha), and under Unity's Mono 5.7 to 18.6 times slower. Object creation (`classes`), calls (`fib`) and generators are the slowest paths on every runtime, so they are where the work goes next.

What each script does:

- **builtins_stream**: `sum`, `max`, `min(key=)`, `any` and `all` over a million-item range, and a generator sum of 100,000 floats.
- **classes**: 100,000 method calls that each create an object.
- **collections_ops**: `Counter` and `defaultdict` over 50,000 words, a `deque` used as a queue, and 30,000 `namedtuple` instances.
- **dict_ops**: 100,000 `dict.get` and item assignments.
- **fib**: recursive `fib(24)`, about 75,000 calls.
- **generators**: sum 200,000 values from a generator.
- **list_ops**: append 100,000 items, sort, then a list comprehension.
- **loop_sum**: a million-iteration `for` loop with arithmetic.
- **random_math**: 50,000 rounds of `random` and `math` calls, 50 shuffles of 1,000 items and a `sample`.
- **strings**: build and split 20,000 f-strings, `join`, `upper`, `count`.

These numbers are one machine and one run, so treat them as an order of magnitude. WebGL and mobile builds are not measured here, and a phone is much slower than this laptop, so measure in your own target.

## Running it yourself

```
python3 benchmarks/run.py                          # every config that can run on this machine
python3 benchmarks/run.py dotnet unity-7-il2cpp    # only these
python3 benchmarks/run.py --report-only            # rebuild the chart and table from benchmarks/results
```

Each folder in `benchmarks/configs` is one way of running the scripts: `dotnet` (a console app), `unity-6.3-mono`, `unity-6.3-il2cpp` and `unity-7-il2cpp`. A Unity config builds a small headless player from `benchmarks/unity/Bench` with the [Unity CLI](https://unity.com/) (`unity`), runs it, and reads the timings it writes; it needs that editor version and the platform's Mono or IL2CPP module installed, and a config whose editor is missing is skipped. The builds happen in `benchmarks/.work`, which git ignores. Each config can also be run alone (`python3 benchmarks/configs/unity-6.3-il2cpp/run.py`), and a new one is a folder with a `config.json` and a `run.py`.

Every run writes `benchmarks/results/<config>.json`, then `docs/performance.svg` (the chart above) and `benchmarks/results/table.md` (the table above). The scripts in `benchmarks/scripts` are ordinary Python that runs unchanged on both interpreters (each checks its own result with `assert`), so you can add your own.

## Keeping scripts fast

- Put hot code inside functions: local variables are faster than globals.
- Do the heavy work in a native function and call it from the script.
- Hand a generator expression to `sum`, `any`, `all` or `list` (`any(e.hp <= 0 for e in enemies)`) instead of looping over it in a `for` loop: the built-ins take its values in one run, about twice as fast as a `for` loop over the same generator.
- Keep key and filter functions small: a `lambda` that only does arithmetic, comparisons and reads (`key=lambda p: p.hp`) runs several times faster inside `sorted`, `min`, `max` and `map` than one that calls other functions.
- `StepsPerSlice` only decides how often a task yields back to the game; it does not change the total work done.

## The first script in a process

Where the .NET runtime compiles code the first time it runs (desktop .NET, Mono, and so the Unity editor), the first script a process compiles and runs pays for that, whatever the script is: measured on net10.0 with an Explorer-like script, about 13 ms for the first compile and 11 ms for the first `Update`, then 0.05 ms and 1.3 ms for 192 tasks. A game that starts many tasks at once sees this as one slow first frame. Call `PyVm.WarmUp()` while the game loads: it runs a short script on a VM of its own (about 50 ms the first time, under 1 ms after), also saves and restores a task, and takes the first compile and the first tick of 192 tasks down to 0.2 and 1.4 ms. `BlockEditor.WarmUp()` does the same for the block compiler and editor (about 25 ms; the first block compile and editor use fall from 28 ms to 0.4 ms). Both touch nothing of yours, never throw, and can be called more than once. Under IL2CPP the code is compiled ahead of time and there is little to gain.
