class M:
    def __init__(self):
        self.v = 3
    def get(self):
        return self.v
m = M()
g = m.get
m.v = 9
print(g(), g())
