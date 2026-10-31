class P:
    def __init__(self, x, y):
        self.x = x
        self.y = y
    def __eq__(self, other):
        return isinstance(other, P) and self.x == other.x and self.y == other.y
a = P(1, 2)
b = P(1, 2)
c = P(3, 4)
print(a == b, a == c, a != b, a != c, a == 5, a is b, a == a)
