class A: pass
print(isinstance(1, A), isinstance('a', A), isinstance(None, A), isinstance(A, A), isinstance(A(), int))
