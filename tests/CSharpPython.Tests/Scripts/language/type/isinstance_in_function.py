def total(xs):
    t = 0
    for x in xs:
        if isinstance(x, int):
            t += x
        elif isinstance(x, (list, tuple)):
            t += total(x)
    return t
print(total([1, [2, 3], (4, [5]), 'a', None]))
