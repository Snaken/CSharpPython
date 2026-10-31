f = lambda a, *r, k=1, **kw: (a, r, k, kw)
print(f(1, 2, 3, k=4, z=5))
