## What this changes

<!-- A sentence or two, and the issue it closes if there is one. -->

## Checklist

- [ ] `dotnet build -c Release -warnaserror` is clean and `dotnet test -c Release` passes
- [ ] New behaviour has tests; a bug fix has a regression test that failed before the fix
- [ ] Language behaviour was checked against CPython 3
- [ ] `docs/` and the README are updated if the behaviour users see changed
- [ ] `python3 scripts/generate-unity-meta.py --check` passes if source files were added or removed
- [ ] The snapshot version is bumped if the saved format changed
