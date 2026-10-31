def f(order):
    return sorted(['a','b','c'], key=lambda ch: order[ch])
print(f({'a': 3, 'b': 1, 'c': 2}))
