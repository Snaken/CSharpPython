class Count:
    def __init__(self, n):
        self.n = n
        self.i = 0
    def __iter__(self):
        return self
    def __next__(self):
        if self.i >= self.n:
            raise StopIteration
        self.i += 1
        return self.i
for x in Count(3):
    print(x)
print(list(Count(4)), sum(Count(4)), max(Count(5)), sorted(Count(3), reverse=True))
