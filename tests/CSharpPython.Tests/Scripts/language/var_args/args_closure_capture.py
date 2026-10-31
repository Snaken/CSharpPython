def f(*a):
    return lambda: a
print(f(1, 2)())
