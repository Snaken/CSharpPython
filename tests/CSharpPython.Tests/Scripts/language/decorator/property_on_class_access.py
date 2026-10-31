class A:
    @property
    def p(self): return 1
print(type(A.p).__name__)
print(isinstance(A.p, property), isinstance(A.p, int))
