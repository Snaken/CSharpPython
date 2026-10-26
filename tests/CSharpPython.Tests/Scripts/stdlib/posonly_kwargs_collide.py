def f(a, /, **kw):
    return (a, kw)
print(f(1, a=2), f(1), f(1, b=3, a=4))
def g(a, /, b, *, c):
    return (a, b, c)
print(g(1, 2, c=3), g(1, b=2, c=3))
def h(a=1, /, b=2):
    return (a, b)
print(h(), h(5), h(5, 6), h(b=7))
def k(x, /, *args, **kw):
    return (x, args, kw)
print(k(1, 2, 3, y=4), k(x=1) if False else 'skip')
