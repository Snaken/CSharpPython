def f(a, **kw):
    return a, kw
print(f(1), f(1, x=2), f(a=3, x=2))
