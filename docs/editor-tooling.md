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

A host that colours the text and underlines problems on every change can ask for both at once:

```csharp
EditorView view = EditorView.Build(source, vm);
foreach (HighlightSpan span in view.Spans)        // the same spans as Highlight, split where a problem starts or ends
    Paint(span.Start, span.Length, span.Kind, error: span.Problem != null);   // span.Problem is the message, or null
foreach (var problem in view.Diagnostics)         // the same list as Analyzer.Analyze
    ListProblem(problem.Line, problem.Column, problem.Message);
```

`Build` lexes the text once where `Highlight` and `Analyze` called one after the other lex it up to three times, and it does the merge every host otherwise writes by hand: a span that a problem covers is marked, and one that a problem only touches is split so that the mark lies on the right characters. A problem that points at one place marks the character there, and a problem that lies only in whitespace (a bad indent, say) marks no span, because spans never cover whitespace; it is still in `Diagnostics`. Measured on a 600-line script it took 0.41 ms against 0.52 ms for the two calls, and 0.16 ms against 0.19 ms for 60 lines, so it is a convenience first and a small saving second. `Highlight` and `Analyze` are unchanged for a host that wants only one of them.

A host that draws the spans as Unity rich text must escape `<` in the text it puts between tags (`<noparse><</noparse>` does it; a bare `<` starts a tag), and a text field whose last line is empty loses that line's height unless the text ends with one more character such as a space. Offsets in spans refer to the text before any of that, so add them back when mapping a span to a position in the marked-up string.

Passing the `PyVm` makes built-ins and host modules known: `game.` completes to the functions and constants of your `game` module, and `print` is coloured as a built-in. Completion offers the script's own names (respecting function scope), built-ins, keywords that make sense at that position, module names after `import`, members after `from m import`, and methods after a dot when the value's type is obvious from a literal, a constructor call or an assignment (otherwise every method name is offered). Members of your own classes are completed after `self.` (methods, class variables, attributes assigned through `self`, and inherited ones), `super().` (the base classes' members), a variable assigned `ClassName(...)`, and the `e` of `except X as e` (`args`, plus the members of a script exception class).

Rename changes a function, class, variable or parameter everywhere the script uses it (including `name=` keyword arguments at calls of a renamed parameter) and leaves `obj.name` attributes alone. It refuses, changing nothing and saying why, for imported names, methods and class attributes, names used in `global`/`nonlocal`, an invalid new name, or a new name already in use.

Signature help covers the script's own functions (including `*args`, `**kwargs` and keyword-only parameters), built-ins, standard modules, methods of values whose type is obvious, and host module functions. Native functions don't carry parameter names, so give yours a description with `module.Describe("spawn", "kind, count=1")` (or `NativeFunction.Signature`); without one the editor shows a made-up list such as `arg1, arg2=...` from the argument counts. `Describe` also takes `doc:` for a sentence or two on what the function does, and `DescribeParameter` gives one parameter a text: hover shows the doc, `SignatureHelp` has `Documentation` and a `ParameterDocs` list in the order of `Parameters` (null where a parameter has no text), and a `CompletionItem` has `Documentation` next to its one-line `Detail`.
