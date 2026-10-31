class A: pass
a = A()
b = A()
d = {a: 1}
d[b] = 2
d[a] = 3
print(len(d), d[a], d[b])
