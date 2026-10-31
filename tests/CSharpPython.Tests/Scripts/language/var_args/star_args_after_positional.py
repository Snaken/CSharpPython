def f(a, b, *rest):
    return a, b, rest
print(f(1, 2), f(1, 2, 3, 4))
