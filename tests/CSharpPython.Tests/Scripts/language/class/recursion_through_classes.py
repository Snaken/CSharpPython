class A:
    def f(self, n):
        return 0 if n == 0 else 1 + B().g(n - 1)
class B:
    def g(self, n):
        return A().f(n)
print(A().f(10))
