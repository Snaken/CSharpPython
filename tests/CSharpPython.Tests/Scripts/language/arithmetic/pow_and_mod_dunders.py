class N:
    def __init__(self, v):
        self.v = v
    def __pow__(self, o):
        return N(self.v ** o)
    def __mod__(self, o):
        return N(self.v % o)
    def __repr__(self):
        return 'N' + str(self.v)
print(N(3) ** 2, N(10) % 4, pow(N(2), 5), divmod(7, 2))
