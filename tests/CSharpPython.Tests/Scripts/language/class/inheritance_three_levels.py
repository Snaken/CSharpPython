class A:
    x = 1
    def f(self):
        return 'A.f'
    def g(self):
        return 'A.g'
class B(A):
    def f(self):
        return 'B.f'
class C(B):
    def g(self):
        return 'C.g'
c = C()
print(c.f(), c.g(), c.x, isinstance(c, A), isinstance(c, B), issubclass(C, A), issubclass(A, C))
