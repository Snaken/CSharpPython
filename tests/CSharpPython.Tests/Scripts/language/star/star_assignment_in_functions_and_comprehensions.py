def split(xs):
    head, *tail = xs
    return head, tail
print(split([1, 2, 3]))
print([(h, t) for h, *t in [(1, 2), (3,), (4, 5, 6)]])
total = 0
for first, *others in [[1, 2], [3, 4, 5]]:
    total += first * len(others)
print(total)
g = lambda s: s[0]
a, *b = (g(s) for s in ['xx', 'yy', 'zz'])
print(a, b)
