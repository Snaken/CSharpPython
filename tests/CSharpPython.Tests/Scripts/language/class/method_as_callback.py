class M:
    def __init__(self, k):
        self.k = k
    def scale(self, x):
        return x * self.k
m = M(3)
print(list(map(m.scale, [1, 2, 3])) if False else [m.scale(x) for x in [1, 2, 3]], sorted([3, 1, 2], key=m.scale))
