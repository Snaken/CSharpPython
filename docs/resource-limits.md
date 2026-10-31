# Resource limits

How a host keeps a player's script from using too much time or memory. Written for the security review of 2026-10-27; see [SECURITY.md](../SECURITY.md).

| Limit | Option | Default | Allowed |
| --- | --- | --- | --- |
| Steps a task may run | `MaxSteps` | 0 (no limit) | any |
| Steps in one `Update()` | `StepsPerSlice` | 10,000 | any |
| Size of one integer | `MaxIntBits` | 16,384 | any |
| Call depth | `MaxCallDepth` | 200 | any |
| Items in one string, list, tuple, dict, set or deque | `MaxSequenceLength` | 100,000 | 1 to 4,194,304 (planned) |
| Memory one task can reach | `MaxMemoryBytes` | 64 MB (planned) | 0 for no limit, or any size (planned) |

## Native work costs steps

A native operation on a large value used to be one step however long the value was, so one `x in big_list` on a 4 million item list froze a frame for about 25 ms, and a loop of them for minutes while `MaxSteps` and `StepsPerSlice` saw almost nothing. Operations that walk or copy a value are now charged in proportion to its size, before the work is done:

- **1 step for every 16 items.** A native loop handles an item in about 6 ns and a VM step takes about 100 ns. Values under 16 items are free.
- **Sorting** is charged for `n log n` items.
- Charged: `in` on a string, list, tuple or deque; `+` and `*` on sequences; slicing; `==` and ordering of sequences and dicts; `sorted`, `reversed`, `tuple`, `set`, `frozenset`, `dict`; every `str` method; the list methods `extend`, `insert`, `remove`, `index`, `count`, `copy`, `reverse` and `sort`; the tuple, dict, set and deque methods that walk a value (`index`, `count`, `update`, `copy`, `union`, `intersection`, `issubset`, `extend`, `rotate` and similar). `list`, `sum`, `min`, `max`, `any`, `all` and `functools` already charged.
- Not charged: `append`, `pop`, `get`, `add`, `len`, indexing and anything that takes one step of work.
- A charged call ends the slice, as a charge from a host function does (see [host API](host-api.md)). A script that scans a 4 million item list in a loop now takes one frame per scan and stops at `MaxSteps` after a handful of them, where it used to run for a minute without stopping.

The cost is a measured estimate, not an exact time: it keeps `MaxSteps` a meaningful bound on running time, within a factor of a few.

## Planned: collection and memory limits

- `MaxSequenceLength` becomes a per-VM option (it is a fixed 4,194,304 now), read from the running task the way `MaxIntBits` is. The default drops to 100,000, which suits a game; a host can raise it, up to the ceiling of 4,194,304 that the VM was tested at. A value outside 1 to the ceiling is refused when the VM is made.
- `MaxMemoryBytes` limits what one task can reach. It is an estimate of live size (it adds up approximate sizes of the globals, stack and frames of the task and everything they hold), not an allocation counter, so a script that runs for hours and builds a new string every frame is fine. The estimate is refreshed on a schedule that grows with the size of the heap, and straight after a single large allocation. Going over raises an uncatchable `MemoryError`, as `MaxSteps` raises `StepLimitExceeded`. The default is 64 MB; 0 turns the limit off; a host may set any size.
