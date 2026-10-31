class A:
    count = 0
    @classmethod
    def make(cls, n):
        cls.count += 1
        return cls(n)
    def __init__(self, n): self.n = n
class B(A):
    pass
a = A.make(1)
b = B.make(2)
c = b.make(3)
print(type(a).__name__, type(b).__name__, type(c).__name__, A.count, B.count)
print(a.n, b.n, c.n)
