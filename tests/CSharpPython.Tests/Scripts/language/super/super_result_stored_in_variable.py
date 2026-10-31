class A:
    def f(self):
        return 1
class B(A):
    def f(self):
        g = super().f
        return g() + 1
print(B().f())
