def f():
    yield 1
g = f()
print(type(g).__name__, bool(g), g is g, callable(g), hasattr(g, 'send'))
print(f() == f(), iter(g) is g)
print(isinstance([], list))
