class Bag:
    def __init__(self, items):
        self.items = items
    def __iter__(self):
        for i in self.items:
            yield i
    def doubled(self):
        for i in self.items:
            yield i * 2
b = Bag([1, 2, 3])
print(list(b), list(b.doubled()), sum(b))
for x in b:
    print(x)
def make(k):
    def gen(n):
        for i in range(n):
            yield i * k
    return gen
print(list(make(3)(4)))
