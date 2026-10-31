def f(a, b=5, *rest):
    return a, b, rest
print(f(1), f(1, 2), f(1, 2, 3))
