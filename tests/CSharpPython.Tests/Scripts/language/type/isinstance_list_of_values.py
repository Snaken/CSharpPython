vals = [1, 'a', 2.5, None, [1], (2,), {}]
print([isinstance(v, (int, float)) for v in vals])
