def f(a, **kw):
    kw['extra'] = a
    return kw
print(f(1, x=2))
