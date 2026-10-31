class A:
    @staticmethod
    def s(): pass
    @classmethod
    def c(cls): pass
    @property
    def p(self): return 1
print(type(A.s).__name__, type(A.c).__name__, type(A().c).__name__)
print(callable(A.s), callable(A.c), callable(A.p))
print(isinstance(A.p, staticmethod))
print(isinstance(A.p, classmethod))
