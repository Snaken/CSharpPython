# An operator method that calls deeply makes the task's stack grow while the operator is still running.
def deep(n):
    if n == 0:
        return 0
    return 1 + deep(n - 1)

class V:
    def __init__(self, n):
        self.n = n
    def __add__(self, o):
        return V(self.n + o.n + deep(150))
    def __mul__(self, o):
        return V(self.n * o.n + deep(150))
    def __neg__(self):
        return V(-self.n - deep(150))
    def __lt__(self, o):
        return deep(150) > 0 and self.n < o.n
    def __eq__(self, o):
        return deep(150) > 0 and self.n == o.n

a = V(2)
b = V(5)
c = a + b
d = a * b
e = -a
print(c.n, d.n, e.n, a < b, a == b, a == V(2))
total = V(0)
for i in range(5):
    total = total + V(i)
print(total.n)
