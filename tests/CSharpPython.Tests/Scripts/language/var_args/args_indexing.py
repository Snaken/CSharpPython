def f(*a):
    return a[0], a[-1], len(a), a[1:]
print(f(1, 2, 3))
