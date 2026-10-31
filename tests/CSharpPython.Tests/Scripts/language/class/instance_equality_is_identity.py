class A: pass
a = A()
b = A()
c = a
print(a == a, a == b, a is c, a != b, a is not b)
