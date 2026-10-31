class A:
    pass
a = A()
a.f = lambda x: x + 1
print(a.f(1))
