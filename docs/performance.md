# Performance

CSharpPython is a bytecode interpreter written in plain C#, built to be safe and predictable inside a game rather than to be fast. As a rough guide it runs **about 1.3 to 4 times slower than CPython**, which is itself a slow-ish language. Scripts that do game logic once per tick are nowhere near this limit; heavy number crunching belongs in C#, exposed as a [native function](https://github.com/Snaken/CSharpPython/blob/main/docs/host-api.md).

## Results

CPython 3.14.7, .NET 10.0.402, macOS arm64 (Apple M5 Pro), best of 7 runs after a one-second warm-up (so the .NET JIT has optimised the interpreter, as it would in a game that has been running for a while):

| Script | CSharpPython | CPython | Slower by |
| --- | ---: | ---: | ---: |
| builtins_stream | 215.5 ms | 60.9 ms | 3.5x |
| classes | 17.3 ms | 5.6 ms | 3.1x |
| collections_ops | 25.9 ms | 11.0 ms | 2.4x |
| dict_ops | 7.1 ms | 3.0 ms | 2.4x |
| fib | 6.9 ms | 1.8 ms | 3.8x |
| generators | 8.9 ms | 3.2 ms | 2.7x |
| list_ops | 12.8 ms | 4.8 ms | 2.7x |
| loop_sum | 29.4 ms | 16.5 ms | 1.8x |
| random_math | 14.8 ms | 11.3 ms | 1.3x |
| strings | 5.0 ms | 1.9 ms | 2.7x |

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
