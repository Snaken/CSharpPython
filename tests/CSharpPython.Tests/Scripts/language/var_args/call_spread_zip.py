def f(a, b):
    return a * b
print([f(*p) for p in zip([1, 2], [3, 4])])
