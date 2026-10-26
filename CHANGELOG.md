# Changelog

## Unreleased

- The documentation now gives the measured nesting limits for each Unity platform (about 90 to 140 nested brackets, depending on stack size).

- `random` module, matching CPython's numbers for a given `int` seed: `seed`, `random`, `getrandbits`, `randrange`, `randint`, `choice`, `shuffle`, `sample`, `choices`, `uniform`, `triangular`, `normalvariate`, `gauss`, `lognormvariate` and `expovariate`. Each task has its own generator and snapshots save it (snapshot format version 12).
- A damaged snapshot can no longer register exception types with empty or invalid names.
- Positional-only parameters: `def f(a, b, /, c)`.
- `math`: `isfinite`, `isclose`, `copysign`, `fmod`, `remainder`, `modf`, `frexp`, `ldexp`, `cbrt`, `exp2`, `expm1`, `log1p`, the hyperbolic functions, `factorial`, `gcd`, `lcm`, `isqrt`, `comb`, `perm`, `prod`, `fsum` and `dist`; `hypot` takes any number of arguments.
- `str`: `partition`, `rpartition`, `rsplit`, `rindex`, `removeprefix`, `removesuffix`, `swapcase`, `casefold`, `expandtabs`, `isascii`, `isdecimal`, `isnumeric`, `isidentifier`, `isprintable` and `istitle`.
- `dict.fromkeys` and `dict.popitem`; `int.bit_length`, `bit_count` and `is_integer`; `float.is_integer`.
- Methods can be read from the type, as in `sorted(words, key=str.lower)`.
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
