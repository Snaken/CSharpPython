class A: pass
a = A()
a.b = A()
a.b.c = 5
a.b.c += 1
print(a.b.c)
