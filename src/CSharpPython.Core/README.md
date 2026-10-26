# CSharpPython

A Python-style language parser, compiler and bytecode VM written in plain C#, designed to be embedded in Unity games (and any other .NET host).

[![CI](https://github.com/Snaken/CSharpPython/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Snaken/CSharpPython/actions/workflows/ci.yml)
[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=Snaken_CSharpPython&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=Snaken_CSharpPython)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=Snaken_CSharpPython&metric=coverage)](https://sonarcloud.io/summary/new_code?id=Snaken_CSharpPython)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/Snaken/CSharpPython/blob/main/LICENSE)

> Status: early development (0.2.0).

## Why

- **Safe to embed.** Plain managed C# for .NET Standard 2.1 and Unity 6.3 LTS: no `Reflection.Emit`, `dynamic`, threads or runtime code generation, so it is safe for IL2CPP and WebGL, and the core has no `UnityEngine` dependency.
- **Safe for untrusted scripts.** A per-slice step budget, an optional hard step limit, a call-depth limit, a bounded event queue and a limit on integer size.
- **Made for games.** One long-running script per game object, advanced once per tick, with cooperative pauses (`ctx.Wait(ticks)`), host-defined events and a native host API.
- **Saves and resumes.** A running task, including its call stack, generators and waits, can be saved and restored for offline progress and save games.
- **Editor support.** Highlighting, completion, signature help, hover, go to definition, rename and diagnostics that work on half-typed code, plus a Blueprint-style Blocks graph that compiles to ordinary Python.

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

## Documentation

| | |
| --- | --- |
| [Language support](https://github.com/Snaken/CSharpPython/blob/main/docs/language.md) | What Python you can write, and where it differs from CPython |
| [Host API](https://github.com/Snaken/CSharpPython/blob/main/docs/host-api.md) | Native functions, waiting, events, game objects, limits |
| [Saving and resuming tasks](https://github.com/Snaken/CSharpPython/blob/main/docs/snapshots.md) | Snapshots for save games and offline progress |
| [Editor tooling](https://github.com/Snaken/CSharpPython/blob/main/docs/editor-tooling.md) | Highlighting, completion, hover, rename and diagnostics for an in-game editor |
| [Blocks](https://github.com/Snaken/CSharpPython/blob/main/docs/blocks.md) | A visual node graph that compiles to Python |
| [Performance](https://github.com/Snaken/CSharpPython/blob/main/docs/performance.md) | How fast it runs compared with CPython, and how to measure it |
| [Roadmap](https://github.com/Snaken/CSharpPython/blob/main/docs/roadmap.md) | Everything known to be missing, and what is not planned |
| [Contributing](https://github.com/Snaken/CSharpPython/blob/main/CONTRIBUTING.md) | Building, testing and how the code is laid out |

## Install

Unity (Package Manager → Add package from git URL):

```
https://github.com/Snaken/CSharpPython.git?path=/src/CSharpPython.Core
```

Or build `CSharpPython.dll` (`dotnet build -c Release`, output under `artifacts/bin`) and drop it into `Assets/Plugins`. A NuGet package can be produced with `dotnet pack src/CSharpPython.Core`.

## Roadmap

Next up: checks in a WebGL build and the Unity Editor, then `match`, exception chaining, a small standard library (`collections`, `itertools`, `functools`, more of `random` and `math`) and special methods that can wait. The [full roadmap](https://github.com/Snaken/CSharpPython/blob/main/docs/roadmap.md) lists everything known to be missing, and what is not planned.

## License

MIT. If you use this library in a project, a credit is appreciated (see `LICENSE`).

## Acknowledgments

Built with the assistance of Claude Code.
