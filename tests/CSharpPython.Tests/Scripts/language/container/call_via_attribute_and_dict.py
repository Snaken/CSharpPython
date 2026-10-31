class F:
    def __call__(self, v):
        return v + 1
class Holder:
    def __init__(self):
        self.f = F()
h = Holder()
print(h.f(1), {'k': F()}['k'](2), [F(), F()][1](3))
