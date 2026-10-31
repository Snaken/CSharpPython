class P:
    def __init__(self, x):
        self.x = x
a = P(1)
b = P(2)
a.x = 10
print(a.x, b.x)
