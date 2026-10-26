# Roadmap

Everything known to be missing, grouped by area. The order inside a group is roughly the order of value for game scripting. Nothing here is promised for a particular release. For where the language differs from CPython today, see [Language support](language.md).

## Release

- Try the Unity package in a real Unity 6.3 project: Editor, an IL2CPP standalone build and a WebGL build.
- First tag and GitHub release; publish to NuGet and OpenUPM.
- Samples (a script running on a `GameObject`, an in-game editor using the editor tooling) and an API reference.

## Language

- `match` statements (structural pattern matching).
- Exceptions: `raise X from Y`, exception chaining (`__cause__`, `__context__`) and `__traceback__`, `ExceptionGroup` and `except*`, and real `args` on built-in exceptions (today they keep only a message string).
- `return`, `break` and `continue` inside `finally`.
- Classes: multiple inheritance with a proper MRO, inheriting from built-in types (`list`, `dict`, `int`, ...), `object()`, `__slots__`, `__del__`, `__missing__`, and the special methods `__trunc__`, `__floor__`, `__ceil__` and `__getattribute__`.
- Special methods, property getters and setters and attribute hooks that can wait. Today they run to completion, like sort-key callbacks, so they cannot call `game.wait`.
- Built-ins: `globals()` and `locals()`, `iter(callable, sentinel)`, `print(file=)`, `bytes` and `bytearray`.
- Literals and formatting: bytes and complex literals, nested format specs in f-strings.
- Numbers: `range()` with bounds beyond 64 bits, a CPython-compatible `hash()`.
- Strings are UTF-16 internally, so `len('😀')` is 2 where CPython says 1. Making strings count code points is a possible later change.

## Standard library

Only `math` exists today, and only a part of it. Wanted, with games in mind:

- `random`, seedable and saved by snapshots so a restored task continues the same sequence.
- More of `math`: `factorial`, `gcd`, `isqrt`, `copysign` and the hyperbolic functions.
- `collections` (`deque`, `Counter`, `defaultdict`, `namedtuple`), `itertools`, `functools`, `enum`, `dataclasses`, `string`, `json`.
- Missing methods on built-in types, for example `str.partition`, `rpartition`, `rsplit`, `swapcase`, `removeprefix`, `removesuffix`, `casefold`, `expandtabs`, `isdecimal`, `translate` and `format_map`; `dict.fromkeys` and `popitem`; `int.bit_length`; `float.is_integer`.

## Host API and Unity

- A Unity layer on top of the core: a `MonoBehaviour` that owns a task, a script asset type, and an editor window built on the editor tooling.
- A scheduler that shares a time budget between many tasks.
- Scripts importing other scripts (`import utils` served from host-provided sources).
- Attribute and method access on host handles (`player.hp`) without a native function for each.
- Hot reload that keeps a task's state, and snapshot migration when a script is edited.
- Permissions per module, so a script only sees the host functions it was granted, and per-task profiling.

## Editor tooling and Blocks

- A debugger: breakpoints, stepping and variable inspection, built on the step budget.
- Hover and signature help for `self.method(` and `Class.method`.
- A formatter, code actions, unused-name warnings and a console that runs a line against a live task.
- Blocks: more block kinds, loops that wait for a changing call result, variadic calls and several outputs per block.

## Performance and quality

- A benchmark suite, then optimisation: generators allocate arrays on every yield.
- Fuzzing of the lexer, parser, VM and snapshot reader, and property tests that compare against CPython.

## Not planned

- `async` and `await`.
- `eval`, `exec` and `compile`, `open` and other file or network access, threads, and importing CPython modules or C extensions. Scripts should only reach the game through the host API.
- Complex numbers.
