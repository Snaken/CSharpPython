class K:
    def __init__(self, n):
        self.n = n
    def __eq__(self, other):
        return isinstance(other, K) and self.n == other.n
    def __hash__(self):
        return hash(self.n)
d = {K(1): 'one', K(2): 'two'}
print(d[K(1)], d[K(2)], K(3) in d, len(d))
d[K(1)] = 'uno'
print(len(d), d[K(1)])
print(hash(K(7)) == hash(K(7)), hash(K(7)) == hash(7))
