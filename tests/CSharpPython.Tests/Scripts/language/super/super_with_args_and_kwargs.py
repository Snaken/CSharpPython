class A:
    def f(self, a, b=2, *rest, **kw):
        return (a, b, rest, sorted(kw.items()))
class B(A):
    def f(self, *args, **kw):
        return super().f(*args, **kw)
print(B().f(1), B().f(1, 3, 4, z=5))
