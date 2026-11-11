# Performance

CSharpPython is a bytecode interpreter written in plain C#, built to be safe and predictable inside a game rather than to be fast. As a rough guide it runs **between as fast as CPython and about 3 times slower on .NET, and about 1.5 to 5 times slower in a Unity IL2CPP player**, which is itself a slow-ish language (about 6 to 13 times slower under Unity's Mono). Scripts that do game logic once per tick are nowhere near this limit; heavy number crunching belongs in C#, exposed as a [native function](https://github.com/Snaken/CSharpPython/blob/main/docs/host-api.md).

## Results

![Time relative to CPython for each benchmark script on .NET and in Unity players](performance.svg)

The chart shows how many times slower than CPython each runtime is (the dashed line is CPython, and the scale is logarithmic). Measured with CPython 3.14.7, .NET 10.0.402, Unity 6000.3.27f2 and Unity 7000.0.0a16 (an alpha), on macOS arm64 (Apple M5 Pro), release players, best of 7 runs after a one-second warm-up (so a JIT has optimised the interpreter, as it would in a game that has been running for a while):

| Script | CPython 3.14.7 | .NET 10 (JIT) | Unity 7 IL2CPP | Unity 6.3 IL2CPP | Unity 6.3 Mono |
| --- | ---: | ---: | ---: | ---: | ---: |
| builtins_stream | 63.8 ms | 64.8 ms (1.0x) | 112.9 ms (1.8x) | 115.5 ms (1.8x) | 354.1 ms (5.6x) |
| classes | 5.9 ms | 11.2 ms (1.9x) | 19.5 ms (3.3x) | 25.2 ms (4.3x) | 58.7 ms (9.9x) |
| collections_ops | 11.0 ms | 24.0 ms (2.2x) | 29.4 ms (2.7x) | 34.8 ms (3.2x) | 75.4 ms (6.8x) |
| dict_ops | 3.1 ms | 5.3 ms (1.7x) | 8.9 ms (2.9x) | 8.9 ms (2.9x) | 26.3 ms (8.5x) |
| fib | 1.9 ms | 3.9 ms (2.0x) | 5.3 ms (2.7x) | 5.4 ms (2.8x) | 13.8 ms (7.2x) |
| generators | 3.3 ms | 8.7 ms (2.6x) | 12.8 ms (3.8x) | 13.8 ms (4.1x) | 36.0 ms (10.8x) |
| list_ops | 5.1 ms | 9.8 ms (1.9x) | 14.6 ms (2.9x) | 15.2 ms (3.0x) | 42.6 ms (8.4x) |
| loop_sum | 17.0 ms | 27.8 ms (1.6x) | 42.7 ms (2.5x) | 47.8 ms (2.8x) | 166.0 ms (9.7x) |
| random_math | 11.8 ms | 10.2 ms (0.9x) | 15.3 ms (1.3x) | 18.8 ms (1.6x) | 57.9 ms (4.9x) |
| strings | 1.9 ms | 2.1 ms (1.1x) | 4.7 ms (2.5x) | 7.0 ms (3.7x) | 13.0 ms (6.8x) |

On .NET the scripts run from slightly faster than CPython to about 2.6 times slower. A game does not run on .NET, though: under Unity's IL2CPP they run 1.6 to 4.3 times slower than CPython (1.3 to 3.8 on the Unity 7 alpha), and under Unity's Mono 4.9 to 10.8 times slower. Calls (`fib`), generators and object creation (`classes`) are the slowest paths on every runtime, so they are where the work goes next.

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

## Unity's incremental GC

Unity 6 builds with **Use incremental GC** turned on by default. Under IL2CPP that makes every store of a reference into the heap call into the garbage collector (a write barrier), and an interpreter stores a `Value` (which holds a reference) on almost every instruction. In a profile of `fib` under IL2CPP the barrier was about a quarter of the time. Turning the setting off (Project Settings, Player, Other Settings, Configuration) makes the same player run the scripts like this (Unity 6.3 IL2CPP, ms, best of 7):

| Script | Incremental GC on (the default) | Off | Faster by |
| --- | ---: | ---: | ---: |
| builtins_stream | 115.5 | 99.4 | 14% |
| classes | 25.2 | 18.2 | 28% |
| collections_ops | 34.8 | 21.9 | 37% |
| dict_ops | 8.9 | 8.2 | 9% |
| fib | 5.4 | 4.7 | 13% |
| generators | 13.8 | 10.5 | 24% |
| list_ops | 15.2 | 13.4 | 12% |
| loop_sum | 47.8 | 44.1 | 8% |
| random_math | 18.8 | 13.3 | 30% |
| strings | 7.0 | 4.2 | 40% |

What you give up is the spreading of collection work over several frames: with the setting off a collection stops the game for as long as it takes, which is usually a few milliseconds for a small heap and more for a large one. A game that already keeps its allocations down, or one that is not sensitive to a rare longer frame, can take the speed; one with a large heap and a steady frame budget should keep it on. The library does not change the setting. `benchmarks/unity` builds the players with the default, and `BENCH_GC_INCREMENTAL=0` builds one without it.

