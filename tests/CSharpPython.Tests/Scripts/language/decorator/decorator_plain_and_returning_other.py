def twice(fn):
    def w(x):
        return fn(fn(x))
    return w
@twice
def inc(x):
    return x + 1
print(inc(5))
def replace(fn):
    return 42
@replace
def g():
    pass
print(g)
registry = []
def register(fn):
    registry.append(fn)
    return fn
@register
def a(): return 'a'
@register
def b(): return 'b'
print([f() for f in registry])
