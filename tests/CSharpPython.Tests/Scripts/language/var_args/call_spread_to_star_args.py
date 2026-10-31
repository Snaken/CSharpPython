def g(*a, **k):
    return a, k
def f(*a, **k):
    return g(*a, **k)
print(f(1, 2, x=3))
