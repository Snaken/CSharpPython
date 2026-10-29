# Roadmap

Everything known to be missing, grouped by area. The order inside a group is roughly the order of value for game scripting. Nothing here is promised for a particular release. For where the language differs from CPython today, see [Language support](language.md).

## Release

- A WebGL build and a check inside the Editor and on a `GameObject`. A standalone Mono and IL2CPP player passes its checks.
- Publish to NuGet and OpenUPM.
- Samples (a script running on a `GameObject`, an in-game editor using the editor tooling) and an API reference. The samples should include one for each view: Python, Block and Node (see Editor tooling and Blocks).

## Language

- Exceptions: `__traceback__`, assigning `__cause__` or `__context__` on a built-in exception, `ExceptionGroup` and `except*`, and real `args` on built-in exceptions (today they keep only a message string).
- `return`, `break` and `continue` inside `finally`.
- Classes: multiple inheritance with a proper MRO, inheriting from built-in types (`list`, `dict`, `int`, ...), `object()`, `__slots__`, `__del__`, `__missing__`, and the special methods `__trunc__`, `__floor__`, `__ceil__` and `__getattribute__`.
- Special methods, property getters and setters and attribute hooks that can wait. Today they run to completion, like sort-key callbacks, so they cannot call `game.wait`.
- Built-ins: `globals()` and `locals()`, `iter(callable, sentinel)`, `print(file=)`, `bytes` and `bytearray`.
- Literals and formatting: bytes and complex literals, nested format specs in f-strings.
- Numbers: `range()` with bounds beyond 64 bits, a CPython-compatible `hash()`, and a fractional power of a negative number (`(-8) ** (1/3)` raises `ValueError` because there are no complex numbers).
- Strings are UTF-16 internally, so `len('😀')` is 2 where CPython says 1. Making strings count code points is a possible later change.

## Standard library

`math`, `random`, `collections`, `itertools`, `functools` and `operator` exist, each with parts missing. Wanted, with games in mind:

- More of `random`: `float`, `str` and `bytes` seeds, `sample(counts=)`, `Random` objects with `getstate` and `setstate`, `randbytes`, `binomialvariate` and the gamma, beta, pareto, weibull and von Mises distributions.
- More of `math`: `erf`, `gamma`, `lgamma`, `fma`, `nextafter`, `ulp` and `sumprod`.
- The rest of `collections` (`OrderedDict`, `ChainMap`, and `typing.NamedTuple`-style classes that inherit from a named tuple), `functools.singledispatch`, `enum`, `dataclasses`, `string`, `json`.
- Missing methods on built-in types, for example `str.translate`, `maketrans`, `format_map` and `encode`; `int.to_bytes`, `from_bytes` and `as_integer_ratio`; `float.hex` and `fromhex`. `str.casefold` does not fold `İ`, and `isnumeric` misses CJK numerals.

## Host API and Unity

- A Unity layer on top of the core: a `MonoBehaviour` that owns a task, a script asset type, and an editor window built on the editor tooling.
- A scheduler that shares a time budget between many tasks.
- Scripts importing other scripts (`import utils` served from host-provided sources).
- Attribute and method access on host handles (`player.hp`) without a native function for each.
- Hot reload that keeps a task's state, and snapshot migration when a script is edited.
- Permissions per module, so a script only sees the host functions it was granted, and per-task profiling.

## Editor tooling and Blocks

- A public "current line" on `PyTask`, so an editor can highlight the running line or block. There is none today.
- A debugger: breakpoints, stepping and variable inspection, built on the step budget.
- Hover and signature help for `self.method(` and `Class.method`.
- A formatter, code actions, unused-name warnings and a console that runs a line against a live task.
- Blocks: more block kinds, loops that wait for a changing call result, and variadic calls.
- A node view in the style of Unreal Engine Blueprints, next to the block view: nodes on a free canvas, white execution wires for control flow and typed data wires for values, event nodes that start a chain, and latent nodes such as a delay. The graph is the source of truth, wires are type-checked as they are drawn, and the editor can list the nodes that accept or produce a given type. See [Node view: design notes](node-view-design.md).
- One example for each way of writing a script: Python view (the text editor), Block view and Node view. Each runs the same small game script on a `GameObject`, so the three can be compared side by side.

## Performance and quality

- Optimisation, guided by [the benchmarks](performance.md): generators allocate arrays on every yield, and method calls and object creation are the slowest paths.
- Fuzzing of the lexer, parser, VM and snapshot reader, and property tests that compare against CPython.

## Not planned

- `async` and `await`.
- `eval`, `exec` and `compile`, `open` and other file or network access, threads, and importing CPython modules or C extensions. Scripts should only reach the game through the host API.
- Complex numbers.
