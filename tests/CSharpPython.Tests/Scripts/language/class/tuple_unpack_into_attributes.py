class A: pass
a = A()
a.x, a.y = 1, 2
a.x, a.y = a.y, a.x
print(a.x, a.y)
