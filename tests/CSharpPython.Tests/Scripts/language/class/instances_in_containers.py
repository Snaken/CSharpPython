class P:
    def __init__(self, n):
        self.n = n
ps = [P(3), P(1), P(2)]
print([p.n for p in ps], sorted(ps, key=lambda p: p.n)[0].n, max(ps, key=lambda p: p.n).n)
d = {p.n: p for p in ps}
print(d[2].n)
