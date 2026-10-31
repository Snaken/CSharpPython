class V:
    def __init__(self, x):
        self.x = x
    def add(self, d):
        self.x += d
        return self
print(V(1).add(2).add(3).x)
