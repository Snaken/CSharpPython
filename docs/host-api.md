# Host API

How a game talks to scripts: registering functions, pausing a script, queueing events and handing out game objects.

## Running a script

```csharp
using CSharpPython;

var vm = new PyVm();
vm.Output = Console.Write;                       // where print() goes

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

while (task.Update() != TaskState.Finished)      // call once per game tick
{
    // task.QueueEvent("on_hit", 15);
}
```

A `PyTask` is one script running on one game object. `Update()` runs up to `StepsPerSlice` instructions and returns, so a script never blocks a frame. Compile once with `vm.Compile(source)` and start several tasks from the same compiled script with `vm.Start(script)`.

## Native functions and modules

- `module.Function(name, ...)` registers a function that returns a value, `module.Action(name, ...)` one that returns nothing, and `module.Constant(name, value)` a constant. `Function` and `Action` have overloads for zero to three `Value` arguments; use `Native` for more arguments, keyword arguments or a `CallContext`.
- Arguments arrive as `Value`s: `AsInt()`, `AsFloat()`, `AsString()`, `AsBool()`, `AsList()` and so on. A wrong type raises a Python `TypeError` in the script.
- Modules can be dotted (`"game.ai"`); the parent is created for you.
- Native functions don't carry parameter names. Give yours a description for editor tooling with `module.Describe("spawn", "kind, count=1")`. Parameters and the result can carry types: `Describe("spawn", "kind: str, count: int = 1 -> Entity")`, or several named results as `-> (hit: bool, point: Vec)`, which the function returns as a tuple of that length (the block editor shows one output for each name). Types are `any`, `int`, `float`, `bool`, `str`, `none`, `list[T]` and the names of host handle types; an untyped parameter is `any`. A description with no `-> result` gives the function one `any` result, so a block that calls it shows a value pin; write `-> none` for a function that returns nothing. Pass `latent: true` for a function that can pause the script (the block compiler treats a call to it as the end of a flow step, see [Blocks](blocks.md)). Signature help still shows the plain list.

## Waiting

A native function that should pause the script takes a `CallContext`:

```csharp
m.Native("wait", (ctx, args) => { ctx.Wait((int)args[0].AsInt()); return Value.None; }, 1, 1);
```

`ctx.Wait(ticks, result)` pauses the task for that many `Update()` calls; `result` is what the call returns when it wakes. The task reports `TaskState.Waiting` in the meantime.

## Events

The host queues an event by name and the script's `def` of that name handles it:

```csharp
task.QueueEvent("on_hit", 15);                   // runs def on_hit(damage) when the task is free
```

The queue is bounded by `MaxQueuedEvents`; further events are dropped.

## Game objects

Host objects are passed to scripts as plain-data handles: a host-defined type id plus an object id (`Value.FromHandle(typeId, objectId)`). Scripts can store, compare and pass them; the host looks the object up when a native function receives one. Handles are never saved with the game objects themselves, see [Saving and resuming tasks](snapshots.md).

## Where a task is

`task.CurrentLine` is the line of the script the task is at, for an editor to highlight: the line of the instruction it last started in the innermost function. A task that is waiting is on the line of the call that waits. Before the task has run it is the first line, and once it has finished or failed it is 0. `task.GetCallStack()` lists each running function innermost first (`TraceFrame.Function` and `Line`), so an editor can also mark the calls that led there; it is a copy. Reading either costs nothing while the task runs. A restored task reports the same lines. For a block script, `SourceMap.Running(task)` and `ActiveNodes(task)` turn these into block ids (see [blocks.md](blocks.md)).

## Calling back into the script

A native function can call a script function, built-in or bound method with `ctx.Call(callee, args...)`, for example to apply a callback or a sort key. The call runs to completion before `Call` returns, so the callee cannot wait (it gets a `RuntimeError`); it counts towards `MaxSteps`, and an exception it raises continues up through the script as usual. A native function that loops over many items should call `ctx.Charge(steps)` now and then (once per batch of items is enough): the work then counts as steps, so it ends the current slice and `MaxSteps` can stop the loop part-way.

## Exceptions

Register custom exception types with `vm.RegisterExceptionType("GameError")` so scripts can raise and catch them. A script error that is not caught ends the task: `task.State` is `TaskState.Error` and `task.Error` is a `PyRuntimeError` with `TypeName`, `Problem`, the line number and a Python-style traceback.

## Limits

Scripts can be untrusted. `vm.Options` has `StepsPerSlice`, `MaxSteps` (a hard cap that ends the task with an uncatchable `StepLimitExceeded`), `MaxCallDepth` (a catchable `RecursionError`), `MaxQueuedEvents` and `MaxIntBits` (the largest integer result, in bits; bigger raises a catchable `OverflowError`).

## Big integers

An `int` can be any size, up to `MaxIntBits`. `Value.AsInt()` raises an `OverflowError` for a value that does not fit in 64 bits; use `Value.AsBigInteger()` to read one and `Value.FromBigInteger()` to return one.
