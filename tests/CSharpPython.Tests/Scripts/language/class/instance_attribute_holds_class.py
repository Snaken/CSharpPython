class A:
    pass
class B:
    kind = A
b = B()
print(type(b.kind()).__name__, b.kind is A)
