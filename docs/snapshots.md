# Saving and resuming tasks

Snapshots are off by default. Turn them on with `new PyVm(new VmOptions { EnableSnapshots = true })`.

```csharp
byte[] bytes = task.Save();                       // globals, call stack, waits, queued events, iterators in progress

// later, even in a new session: compile the same script, register the same host API, then
var vm2 = new PyVm(new VmOptions { EnableSnapshots = true });
vm2.RegisterModule("game", ...);                  // as before
PyTask task2 = vm2.Restore(vm2.Compile(source), bytes);
task2.FastForward(ticksSinceSave);                // skips quickly over waits to catch up with offline time
```

- The snapshot records a fingerprint of the compiled script, so it only restores against identical code. Changed scripts raise `SnapshotException`.
- Host objects are never saved: scripts hold handles (type id + object id), which are plain data. The host saves and restores its own objects and re-creates the same handles.
- Host functions are saved by name, so every function a script can reach must be a built-in or a module member. A function the VM cannot find again makes `Save` throw.
- `Owner` is not saved; reattach it after restoring.
- Damaged or foreign data always raises `SnapshotException`; it never crashes the host. The format is versioned.
