class Acc:
    def __init__(self):
        self.n = 0
    def __iadd__(self, o):
        self.n += o
        return self
class Box:
    def __init__(self):
        self.acc = Acc()
box = Box()
box.acc += 5
box.acc += 6
d = {'k': Acc()}
d['k'] += 7
xs = [Acc()]
xs[0] += 8
print(box.acc.n, d['k'].n, xs[0].n)
