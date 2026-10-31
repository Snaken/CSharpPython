class A:
    def f(self, x):
        return x + 1
a = A()
print(getattr(a, 'f')(1), getattr(A, 'f')(a, 2))
