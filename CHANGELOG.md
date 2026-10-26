# Changelog

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
