# Editor tooling

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
