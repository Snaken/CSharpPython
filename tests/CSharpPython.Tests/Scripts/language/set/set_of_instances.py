class P:
    def __init__(self, x):
        self.x = x
    def __eq__(self, o):
        return self.x == o.x
    def __hash__(self):
        return hash(self.x)
s = {P(1), P(1), P(2)}
print(len(s), P(2) in s, P(3) in s)
class Q:
    pass
q = Q()
print(len({q, q, Q()}))
