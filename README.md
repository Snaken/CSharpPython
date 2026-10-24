# CSharpPython

A Python-style language parser, compiler and bytecode VM written in plain C#, designed to be embedded in Unity games (and any other .NET host).

> Status: early development (0.1.0). The supported subset and the roadmap are listed below.

## Features

- Lexer → parser → resolver → bytecode compiler → stack VM, all in managed C#.
- Targets .NET Standard 2.1 and Unity 6.3 LTS. No `Reflection.Emit`, `dynamic`, threads or runtime code generation, so it is safe for IL2CPP and WebGL.
- No `UnityEngine` references in the core library, so it is unit tested in plain .NET.
- Safe for untrusted scripts: per-slice step budget, optional hard step limit, call-depth limit, bounded event queue.
- One long-running script per game object (`PyTask`), advanced by `Update()` each tick, with `ctx.Wait(ticks)` for cooperative pauses.
- Host-defined events: the host queues an event by name and the script's `def` of that name handles it.
- Host API: register modules of native functions and constants; host objects are passed as plain-data handles (type id + id).
- Python-style tracebacks with line numbers, and an error-tolerant lexer for editor tooling.

## Quick start

```csharp
using CSharpPython;

var vm = new PyVm();
vm.Output = Console.Write;

vm.RegisterModule("game", m => m
    .Constant("MAX_HP", 100)
    .Function("double", x => x.AsInt() * 2)
    .Action("log", msg => Console.WriteLine(msg.AsString())));

PyTask task = vm.Start(@"
import game

hp = game.MAX_HP

def on_hit(damage):
    global hp
    hp -= damage
    game.log(f'hp is now {hp}')

print(game.double(21))
");

while (task.Update() != TaskState.Finished)   // call once per game tick
{
    // task.QueueEvent("on_hit", 15);
}
```

Native functions that need to pause the script take a `CallContext`
(`ctx.Wait(ticks, result)`). Register custom exception types with
`vm.RegisterExceptionType("GameError")` so scripts can raise and catch them.

## Language support

Supported:

| Area | Details |
| --- | --- |
| Types | `int` (64-bit, overflow raises `OverflowError`), `float`, `str`, `bool`, `None`, `list`, `tuple`, `dict`, `range`, host handles |
| Statements | assignment (chained, tuple unpacking, augmented), `if/elif/else`, `while`, `for`, `break`, `continue`, `pass`, `return`, `global`, `import`/`from ... import`, `def`, `try/except`, `raise` |
| Expressions | arithmetic, comparison chains, `and/or/not`, `in`, `is`, conditional expressions, subscripts and slices, f-strings, keyword and default arguments |

Builtins: `print len range int float str bool repr abs min max sum round sorted list tuple dict enumerate zip reversed any all chr ord divmod pow format`, the standard exception classes, and a `math` module (`pi e tau inf nan sqrt log log2 log10 floor ceil trunc isnan isinf sin cos tan asin acos atan atan2 exp fabs hypot radians degrees pow`).

Methods: `str` (upper, lower, strip, lstrip, rstrip, split, join, replace, startswith, endswith, find, rfind, index, count, isdigit, isalpha, isalnum, isspace, isupper, islower, capitalize, title, format, zfill, ljust, rjust, center, splitlines), `list` (append, extend, insert, pop, remove, index, count, sort, reverse, clear, copy), `dict` (get, keys, values, items, pop, setdefault, update, clear, copy), `tuple` (index, count).

Not supported yet: comprehensions and generator expressions, `finally`, `try/else`, loop `else`, type annotations, `*args`/`**kwargs`, decorators, walrus, nested functions, classes, `lambda`, `yield`, `with`, `del`, `assert`, `nonlocal`, `async`/`await`, set literals, dict unpacking, bit operators, dotted imports, bytes/complex literals, nested format specs in f-strings, `str % args`, `type`/`isinstance`, slice assignment.

### Differences from CPython

- Integers are 64-bit and checked; there are no big ints.
- `dict.keys()/values()/items()` return lists, not views.
- A negative base with a fractional power raises `ValueError` (no complex numbers).

## Limits and safety

`vm.Options`: `StepsPerSlice` (instructions per `Update`), `MaxSteps` (hard cap, uncatchable `StepLimitExceeded`), `MaxCallDepth` (catchable `RecursionError`), `MaxQueuedEvents`.

## Install

Unity (Package Manager → Add package from git URL):

```
https://github.com/Snaken/CSharpPython.git?path=/src/CSharpPython.Core
```

Or build `CSharpPython.dll` (`dotnet build -c Release`, output under `artifacts/bin`) and drop it into `Assets/Plugins`. A NuGet package can be produced with `dotnet pack src/CSharpPython.Core`.

## Build and test

```
dotnet build
dotnet test
dotnet test --collect:"XPlat Code Coverage"
python3 scripts/generate-unity-meta.py   # after adding or removing source files
```

The test suite is large and uses CPython 3 as an oracle for language semantics; each fixed bug gets a regression test. CI enforces a line-coverage floor.

## Layout

- `src/CSharpPython.Core/Syntax` lexer, parser, AST
- `src/CSharpPython.Core/Compilation` resolver, compiler, bytecode
- `src/CSharpPython.Core/Runtime` values, operators, builtins, methods
- `src/CSharpPython.Core/Vm` `PyVm`, `PyTask`, host module API
- `tests/CSharpPython.Tests` NUnit tests

## Roadmap

- Optional snapshot / save / resume of tasks (for offline progress), host objects as handles.
- Blocks module: a Blueprint-style node graph that compiles to Python source with a source map; user-made blocks become `def`s.
- Editor tooling: syntax highlighting and autocomplete on the tolerant lexer.
- Language: `key=` for `sorted`/`sort`, classes, comprehensions, `type`/`isinstance`, `finally`/`try-else`, `*args`/`**kwargs`, nested functions and lambdas, dict views, slice assignment, big ints.

## License

MIT. If you use this library in a project, a credit is appreciated (see `LICENSE`).
