class P:
    def __init__(self, v):
        self.v = v
    def __eq__(self, other):
        return self.v == other.v
xs = [P(1), P(2), P(3)]
print(P(2) in xs, P(9) in xs, xs.index(P(3)), xs.count(P(1)), [P(1)] == [P(1)], (P(1),) == (P(1),), {'a': P(1)} == {'a': P(1)})
