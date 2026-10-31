class Bag:
    def __init__(self, n):
        self.n = n
    def __len__(self):
        return self.n
print(len(Bag(3)), bool(Bag(0)), bool(Bag(2)), 'full' if Bag(1) else 'empty', not Bag(0))
