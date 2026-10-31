class N:
    def __init__(self, v):
        self.v = v
    def __add__(self, o):
        return N(self.v + (o.v if isinstance(o, N) else o))
    def __radd__(self, o):
        return N(o + self.v)
    def __repr__(self):
        return 'N' + str(self.v)
print(sum([N(1), N(2), N(3)]), 5 + N(1), N(1) + 5, sum([N(1), N(2)], N(100)))
