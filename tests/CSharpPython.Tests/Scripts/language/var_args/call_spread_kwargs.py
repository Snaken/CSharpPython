def f(a, b):
    return a - b
print(f(**{'a': 10, 'b': 3}), f(1, **{'b': 5}))
