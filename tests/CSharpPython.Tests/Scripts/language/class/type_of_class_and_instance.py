class A: pass
print(type(A) is type, type(A()) is A, isinstance(A, type), isinstance(A(), type))
