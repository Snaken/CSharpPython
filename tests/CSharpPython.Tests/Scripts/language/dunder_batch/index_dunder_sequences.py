class I:
    def __init__(self, n): self.n = n
    def __index__(self): return self.n
xs = [10, 20, 30, 40]
print(xs[I(1)], xs[I(-1)], (1, 2, 3)[I(2)], 'abc'[I(0)], range(10)[I(3)])
print(xs[I(1):I(3)], xs[:I(2)], xs[::I(2)])
xs[I(0)] = 99
del xs[I(1)]
print(xs)
print(list(range(I(3))), list(range(I(1), I(4))), list(range(I(0), I(10), I(5))))
print(hex(I(255)), bin(I(5)), oct(I(8)))
print(int(I(7)))
