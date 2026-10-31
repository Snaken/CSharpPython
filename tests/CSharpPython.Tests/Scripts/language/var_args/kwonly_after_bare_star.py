def f(a, *, b, c=3):
    return a, b, c
print(f(1, b=2), f(1, c=0, b=2))
