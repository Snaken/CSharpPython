# Performance

CSharpPython is a bytecode interpreter written in plain C#, built to be safe and predictable inside a game rather than to be fast. As a rough guide it runs **2 to 5 times slower than CPython**, which is itself a slow-ish language. Scripts that do game logic once per tick are nowhere near this limit; heavy number crunching belongs in C#, exposed as a [native function](https://github.com/Snaken/CSharpPython/blob/main/docs/host-api.md).

## Results

CPython 3.14.7, .NET 10.0.402, macOS arm64 (Apple M5 Pro), best of 7 runs after a warm-up run:

| Script | CSharpPython | CPython | Slower by |
| --- | ---: | ---: | ---: |
| classes | 29.0 ms | 5.6 ms | 5.2x |
| dict_ops | 12.4 ms | 3.1 ms | 4.0x |
| fib | 10.0 ms | 1.9 ms | 5.4x |
| generators | 13.0 ms | 3.3 ms | 3.9x |
| list_ops | 24.9 ms | 5.1 ms | 4.9x |
| loop_sum | 28.8 ms | 16.2 ms | 1.8x |
| strings | 4.8 ms | 1.9 ms | 2.6x |

What each script does:

- **classes**: 100,000 method calls that each create an object.
- **dict_ops**: 100,000 `dict.get` and item assignments.
- **fib**: recursive `fib(24)`, about 75,000 calls.
- **generators**: sum 200,000 values from a generator.
- **list_ops**: append 100,000 items, sort, then a list comprehension.
- **loop_sum**: a million-iteration `for` loop with arithmetic.
- **strings**: build and split 20,000 f-strings, `join`, `upper`, `count`.

These numbers are one machine and one run, so treat them as an order of magnitude. Unity builds differ: IL2CPP and WebGL are typically slower than the .NET JIT used here, so measure in your own target.

## Running it yourself

```
python3 benchmarks/run.py
```

This builds and runs [the benchmark project](https://github.com/Snaken/CSharpPython/tree/main/benchmarks) and prints the table above for your machine. Each script in `benchmarks/CSharpPython.Benchmarks/scripts` is ordinary Python that runs unchanged on both interpreters (it checks its own result with `assert`), so you can add your own.

## Keeping scripts fast

- Put hot code inside functions: local variables are faster than globals.
- Do the heavy work in a native function and call it from the script.
- `StepsPerSlice` only decides how often a task yields back to the game; it does not change the total work done.
