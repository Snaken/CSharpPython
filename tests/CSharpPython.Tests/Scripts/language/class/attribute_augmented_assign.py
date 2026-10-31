class A:
    def __init__(self):
        self.n = 1
        self.items = []
a = A()
a.n += 4
a.n *= 2
a.items += [1, 2]
a.items.append(3)
print(a.n, a.items)
