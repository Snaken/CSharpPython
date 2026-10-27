# Changelog

## Unreleased

- `match` statements: literal, capture, wildcard, value, sequence (with `*rest`), mapping (with `**rest`), class, `|` and `as` patterns, and guards. `match` and `case` stay ordinary names anywhere else. The editor tooling highlights them as keywords where they start a block.

- `itertools` module: `count`, `cycle`, `repeat`, `chain` and `chain.from_iterable`, `islice`, `accumulate`, `compress`, `dropwhile`, `takewhile`, `filterfalse`, `starmap`, `zip_longest`, `pairwise`, `batched`, `product`, `permutations`, `combinations`, `combinations_with_replacement` and `groupby` (`tee` is missing). They are lazy, and snapshots save them in the middle of a loop (snapshot format version 15, which still reads version 13 and 14 saves).
- `functools.reduce`, `partial`, `lru_cache`, `cache` and `wraps`, and an `operator` module with the arithmetic, bitwise, comparison and truth functions, `itemgetter`, `attrgetter` and `methodcaller`. A cached function that is called while a snapshot is taken is saved with its cache (snapshot format version 15, which still reads version 13 and 14 saves).

## 0.2.1

A speed release: no language changes, and saved tasks from 0.2.0 still restore (the snapshot format is still version 13).

- Faster: small dictionaries and object attributes are searched in place instead of through a hash table (creating an object and setting attributes take about 40% less time), `sort`/`sorted` of ints, floats and strings, `sum` over ints, list and set comprehensions, calls of list, str, deque and dict methods, `str.split` on one character, and f-strings.
- Faster: the interpreter combines common runs of instructions (a local with a constant, a comparison with its jump, arithmetic on a local), calls script functions without the general argument binding, reads and sets instance attributes directly, and keeps the less common instructions out of its main loop so that each call from native code into a script costs less. `fib(24)` takes 4.8 ms instead of 6.9 ms.
- Faster: a small function that only does arithmetic, comparisons and reads (`key=lambda x: -x`, `lambda p: p.hp`) runs without a frame when `sorted`, `min`, `max`, `map` or `filter` call it, and generators no longer allocate on every `yield`. `min(range(10**6), key=lambda x: -x)` is 4 times quicker.
- Measured against CPython, every benchmark script runs 1.3 to 2.8 times slower than it, where before it was up to 5 times. (`benchmarks/` now warms up for a second before timing.)

## 0.2.0

Found by testing 0.1.1 in a Unity 6.3 player: this release fixes what that turned up (positional-only parameters, missing `math` and `str` functions, `sum(range(10**7))`) and adds the first standard library modules.

**Saved tasks from 0.1.x cannot be restored.** The snapshot format is now version 13 and restoring any other version raises `SnapshotException`, so save games made with 0.1.x need to be started again.

- The documentation now gives the measured nesting limits for each Unity platform (about 90 to 140 nested brackets, depending on stack size).
- `collections` module with `defaultdict` and `Counter` (snapshot format version 13). `deque` (a ring buffer, with `maxlen`) and `namedtuple` (instances are real tuples that point to their type; types and instances are saved by snapshots) are there too.
- A bound method of a `Counter`, a frozenset or a named tuple is restored to the right method (it used to be looked up on the plain type).
- `random` module, matching CPython's numbers for a given `int` seed: `seed`, `random`, `getrandbits`, `randrange`, `randint`, `choice`, `shuffle`, `sample`, `choices`, `uniform`, `triangular`, `normalvariate`, `gauss`, `lognormvariate` and `expovariate`. Each task has its own generator and snapshots save it (snapshot format version 12 for random, 13 with collections).
- A damaged snapshot can no longer register exception types with empty or invalid names.
- Positional-only parameters: `def f(a, b, /, c)`.
- `math`: `isfinite`, `isclose`, `copysign`, `fmod`, `remainder`, `modf`, `frexp`, `ldexp`, `cbrt`, `exp2`, `expm1`, `log1p`, the hyperbolic functions, `factorial`, `gcd`, `lcm`, `isqrt`, `comb`, `perm`, `prod`, `fsum` and `dist`; `hypot` takes any number of arguments.
- `str`: `partition`, `rpartition`, `rsplit`, `rindex`, `removeprefix`, `removesuffix`, `swapcase`, `casefold`, `expandtabs`, `isascii`, `isdecimal`, `isnumeric`, `isidentifier`, `isprintable` and `istitle`.
- `dict.fromkeys` and `dict.popitem`; `int.bit_length`, `bit_count` and `is_integer`; `float.is_integer`.
- Methods can be read from the type, as in `sorted(words, key=str.lower)`.
- `sum`, `min`, `max`, `any` and `all` read their input one item at a time, so `sum(range(10**7))` works, `any` and `all` stop at the first answer, and a generator is no longer run to the end first. Their work counts as steps (ends the slice, stopped by `MaxSteps`). Host functions can do the same with `ctx.Charge(steps)`.
- `sum` adds floats with compensation, as CPython 3.12 and later does.

## 0.1.1

Found by testing 0.1.0 in a Unity 6.3 player (Mono and IL2CPP): the player checks all passed, and this release fixes the one serious issue it turned up.

- Source nested too deeply, or with very long operator chains, now raises a `PySyntaxError` instead of overflowing the stack and ending the process. Brackets, operators and blocks may nest about 150 levels, and an expression may chain 500 operators.
- Added benchmarks against CPython (`benchmarks/`, `docs/performance.md`).

## 0.1.0

First release.

- Python-style source is parsed, compiled to bytecode and run on a stack VM, in plain C# (.NET Standard 2.1). IL2CPP and WebGL safe; no UnityEngine in the core.
- One `PyTask` per game object, running a slice of instructions per `Update`, with host functions that can wait, host events, and a native host API.
- Language: functions, closures, classes with single inheritance and special methods, decorators, properties, generators (`send`, `throw`, `close`, `yield from`), exceptions, `with`, comprehensions, f-strings, `%` formatting, walrus, slices, star-unpacking, big integers, dict views.
- Snapshot, save and resume of a running task.
- Editor tooling: syntax highlighting, completion, signature help, hover and diagnostics, and a Blocks module.
- Limits: `StepsPerSlice`, `MaxSteps`, `MaxCallDepth`, `MaxQueuedEvents`, `MaxIntBits`.
- Snapshot format version 11.

See the README for what is not supported yet.
