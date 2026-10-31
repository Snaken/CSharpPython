class N:
    def __init__(self, v):
        self.v = v
    def __add__(self, o): return ('add', self.v, o)
    def __sub__(self, o): return ('sub', self.v, o)
    def __mul__(self, o): return ('mul', self.v, o)
    def __truediv__(self, o): return ('truediv', self.v, o)
    def __floordiv__(self, o): return ('floordiv', self.v, o)
    def __mod__(self, o): return ('mod', self.v, o)
    def __pow__(self, o): return ('pow', self.v, o)
n = N(1)
print(n + 2, n - 2, n * 2, n / 2, n // 2, n % 2, n ** 2)
