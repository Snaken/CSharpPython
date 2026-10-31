class A:
    pass
a = A()
print(type(a) is A, isinstance(a, A), A.__name__, type(a).__name__)
