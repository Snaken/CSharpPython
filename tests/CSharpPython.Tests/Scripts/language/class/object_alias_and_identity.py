class A: pass
a = A()
b = a
b.v = 1
print(a.v, a is b, id(a) == id(b) if False else True)
