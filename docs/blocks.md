# Blocks

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
- **Diagnostics.** Unknown blocks or pins, missing inputs, wires whose types do not fit, bad names, flow that loops or joins, results used before they exist, `break` outside a loop, values that depend on themselves and more are reported against the block and pin at fault. Blocks connected to nothing are warnings.
- **Typed wires.** Every pin has a type, and the compiler reports a wire that does not fit against the block and pin at fault (`BlockTypeChecker.Check` returns the same errors without compiling). `any` fits everywhere, an `int` fits a `float` pin, and every other mismatch is refused: a `str` cannot go to the `count` of `repeat`, and a function's result of type `none` has no value to wire. Built-in blocks have fixed pin types (`repeat` takes an `int`, comparisons give `bool`); host functions get theirs from `Describe` (see [host API](host-api.md)). Some pins are wildcards: `append`, `set_index`, `index` and `choose` tie their pins to one type, taken from the wires (`index` on a `list[int]` gives an `int`, and `append` to it refuses a `str`). An `int` and a `float` make `float`. Operators give `int`, `float`, `str` or a list from their inputs, and `any` otherwise. A `list` block's items need not match (it is `list[any]` then). Variables and `None` are `any`. An editor can ask `BlockTypeChecker.CheckWire(graph, function, toNode, pin, fromNode)` whether a wire may be drawn, and get the reason when it may not, and `OutputType` for the type of a pin.
- **Saving.** `graph.ToJson()` writes a graph as JSON and `BlockGraph.FromJson(text)` reads it back (a bad document throws `BlockGraphFormatException`). The field names match the public fields, so Unity's `JsonUtility` output reads too, unknown fields are ignored, and a `Version` field guards against newer files.
- **Palette from host modules.** `library.AddModule(vm, "game")` (or `AddAllModules(vm)`, which treats `math` as pure) adds every function of a registered module, with pin names taken from its signature (`ModuleBuilder.Describe`). Parameters with defaults become optional pins: leave them empty and the argument is left out. `library.Palette()` returns the blocks and functions grouped by category and module, ready for an editor's block list.
- **Source map.** `SourceMap.NodeAt(line)` and `LinesOf(nodeId)` link generated lines and blocks, and `Locate(error)` finds the block behind a runtime error.

Not in the first version: loops that wait for a changing call result (a `while` condition cannot use a stored call result; compute it inside the loop and use `if`/`break`), variadic calls and several outputs per block.
