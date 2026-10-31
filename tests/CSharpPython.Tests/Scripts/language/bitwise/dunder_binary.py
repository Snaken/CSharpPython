class Bits:
    def __init__(self, v):
        self.v = v
    def __and__(self, o):
        return Bits(self.v & (o.v if isinstance(o, Bits) else o))
    def __or__(self, o):
        return Bits(self.v | (o.v if isinstance(o, Bits) else o))
    def __xor__(self, o):
        return Bits(self.v ^ (o.v if isinstance(o, Bits) else o))
    def __lshift__(self, n):
        return Bits(self.v << n)
    def __rshift__(self, n):
        return Bits(self.v >> n)
    def __invert__(self):
        return Bits(~self.v)
    def __repr__(self):
        return 'Bits(%d)' % self.v if False else 'Bits(' + str(self.v) + ')'
a = Bits(12)
print(a & Bits(10), a | 3, a ^ 5, a << 2, a >> 2, ~a, a & 4 | 1, (a | 1) << 1)
