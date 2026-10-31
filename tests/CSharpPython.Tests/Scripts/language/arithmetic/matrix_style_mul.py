class M:
    def __init__(self, rows):
        self.rows = rows
    def __mul__(self, o):
        if isinstance(o, M):
            return M([[sum(a * b for a, b in zip(r, c)) for c in zip(*o.rows)] for r in self.rows])
        return M([[v * o for v in r] for r in self.rows])
    def __rmul__(self, o):
        return self * o
    def __repr__(self):
        return 'M' + str(self.rows)
m = M([[1, 2], [3, 4]])
print(m * m, 2 * m, m * 3)
