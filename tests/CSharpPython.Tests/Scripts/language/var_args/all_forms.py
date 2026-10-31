def f(a, b=2, *args, c, d=4, **kw):
    return a, b, args, c, d, kw
print(f(1, c=3))
print(f(1, 9, 8, 7, c=3, d=0, z=1))
