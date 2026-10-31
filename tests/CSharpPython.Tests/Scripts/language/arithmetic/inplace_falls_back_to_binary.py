class V:
    def __init__(self, n):
        self.n = n
    def __add__(self, o):
        return V(self.n + o.n)
    def __mul__(self, k):
        return V(self.n * k)
a = V(1)
b = a
a += V(2)
a *= 5
print(a.n, b.n, a is b)
