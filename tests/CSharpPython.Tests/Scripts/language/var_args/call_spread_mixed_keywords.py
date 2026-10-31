def f(**kw):
    return kw
print(f(a=1, **{'b': 2}, c=3))
