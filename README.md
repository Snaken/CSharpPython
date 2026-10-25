# CSharpPython

A Python-style language parser, compiler and bytecode VM written in plain C#, designed to be embedded in Unity games (and any other .NET host).

> Status: early development (0.1.0). The supported subset and the roadmap are listed below.

## Features

- Lexer → parser → resolver → bytecode compiler → stack VM, all in managed C#.
- Targets .NET Standard 2.1 and Unity 6.3 LTS. No `Reflection.Emit`, `dynamic`, threads or runtime code generation, so it is safe for IL2CPP and WebGL.
- No `UnityEngine` references in the core library, so it is unit tested in plain .NET.
- Safe for untrusted scripts: per-slice step budget, optional hard step limit, call-depth limit, bounded event queue.
- One long-running script per game object (`PyTask`), advanced by `Update()` each tick, with `ctx.Wait(ticks)` for cooperative pauses.
- Host-defined events: the host queues an event by name and the script's `def` of that name handles it.
- Host API: register modules of native functions and constants; host objects are passed as plain-data handles (type id + id).
- Python-style tracebacks with line numbers, and an error-tolerant lexer for editor tooling.
- Editor tooling for in-game script editors: syntax highlighting, auto-complete, signature help, go-to-definition and diagnostics that work on half-typed code.
- Blocks: a Blueprint-style node graph (execution and value pins, user-made blocks) that compiles to ordinary Python source, with a source map back to the blocks.
- Optional save and resume of a running task (globals, call stack, waits, queued events), for offline progress and save games.

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

Native functions that need to pause the script take a `CallContext`
(`ctx.Wait(ticks, result)`). Register custom exception types with
`vm.RegisterExceptionType("GameError")` so scripts can raise and catch them.

A native function can call back into the script with `ctx.Call(callee, args...)` (a script function, built-in or bound method), for example to apply a callback or a sort key. The call runs to completion before `Call` returns, so the callee cannot wait (it gets a `RuntimeError`); it counts towards `MaxSteps`, and an exception it raises continues up through the script as usual.

## Saving and resuming tasks

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

## Editor tooling

The `CSharpPython.Editor` namespace has what an in-game code editor needs. All of it works on code that does not parse yet, never throws, and has no UnityEngine dependency.

```csharp
using CSharpPython.Editor;

// Colour spans: sorted, non-overlapping offsets into the text. Whitespace is not covered.
foreach (HighlightSpan span in SyntaxHighlighter.Highlight(source, vm))
    Paint(span.Start, span.Length, span.Kind);          // Keyword, String, Comment, Builtin, FunctionName, ...

// Suggestions at a cursor offset (a list of items plus the range of text they replace).
CompletionList list = Completer.Complete(source, cursorOffset, vm);

// The signature of the call the cursor is inside, and which parameter is being typed (null if unknown).
SignatureHelp help = SignatureHelper.Get(source, cursorOffset, vm);
if (help != null) Show(help.Label, help.Parameters, help.ActiveParameter);   // move(target, speed=1.0)

// Where the name under the cursor is defined in the script (null for built-ins, members and unknown names).
DefinitionLocation def = Navigator.FindDefinition(source, cursorOffset);
if (def != null) MoveCursorTo(def.Offset);                                    // also def.Line, def.Column, def.Kind

// Problems to underline: every lexical error, otherwise the first syntax or scoping error.
foreach (var problem in Analyzer.Analyze(source))
    Underline(problem.Line, problem.Column, problem.Length, problem.Message);
```

Passing the `PyVm` makes built-ins and host modules known: `game.` completes to the functions and constants of your `game` module, and `print` is coloured as a built-in. Completion offers the script's own names (respecting function scope), built-ins, keywords that make sense at that position, module names after `import`, members after `from m import`, and methods after a dot when the value's type is obvious from a literal, a constructor call or an assignment (otherwise every method name is offered).

Signature help covers the script's own functions (including `*args`, `**kwargs` and keyword-only parameters), built-ins, standard modules, methods of values whose type is obvious, and host module functions. Native functions don't carry parameter names, so give yours a description with `module.Describe("spawn", "kind, count=1")` (or `NativeFunction.Signature`); without one the editor shows a made-up list such as `arg1, arg2=...` from the argument counts.

## Blocks

The `CSharpPython.Blocks` namespace turns a visual node graph into Python source, so a block editor and a text editor share one runtime. The graph is plain serializable data (public fields, lists and strings, so `JsonUtility` or your own serializer can store it), and the compiler checks it as it writes, so a graph that compiles always produces a script the VM accepts.

```csharp
using CSharpPython.Blocks;

var graph = new BlockGraph().AddVariable("hp", "100");

// on_hit(amount): hp -= amount; if hp <= 0: game.explode()
var onHit = new BlockFunction("on_hit", "amount") { Start = "sub" };
onHit.Add(new BlockNode("sub", "change", "hp").Connect("value", "neg").Then("dead"));
onHit.Add(new BlockNode("neg", "neg").Connect("a", "amount"));
onHit.Add(new BlockNode("amount", "get", "amount"));
onHit.Add(new BlockNode("dead", "if").Connect("cond", "low").Then("then", "boom"));
onHit.Add(new BlockNode("low", "le").Connect("a", "hp").Literal("b", "0"));
onHit.Add(new BlockNode("hp", "get", "hp"));
onHit.Add(new BlockNode("boom", "call", "game.explode"));
graph.AddFunction(onHit);

var library = new BlockLibrary().AddFunction("explode", new string[0], module: "game");
BlockCompileResult result = BlockCompiler.Compile(graph, library);

if (result.Succeeded)
    vm.Start(result.Source);                         // ordinary Python: "import game", "def on_hit(amount): ..."
else
    foreach (var d in result.Diagnostics) Highlight(d.NodeId, d.Pin, d.Message);

// At run time, blame the block behind a failed task.
string badBlock = result.Map.Locate(task.Error);
```

- **Execution and values.** Flow blocks (`set`, `change`, `if`, `while`, `repeat`, `for_each`, `break`, `continue`, `return`, `append`, `set_index`, calls) are chained through execution pins (`next`, `then`, `else`, `body`). Value blocks (`literal`, `get`, arithmetic, comparison, `and/or/not`, `neg`, `index`, `list`, pure calls) have no execution pins and are written inline wherever their output is used.
- **Calls.** A `call` block names a function: a built-in (`print`, `len`, ...), a host function added to the `BlockLibrary` (`game.explode`; the `import` is written for you), or another function in the graph. Arguments are the pins `arg0`, `arg1`, .... A call that is not pure runs in the flow, and a later block may use its result as long as it runs after it, in the same or an enclosing branch.
- **User-made blocks and events.** Every function in the graph becomes a `def`. A function whose name matches a host event is that event's handler; any function can also be called from other functions, which is how user-made blocks work.
- **Globals.** Graph variables become module-level assignments, and `global` statements are written where functions assign them.
- **Literals.** A typed-in value must be a single number, string, `True`, `False` or `None`, so editor input can never smuggle in code. `BlockLiterals.String(text)` quotes any text safely.
- **Diagnostics.** Unknown blocks or pins, missing inputs, bad names, flow that loops or joins, results used before they exist, `break` outside a loop, values that depend on themselves and more are reported against the block and pin at fault. Blocks connected to nothing are warnings.
- **Source map.** `SourceMap.NodeAt(line)` and `LinesOf(nodeId)` link generated lines and blocks, and `Locate(error)` finds the block behind a runtime error.

Not in the first version: loops that wait for a changing call result (a `while` condition cannot use a stored call result; compute it inside the loop and use `if`/`break`), variadic calls, several outputs per block, and saving graphs to a file (use your own serializer).

## Language support

Supported:

| Area | Details |
| --- | --- |
| Types | `int` (64-bit, overflow raises `OverflowError`), `float`, `str`, `bool`, `None`, `list`, `tuple`, `dict`, `range`, host handles |
| Statements | assignment (chained, tuple unpacking, augmented), `if/elif/else`, `while`, `for`, `break`, `continue`, `pass`, `return`, `global`, `nonlocal`, `import`/`from ... import`, `def` (including nested functions), `class`, `lambda`, list and dict comprehensions, generator expressions, `try/except/else/finally`, `raise` |
| Expressions | arithmetic, comparison chains, `and/or/not`, `in`, `is`, conditional expressions, subscripts and slices, f-strings, keyword and default arguments |

Builtins: `print len range int float str bool repr abs min max sum round sorted list tuple dict enumerate zip reversed any all chr ord divmod pow format type isinstance issubclass callable getattr setattr hasattr object`, the standard exception classes, and a `math` module (`pi e tau inf nan sqrt log log2 log10 floor ceil trunc isnan isinf sin cos tan asin acos atan atan2 exp fabs hypot radians degrees pow`).

Methods: `str` (upper, lower, strip, lstrip, rstrip, split, join, replace, startswith, endswith, find, rfind, index, count, isdigit, isalpha, isalnum, isspace, isupper, islower, capitalize, title, format, zfill, ljust, rjust, center, splitlines), `list` (append, extend, insert, pop, remove, index, count, sort, reverse, clear, copy), `dict` (get, keys, values, items, pop, setdefault, update, clear, copy), `tuple` (index, count).

Functions take positional, default, keyword-only, `*args` and `**kwargs` parameters, and calls can spread with `f(*items, **options)`. A `*items` argument can't follow a keyword argument (`f(a=1, *items)`). `type(x)`, `isinstance` and `issubclass` work with the built-in types (`int`, `float`, `str`, `bool`, `list`, `tuple`, `dict`, `range`, `NoneType`, `function`, ...) and the exception classes, with tuples of them, and `bool` counts as an `int`. The type names are real type objects: calling them converts (`int('5')`), they print as `<class 'int'>`, compare with `==` and `is`, work as dict keys and have `__name__`; so does `x.__class__`. 

Classes have instances, instance and class attributes, methods, `__init__` and single inheritance (`class B(A):`, or `class A(object):`). Instances compare and hash by identity and are always truthy. They print as `<__main__.A object at 0x...>` unless the class defines `__str__` or `__repr__`, which `print`, `str`, `repr`, f-strings and the display of lists and dicts all use (`str` falls back to `__repr__`; both must return a string and, like sort keys, can't wait). `super().name` and `super(Class, self).name` reach the parent class's method or attribute, including `super().__init__(...)`.  `isinstance`, `issubclass`, `type(x)`, `x.__class__`, `A.__name__`, `getattr`, `setattr` and `hasattr` work with them, and a class or instance can be a dict key. `__init__` runs as an ordinary script frame, so it can wait (`game.wait`) and survive a snapshot, and snapshots keep classes, instances and cycles between them. Methods don't see the names of their class body (use `self.x` or `A.x`), as in Python, and a class body can't read a global before assigning a class variable of the same name (`x = x + 1` raises `UnboundLocalError` there). `super()` must be used as `super().name` directly inside a method (not stored in a variable, and not in a lambda or inner function inside it). Not supported yet: the other special methods (`__eq__`, `__add__`, `__len__`, ...), inheriting from built-in types other than exceptions, multiple inheritance, `@staticmethod`/`@classmethod`/`@property`, `__slots__`, metaclasses, `object()`.

Exceptions can be subclassed: `class HitError(ValueError):` (or `Exception`, or any built-in or host-registered exception type) makes a class you can `raise`, `raise HitError` (the instance is made for you) and catch with `except HitError as e`, alone or in a tuple. A built-in `except ValueError` catches the subclass too, `e` is the instance (so it has your fields and methods), and `e.args`, `str(e)` and `repr(e)` behave as in Python; `__init__` may call `super().__init__(msg)`, and `__str__` overrides the text. An uncaught one stops the task with the class name as `PyRuntimeError.TypeName`. Built-in errors raised by the VM (a `ZeroDivisionError`) are never instances of your subclasses. The traceback of an exception instance isn't kept across a snapshot.

Nested functions and `lambda` are closures with Python's semantics: inner functions share the variables of the function around them (late binding), and `nonlocal` rebinds them. List and dict comprehensions have their own scope like in Python (loop variables don't leak, the first iterable is evaluated outside). A generator expression is evaluated eagerly into a list, so it works with `sum`, `any`, `all`, `min`, `max`, `sorted`, `list`, `tuple`, `dict` and `str.join`, but is not lazy and can't be advanced with `next`. Snapshots keep closures, including cycles from recursive inner functions.

Not supported yet: set comprehensions, lazy generators, loop `else`, type annotations, decorators, walrus, `yield`, `with`, `del`, `assert`, `async`/`await`, set literals, dict unpacking, bit operators, dotted imports, bytes/complex literals, nested format specs in f-strings, `str % args`, slice assignment.

### Differences from CPython

- Integers are 64-bit and checked; there are no big ints.
- `dict.keys()/values()/items()` return lists, not views.
- `sorted`, `list.sort`, `min` and `max` accept `key=` (and `min`/`max` accept `default=`), but a key function runs to completion and cannot wait.
- `return`, `break` and `continue` are not allowed inside a `finally` block (they are in `try`, `except` and `else`, and run the enclosing `finally` blocks).
- A negative base with a fractional power raises `ValueError` (no complex numbers).

## Limits and safety

`vm.Options`: `StepsPerSlice` (instructions per `Update`), `MaxSteps` (hard cap, uncatchable `StepLimitExceeded`), `MaxCallDepth` (catchable `RecursionError`), `MaxQueuedEvents`.

## Install

Unity (Package Manager → Add package from git URL):

```
https://github.com/Snaken/CSharpPython.git?path=/src/CSharpPython.Core
```

Or build `CSharpPython.dll` (`dotnet build -c Release`, output under `artifacts/bin`) and drop it into `Assets/Plugins`. A NuGet package can be produced with `dotnet pack src/CSharpPython.Core`.

## Build and test

```
dotnet build
dotnet test
dotnet test --collect:"XPlat Code Coverage"
python3 scripts/generate-unity-meta.py   # after adding or removing source files
```

The test suite is large and uses CPython 3 as an oracle for language semantics; each fixed bug gets a regression test. CI enforces a line-coverage floor.

## Layout

- `src/CSharpPython.Core/Syntax` lexer, parser, AST
- `src/CSharpPython.Core/Compilation` resolver, compiler, bytecode
- `src/CSharpPython.Core/Runtime` values, operators, builtins, methods
- `src/CSharpPython.Core/Vm` `PyVm`, `PyTask`, host module API, snapshots
- `src/CSharpPython.Core/Tooling` syntax highlighting, completion, diagnostics
- `src/CSharpPython.Core/Blocks` block graph model, block library, graph-to-Python compiler, source map
- `tests/CSharpPython.Tests` NUnit tests

## Roadmap

- Blocks: graph serialization helpers, a block palette generated from host modules, comments and more block kinds.
- Editor tooling: references and rename, hover documentation.
- Language: more special methods (`__eq__`, `__add__`, `__len__`, ...), set literals and comprehensions, lazy generators (`yield`), dict views, slice assignment, big ints.

## License

MIT. If you use this library in a project, a credit is appreciated (see `LICENSE`).
