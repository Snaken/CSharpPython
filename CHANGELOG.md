# Changelog

## Unreleased

- Host functions can declare types: `Describe("spawn", "kind: str, count: int = 1 -> Entity")`, with several named results as `-> (hit: bool, point: Vec)`, and `latent: true` for a function that can pause the script. Types are `any`, `int`, `float`, `bool`, `str`, `none`, `list[T]` and host handle names. Untyped descriptions work as before and signature help still shows the plain parameter list. This is the first step of the [node view](docs/node-view-design.md).
- The built-in functions and the `math` and `random` modules have types (`HostSignature` on `NativeFunction.Typed` for host functions; the built-ins are described by an internal table).
- `PinCompatibility.Check(from, to)` says whether an output of one type may be wired to an input of another, with the reason when it may not: `any` fits on either side, `int` fits `float`, lists need the same element type, and every other mismatch is refused.
- `BlockLibrary` functions carry pin types (`FunctionSpec.ParameterTypes` and `Outputs`) from the typed descriptions, and `PinTypeIndex` finds the functions that fit a pin: `Accepting(type)` lists the inputs an output may be wired to, and `Producing(type)` lists the outputs that fit an input.
- Blocks are type-checked. Every built-in block has pin types (`BlockSpec.InputType` and `OutputType`), with wildcard pins written `$T` that take their type from the wires (`index`, `append`, `set_index` and `choose`), and the compiler reports a wire that does not fit against its block and pin. **Block graphs that wire a value of the wrong type (a string into a `repeat` count, a string appended to a list of numbers) no longer compile.** `BlockTypeChecker` checks a graph, gives the type of a block's value, and says whether a wire may be drawn (`CheckWire`) with the reason when it may not.
- Pin types of `print` and other built-ins that were registered with their own pin names are matched to the parameter they stand for, not by position.
- A block can have several outputs. A host function described as `-> (hit: bool, point: Vec)` gives one output per name, and an input picks one with the new `BlockInput.FromPin` (`Connect("arg0", "ray", "hit")`). A call in the flow is unpacked into one local per output, and a pure call is indexed where it is used. Each output is type-checked on its own, and `BlockTypeChecker.OutputType` and `CheckWire` take an optional output name. Saved graphs gain an optional `FromPin` field; older files load unchanged.
- A value block that several inputs read is worked out once per flow step: the first time an input needs it, it is written to a local on the line before that statement (`_b1 = x + 1`) and reused. A loop pass starts a new step, and so does a call that can pause the script (`Describe(..., latent: true)` on a host function, or a call to a function in the graph). Variable reads and typed-in values are not shared, and a value is not worked out early where Python only sometimes runs it (the right side of `and`/`or`, the branches of `choose`, a `while` condition). `FunctionSpec.Latent` and `BlockLibrary.AddFunction(latent:)` carry the flag. **A saved task of a block script in which one value block feeds several inputs cannot be restored after this change**, because the generated script, and so its fingerprint, is different.
- Flow joins: more than one execution wire may enter a block. The flow from that block on is written once as a local function (`_join1`) and each path calls it; a `return` in it leaves the function, and variables it assigns are declared `nonlocal` or `global`. The old error "flow cannot loop back or join up again" now only reports a loop back. `_join1`, `_join2`, ... are reserved names, like `_b1`. Reported as errors: `break` or `continue` after a join inside a loop, a join entered from inside and outside a loop or from different parts of the flow, a result that did not run on every path into the join, and a variable first set after a join that is also used outside it.
- Loop blocks have an output: `for_each`, `for_range` and a named `repeat` give their item, count or pass number to an input wired to the loop block. It can only be used inside the loop's body; the compiler reports any other use, and `BlockTypeChecker.CheckWire` refuses the wire for an editor (`BlockSpec.LoopVariable` marks the blocks that have it). `BlockTypeChecker.OutputType` types it (`int`, or the element type of the list).
- Global block variables can be declared with a type (`BlockVariable.Type`, `AddVariable(name, initial, type)`, saved as an optional `Type` in the JSON). Reading one gives its type, `set`, `change` and loops that store into it are checked against it, a number, bool or string variable starts as `0`, `0.0`, `False` or `""` when no starting value is given, and a starting value of the wrong type is an error.
- Block graphs keep editor-only data in the same file: `BlockGraph.Editor` holds block positions, collapsed flags, comment boxes, reroute points and groups, saved as an optional `Editor` section (format version still 1). The compiler ignores it, so moving a block never changes the generated program or invalidates a saved task.
- `BlockPlaceholders.Find(graph, library)` lists the blocks that name a function or block type the host does not have, so an editor can show them as placeholders; the graph still loads and keeps their wires, and the compile error now says the block is a placeholder.
- `BlockGraph.FromJson(json, limits)` checks a file against `BlockLimits` (document length, counts of functions, blocks, variables and links, lengths of ids, names, values and types, and the editor section) and rejects one that goes over, saying which limit. The defaults are generous for hand-made graphs, and a host can change them. `BlockLimits.Problem(graph)` checks a graph built in code.

## 0.2.2

The first standard library modules beyond 0.2.0 (`itertools`, `functools`, `operator`), `match` statements and exception chaining. **Saved tasks from 0.2.1 or earlier of a script with a `try` statement cannot be restored** (see the exception chaining entry), and the snapshot format is now version 16, which still reads versions 13 to 15.

- `functools.cmp_to_key` and `total_ordering`, and `itertools.tee`.
- `int(x, base=2)` takes the base as a keyword.
- Error messages follow CPython 3.14: every division or modulo by zero says `division by zero` (and `0 ** -1` says `zero to a negative power`), and a variable used before it has a value says `cannot access local variable 'x' where it is not associated with a value` (for a variable of an enclosing function, `cannot access free variable 'x' where it is not associated with a value in enclosing scope`).
- Exception chaining: `raise X from Y` (and `from None`), `__cause__`, `__context__` and `__suppress_context__`. An error raised while an `except` block handles another one remembers it as its context, including errors the VM raises and ones raised by a function that the block calls. A task that stops with a chained error has `PyRuntimeError.Cause` and `PyRuntimeError.Context`, and `FormatTraceback()` prints the whole chain. A generator that lets `StopIteration` escape now raises `RuntimeError` with it as the cause. Snapshot format version 16 (it still reads version 13 to 15). **Saved tasks of a script with a `try` statement from before this release cannot be restored**, because `try` now compiles to a few more instructions and a snapshot only restores against the same compiled script.
- `match` statements: literal, capture, wildcard, value, sequence (with `*rest`), mapping (with `**rest`), class, `|` and `as` patterns, and guards. `match` and `case` stay ordinary names anywhere else. The editor tooling highlights them as keywords where they start a block.
- `itertools` module: `count`, `cycle`, `repeat`, `chain` and `chain.from_iterable`, `islice`, `accumulate`, `compress`, `dropwhile`, `takewhile`, `filterfalse`, `starmap`, `zip_longest`, `pairwise`, `batched`, `product`, `permutations`, `combinations`, `combinations_with_replacement`, `groupby` and `tee`. They are lazy, and snapshots save them in the middle of a loop (snapshot format version 15, which still reads version 13 and 14 saves).
- `functools.reduce`, `partial`, `lru_cache`, `cache` and `wraps`, and an `operator` module with the arithmetic, bitwise, comparison and truth functions, `itemgetter`, `attrgetter` and `methodcaller`. A cached function that is called while a snapshot is taken is saved with its cache (snapshot format version 15, which still reads version 13 and 14 saves).

## 0.2.1

A speed release: no language changes, and saved tasks from 0.2.0 still restore (the snapshot format is still version 13).

- Faster: small dictionaries and object attributes are searched in place instead of through a hash table (creating an object and setting attributes take about 40% less time), `sort`/`sorted` of ints, floats and strings, `sum` over ints, list and set comprehensions, calls of list, str, deque and dict methods, `str.split` on one character, and f-strings.
- Faster: the interpreter combines common runs of instructions (a local with a constant, a comparison with its jump, arithmetic on a local), calls script functions without the general argument binding, reads and sets instance attributes directly, and keeps the less common instructions out of its main loop so that each call from native code into a script costs less. `fib(24)` takes 4.8 ms instead of 6.9 ms.
- Faster: a small function that only does arithmetic, comparisons and reads (`key=lambda x: -x`, `lambda p: p.hp`) runs without a frame when `sorted`, `min`, `max`, `map` or `filter` call it, and generators no longer allocate on every `yield`. `min(range(10**6), key=lambda x: -x)` is 4 times quicker.
- Measured against CPython, every benchmark script runs 1.3 to 2.8 times slower than it, where before it was up to 5 times. (`benchmarks/` now warms up for a second before timing.)

## 0.2.0

Found by testing 0.1.1 in a Unity 6.3 player: this release fixes what that turned up (positional-only parameters, missing `math` and `str` functions, `sum(range(10**7))`) and adds the first standard library modules.

**Saved tasks from 0.1.x cannot be restored.** The snapshot format is now version 13 and restoring any other version raises `SnapshotException`, so save games made with 0.1.x need to be started again.

- The documentation now gives the measured nesting limits for each Unity platform (about 90 to 140 nested brackets, depending on stack size).
- `collections` module with `defaultdict` and `Counter` (snapshot format version 13). `deque` (a ring buffer, with `maxlen`) and `namedtuple` (instances are real tuples that point to their type; types and instances are saved by snapshots) are there too.
- A bound method of a `Counter`, a frozenset or a named tuple is restored to the right method (it used to be looked up on the plain type).
- `random` module, matching CPython's numbers for a given `int` seed: `seed`, `random`, `getrandbits`, `randrange`, `randint`, `choice`, `shuffle`, `sample`, `choices`, `uniform`, `triangular`, `normalvariate`, `gauss`, `lognormvariate` and `expovariate`. Each task has its own generator and snapshots save it (snapshot format version 12 for random, 13 with collections).
- A damaged snapshot can no longer register exception types with empty or invalid names.
- Positional-only parameters: `def f(a, b, /, c)`.
- `math`: `isfinite`, `isclose`, `copysign`, `fmod`, `remainder`, `modf`, `frexp`, `ldexp`, `cbrt`, `exp2`, `expm1`, `log1p`, the hyperbolic functions, `factorial`, `gcd`, `lcm`, `isqrt`, `comb`, `perm`, `prod`, `fsum` and `dist`; `hypot` takes any number of arguments.
- `str`: `partition`, `rpartition`, `rsplit`, `rindex`, `removeprefix`, `removesuffix`, `swapcase`, `casefold`, `expandtabs`, `isascii`, `isdecimal`, `isnumeric`, `isidentifier`, `isprintable` and `istitle`.
- `dict.fromkeys` and `dict.popitem`; `int.bit_length`, `bit_count` and `is_integer`; `float.is_integer`.
- Methods can be read from the type, as in `sorted(words, key=str.lower)`.
- `sum`, `min`, `max`, `any` and `all` read their input one item at a time, so `sum(range(10**7))` works, `any` and `all` stop at the first answer, and a generator is no longer run to the end first. Their work counts as steps (ends the slice, stopped by `MaxSteps`). Host functions can do the same with `ctx.Charge(steps)`.
- `sum` adds floats with compensation, as CPython 3.12 and later does.

## 0.1.1

Found by testing 0.1.0 in a Unity 6.3 player (Mono and IL2CPP): the player checks all passed, and this release fixes the one serious issue it turned up.

- Source nested too deeply, or with very long operator chains, now raises a `PySyntaxError` instead of overflowing the stack and ending the process. Brackets, operators and blocks may nest about 150 levels, and an expression may chain 500 operators.
- Added benchmarks against CPython (`benchmarks/`, `docs/performance.md`).

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
