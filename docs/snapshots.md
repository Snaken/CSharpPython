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
- The state of `random` is saved with the task, so a restored task carries on with the same sequence of numbers (about 2.5 KB, and only for a task that used the module).
- `Owner` is not saved; reattach it after restoring.
- Damaged or foreign data always raises `SnapshotException`; it never crashes the host. The format is versioned.

## Snapshots are untrusted input unless you protect them

A snapshot is plain data, and the reader checks that data well: damaged or edited bytes either raise `SnapshotException` or restore into a task the VM can run (20,000 randomly damaged snapshots in a security review never crashed, hung or threw anything else). But a snapshot that restores is not a snapshot you wrote. Whoever can edit the saved bytes can:

- **reset the step budget**, because the number of steps already run is in the snapshot, so `MaxSteps` starts again from whatever they write;
- **change any value the script holds**, including its globals and the handles it uses. A handle is only a type id and an object id, so an edited save can name any host object. A host function must check every handle it is given, as it would any untrusted input;
- **restore a state the script could never have reached**, which can make it fail in the VM (a `HostError`, see [host API](host-api.md)) rather than with a clean error.

If saves live where players can edit them (a save file on disk, a cloud save), protect them. Set `VmOptions.SnapshotKey` to a secret of at least 16 bytes:

```csharp
var vm = new PyVm(new VmOptions { EnableSnapshots = true, SnapshotKey = secretBytes });
```

`Save()` then writes an HMAC-SHA256 signature in front of the snapshot, and `Restore()` throws `SnapshotException` for data that was not signed with the same key or has been changed in any way (the step count included). A signed snapshot cannot be restored by a VM without the key and the other way round, so decide before you ship. The key has to stay out of the player's reach: a key inside a game the player can read keeps out careless edits, not a determined person, and the real fix there is to keep the state on a server. Without a key, treat the bytes as untrusted: do not restore a snapshot that came from another player, and keep the step budget (or a count of steps) on the host.
