def apply(fn, v):
    return fn(v)
def f(n):
    return apply(lambda x: x + n, 1)
print(f(41))
