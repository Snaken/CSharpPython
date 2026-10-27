# Contributing

## Build and test

```
dotnet build -c Release -warnaserror
dotnet test -c Release
dotnet test -c Release --collect:"XPlat Code Coverage"
python3 scripts/check-coverage.py <coverage.cobertura.xml> 95
python3 scripts/generate-unity-meta.py          # after adding or removing source files (add --check to verify)
git config core.hooksPath scripts/git-hooks      # once: commits then regenerate and include the .meta files
python3 scripts/update-expected.py               # regenerate tests/CSharpPython.Tests/Scripts/**/*.expected from CPython
python3 benchmarks/run.py                        # timings against CPython (see docs/performance.md)
```

The test suite is large and uses CPython 3 as an oracle for language semantics (generated test cases hold the output CPython produced); each fixed bug gets a regression test. New language and library behaviour is best tested as a pair of files under `tests/CSharpPython.Tests/Scripts`: a readable `name.py` and a `name.expected` that `scripts/update-expected.py` fills in from CPython. CI builds with warnings as errors, runs the tests on Linux, macOS and Windows, and enforces a line-coverage floor.

## Layout

- `src/CSharpPython.Core` the library, which is also the Unity package root
  - `Syntax` lexer, parser, AST
  - `Compilation` resolver, compiler, bytecode
  - `Runtime` values, operators, built-ins, methods
  - `Vm` `PyVm`, `PyTask`, the host module API, snapshots
  - `Tooling` syntax highlighting, completion, diagnostics
  - `Blocks` block graph model, block library, graph-to-Python compiler, source map
- `tests/CSharpPython.Tests` NUnit tests
- `docs` user documentation
- `scripts` coverage check and Unity `.meta` file generator

## Rules of the codebase

- Plain C# that is safe for IL2CPP and WebGL: no `Reflection.Emit`, `dynamic`, threads or runtime code generation, and no `UnityEngine` in the core.
- Keep `README.md` and `src/CSharpPython.Core/README.md` identical (CI checks it); links in them must be absolute because the package README is shown outside the repository.
- A snapshot format change bumps the version in `PyTaskSnapshot.cs`.

## Using AI tools

Using AI tools to help with a contribution is fine, and you do not need to say so in the pull request. You are responsible for what you submit: read it, run the tests, and make sure you understand it and that it follows the rules above. Changes that were not checked, or that the author cannot explain when asked, may be declined.
