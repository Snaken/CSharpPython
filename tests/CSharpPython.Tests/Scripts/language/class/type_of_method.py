class A:
    def f(self): pass
print(type(A().f).__name__, type(A.f).__name__)
