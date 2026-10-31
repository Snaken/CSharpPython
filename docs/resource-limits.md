# Resource limits

How a host keeps a player's script from using too much time or memory. Written for the security review of 2026-10-27; see [SECURITY.md](../SECURITY.md).

| Limit | Option | Default | Allowed |
| --- | --- | --- | --- |
| Steps a task may run | `MaxSteps` | 0 (no limit) | any |
| Steps in one `Update()` | `StepsPerSlice` | 10,000 | any |
| Size of one integer | `MaxIntBits` | 16,384 | any |
| Call depth | `MaxCallDepth` | 200 | any |
| Items in one string, list, tuple, dict, set or deque | `MaxSequenceLength` | 100,000 | 1 to 4,194,304 |
| Memory one task can reach (an estimate) | `MaxMemoryBytes` | 64 MB | 0 for no limit, or any size |

## Native work costs steps

A native operation on a large value used to be one step however long the value was, so one `x in big_list` on a 4 million item list froze a frame for about 25 ms, and a loop of them for minutes while `MaxSteps` and `StepsPerSlice` saw almost nothing. Operations that walk or copy a value are now charged in proportion to its size, before the work is done:

- **1 step for every 16 items.** A native loop handles an item in about 6 ns and a VM step takes about 100 ns. Values under 16 items are free.
- **Sorting** is charged for `n log n` items.
- Charged: `in` on a string, list, tuple or deque; `+` and `*` on sequences; slicing; `==` and ordering of sequences and dicts; `sorted`, `reversed`, `tuple`, `set`, `frozenset`, `dict`; every `str` method; the list methods `extend`, `insert`, `remove`, `index`, `count`, `copy`, `reverse` and `sort`; the tuple, dict, set and deque methods that walk a value (`index`, `count`, `update`, `copy`, `union`, `intersection`, `issubset`, `extend`, `rotate` and similar). `list`, `sum`, `min`, `max`, `any`, `all` and `functools` already charged.
- Not charged: `append`, `pop`, `get`, `add`, `len`, indexing and anything that takes one step of work.
- A charged call ends the slice, as a charge from a host function does (see [host API](host-api.md)). A script that scans a 4 million item list in a loop now takes one frame per scan and stops at `MaxSteps` after a handful of them, where it used to run for a minute without stopping.

The cost is a measured estimate, not an exact time: it keeps `MaxSteps` a meaningful bound on running time, within a factor of a few.

## Items in one collection

`MaxSequenceLength` is the most items one string, list, tuple, dict, set or deque may hold, and the widest a format field may be. It is read from the running task's VM, the way `MaxIntBits` is, so two VMs can have different limits. The default, 100,000, suits a game. A host can raise it up to 4,194,304, the size the VM was built and tested for; any value outside 1 to 4,194,304 throws `ArgumentOutOfRangeException` when the `PyVm` is made.

Every way a container grows is checked: `append`, `extend`, `insert`, slice assignment, `+`, `*`, `join`, comprehensions, dict and set stores, `update`, `setdefault`, `add`, `union` and the deque methods. Going over raises a catchable `MemoryError` (a format width past the limit raises `ValueError`, as before). A container cannot be more than one operation over the limit, and a string or list built by copying costs steps as well (see above), so a loop that doubles a string stops at `MaxSteps` before it gets far.

The limit is per object, so many objects at the limit still add up (100 lists of 100,000 items is 10 million items). That is what `MaxMemoryBytes` is for.

## Memory a task holds

`MaxMemoryBytes` limits what one task can reach. The default is 64 MB; 0 turns the limit off; any positive size may be set (a negative one throws `ArgumentOutOfRangeException` from the `PyVm` constructor). Going over ends the task with an uncatchable `MemoryLimitExceeded` error, as `MaxSteps` ends it with `StepLimitExceeded`: a script cannot catch it with `try` or `finally`, and the host sees `TaskState.Error`.

**It is an estimate of live size, not an allocation counter.** The VM walks everything the task can reach (globals, stack, call frames, queued events, and everything they hold, each object once) and adds up approximate sizes: 24 bytes for a slot that holds a value, 24 + 2 bytes a character for a string, 32 + 24 bytes an item for a list or tuple, 72 bytes an entry for a dict and 48 for a set, and small fixed sizes for instances, functions and the like. A script that runs for hours and builds and drops a string every frame is fine, because what it dropped is no longer reachable. Objects shared with other tasks are counted in each. It ignores what the host holds and the memory manager's overhead, so it is not what the process uses. `PyTask.EstimatedMemoryBytes` gives the same number for a debug display (it walks the heap, so do not call it every frame).

**When it is checked.** At the end of each slice, but a walk costs about a step for every object, so the task does not walk every time. It keeps an upper bound on how much it can have grown since the last walk (32 bytes for each step, and more for work charged on big values, see above) and walks again only when the last measurement plus that bound could reach the limit. A script far below the limit is almost never walked; one near it is walked often enough not to go far past it. This makes the limit **soft**: a script can pass it by a little before it is stopped, and one that allocates much more than 32 bytes a step in a sustained way can pass it by more. Treat it as a guard against runaway scripts, not as an exact budget.

**What it costs.** Nothing measurable for scripts that keep little (the usual case for game scripts). A task that holds most of its limit pays for the walks: in a measurement of a hot loop running with 25 to 40 MB of the 64 MB limit held, it ran 65 to 80% slower than with the limit off. A host that cannot afford that sets a higher limit (the cost falls as the headroom grows) or 0.

The limit is per task, so a game with 100 tasks can hold 100 times the limit. Size it for the number of tasks, or give each task its own `PyVm` with its own limit.
