def f(a, **kw):
    return a
try:
    f(1, a=2)
except TypeError as e:
    print(e)
