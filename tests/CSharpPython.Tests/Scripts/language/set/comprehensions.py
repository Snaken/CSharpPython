print(sorted({x % 3 for x in range(10)}), {x for x in range(0)}, sorted({x * y for x in range(3) for y in range(3) if x != y}))
print(sorted({c.upper() for c in 'hello world' if c != ' '}))
n = 5
print(sorted({i * n for i in range(3)}), len({(i, i) for i in range(4)}))
fs = [lambda: {n} for n in range(2)]
print(fs[0](), fs[1]())
print(sorted({x for x in {1, 2, 3}}))
