# Performance

CSharpPython is a bytecode interpreter written in plain C#, built to be safe and predictable inside a game rather than to be fast. As a rough guide it runs **about 2 to 6 times slower than CPython**, which is itself a slow-ish language. Scripts that do game logic once per tick are nowhere near this limit; heavy number crunching belongs in C#, exposed as a [native function](https://github.com/Snaken/CSharpPython/blob/main/docs/host-api.md).

## Results

CPython 3.14.7, .NET 10.0.402, macOS arm64 (Apple M5 Pro), best of 7 runs after a warm-up run:

| Script | CSharpPython | CPython | Slower by |
| --- | ---: | ---: | ---: |
| builtins_stream | 260.6 ms | 60.9 ms | 4.3x |
| classes | 32.0 ms | 5.4 ms | 5.9x |
| collections_ops | 37.0 ms | 10.7 ms | 3.5x |
| dict_ops | 8.8 ms | 3.0 ms | 3.0x |
| fib | 6.9 ms | 1.8 ms | 3.8x |
| generators | 9.7 ms | 3.2 ms | 3.0x |
| list_ops | 23.5 ms | 4.8 ms | 4.9x |
| loop_sum | 28.9 ms | 16.2 ms | 1.8x |
| random_math | 25.1 ms | 11.2 ms | 2.2x |
| strings | 6.1 ms | 1.9 ms | 3.2x |

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
