class Double:
    def __call__(self, x):
        return x * 2
d = Double()
print([d(i) for i in range(3)], sorted([3, 1, 2], key=d), sorted([3, 1, 2], key=Double()))
