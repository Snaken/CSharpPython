xs = [1, 2, 3]
g = (x + y for x in xs for y in (10, 20) if x != 2)
print(list(g))
n = 2
h = (i * n for i in range(3))
n = 10
print(list(h))
src = [1, 2]
k = (a for a in src)
src = [7, 8]
print(list(k))
nested = ((a, b) for a in range(2) for b in range(a + 1))
print(list(nested))
print(list(x for x in 'abc'))
print(dict((k, v) for k, v in [(1, 2), (3, 4)]))
print(sorted(x for x in {3, 1, 2}))
print(', '.join(str(x) for x in range(3)))
