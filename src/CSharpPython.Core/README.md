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
- Editor tooling for in-game script editors: syntax highlighting, auto-complete, signature help, go-to-definition, hover documentation, find references, rename and diagnostics that work on half-typed code.
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

// What to show when the mouse rests on a name: a signature, type or docstring (null for unknown names).
HoverInfo info = Hover.Get(source, cursorOffset, vm);
if (info != null) ShowTooltip(info.Text);                                     // also info.Label, .Origin, .Documentation

// Every place that name is defined or used (find all references), and a safe rename.
IReadOnlyList<ReferenceLocation> uses = Refactoring.FindReferences(source, cursorOffset);   // also .IsDefinition
RenameResult renamed = Refactoring.Rename(source, cursorOffset, "newName");
if (renamed.Success) SetText(renamed.NewSource); else ShowError(renamed.Error);

// Problems to underline: every lexical error, otherwise the first syntax or scoping error.
foreach (var problem in Analyzer.Analyze(source))
    Underline(problem.Line, problem.Column, problem.Length, problem.Message);
```

Passing the `PyVm` makes built-ins and host modules known: `game.` completes to the functions and constants of your `game` module, and `print` is coloured as a built-in. Completion offers the script's own names (respecting function scope), built-ins, keywords that make sense at that position, module names after `import`, members after `from m import`, and methods after a dot when the value's type is obvious from a literal, a constructor call or an assignment (otherwise every method name is offered). Members of your own classes are completed after `self.` (methods, class variables, attributes assigned through `self`, and inherited ones), `super().` (the base classes' members), a variable assigned `ClassName(...)`, and the `e` of `except X as e` (`args`, plus the members of a script exception class).

Rename changes a function, class, variable or parameter everywhere the script uses it (including `name=` keyword arguments at calls of a renamed parameter) and leaves `obj.name` attributes alone. It refuses, changing nothing and saying why, for imported names, methods and class attributes, names used in `global`/`nonlocal`, an invalid new name, or a new name already in use.

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

- **Execution and values.** Flow blocks (`set`, `change`, `if`, `while`, `repeat`, `for_each`, `for_range`, `break`, `continue`, `return`, `append`, `set_index`, `remove_at`, `set_attr`, `comment`, calls) are chained through execution pins (`next`, `then`, `else`, `body`). `for_range` counts with optional `start`, `stop` and `step` inputs, `get_attr`/`set_attr` read and write a field (the field name is the block's name), and a `comment` block becomes a `#` line in the generated source. Value blocks (`literal`, `get`, arithmetic, comparison, `and/or/not`, `neg`, `index`, `contains`, `is_none`, `choose` (a conditional value), `get_attr`, `list`, pure calls) have no execution pins and are written inline wherever their output is used.
- **Calls.** A `call` block names a function: a built-in (`print`, `len`, ...), a host function added to the `BlockLibrary` (`game.explode`; the `import` is written for you), or another function in the graph. Arguments are the pins `arg0`, `arg1`, .... A call that is not pure runs in the flow, and a later block may use its result as long as it runs after it, in the same or an enclosing branch.
- **User-made blocks and events.** Every function in the graph becomes a `def`. A function whose name matches a host event is that event's handler; any function can also be called from other functions, which is how user-made blocks work.
- **Globals.** Graph variables become module-level assignments, and `global` statements are written where functions assign them.
- **Literals.** A typed-in value must be a single number, string, `True`, `False` or `None`, so editor input can never smuggle in code. `BlockLiterals.String(text)` quotes any text safely.
- **Diagnostics.** Unknown blocks or pins, missing inputs, bad names, flow that loops or joins, results used before they exist, `break` outside a loop, values that depend on themselves and more are reported against the block and pin at fault. Blocks connected to nothing are warnings.
- **Saving.** `graph.ToJson()` writes a graph as JSON and `BlockGraph.FromJson(text)` reads it back (a bad document throws `BlockGraphFormatException`). The field names match the public fields, so Unity's `JsonUtility` output reads too, unknown fields are ignored, and a `Version` field guards against newer files.
- **Palette from host modules.** `library.AddModule(vm, "game")` (or `AddAllModules(vm)`, which treats `math` as pure) adds every function of a registered module, with pin names taken from its signature (`ModuleBuilder.Describe`). Parameters with defaults become optional pins: leave them empty and the argument is left out. `library.Palette()` returns the blocks and functions grouped by category and module, ready for an editor's block list.
- **Source map.** `SourceMap.NodeAt(line)` and `LinesOf(nodeId)` link generated lines and blocks, and `Locate(error)` finds the block behind a runtime error.

Not in the first version: loops that wait for a changing call result (a `while` condition cannot use a stored call result; compute it inside the loop and use `if`/`break`), variadic calls and several outputs per block.

## Language support

Supported:

| Area | Details |
| --- | --- |
| Types | `int` (64-bit, overflow raises `OverflowError`), `float`, `str`, `bool`, `None`, `list`, `tuple`, `dict`, `set`, `range`, host handles |
| Statements | assignment (chained, tuple unpacking including `a, *rest = xs`, augmented), `if/elif/else`, `while` and `for` (with an `else` block that a `break` skips), `break`, `continue`, `pass`, `return`, `global`, `nonlocal`, `del` (names, attributes, items and list slices), slice assignment (`xs[1:3] = [...]`, `xs[::2] = [...]` on lists, with CPython's size rules), type annotations (`x: int = 5`, `def f(a: int = 0, *args: str) -> bool:`; they are checked for syntax and then ignored, never evaluated or stored), `assert`, `with` (context managers, with `as` targets and several items), `yield` and `yield from` (generators), `import`/`from ... import`, `def` (including nested functions), `class`, `lambda`, list, dict and set comprehensions, set displays (`{1, 2}`), `*iterable` and `**mapping` unpacking inside list, tuple, set and dict displays (`[*a, 1]`, `{**d, 'k': v}`), generator expressions, `try/except/else/finally`, `raise` |
| Expressions | arithmetic, bit operators (`&`, `\|`, `^`, `~`, `<<`, `>>`, on `int` and `bool`; `\|`, `&` and `^` also on sets), comparison chains, `and/or/not`, `in`, `is`, conditional expressions, assignment expressions (`if (n := len(xs)) > 3:`, `while (line := next_line()):`, `[y for x in xs if (y := f(x))]`; in a comprehension the name is bound in the enclosing function or module, as in Python), subscripts and slices, f-strings, keyword and default arguments |

Builtins: `print len range int float str bool repr abs min max sum round sorted list tuple dict set enumerate frozenset slice zip map filter reversed any all chr ord divmod pow format type isinstance issubclass callable getattr setattr hasattr delattr object hash id bin oct hex ascii dir vars staticmethod classmethod property`, the constants `NotImplemented` and `Ellipsis` (also written `...`), the standard exception classes (including `BaseException`, `OSError` and its subclasses, and the `Warning` family; `SystemExit`, `KeyboardInterrupt` and `GeneratorExit` derive from `BaseException`, so `except Exception` does not catch them), and a `math` module (`pi e tau inf nan sqrt log log2 log10 floor ceil trunc isnan isinf sin cos tan asin acos atan atan2 exp fabs hypot radians degrees pow`).

Methods: `str` (upper, lower, strip, lstrip, rstrip, split, join, replace, startswith, endswith, find, rfind, index, count, isdigit, isalpha, isalnum, isspace, isupper, islower, capitalize, title, format, zfill, ljust, rjust, center, splitlines), `list` (append, extend, insert, pop, remove, index, count, sort, reverse, clear, copy), `dict` (get, keys, values, items, pop, setdefault, update, clear, copy), `tuple` (index, count), `set` (add, remove, discard, pop, clear, copy, update, union, intersection, difference, symmetric_difference, intersection_update, difference_update, symmetric_difference_update, issubset, issuperset, isdisjoint).

Functions take positional, default, keyword-only, `*args` and `**kwargs` parameters, and calls can spread with `f(*items, **options)`. A `*items` argument can't follow a keyword argument (`f(a=1, *items)`). `type(x)`, `isinstance` and `issubclass` work with the built-in types (`int`, `float`, `str`, `bool`, `list`, `tuple`, `dict`, `range`, `NoneType`, `function`, ...) and the exception classes, with tuples of them, and `bool` counts as an `int`. The type names are real type objects: calling them converts (`int('5')`), they print as `<class 'int'>`, compare with `==` and `is`, work as dict keys and have `__name__`; so does `x.__class__`. 

Modules can be dotted: register `game.ai` (and its parent `game` is created for you if you didn't register it) and scripts can `import game.ai`, `import game.ai as ai`, `from game.ai import think` or `from game import ai`. `import a, b` (several modules in one statement) is still not supported. Decorators work on functions, methods and classes (`@name`, `@obj.attr`, `@factory(args)`, stacked; the `@` must start the line, `a @ b` is still not supported). `@staticmethod`, `@classmethod` and `@property` (with `@x.setter` and `@x.deleter`) behave as in Python, including through `super()`; a property getter or setter runs to completion like the other special methods, so it cannot wait. Functions and methods have `__name__`, but not other attributes. A class can also define `__getattr__` (called when normal lookup fails, including when a property getter raises `AttributeError`), `__setattr__` and `__delattr__` (called for every set or delete; call `object.__setattr__(self, name, value)` or `super().__setattr__(name, value)` to store), and these run to completion, so they cannot wait. Classes have instances, instance and class attributes, methods, `__init__` and single inheritance (`class B(A):`, or `class A(object):`). Instances compare and hash by identity and are truthy unless the class says otherwise with `__eq__`, `__ne__`, `__hash__`, `__lt__`/`__le__`/`__gt__`/`__ge__`, `__bool__` or `__len__`. These work as in Python: the operators return whatever the method returns, `NotImplemented` declines so the other operand's method is tried (a subclass's goes first), a class that defines `__eq__` without `__hash__` is unhashable, and `sorted`, `min`, `max`, `in`, `list.index` and dict keys use them. The arithmetic operators work the same way: `__add__`, `__sub__`, `__mul__`, `__truediv__`, `__floordiv__`, `__mod__`, `__pow__`, and the bit operators `__and__`, `__or__`, `__xor__`, `__lshift__` and `__rshift__`, their reflected `__r...__` forms (so `sum` works with `__radd__`), the in-place `__i...__` forms used by `+=` and friends (falling back to the plain operator), `__neg__`, `__pos__`, `__invert__`, and the conversions behind `abs()`, `int()` and `float()` (`__abs__`, `__int__`, `__float__`). Objects can also be containers and callables: `__getitem__` and `__setitem__` and `__delitem__` (index, `del` and `+=` on an item; a slice arrives as a `slice` object), `__contains__` (`in`, falling back to iterating), `__iter__` and `__next__` (`for` loops, comprehensions, `list()`, `sum()`, unpacking and so on; `__iter__` may return `self`, another object with `__next__`, or `iter(some_list)`; raising `StopIteration` ends the loop), and `__call__`, `__enter__` and `__exit__` (for `with`), which, unlike the other special methods, are ordinary calls and can wait. `iter(x)` and `next(it[, default])` are built in. The other special methods run to completion like sort-key callbacks, so they can't wait, and a dictionary keyed by script objects can only be looked up while its script is running (after a snapshot restore, it is indexed when the script next uses it). They print as `<__main__.A object at 0x...>` unless the class defines `__str__` or `__repr__`, which `print`, `str`, `repr`, f-strings and the display of lists and dicts all use (`str` falls back to `__repr__`; both must return a string and, like sort keys, can't wait). `super().name` and `super(Class, self).name` reach the parent class's method or attribute, including `super().__init__(...)`.  `isinstance`, `issubclass`, `type(x)`, `x.__class__`, `A.__name__`, `getattr`, `setattr` and `hasattr` work with them, and a class or instance can be a dict key. `__init__` runs as an ordinary script frame, so it can wait (`game.wait`) and survive a snapshot, and snapshots keep classes, instances and cycles between them. Methods don't see the names of their class body (use `self.x` or `A.x`), as in Python, and a class body can't read a global before assigning a class variable of the same name (`x = x + 1` raises `UnboundLocalError` there). `super()` must be used as `super().name` directly inside a method (not stored in a variable, and not in a lambda or inner function inside it). Not supported yet: `__getattr__`/`__setattr__`, `__format__`, inheriting from built-in types other than exceptions, multiple inheritance, `@staticmethod`/`@classmethod`/`@property`, `__slots__`, metaclasses, `object()`.

Exceptions can be subclassed: `class HitError(ValueError):` (or `Exception`, or any built-in or host-registered exception type) makes a class you can `raise`, `raise HitError` (the instance is made for you) and catch with `except HitError as e`, alone or in a tuple. A built-in `except ValueError` catches the subclass too, `e` is the instance (so it has your fields and methods), and `e.args`, `str(e)` and `repr(e)` behave as in Python; `__init__` may call `super().__init__(msg)`, and `__str__` overrides the text. An uncaught one stops the task with the class name as `PyRuntimeError.TypeName`. Built-in errors raised by the VM (a `ZeroDivisionError`) are never instances of your subclasses. The traceback of an exception instance isn't kept across a snapshot.

Nested functions and `lambda` are closures with Python's semantics: inner functions share the variables of the function around them (late binding), and `nonlocal` rebinds them. Sets keep their members in insertion order (CPython orders them by hash, so sort before printing if you compare output), take `-`, `-=` `|`, `&`, `^` (and their `=` forms), and the subset comparisons `<=`, `<`, `>=`, `>`, and are unhashable. `frozenset(iterable)` makes an immutable, hashable set that can be a dict key or a member of a set; it has the non-mutating set methods and operators (the result of `|`, `&`, `-` and `^` has the type of the left operand), compares equal to a set with the same members, prints as `frozenset({1, 2})`, and is kept by snapshots. Looking up a set in a set (`{1} in s`) compares it as a frozenset, as in Python. `dir(x)` and `vars(x)` take exactly one argument, and `dir` leaves out the dunder methods every object has. `slice(stop)` and `slice(start, stop[, step])` make slice objects, which are hashable and comparable. List, dict and set comprehensions have their own scope like in Python (loop variables don't leak, the first iterable is evaluated outside). Generator expressions are lazy generators, like in Python (the first iterable is evaluated, and checked to be iterable, when the generator is made). Snapshots keep closures, including cycles from recursive inner functions.

Generators: a function containing `yield` returns a generator when called, which runs when something asks for its next value (`for`, `next(g)`, `g.send(x)`, `list(g)`, `sum(g)`, unpacking, and so on). `return value` ends it, `yield from` hands on every item of another iterable and evaluates to the value a sub-generator returned, and `g.close()` ends it. A `for` loop, `next(g)`, `g.send(x)` and `g.__next__()` run the generator as an ordinary frame, so its body can wait (`game.wait`) and a saved task can contain a half-run generator; a generator consumed by a built-in such as `list()`, `sum()` or `sorted()` cannot wait, like a sort-key callback. Not supported: `g.throw()`, `finally` blocks do not run on `close()`, values sent to a generator are not forwarded through `yield from`, `StopIteration.value`, async generators, and a generator function as an event handler. `yield` is not allowed in a class body, at module level or inside a comprehension. Note that `sum(f(x) for x in xs)` and similar now pull values from a lazy generator through the built-in, so `f` can no longer wait there (it used to run in a script frame); use a `for` loop or a list comprehension if it needs to.

`with` follows PEP 343: `__exit__` receives the exception type and instance (the traceback argument is always `None`) and a true return value suppresses the exception; `return`, `break` and `continue` inside the body run `__exit__` too. The parenthesized form `with (a as x, b as y):` is not supported.

Not supported yet: `async`/`await`, bytes/complex literals, nested format specs in f-strings, `str % args`.

### Differences from CPython

- Integers are 64-bit and checked; there are no big ints.
- Built-in exceptions keep only a message string, so `ValueError(42).args` is `('42',)`. Exception classes you write keep their real `args`.
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

- Blocks: more block kinds as they are needed.
- Language: more special methods (`__format__`, `__index__`, ...), dict views, big ints.

## License

MIT. If you use this library in a project, a credit is appreciated (see `LICENSE`).
