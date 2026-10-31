class M:
    @staticmethod
    def add(a, b):
        return a + b
    def inst(self):
        return self.add(1, 2)
print(M.add(1, 2), M().add(3, 4), M().inst())
m = M()
m.add = lambda a, b: 'shadow'
print(m.add(1, 2), M.add(1, 2))
f = M.add
print(f(5, 6))
print(isinstance(M.__dict__ if False else M, type))
