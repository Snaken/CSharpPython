class Adder:
    def __init__(self, n):
        self.n = n
    def __call__(self, x, y=0):
        return self.n + x + y
a = Adder(10)
print(a(1), a(1, y=5), a(*[1, 2]), callable(a), callable(Adder), callable(Adder(1).n) )
