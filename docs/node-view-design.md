# Node view: design notes

Status: design agreed. Step 1 is built: typed `Describe` strings (`HostSignature`, `PinType`), the `latent` flag, and types for the built-in functions, `math` and `random` (methods of built-in types are not typed yet). Step 2 is built for functions: `PinCompatibility` decides whether a wire is allowed, and `PinTypeIndex` searches functions by pin type. Step 3 has started: built-in blocks have pin types and wildcard pins, and the compiler refuses wires that do not fit (`BlockTypeChecker`). Several outputs per node are built (`BlockInput.FromPin`). Shared pure nodes are built (decision 11), flow joins (decision 13), branch-scoped values (decision 12) and typed global variables (decision 16: declared type, checked `get`/`set`/`change`; the variable list for watching is for the editor). The editor section of the saved file (decision 17) and placeholders for missing host functions (decision 18) are built. Not built yet: the block types in the search index, load validation with limits, typed variables and the editor section.

The node view is a Blueprint-style editor: a script is a graph of nodes on a free canvas, joined by wires. The player picks a script's kind once. A script made as a node graph stays a node graph; changing the kind discards the graph.

## Decided

**The model**

1. **The graph is the source of truth.** The Python source of a node script is generated and read-only. Nothing parses Python back into a graph.
2. **Blocks and nodes share one backing model.** The node view is built on the existing Blocks graph (see below) with its gaps closed. A stacked block view is a second editor over the same data and may enforce its own limits.
3. **A script is attached to an object** (for example, a character in a maze). Event nodes listen to game events, and the player drags built-in and native-function nodes onto the canvas. Native functions are the same host functions scripts call today.
4. **The first version has event handlers and callable graph functions.** Callable functions have typed parameters and their own entry and exit nodes. Reusable nodes that a player makes and shares between scripts come later and are built from nodes only. Python functions as nodes are a separate, later question.

**Types and wires**

5. **Wires are checked while they are drawn.** Every pin has a type. A wire between pins whose types do not fit is refused by the editor, with the reason. A graph with a bad wire never compiles, so there are no run-time wiring errors.
6. **The editor can search by type.** Given an output pin, list every node that accepts it as an input. Given an input pin, list every node that produces a value it accepts. This drives the sidebar, and it needs an index from type to nodes.
7. **Host functions declare types in the signature string.** Extend `Describe`: `"kind: str, count: int = 1 -> Entity"`. Existing untyped strings keep working and give `any` pins. Host handle types are registered separately. An `int` wire is accepted by a `float` pin, as in Python; every other mismatch is refused.
8. **Lists are typed, with wildcard pins.** A list has an element type (`list of int`). Nodes such as "add to list" and "get item" have a wildcard pin that takes its type from the first wire.
9. **Host objects have declared types.** The host registers a handle type such as `Character` with named, typed fields and methods. The editor gets get and set field nodes. Design this together with the roadmap's attribute access on host handles (`player.hp`).
10. **A node can have several outputs.** A host function declares `-> (hit: bool, point: Vec)` and returns a tuple. The compiler unpacks it into one local per output pin, so each pin can be wired separately.

**Running**

11. **A pure node with several consumers runs once, and the result is shared, per flow step.** The value is computed the first time a consumer needs it during the nearest enclosing flow step and reused after that. Inside a loop body that means once per iteration. A variable changed between two uses does not change the shared value. A node that must give a new value on each request (a random number) will be a separate kind later, as will one that computes on first use and then keeps its value. As built: a flow step ends at a latent call and at each loop pass; variable reads and literals are not shared; a value is never worked out early where it only sometimes runs (the right side of `and`/`or`, the branches of `choose`, a `while` condition).
12. **A value that only exists inside a branch cannot be wired outside it.** A `for each` node's element output is valid only inside its loop body. Wiring it elsewhere is a compile-time error and the editor refuses the wire. As built: `for_each`, `for_range` and a named `repeat` have one output, the loop's variable; the compiler allows it only while the loop's body is being written, and `CheckWire` refuses a wire that would take it out of the body (including through a value block that is also read outside). A block not yet joined to the flow is not judged by the editor, so the compiler is the last check. A call result stored before a branch and used after a join is covered by decision 13.
13. **Flow joins are allowed.** Two execution wires may enter one node. The code after the join is emitted once as a local function and called from each path that reaches it, because Python has no goto. As built: the function sits at the top of the function that holds the join; a `return` in it answers with a one-item tuple that its callers pass on; names it assigns are `nonlocal` (or `global`), and a name it first sets that is also used outside it is an error. Not allowed: `break` or `continue` in a join inside a loop, a join entered from inside and outside a loop or from different parts of the flow, and a stored call result that did not run on every path into the join.
14. **Latent nodes are marked by the host.** A host function that can wait carries a `latent` flag in its description. The editor draws a clock mark on it, and the compiler knows the flow can pause there (this matters for decision 11).
15. **The running node can be highlighted.** This needs the "current line" on `PyTask` from the roadmap, plus the map from generated lines back to nodes.

**Saving and loading**

16. **Variables are declared, typed and listed.** A variable has a name, a type and a starting value, belongs to the script instance and is shown in a list in the editor. A player can watch variables while the game runs. Editing them live comes later.
17. **Editor-only data is in the same file.** Node positions, comment boxes, reroute points and collapsed groups go in an editor section keyed by node id. The compiler ignores it, it never changes the compiled program, and moving a node does not invalidate a snapshot.
18. **A missing host function does not stop a graph loading.** The node shows as a placeholder with its wires kept, and compiling reports it. There is no migration support at first.
19. **Graph files are validated on load.** Node and wire counts, id and name lengths, type names and literals are checked against limits the host can change, and a bad file is rejected with a reason. `MaxSteps` still bounds what a graph does at run time.

**Where it lives**

20. **The core owns the model, not the canvas.** The graph model, type checking, compiler and search index stay free of Unity code. The node canvas ships as a sample inside the package first and moves to its own package once it settles.

## What already exists

The Blocks module is already a graph, not a stack of blocks. Its model has nodes with ids, input pins wired to other nodes' outputs, execution pins that link nodes, value nodes with no execution pins that are evaluated where used, functions that serve as event handlers, JSON saving, diagnostics reported against a node and pin, and a source map from generated lines back to nodes (`SourceMap.Locate` finds the node behind a runtime error). The Scratch look of the demo came from the editor drawn over it, not from the model.

So the question is less "a second system" than "which parts of this model does a Blueprint-style editor need that it lacks". The gaps, as the code stands:

| Need | Today |
| --- | --- |
| Typed pins | None. Host functions describe parameters as a name string (`Describe("spawn", "kind, count=1")`), with no types and no result type. |
| Several outputs from one node | Built: an input names the output it reads (`BlockInput.FromPin`). |
| Two flows joining into one node | Built: the flow from a joined block on is a local function that each path calls. A loop back is still an error. |
| A value used by several nodes | Built: a pure node several inputs read is worked out once per flow step (a loop pass or a latent call starts a new step). Variable reads and literals are not shared. |
| Waiting inside a loop condition | A `while` condition cannot use a stored call result. |
| Variadic calls | Not supported. |

Everything else a node view needs (events, user-made functions, globals, JSON, diagnostics, the source map) is there. **Decided:** build the node view on this model, with these gaps closed, and treat the block view as a second editor over the same data. If a stacked block view needs its own limits (for instance, no joins), it can enforce them in its editor.

## To work out while building

These are details, not open design questions:

- Whether the type check sits in the compiler, in a separate class the editor calls, or both (the editor needs it per wire, the compiler needs it for the whole graph).
- The exact rules for `int`, `float`, `bool` and `any` when a wire is drawn, and which conversions show a conversion node.
- How an entry node's typed parameters and an exit node's results appear for callable functions, and how recursion is limited.
- Which limits the load validation uses by default.

## Suggested order

1. Add pin types to the host function description (typed signature strings, handle types, the `latent` flag), with a type for each existing built-in function. This helps the Python editor tooling as well (hover and completion).
2. Add the type-compatibility check and the search-by-type index, with tests, before any editor exists.
3. Close the compiler gaps: shared pure nodes, flow joins, several outputs, branch-scoped values, typed variables and the editor section in the saved file, with load validation.
4. Add the "current line" to `PyTask` and map it to nodes.
5. Build a sample node editor, then the samples for the three views.
