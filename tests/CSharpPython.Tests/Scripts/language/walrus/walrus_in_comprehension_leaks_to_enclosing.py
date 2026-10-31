data = [1, 2, 3, 4]
res = [y for x in data if (y := x * x) > 4]
print(res, y)
print([z := x for x in data], z)
print({w for x in data if (w := x % 2)}, w)
print(sum(last := n for n in data), last)
g = {p: (q := p + 1) for p in range(3)}
print(g, q)
