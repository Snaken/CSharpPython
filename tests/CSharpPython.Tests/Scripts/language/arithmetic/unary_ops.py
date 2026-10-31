class V:
    def __init__(self, n):
        self.n = n
    def __neg__(self): return V(-self.n)
    def __pos__(self): return V(self.n + 100)
    def __abs__(self): return abs(self.n)
    def __repr__(self): return 'V' + str(self.n)
print(-V(3), +V(3), abs(V(-4)), -(-V(1)), abs(V(5)))
