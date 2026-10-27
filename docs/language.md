# Language support

CSharpPython runs a large, practical subset of Python 3. This page lists what works; the last section lists where it
differs from CPython. Anything not mentioned here should be assumed unsupported.

Contents: [Syntax and types](#syntax-and-types) · [Built-ins](#built-ins-and-methods) · [Functions and modules](#functions-and-modules) ·
[Classes](#classes) · [Exceptions](#exceptions) · [Closures, sets and comprehensions](#closures-sets-and-comprehensions) ·
[Generators](#generators) · [with](#with) · [match](#match) · [Not supported](#not-supported-yet) · [Differences from CPython](#differences-from-cpython) · [Limits](#limits-and-safety)

## Syntax and types

| Area | Details |
| --- | --- |
| Types | `int` (any size, up to `MaxIntBits`), `float`, `str`, `bool`, `None`, `list`, `tuple`, `dict`, `set`, `range`, host handles |
| Statements | assignment (chained, tuple unpacking including `a, *rest = xs`, augmented), `if/elif/else`, `while` and `for` (with an `else` block that a `break` skips), `break`, `continue`, `pass`, `return`, `global`, `nonlocal`, `del` (names, attributes, items and list slices), slice assignment (`xs[1:3] = [...]`, `xs[::2] = [...]` on lists, with CPython's size rules), type annotations (`x: int = 5`, `def f(a: int = 0, *args: str) -> bool:`; they are checked for syntax and then ignored, never evaluated or stored), `assert`, `with` (context managers, with `as` targets and several items), `match` (see [match](#match)), `yield` and `yield from` (generators), `import`/`from ... import`, `def` (including nested functions), `class`, `lambda`, list, dict and set comprehensions, set displays (`{1, 2}`), `*iterable` and `**mapping` unpacking inside list, tuple, set and dict displays (`[*a, 1]`, `{**d, 'k': v}`), generator expressions, `try/except/else/finally`, `raise` |
| Expressions | arithmetic, bit operators (`&`, `\|`, `^`, `~`, `<<`, `>>`, on `int` and `bool`; `\|`, `&` and `^` also on sets; `\|` and `\|=` on dicts), comparison chains, `and/or/not`, `in`, `is`, conditional expressions, assignment expressions (`if (n := len(xs)) > 3:`, `while (line := next_line()):`, `[y for x in xs if (y := f(x))]`; in a comprehension the name is bound in the enclosing function or module, as in Python), subscripts and slices, f-strings, printf-style `"%d items" % n` (flags, width, precision, `*`, `%(name)s` with a dict, and `d i u o x X e E f F g G c r s a %`), keyword and default arguments |

## Built-ins and methods

**Built-in functions and names:** `print len range int float str bool repr abs min max sum round sorted list tuple dict set enumerate frozenset slice zip map filter reversed any all chr ord divmod pow format type isinstance issubclass callable getattr setattr hasattr delattr object hash id bin oct hex ascii dir vars staticmethod classmethod property`, the constants `NotImplemented` and `Ellipsis` (also written `...`), the standard exception classes (including `BaseException`, `OSError` and its subclasses, and the `Warning` family; `SystemExit`, `KeyboardInterrupt` and `GeneratorExit` derive from `BaseException`, so `except Exception` does not catch them), and a `math` module (`pi e tau inf nan sqrt log log2 log10 floor ceil trunc isnan isinf sin cos tan asin acos atan atan2 exp fabs hypot radians degrees pow isfinite isclose copysign fmod remainder modf frexp ldexp cbrt exp2 expm1 log1p sinh cosh tanh asinh acosh atanh factorial gcd lcm isqrt comb perm prod fsum dist`; `hypot` takes any number of coordinates).

**`random`:** `seed random getrandbits randrange randint choice shuffle sample choices uniform triangular normalvariate gauss lognormvariate expovariate`. It uses CPython's Mersenne Twister and its algorithms, so `random.seed(42)` followed by the same calls gives the same numbers as CPython. Each task has its own generator, which snapshots save. A task that never calls `seed` starts from a random seed. Only `None` and `int` seeds are accepted (not `float`, `str` or `bytes`), `sample` has no `counts`, and there is no `Random` class, `getstate`/`setstate`, `randbytes` or the rarer distributions.

**`itertools`:** `count`, `cycle`, `repeat`, `chain` (and `chain.from_iterable`), `islice`, `accumulate`, `compress`, `dropwhile`, `takewhile`, `filterfalse`, `starmap`, `zip_longest`, `pairwise`, `batched`, `product`, `permutations`, `combinations`, `combinations_with_replacement` and `groupby`, which match CPython's output. They are lazy, so `islice(count(), 5)` works, and a task that is paused in the middle of a loop over one is saved and restored with it. `product`, `permutations` and the combinations read their input into a list first, as CPython does. `tee(iterable, n=2)` gives independent iterators that share one list of the items already read; the list is never trimmed, so an endless source that one copy runs far ahead of keeps all of it in memory. Functions passed to them (`takewhile`, `groupby(key=)`, `accumulate(func)`...) run to completion and cannot wait with `game.wait`.

**`functools`:** `reduce`, `partial` (with `func`, `args` and `keywords`), `lru_cache` and `cache` (with `cache_info()`, `cache_clear()` and `__wrapped__`; a recursive cached function can recurse as deep as any other, and a call in progress is saved by snapshots), and `wraps`, which only passes the function through (the wrapper keeps its own `__name__`). `cmp_to_key(f)` makes a key whose values order by calling `f` (`sorted`, `sort`, `min`, `max` and `<`, `<=`, `>`, `>=` work; `==` on two keys is identity). `total_ordering` works out the missing comparisons of a class from one of `__lt__`, `__le__`, `__gt__` or `__ge__`, as CPython does; the comparison functions it calls cannot wait. `singledispatch` is missing. A cached method gets `self` as part of its key, like in CPython. **`operator`:** the arithmetic, bitwise, comparison and truth functions (`add`, `mul`, `lt`, `not_`, `getitem`, `contains`, `neg`...), `itemgetter`, `attrgetter` (dotted names too) and `methodcaller` (positional arguments only). Functions called through `methodcaller` cannot wait with `game.wait`.

**`collections`:** `defaultdict`, `Counter`, `deque` and `namedtuple`. The first two are dictionaries, so `isinstance(d, dict)` is true, while `type(d) is dict` is false. A `defaultdict` calls its `default_factory` for a missing key (`d[k]`, not `get`, `in` or `pop`) and stores the result; the factory runs to completion, so it cannot wait with `game.wait`. A `Counter` answers 0 for a missing key and has `most_common`, `elements`, `total`, a counting `update`, `subtract`, and `+ - | &` (which keep only positive counts); two counters compare as equal when only their zero counts differ. `copy()` keeps the type, while `dict(c)` gives a plain dict. Counters do not have the unary `+` and `-`. A `deque` is a ring buffer, so `append`, `appendleft`, `pop` and `popleft` take constant time; it has `maxlen`, `extend`, `extendleft`, `rotate`, `insert`, `remove`, `index`, `count`, `reverse`, `copy`, `clear`, indexing, `+`, `+=` and comparison with other deques, and it is not a list (`isinstance(d, list)` is false, no slicing, no `sort`, `d == [..]` is false). Changing a deque while looping over it raises `RuntimeError`. `namedtuple(typename, field_names, rename=, defaults=)` makes a tuple type: its instances are real tuples (they unpack, index, hash, compare with plain tuples, and `isinstance(p, tuple)` is true) with fields by name, a repr like `Point(x=1, y=2)`, `_fields`, `_field_defaults`, `_asdict()`, `_replace(**changes)` and `Point._make(iterable)`. A class cannot inherit from a named tuple yet (`class P(namedtuple(...))` raises `TypeError`), because classes cannot inherit from built-in types yet.

**Methods:** `str` (upper, lower, strip, lstrip, rstrip, split, join, replace, startswith, endswith, find, rfind, index, count, isdigit, isalpha, isalnum, isspace, isupper, islower, capitalize, title, format, zfill, ljust, rjust, center, splitlines, casefold, swapcase, isascii, isdecimal, isnumeric, isidentifier, isprintable, istitle, expandtabs, partition, rpartition, removeprefix, removesuffix, rindex, rsplit), `list` (append, extend, insert, pop, remove, index, count, sort, reverse, clear, copy), `dict` (get, keys, values, items, pop, popitem, fromkeys, setdefault, update, clear, copy), `tuple` (index, count), `set` (add, remove, discard, pop, clear, copy, update, union, intersection, difference, symmetric_difference, intersection_update, difference_update, symmetric_difference_update, issubset, issuperset, isdisjoint).

## Functions and modules

- Functions take positional, default, positional-only (`def f(a, /, b)`), keyword-only, `*args` and `**kwargs` parameters, and calls can spread with `f(*items, **options)`. A `*items` argument can't follow a keyword argument (`f(a=1, *items)`).
- `type(x)`, `isinstance` and `issubclass` work with the built-in types (`int`, `float`, `str`, `bool`, `list`, `tuple`, `dict`, `range`, `NoneType`, `function`, ...) and the exception classes, with tuples of them, and `bool` counts as an `int`.
- The type names are real type objects: calling them converts (`int('5')`), they print as `<class 'int'>`, compare with `==` and `is`, work as dict keys and have `__name__`; so does `x.__class__`.
- The methods of the built-in types can be used through the type: `str.lower`, `sorted(words, key=str.lower)`, `map(str.strip, lines)`, `dict.fromkeys(keys)`.
- `int` has `bit_length`, `bit_count` and `is_integer`, and `float` has `is_integer`. `sum` adds floats with compensation, like CPython 3.12 and later, so `sum([0.1, 0.2, 0.3])` is `0.6`.
- Functions and methods have `__name__`, but not other attributes.
- Modules can be dotted: register `game.ai` (its parent `game` is created for you if you didn't register it) and scripts can `import game.ai`, `import game.ai as ai`, `from game.ai import think` or `from game import ai`.
- Decorators work on functions, methods and classes (`@name`, `@obj.attr`, `@factory(args)`, stacked). The `@` must start the line; `a @ b` is not supported.

## Classes

Classes have instances, instance and class attributes, methods, `__init__` and single inheritance (`class B(A):`, or `class A(object):`).

- **Decorators on members.** `@staticmethod`, `@classmethod` and `@property` (with `@x.setter` and `@x.deleter`) behave as in Python, including through `super()`.
- **`super()`.** `super().name` and `super(Class, self).name` reach the parent class's method or attribute, including `super().__init__(...)`. It must be written as `super().name` directly inside a method (not stored in a variable, and not in a lambda or inner function inside it).
- **Attribute hooks.** `__getattr__` is called when normal lookup fails (including when a property getter raises `AttributeError`). `__setattr__` and `__delattr__` are called for every set or delete; call `object.__setattr__(self, name, value)` or `super().__setattr__(name, value)` to store.
- **Comparison, hashing and truth.** Instances compare and hash by identity and are truthy unless the class says otherwise with `__eq__`, `__ne__`, `__hash__`, `__lt__`/`__le__`/`__gt__`/`__ge__`, `__bool__` or `__len__`. The operators return whatever the method returns, `NotImplemented` declines so the other operand's method is tried (a subclass's goes first), and a class that defines `__eq__` without `__hash__` is unhashable. `sorted`, `min`, `max`, `in`, `list.index` and dict keys use them.
- **Arithmetic.** `__add__`, `__sub__`, `__mul__`, `__truediv__`, `__floordiv__`, `__mod__`, `__pow__`, `__and__`, `__or__`, `__xor__`, `__lshift__`, `__rshift__`; the reflected `__r...__` forms (so `sum` works with `__radd__`); the in-place `__i...__` forms used by `+=` and friends (falling back to the plain operator); `__neg__`, `__pos__`, `__invert__`; and the conversions behind `abs()`, `int()` and `float()` (`__abs__`, `__int__`, `__float__`).
- **Containers.** `__getitem__`, `__setitem__` and `__delitem__` (index, `del` and `+=` on an item; a slice arrives as a `slice` object), `__contains__` (`in`, falling back to iterating), `__reversed__` (`reversed()`), `__round__` (`round(x)` and `round(x, n)`), and `__index__` (as a list, tuple, string or range index, in slice bounds, in `range()`, `hex`/`bin`/`oct`, and as the fallback of `int()`).
- **Iteration.** `__iter__` and `__next__` work in `for` loops, comprehensions, `list()`, `sum()`, unpacking and so on. `__iter__` may return `self`, another object with `__next__`, or `iter(some_list)`; raising `StopIteration` ends the loop. `iter(x)` and `next(it[, default])` are built in.
- **Calls and `with`.** `__call__`, `__enter__` and `__exit__`.
- **Text.** Instances print as `<__main__.A object at 0x...>` unless the class defines `__str__` or `__repr__`, which `print`, `str`, `repr`, f-strings and the display of lists and dicts all use (`str` falls back to `__repr__`; both must return a string). `__format__` serves `format()`, f-strings and `str.format`; without it a format spec is an error.
- **Reflection.** `isinstance`, `issubclass`, `type(x)`, `x.__class__`, `A.__name__`, `getattr`, `setattr` and `hasattr` work with classes and instances, and a class or instance can be a dict key.
- **Waiting and snapshots.** `__init__`, `__call__`, `__enter__`, `__exit__` and `__next__` run as ordinary script frames, so they can wait (`game.wait`) and survive a snapshot. All the other special methods, property getters and setters, and the attribute hooks run to completion like sort-key callbacks, so they cannot wait. Snapshots keep classes, instances and cycles between them. A dictionary keyed by script objects can only be looked up while its script is running (after a restore it is indexed when the script next uses it).
- **Scoping.** Methods don't see the names of their class body (use `self.x` or `A.x`), as in Python, and a class body can't read a global before assigning a class variable of the same name (`x = x + 1` raises `UnboundLocalError` there).

## Exceptions

- Exceptions can be subclassed: `class HitError(ValueError):` (or `Exception`, or any built-in or host-registered exception type) makes a class you can `raise`, `raise HitError` (the instance is made for you) and catch with `except HitError as e`, alone or in a tuple.
- A built-in `except ValueError` catches the subclass too, and `e` is the instance (so it has your fields and methods). `e.args`, `str(e)` and `repr(e)` behave as in Python; `__init__` may call `super().__init__(msg)`, and `__str__` overrides the text.
- An uncaught one stops the task with the class name as `PyRuntimeError.TypeName`.
- Built-in errors raised by the VM (a `ZeroDivisionError`) are never instances of your subclasses.
- The traceback of an exception instance isn't kept across a snapshot.
- Register host exception types with `vm.RegisterExceptionType("GameError")`.
- **Chaining.** `raise X from Y` sets `X.__cause__` to `Y` (an exception, an exception class, or `None`) and `__suppress_context__` to `True`. An exception raised while an `except` block is handling another one (also from a function that block calls, or by the VM, like a `ZeroDivisionError`) gets that one as `__context__`; the handled exception is forgotten again when the block ends, and an inner `try` inside an `except` block gives the outer one back. Without a chain the three attributes are `None`, `None` and `False`. They are read-only on built-in exceptions; an exception of your own class can assign them.
- A task that stops with a chained error keeps the chain: `PyRuntimeError.Cause` and `PyRuntimeError.Context` (also `InnerException`) hold the earlier errors, and `FormatTraceback()` prints them first, with CPython's "The above exception was the direct cause of the following exception:" and "During handling of the above exception, another exception occurred:" lines. The chain is not kept in the saved state of a task that has already stopped with an error.
- Not there yet: `__traceback__`, `with_traceback`, `add_note`, and the exception groups.

## Closures, sets and comprehensions

- Nested functions and `lambda` are closures with Python's semantics: inner functions share the variables of the function around them (late binding), and `nonlocal` rebinds them. Snapshots keep closures, including cycles from recursive inner functions.
- List, dict and set comprehensions have their own scope like in Python (loop variables don't leak, the first iterable is evaluated outside). Generator expressions are lazy generators (the first iterable is evaluated, and checked to be iterable, when the generator is made).
- Sets keep their members in insertion order (CPython orders them by hash, so sort before printing if you compare output). They take `-`, `|`, `&`, `^` (and their `=` forms) and the subset comparisons `<=`, `<`, `>=`, `>`, and are unhashable. Looking up a set in a set (`{1} in s`) compares it as a frozenset, as in Python.
- `frozenset(iterable)` makes an immutable, hashable set that can be a dict key or a member of a set. It has the non-mutating set methods and operators (the result of `|`, `&`, `-` and `^` has the type of the left operand), compares equal to a set with the same members, prints as `frozenset({1, 2})`, and is kept by snapshots.
- `slice(stop)` and `slice(start, stop[, step])` make slice objects, which are hashable and comparable.
- `dir(x)` and `vars(x)` take exactly one argument, and `dir` leaves out the dunder methods every object has.

## Generators

- A function containing `yield` returns a generator when called, which runs when something asks for its next value (`for`, `next(g)`, `g.send(x)`, `list(g)`, `sum(g)`, unpacking, and so on). `return value` ends it, and `StopIteration.value` is what it returned.
- `yield from` hands on every item of another iterable and evaluates to the value a sub-generator returned. `g.send(x)`, `g.throw()` and `g.close()` are passed on through `yield from` to the sub-generator (its own `except` and `finally` run first). A plain iterable cannot take them, so the exception is raised in the outer generator.
- `g.throw(type[, value])` raises an exception at the paused `yield`, and `g.close()` raises `GeneratorExit` there so `finally` blocks run.
- A `for` loop, `next(g)`, `g.send(x)`, `g.throw()`, `g.close()` and `g.__next__()` run the generator as an ordinary frame, so its body can wait (`game.wait`) and a saved task can contain a half-run generator.
- A generator consumed by a built-in such as `list()`, `sum()` or `sorted()` cannot wait, like a sort-key callback. `sum(f(x) for x in xs)` pulls values from a lazy generator through the built-in, so `f` cannot wait there; use a `for` loop or a list comprehension if it needs to.
- Not supported: `throw()` with a traceback argument, async generators, and a generator function as an event handler. `yield` is not allowed in a class body, at module level or inside a comprehension. Sending a non-`None` value to a plain iterable in `yield from` is ignored instead of raising an error.

## with

`with` follows PEP 343: `__exit__` receives the exception type and instance (the traceback argument is always `None`) and a true return value suppresses the exception; `return`, `break` and `continue` inside the body run `__exit__` too. Several items work, and so does the parenthesized form `with (a as x, b as y):`.

## match

`match` and `case` work as in Python 3.10 and later, and are ordinary names everywhere else (`match = 1` is fine). The patterns are:

| Pattern | Example |
| --- | --- |
| literals (compared with `==`; `None`, `True` and `False` with `is`) | `case 0`, `case -1.5`, `case 'go'`, `case None` |
| capture and wildcard | `case x`, `case _` |
| value (a dotted name) | `case Color.RED` |
| sequence (not `str`), with one `*rest` | `case [x, y]`, `case (1, *rest)`, `case first, *_` |
| mapping, with `**rest` | `case {'kind': 'circle', 'r': r, **others}` |
| class, positional and by keyword | `case Point(0, y)`, `case Circle(radius=r)` |
| alternatives and `as` | `case 1 \| 2`, `case [x, y] as pair` |
| guard | `case [x, y] if x < y` |

A class takes positional patterns from its `__match_args__` (a tuple of attribute names), and a named tuple from its fields. `int`, `float`, `str`, `bool`, `list`, `dict`, `tuple`, `set` and `frozenset` take one positional pattern that matches the value itself, as in `case int(n)`. Names are only bound once the whole pattern has matched, and stay bound after the block, like in CPython. A task that is paused inside a `case` block is saved and restored like any other. The `match` statement is rewritten into `if` and `elif` statements while it is parsed, using a few built-in helpers whose names start with a dot, so the subject is kept in a hidden variable such as `.m1`. Complex-number literals (`case 1+2j`) are not supported.

## Not supported yet

`async`/`await`, bytes and complex literals, nested format specs in f-strings, `__slots__`, metaclasses, multiple inheritance, inheriting from built-in types other than exceptions, and `object()`.

## Differences from CPython

- **Integers** have any size, but a result larger than `vm.Options.MaxIntBits` (16384 bits by default, about 4900 digits) raises a catchable `OverflowError`, so `2 ** 10 ** 9` cannot stall the game. Like CPython, converting an integer of more than 4300 digits to or from text raises `ValueError`, and an integer literal can't have more digits than that. `range()` can't take bounds beyond 64 bits, `hash()` is not CPython's, and `x is y` is only true for the very same object. A host function that reads an argument with `Value.AsInt()` gets an `OverflowError` for a big one; `Value.AsBigInteger()`, `Value.ToString()` and `AsFloat()` work, and `Value.FromBigInteger` makes one.
- **Built-in exceptions** keep only a message string, so `ValueError(42).args` is `('42',)`. Exception classes you write keep their real `args`.
- **Dict views.** `dict.keys()/values()/items()` return live views (`dict_keys([...])`; `len`, `in`, iteration, `reversed`, and set operators and comparisons on keys and items, as in Python). Iterating `values()` or `items()` walks a copy, so adding a key in the loop doesn't raise as it does in CPython, and views have no methods (no `isdisjoint` or `.mapping`).
- **Callbacks cannot wait.** `sorted`, `list.sort`, `min` and `max` accept `key=` (and `min`/`max` accept `default=`), but a key function runs to completion and cannot wait.
- **`finally`.** `return`, `break` and `continue` are not allowed inside a `finally` block (they are in `try`, `except` and `else`, and run the enclosing `finally` blocks).
- **Strings** are UTF-16, so a character outside the Basic Multilingual Plane such as an emoji counts as two in `len()` and indexing.
- **No complex numbers.** A negative base with a fractional power raises `ValueError`.

## Limits and safety

`vm.Options`: `StepsPerSlice` (instructions per `Update`), `MaxSteps` (hard cap, uncatchable `StepLimitExceeded`), `MaxCallDepth` (catchable `RecursionError`), `MaxQueuedEvents`, `MaxIntBits` (largest integer result, in bits; bigger raises a catchable `OverflowError`).

Source code is also bounded so that hostile text cannot overflow the host's stack: brackets, operators and blocks are limited to 300 nesting steps in total (about 290 nested blocks, or about 150 nested brackets, which count twice), and one expression may chain at most 500 operators (`a + b + c`, `a.b.c`). Beyond that, compiling raises a `PySyntaxError`. The parser also checks how much stack is left, so the depth that works for brackets depends on the platform and its stack size. Measured with 10-level steps: 140 in a Windows IL2CPP build and in Linux Mono, 90 to 99 in a Windows Mono player, and about 90 in the Unity Editor. A script that nests that deeply could work in a build and fail in the Editor, but real scripts nest far less. Built-ins that build a list from an iterable (`list`, `tuple`, `sorted`, ...) stop with a `MemoryError` after about 4 million items. `sum`, `min`, `max`, `any` and `all` read one item at a time instead, so `sum(range(10**7))` works, and `any` and `all` stop at the first answer (an endless generator is fine). Their work counts as steps: a long walk ends the current slice, and `MaxSteps` stops it part-way.
