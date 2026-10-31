class N:
    def __init__(self, v):
        self.v = v
    def __radd__(self, o): return ('radd', o, self.v)
    def __rsub__(self, o): return ('rsub', o, self.v)
    def __rmul__(self, o): return ('rmul', o, self.v)
    def __rtruediv__(self, o): return ('rtruediv', o, self.v)
    def __rfloordiv__(self, o): return ('rfloordiv', o, self.v)
    def __rmod__(self, o): return ('rmod', o, self.v)
    def __rpow__(self, o): return ('rpow', o, self.v)
n = N(1)
print(2 + n, 2 - n, 2 * n, 2 / n, 2 // n, 2 % n, 2 ** n)
print('s' + n, [1] * n)
