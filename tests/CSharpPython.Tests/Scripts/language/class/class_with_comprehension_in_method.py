class P:
    def __init__(self):
        self.xs = [1, 2, 3]
    def doubled(self):
        return [x * 2 for x in self.xs]
    def total(self):
        return sum(x for x in self.xs)
print(P().doubled(), P().total())
