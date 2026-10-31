class A:
    def f(self):
        return 1
g = A.f
print(g(A()), g(A()))
