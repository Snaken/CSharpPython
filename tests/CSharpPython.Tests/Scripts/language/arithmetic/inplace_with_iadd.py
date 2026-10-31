class Acc:
    def __init__(self):
        self.items = []
    def __iadd__(self, x):
        self.items.append(x)
        return self
    def __add__(self, x):
        return 'plain add'
a = Acc()
b = a
a += 1
a += 2
print(a is b, a.items, a + 1)
