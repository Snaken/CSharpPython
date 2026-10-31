class V:
    def __init__(self, x, y):
        self.x = x
        self.y = y
    def __repr__(self):
        return 'V(' + str(self.x) + ', ' + str(self.y) + ')'
    def __add__(self, o):
        return V(self.x + o.x, self.y + o.y)
    def __sub__(self, o):
        return V(self.x - o.x, self.y - o.y)
    def __mul__(self, k):
        return V(self.x * k, self.y * k)
    def __rmul__(self, k):
        return V(self.x * k, self.y * k)
    def __neg__(self):
        return V(-self.x, -self.y)
    def __truediv__(self, k):
        return V(self.x / k, self.y / k)

a = V(1, 1)
print(a + a * 2 - V(0, 1), (a + a) * 3, -(a * 2) + a)
x = V(0, 0)
for i in range(4):
    x = x + V(i, 1)
print(x)
