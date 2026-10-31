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

a = V(1, 2)
b = V(10, 20)
print(a + b, b - a, a * 3, 3 * a, -a, b / 4, a + b + a)
