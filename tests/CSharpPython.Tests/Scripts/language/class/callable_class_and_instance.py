class A:
    def f(self): pass
print(callable(A), callable(A()), callable(A().f), callable(A.f))
