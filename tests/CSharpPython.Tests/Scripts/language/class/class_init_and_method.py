class Counter:
    def __init__(self, start):
        self.n = start
    def inc(self):
        self.n += 1
        return self.n
c = Counter(5)
print(c.inc(), c.inc(), c.n)
