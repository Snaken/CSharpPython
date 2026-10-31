a, *b = 'hello'
print(a, b)
[x, *y] = (1, 2, 3)
print(x, y)
(p, *q), r = [1, 2, 3], 4
print(p, q, r)
a, (b, *c), d = 1, (2, 3, 4), 5
print(a, b, c, d)
*a, = [1, 2, 3]
print(a)
a, *b = {1: 'a'}
print(a, b)
a, *b = iter([7, 8, 9])
print(a, b)
for h, *t in [(1, 2, 3), (4,), (5, 6)]:
    print(h, t)
