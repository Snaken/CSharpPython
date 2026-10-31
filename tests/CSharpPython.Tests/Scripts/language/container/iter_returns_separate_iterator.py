class Bag:
    def __init__(self):
        self.items = ['a', 'b', 'c']
    def __iter__(self):
        return BagIter(self.items)
class BagIter:
    def __init__(self, items):
        self.items = items
        self.i = 0
    def __iter__(self):
        return self
    def __next__(self):
        if self.i == len(self.items):
            raise StopIteration()
        v = self.items[self.i]
        self.i += 1
        return v
b = Bag()
print(list(b), list(b), [x.upper() for x in b], ''.join(b))
