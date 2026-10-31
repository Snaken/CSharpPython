class V:
    def __init__(self, n):
        self.n = n
    def __lt__(self, other):
        if isinstance(other, V):
            return self.n < other.n
        return self.n < other
    def __gt__(self, other):
        if isinstance(other, V):
            return self.n > other.n
        return self.n > other
print(V(1) < 2, 5 < V(3), V(3) > 2, 1 > V(3))
