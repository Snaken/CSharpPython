class V:
    def __init__(self, n):
        self.n = n
    def __lt__(self, other):
        return self.n < other.n
    def __gt__(self, other):
        return self.n > other.n
    def __repr__(self):
        return 'V' + str(self.n)
xs = [V(3), V(1), V(2)]
print(sorted(xs), min(xs), max(xs), sorted(xs, reverse=True))
xs.sort()
print(xs)
